using System.Runtime.InteropServices;
using Silk.NET.Vulkan;
using Buffer = Silk.NET.Vulkan.Buffer;

namespace SuperRender.Renderer.Gpu;

/// <summary>
/// Orchestrates GPU-accelerated JPEG 8x8 IDCT using a Vulkan compute shader.
/// Uploads dequantized DCT coefficients → dispatches compute → reads back pixel values.
/// Falls back gracefully if the compute pipeline is unavailable.
/// </summary>
public sealed unsafe class IdctCompute : IDisposable
{
    private readonly VulkanContext _ctx;
    private readonly ComputePipelineManager _pipeline;
    private readonly DequantIdctPipeline _dequantPipeline;
    private DescriptorPool _descriptorPool;
    private DescriptorSet _descriptorSet;
    private DescriptorPool _dequantDescriptorPool;
    private DescriptorSet _dequantDescriptorSet;
    private CommandPool _commandPool;
    private bool _disposed;

    /// <summary>
    /// Returns true if the GPU compute pipeline was created successfully.
    /// When false, callers should use the CPU fallback path.
    /// </summary>
    public bool IsAvailable => _pipeline.Pipeline.Handle != 0;

    /// <summary>
    /// Returns true if the combined dequant+IDCT pipeline is available.
    /// </summary>
    public bool IsDequantAvailable => _dequantPipeline.Pipeline.Handle != 0;

    public IdctCompute(VulkanContext ctx)
    {
        _ctx = ctx;
        _pipeline = new ComputePipelineManager(ctx);
        _dequantPipeline = new DequantIdctPipeline(ctx);

        if (IsAvailable || IsDequantAvailable)
        {
            CreateDescriptorPools();
            CreateCommandPool();
        }
    }

    /// <summary>
    /// Runs IDCT on the GPU for a batch of 8x8 DCT blocks.
    /// Input: flat array of dequantized DCT coefficients (blockCount * 64 ints).
    /// Output: flat array of pixel values 0-255 (blockCount * 64 ints).
    /// </summary>
    public int[] TransformBlocks(int[] dctCoeffs, int blockCount)
    {
        if (!IsAvailable || blockCount == 0)
            return [];

        int totalInts = blockCount * 64;
        int bufferSize = totalInts * sizeof(int);

        // Create input SSBO and upload DCT coefficients
        var (inputBuffer, inputMemory) = CreateStorageBuffer((ulong)bufferSize);
        UploadToBuffer(inputMemory, dctCoeffs.AsSpan(), (ulong)bufferSize);

        // Create output SSBO
        var (outputBuffer, outputMemory) = CreateStorageBuffer((ulong)bufferSize);

        // Update descriptor set
        AllocateAndWriteDescriptorSet(inputBuffer, outputBuffer, (ulong)bufferSize);

        // Record and submit compute command buffer
        var cmd = BeginCompute();

        _ctx.Vk.CmdBindPipeline(cmd, PipelineBindPoint.Compute, _pipeline.Pipeline);
        var descSet = _descriptorSet;
        _ctx.Vk.CmdBindDescriptorSets(cmd, PipelineBindPoint.Compute,
            _pipeline.PipelineLayout, 0, 1, &descSet, 0, null);

        uint bc = (uint)blockCount;
        _ctx.Vk.CmdPushConstants(cmd, _pipeline.PipelineLayout,
            ShaderStageFlags.ComputeBit, 0, 4, &bc);

        // Dispatch: one workgroup per block, 64 threads per workgroup
        _ctx.Vk.CmdDispatch(cmd, (uint)blockCount, 1, 1);

        // Memory barrier: compute write → host read
        var memBarrier = new MemoryBarrier
        {
            SType = StructureType.MemoryBarrier,
            SrcAccessMask = AccessFlags.ShaderWriteBit,
            DstAccessMask = AccessFlags.HostReadBit,
        };
        _ctx.Vk.CmdPipelineBarrier(cmd,
            PipelineStageFlags.ComputeShaderBit, PipelineStageFlags.HostBit,
            0, 1, &memBarrier, 0, null, 0, null);

        EndCompute(cmd);

        // Read back results
        var result = new int[totalInts];
        ReadFromBuffer(outputMemory, result.AsSpan(), (ulong)bufferSize);

        // Cleanup buffers
        _ctx.Vk.DestroyBuffer(_ctx.Device, inputBuffer, null);
        _ctx.Vk.FreeMemory(_ctx.Device, inputMemory, null);
        _ctx.Vk.DestroyBuffer(_ctx.Device, outputBuffer, null);
        _ctx.Vk.FreeMemory(_ctx.Device, outputMemory, null);

        return result;
    }

    /// <summary>
    /// Runs combined dequantization + IDCT on the GPU for a batch of 8x8 DCT blocks.
    /// Input: flat array of raw (NOT dequantized) DCT coefficients (blockCount * 64 ints)
    /// and a quantization table (64 ints).
    /// Output: flat array of pixel values 0-255 (blockCount * 64 ints).
    /// Returns null if the combined pipeline is unavailable.
    /// </summary>
    public int[]? TransformBlocksWithDequant(int[] rawDctCoeffs, int[] quantTable, int blockCount)
    {
        if (!IsDequantAvailable || blockCount == 0)
            return null;

        int totalInts = blockCount * 64;
        int coeffBufferSize = totalInts * sizeof(int);
        int quantBufferSize = 64 * sizeof(int);

        // Create SSBOs
        var (inputBuffer, inputMemory) = CreateStorageBuffer((ulong)coeffBufferSize);
        UploadToBuffer(inputMemory, rawDctCoeffs.AsSpan(), (ulong)coeffBufferSize);

        var (quantBuffer, quantMemory) = CreateStorageBuffer((ulong)quantBufferSize);
        UploadToBuffer(quantMemory, quantTable.AsSpan(0, 64), (ulong)quantBufferSize);

        var (outputBuffer, outputMemory) = CreateStorageBuffer((ulong)coeffBufferSize);

        // Update descriptor set for 3 SSBOs
        AllocateAndWriteDequantDescriptorSet(inputBuffer, quantBuffer, outputBuffer,
            (ulong)coeffBufferSize, (ulong)quantBufferSize);

        // Record and submit compute command buffer
        var cmd = BeginCompute();

        _ctx.Vk.CmdBindPipeline(cmd, PipelineBindPoint.Compute, _dequantPipeline.Pipeline);
        var descSet = _dequantDescriptorSet;
        _ctx.Vk.CmdBindDescriptorSets(cmd, PipelineBindPoint.Compute,
            _dequantPipeline.PipelineLayout, 0, 1, &descSet, 0, null);

        uint bc = (uint)blockCount;
        _ctx.Vk.CmdPushConstants(cmd, _dequantPipeline.PipelineLayout,
            ShaderStageFlags.ComputeBit, 0, 4, &bc);

        _ctx.Vk.CmdDispatch(cmd, (uint)blockCount, 1, 1);

        var memBarrier = new MemoryBarrier
        {
            SType = StructureType.MemoryBarrier,
            SrcAccessMask = AccessFlags.ShaderWriteBit,
            DstAccessMask = AccessFlags.HostReadBit,
        };
        _ctx.Vk.CmdPipelineBarrier(cmd,
            PipelineStageFlags.ComputeShaderBit, PipelineStageFlags.HostBit,
            0, 1, &memBarrier, 0, null, 0, null);

        EndCompute(cmd);

        // Read back results
        var result = new int[totalInts];
        ReadFromBuffer(outputMemory, result.AsSpan(), (ulong)coeffBufferSize);

        // Cleanup buffers
        _ctx.Vk.DestroyBuffer(_ctx.Device, inputBuffer, null);
        _ctx.Vk.FreeMemory(_ctx.Device, inputMemory, null);
        _ctx.Vk.DestroyBuffer(_ctx.Device, quantBuffer, null);
        _ctx.Vk.FreeMemory(_ctx.Device, quantMemory, null);
        _ctx.Vk.DestroyBuffer(_ctx.Device, outputBuffer, null);
        _ctx.Vk.FreeMemory(_ctx.Device, outputMemory, null);

        return result;
    }

    private (Buffer buffer, DeviceMemory memory) CreateStorageBuffer(ulong size)
    {
        var bufferInfo = new BufferCreateInfo
        {
            SType = StructureType.BufferCreateInfo,
            Size = size,
            Usage = BufferUsageFlags.StorageBufferBit,
            SharingMode = SharingMode.Exclusive,
        };

        var result = _ctx.Vk.CreateBuffer(_ctx.Device, in bufferInfo, null, out var buffer);
        if (result != Result.Success)
            throw new InvalidOperationException($"Failed to create compute storage buffer: {result}");

        _ctx.Vk.GetBufferMemoryRequirements(_ctx.Device, buffer, out var memReqs);

        var allocInfo = new MemoryAllocateInfo
        {
            SType = StructureType.MemoryAllocateInfo,
            AllocationSize = memReqs.Size,
            MemoryTypeIndex = FindMemoryType(memReqs.MemoryTypeBits,
                MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit),
        };

        result = _ctx.Vk.AllocateMemory(_ctx.Device, in allocInfo, null, out var memory);
        if (result != Result.Success)
            throw new InvalidOperationException($"Failed to allocate compute buffer memory: {result}");

        _ctx.Vk.BindBufferMemory(_ctx.Device, buffer, memory, 0);
        return (buffer, memory);
    }

    private void UploadToBuffer(DeviceMemory memory, ReadOnlySpan<int> data, ulong size)
    {
        void* mapped;
        _ctx.Vk.MapMemory(_ctx.Device, memory, 0, size, 0, &mapped);
        MemoryMarshal.AsBytes(data).CopyTo(new Span<byte>(mapped, (int)size));
        _ctx.Vk.UnmapMemory(_ctx.Device, memory);
    }

    private void ReadFromBuffer(DeviceMemory memory, Span<int> data, ulong size)
    {
        void* mapped;
        _ctx.Vk.MapMemory(_ctx.Device, memory, 0, size, 0, &mapped);
        new ReadOnlySpan<byte>(mapped, (int)size).CopyTo(MemoryMarshal.AsBytes(data));
        _ctx.Vk.UnmapMemory(_ctx.Device, memory);
    }

    private void AllocateAndWriteDescriptorSet(Buffer inputBuffer, Buffer outputBuffer, ulong bufferSize)
    {
        // Reset pool to reuse the descriptor set
        _ctx.Vk.ResetDescriptorPool(_ctx.Device, _descriptorPool, 0);

        var layout = _pipeline.DescriptorSetLayout;
        var allocInfo = new DescriptorSetAllocateInfo
        {
            SType = StructureType.DescriptorSetAllocateInfo,
            DescriptorPool = _descriptorPool,
            DescriptorSetCount = 1,
            PSetLayouts = &layout,
        };
        _ctx.Vk.AllocateDescriptorSets(_ctx.Device, in allocInfo, out _descriptorSet);

        var inputInfo = new DescriptorBufferInfo
        {
            Buffer = inputBuffer,
            Offset = 0,
            Range = bufferSize,
        };
        var outputInfo = new DescriptorBufferInfo
        {
            Buffer = outputBuffer,
            Offset = 0,
            Range = bufferSize,
        };

        var writes = new WriteDescriptorSet[2];
        writes[0] = new WriteDescriptorSet
        {
            SType = StructureType.WriteDescriptorSet,
            DstSet = _descriptorSet,
            DstBinding = 0,
            DescriptorType = DescriptorType.StorageBuffer,
            DescriptorCount = 1,
            PBufferInfo = &inputInfo,
        };
        writes[1] = new WriteDescriptorSet
        {
            SType = StructureType.WriteDescriptorSet,
            DstSet = _descriptorSet,
            DstBinding = 1,
            DescriptorType = DescriptorType.StorageBuffer,
            DescriptorCount = 1,
            PBufferInfo = &outputInfo,
        };

        fixed (WriteDescriptorSet* pWrites = writes)
        {
            _ctx.Vk.UpdateDescriptorSets(_ctx.Device, 2, pWrites, 0, null);
        }
    }

    private void AllocateAndWriteDequantDescriptorSet(Buffer inputBuffer, Buffer quantBuffer,
        Buffer outputBuffer, ulong coeffBufferSize, ulong quantBufferSize)
    {
        _ctx.Vk.ResetDescriptorPool(_ctx.Device, _dequantDescriptorPool, 0);

        var layout = _dequantPipeline.DescriptorSetLayout;
        var allocInfo = new DescriptorSetAllocateInfo
        {
            SType = StructureType.DescriptorSetAllocateInfo,
            DescriptorPool = _dequantDescriptorPool,
            DescriptorSetCount = 1,
            PSetLayouts = &layout,
        };
        _ctx.Vk.AllocateDescriptorSets(_ctx.Device, in allocInfo, out _dequantDescriptorSet);

        var inputInfo = new DescriptorBufferInfo { Buffer = inputBuffer, Offset = 0, Range = coeffBufferSize };
        var quantInfo = new DescriptorBufferInfo { Buffer = quantBuffer, Offset = 0, Range = quantBufferSize };
        var outputInfo = new DescriptorBufferInfo { Buffer = outputBuffer, Offset = 0, Range = coeffBufferSize };

        var writes = new WriteDescriptorSet[3];
        writes[0] = new WriteDescriptorSet
        {
            SType = StructureType.WriteDescriptorSet,
            DstSet = _dequantDescriptorSet, DstBinding = 0,
            DescriptorType = DescriptorType.StorageBuffer, DescriptorCount = 1,
            PBufferInfo = &inputInfo,
        };
        writes[1] = new WriteDescriptorSet
        {
            SType = StructureType.WriteDescriptorSet,
            DstSet = _dequantDescriptorSet, DstBinding = 1,
            DescriptorType = DescriptorType.StorageBuffer, DescriptorCount = 1,
            PBufferInfo = &quantInfo,
        };
        writes[2] = new WriteDescriptorSet
        {
            SType = StructureType.WriteDescriptorSet,
            DstSet = _dequantDescriptorSet, DstBinding = 2,
            DescriptorType = DescriptorType.StorageBuffer, DescriptorCount = 1,
            PBufferInfo = &outputInfo,
        };

        fixed (WriteDescriptorSet* pWrites = writes)
        {
            _ctx.Vk.UpdateDescriptorSets(_ctx.Device, 3, pWrites, 0, null);
        }
    }

    private CommandBuffer BeginCompute()
    {
        var allocInfo = new CommandBufferAllocateInfo
        {
            SType = StructureType.CommandBufferAllocateInfo,
            Level = CommandBufferLevel.Primary,
            CommandPool = _commandPool,
            CommandBufferCount = 1,
        };
        _ctx.Vk.AllocateCommandBuffers(_ctx.Device, in allocInfo, out var cmd);

        var beginInfo = new CommandBufferBeginInfo
        {
            SType = StructureType.CommandBufferBeginInfo,
            Flags = CommandBufferUsageFlags.OneTimeSubmitBit,
        };
        _ctx.Vk.BeginCommandBuffer(cmd, in beginInfo);
        return cmd;
    }

    private void EndCompute(CommandBuffer cmd)
    {
        _ctx.Vk.EndCommandBuffer(cmd);

        var submitInfo = new SubmitInfo
        {
            SType = StructureType.SubmitInfo,
            CommandBufferCount = 1,
            PCommandBuffers = &cmd,
        };

        // Use the graphics queue (which supports compute on most GPUs)
        _ctx.Vk.QueueSubmit(_ctx.GraphicsQueue, 1, in submitInfo, default);
        _ctx.Vk.QueueWaitIdle(_ctx.GraphicsQueue);

        _ctx.Vk.FreeCommandBuffers(_ctx.Device, _commandPool, 1, in cmd);
    }

    private void CreateDescriptorPools()
    {
        if (IsAvailable)
        {
            var poolSize = new DescriptorPoolSize
            {
                Type = DescriptorType.StorageBuffer,
                DescriptorCount = 2, // input + output SSBOs
            };

            var poolInfo = new DescriptorPoolCreateInfo
            {
                SType = StructureType.DescriptorPoolCreateInfo,
                PoolSizeCount = 1,
                PPoolSizes = &poolSize,
                MaxSets = 1,
            };
            _ctx.Vk.CreateDescriptorPool(_ctx.Device, in poolInfo, null, out _descriptorPool);
        }

        if (IsDequantAvailable)
        {
            var poolSize = new DescriptorPoolSize
            {
                Type = DescriptorType.StorageBuffer,
                DescriptorCount = 3, // raw coeffs + quant table + output SSBOs
            };

            var poolInfo = new DescriptorPoolCreateInfo
            {
                SType = StructureType.DescriptorPoolCreateInfo,
                PoolSizeCount = 1,
                PPoolSizes = &poolSize,
                MaxSets = 1,
            };
            _ctx.Vk.CreateDescriptorPool(_ctx.Device, in poolInfo, null, out _dequantDescriptorPool);
        }
    }

    private void CreateCommandPool()
    {
        var poolInfo = new CommandPoolCreateInfo
        {
            SType = StructureType.CommandPoolCreateInfo,
            QueueFamilyIndex = _ctx.GraphicsQueueFamily,
            Flags = CommandPoolCreateFlags.ResetCommandBufferBit,
        };
        _ctx.Vk.CreateCommandPool(_ctx.Device, in poolInfo, null, out _commandPool);
    }

    private uint FindMemoryType(uint typeFilter, MemoryPropertyFlags properties)
    {
        _ctx.Vk.GetPhysicalDeviceMemoryProperties(_ctx.PhysicalDevice, out var memProps);
        for (var i = 0u; i < memProps.MemoryTypeCount; i++)
        {
            if ((typeFilter & (1u << (int)i)) != 0 &&
                (memProps.MemoryTypes[(int)i].PropertyFlags & properties) == properties)
                return i;
        }
        throw new InvalidOperationException("No suitable memory type for compute buffer.");
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_commandPool.Handle != 0)
            _ctx.Vk.DestroyCommandPool(_ctx.Device, _commandPool, null);
        if (_descriptorPool.Handle != 0)
            _ctx.Vk.DestroyDescriptorPool(_ctx.Device, _descriptorPool, null);
        if (_dequantDescriptorPool.Handle != 0)
            _ctx.Vk.DestroyDescriptorPool(_ctx.Device, _dequantDescriptorPool, null);
        _pipeline.Dispose();
        _dequantPipeline.Dispose();
    }
}
