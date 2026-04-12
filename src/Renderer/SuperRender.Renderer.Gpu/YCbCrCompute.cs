using System.Runtime.InteropServices;
using Silk.NET.Vulkan;
using Buffer = Silk.NET.Vulkan.Buffer;

namespace SuperRender.Renderer.Gpu;

/// <summary>
/// Orchestrates GPU-accelerated YCbCr→RGBA color conversion using a Vulkan compute shader.
/// Uploads per-pixel Y/Cb/Cr planes → dispatches compute → reads back RGBA pixel data.
/// Falls back gracefully if the compute pipeline is unavailable.
/// </summary>
public sealed unsafe class YCbCrCompute : IDisposable
{
    private readonly VulkanContext _ctx;
    private readonly YCbCrComputePipeline _pipeline;
    private DescriptorPool _descriptorPool;
    private DescriptorSet _descriptorSet;
    private CommandPool _commandPool;
    private bool _disposed;

    public bool IsAvailable => _pipeline.Pipeline.Handle != 0;

    public YCbCrCompute(VulkanContext ctx)
    {
        _ctx = ctx;
        _pipeline = new YCbCrComputePipeline(ctx);

        if (IsAvailable)
        {
            CreateDescriptorPool();
            CreateCommandPool();
        }
    }

    /// <summary>
    /// Converts per-pixel YCbCr planes to RGBA byte array on the GPU.
    /// </summary>
    public byte[] ConvertYCbCrToRgba(int[] yPlane, int[] cbPlane, int[] crPlane, int pixelCount)
    {
        if (!IsAvailable || pixelCount == 0)
            return [];

        int intBufferSize = pixelCount * sizeof(int);
        int uintBufferSize = pixelCount * sizeof(uint);

        var (yBuffer, yMemory) = CreateStorageBuffer((ulong)intBufferSize);
        var (cbBuffer, cbMemory) = CreateStorageBuffer((ulong)intBufferSize);
        var (crBuffer, crMemory) = CreateStorageBuffer((ulong)intBufferSize);
        var (outBuffer, outMemory) = CreateStorageBuffer((ulong)uintBufferSize);

        UploadToBuffer(yMemory, yPlane.AsSpan(), (ulong)intBufferSize);
        UploadToBuffer(cbMemory, cbPlane.AsSpan(), (ulong)intBufferSize);
        UploadToBuffer(crMemory, crPlane.AsSpan(), (ulong)intBufferSize);

        AllocateAndWriteDescriptorSet(yBuffer, cbBuffer, crBuffer, outBuffer,
            (ulong)intBufferSize, (ulong)uintBufferSize);

        var cmd = BeginCompute();

        _ctx.Vk.CmdBindPipeline(cmd, PipelineBindPoint.Compute, _pipeline.Pipeline);
        var descSet = _descriptorSet;
        _ctx.Vk.CmdBindDescriptorSets(cmd, PipelineBindPoint.Compute,
            _pipeline.PipelineLayout, 0, 1, &descSet, 0, null);

        uint pc = (uint)pixelCount;
        _ctx.Vk.CmdPushConstants(cmd, _pipeline.PipelineLayout,
            ShaderStageFlags.ComputeBit, 0, 4, &pc);

        // Dispatch: ceil(pixelCount / 256) workgroups
        uint workgroups = ((uint)pixelCount + 255) / 256;
        _ctx.Vk.CmdDispatch(cmd, workgroups, 1, 1);

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

        // Read back packed RGBA uint values and convert to byte[]
        var rgbaUints = new uint[pixelCount];
        ReadFromBuffer(outMemory, rgbaUints.AsSpan(), (ulong)uintBufferSize);

        var pixels = new byte[pixelCount * 4];
        MemoryMarshal.AsBytes(rgbaUints.AsSpan()).CopyTo(pixels);

        // Cleanup
        DestroyBuffer(yBuffer, yMemory);
        DestroyBuffer(cbBuffer, cbMemory);
        DestroyBuffer(crBuffer, crMemory);
        DestroyBuffer(outBuffer, outMemory);

        return pixels;
    }

    private void DestroyBuffer(Buffer buffer, DeviceMemory memory)
    {
        _ctx.Vk.DestroyBuffer(_ctx.Device, buffer, null);
        _ctx.Vk.FreeMemory(_ctx.Device, memory, null);
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
            throw new InvalidOperationException($"Failed to create YCbCr storage buffer: {result}");

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
            throw new InvalidOperationException($"Failed to allocate YCbCr buffer memory: {result}");

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

    private void ReadFromBuffer(DeviceMemory memory, Span<uint> data, ulong size)
    {
        void* mapped;
        _ctx.Vk.MapMemory(_ctx.Device, memory, 0, size, 0, &mapped);
        new ReadOnlySpan<byte>(mapped, (int)size).CopyTo(MemoryMarshal.AsBytes(data));
        _ctx.Vk.UnmapMemory(_ctx.Device, memory);
    }

    private void AllocateAndWriteDescriptorSet(
        Buffer yBuffer, Buffer cbBuffer, Buffer crBuffer, Buffer outBuffer,
        ulong inputSize, ulong outputSize)
    {
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

        var yInfo = new DescriptorBufferInfo { Buffer = yBuffer, Offset = 0, Range = inputSize };
        var cbInfo = new DescriptorBufferInfo { Buffer = cbBuffer, Offset = 0, Range = inputSize };
        var crInfo = new DescriptorBufferInfo { Buffer = crBuffer, Offset = 0, Range = inputSize };
        var outInfo = new DescriptorBufferInfo { Buffer = outBuffer, Offset = 0, Range = outputSize };

        var writes = new WriteDescriptorSet[4];
        var bufferInfos = stackalloc DescriptorBufferInfo[4];
        bufferInfos[0] = yInfo;
        bufferInfos[1] = cbInfo;
        bufferInfos[2] = crInfo;
        bufferInfos[3] = outInfo;

        for (int i = 0; i < 4; i++)
        {
            writes[i] = new WriteDescriptorSet
            {
                SType = StructureType.WriteDescriptorSet,
                DstSet = _descriptorSet,
                DstBinding = (uint)i,
                DescriptorType = DescriptorType.StorageBuffer,
                DescriptorCount = 1,
                PBufferInfo = &bufferInfos[i],
            };
        }

        fixed (WriteDescriptorSet* pWrites = writes)
        {
            _ctx.Vk.UpdateDescriptorSets(_ctx.Device, 4, pWrites, 0, null);
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

        _ctx.Vk.QueueSubmit(_ctx.GraphicsQueue, 1, in submitInfo, default);
        _ctx.Vk.QueueWaitIdle(_ctx.GraphicsQueue);
        _ctx.Vk.FreeCommandBuffers(_ctx.Device, _commandPool, 1, in cmd);
    }

    private void CreateDescriptorPool()
    {
        var poolSize = new DescriptorPoolSize
        {
            Type = DescriptorType.StorageBuffer,
            DescriptorCount = 4, // Y + Cb + Cr + RGBA
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
        throw new InvalidOperationException("No suitable memory type for YCbCr compute buffer.");
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_commandPool.Handle != 0)
            _ctx.Vk.DestroyCommandPool(_ctx.Device, _commandPool, null);
        if (_descriptorPool.Handle != 0)
            _ctx.Vk.DestroyDescriptorPool(_ctx.Device, _descriptorPool, null);
        _pipeline.Dispose();
    }
}
