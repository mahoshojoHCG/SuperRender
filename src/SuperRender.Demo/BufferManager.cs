using System.Runtime.InteropServices;
using Silk.NET.Vulkan;
using Buffer = Silk.NET.Vulkan.Buffer;

namespace SuperRender.Demo;

public sealed unsafe class BufferManager : IDisposable
{
    private readonly VulkanContext _ctx;

    public Buffer QuadVertexBuffer { get; private set; }
    public DeviceMemory QuadVertexMemory { get; private set; }
    public Buffer QuadIndexBuffer { get; private set; }
    public DeviceMemory QuadIndexMemory { get; private set; }
    public uint QuadIndexCount { get; private set; }

    public Buffer TextVertexBuffer { get; private set; }
    public DeviceMemory TextVertexMemory { get; private set; }
    public Buffer TextIndexBuffer { get; private set; }
    public DeviceMemory TextIndexMemory { get; private set; }
    public uint TextIndexCount { get; private set; }

    private bool _disposed;

    public BufferManager(VulkanContext ctx)
    {
        _ctx = ctx;
    }

    public void UploadQuads(ReadOnlySpan<QuadVertex> vertices, ReadOnlySpan<uint> indices)
    {
        FreeBuffer(QuadVertexBuffer, QuadVertexMemory);
        FreeBuffer(QuadIndexBuffer, QuadIndexMemory);

        if (vertices.Length == 0 || indices.Length == 0)
        {
            QuadIndexCount = 0;
            return;
        }

        (QuadVertexBuffer, QuadVertexMemory) = CreateAndUploadBuffer(
            MemoryMarshal.AsBytes(vertices), BufferUsageFlags.VertexBufferBit);
        (QuadIndexBuffer, QuadIndexMemory) = CreateAndUploadBuffer(
            MemoryMarshal.AsBytes(indices), BufferUsageFlags.IndexBufferBit);
        QuadIndexCount = (uint)indices.Length;
    }

    public void UploadTextQuads(ReadOnlySpan<TextVertex> vertices, ReadOnlySpan<uint> indices)
    {
        FreeBuffer(TextVertexBuffer, TextVertexMemory);
        FreeBuffer(TextIndexBuffer, TextIndexMemory);

        if (vertices.Length == 0 || indices.Length == 0)
        {
            TextIndexCount = 0;
            return;
        }

        (TextVertexBuffer, TextVertexMemory) = CreateAndUploadBuffer(
            MemoryMarshal.AsBytes(vertices), BufferUsageFlags.VertexBufferBit);
        (TextIndexBuffer, TextIndexMemory) = CreateAndUploadBuffer(
            MemoryMarshal.AsBytes(indices), BufferUsageFlags.IndexBufferBit);
        TextIndexCount = (uint)indices.Length;
    }

    private (Buffer buffer, DeviceMemory memory) CreateAndUploadBuffer(
        ReadOnlySpan<byte> data, BufferUsageFlags usage)
    {
        var size = (ulong)data.Length;

        var bufferInfo = new BufferCreateInfo
        {
            SType = StructureType.BufferCreateInfo,
            Size = size,
            Usage = usage,
            SharingMode = SharingMode.Exclusive,
        };

        var result = _ctx.Vk.CreateBuffer(_ctx.Device, in bufferInfo, null, out var buffer);
        if (result != Result.Success)
            throw new InvalidOperationException($"Failed to create buffer: {result}");

        _ctx.Vk.GetBufferMemoryRequirements(_ctx.Device, buffer, out var memRequirements);

        var allocInfo = new MemoryAllocateInfo
        {
            SType = StructureType.MemoryAllocateInfo,
            AllocationSize = memRequirements.Size,
            MemoryTypeIndex = FindMemoryType(memRequirements.MemoryTypeBits,
                MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit),
        };

        result = _ctx.Vk.AllocateMemory(_ctx.Device, in allocInfo, null, out var memory);
        if (result != Result.Success)
            throw new InvalidOperationException($"Failed to allocate buffer memory: {result}");

        _ctx.Vk.BindBufferMemory(_ctx.Device, buffer, memory, 0);

        // Map and copy
        void* mapped;
        _ctx.Vk.MapMemory(_ctx.Device, memory, 0, size, 0, &mapped);
        data.CopyTo(new Span<byte>(mapped, data.Length));
        _ctx.Vk.UnmapMemory(_ctx.Device, memory);

        return (buffer, memory);
    }

    private uint FindMemoryType(uint typeFilter, MemoryPropertyFlags properties)
    {
        _ctx.Vk.GetPhysicalDeviceMemoryProperties(_ctx.PhysicalDevice, out var memProperties);

        for (var i = 0u; i < memProperties.MemoryTypeCount; i++)
        {
            if ((typeFilter & (1u << (int)i)) != 0 &&
                (memProperties.MemoryTypes[(int)i].PropertyFlags & properties) == properties)
            {
                return i;
            }
        }

        throw new InvalidOperationException("Failed to find suitable memory type.");
    }

    public (Image image, DeviceMemory memory) CreateTextureImage(byte[] pixels, int width, int height)
    {
        var imageSize = (ulong)(width * height);

        // Create staging buffer
        var (stagingBuffer, stagingMemory) = CreateAndUploadBuffer(
            pixels.AsSpan(), BufferUsageFlags.TransferSrcBit);

        // Create image
        var imageInfo = new ImageCreateInfo
        {
            SType = StructureType.ImageCreateInfo,
            ImageType = ImageType.Type2D,
            Extent = new Extent3D { Width = (uint)width, Height = (uint)height, Depth = 1 },
            MipLevels = 1,
            ArrayLayers = 1,
            Format = Format.R8Unorm,
            Tiling = ImageTiling.Optimal,
            InitialLayout = ImageLayout.Undefined,
            Usage = ImageUsageFlags.TransferDstBit | ImageUsageFlags.SampledBit,
            SharingMode = SharingMode.Exclusive,
            Samples = SampleCountFlags.Count1Bit,
        };

        var result = _ctx.Vk.CreateImage(_ctx.Device, in imageInfo, null, out var image);
        if (result != Result.Success)
            throw new InvalidOperationException($"Failed to create image: {result}");

        _ctx.Vk.GetImageMemoryRequirements(_ctx.Device, image, out var memReqs);

        var allocInfo = new MemoryAllocateInfo
        {
            SType = StructureType.MemoryAllocateInfo,
            AllocationSize = memReqs.Size,
            MemoryTypeIndex = FindMemoryType(memReqs.MemoryTypeBits, MemoryPropertyFlags.DeviceLocalBit),
        };

        result = _ctx.Vk.AllocateMemory(_ctx.Device, in allocInfo, null, out var imageMemory);
        if (result != Result.Success)
            throw new InvalidOperationException($"Failed to allocate image memory: {result}");

        _ctx.Vk.BindImageMemory(_ctx.Device, image, imageMemory, 0);

        // Transition + copy via a one-shot command buffer
        TransitionAndCopyBufferToImage(stagingBuffer, image, (uint)width, (uint)height);

        // Cleanup staging
        _ctx.Vk.DestroyBuffer(_ctx.Device, stagingBuffer, null);
        _ctx.Vk.FreeMemory(_ctx.Device, stagingMemory, null);

        return (image, imageMemory);
    }

    private void TransitionAndCopyBufferToImage(Buffer buffer, Image image, uint width, uint height)
    {
        var cmdPool = CreateTransientCommandPool();
        var cmd = BeginSingleTimeCommands(cmdPool);

        // Transition to TransferDstOptimal
        var barrier = new ImageMemoryBarrier
        {
            SType = StructureType.ImageMemoryBarrier,
            OldLayout = ImageLayout.Undefined,
            NewLayout = ImageLayout.TransferDstOptimal,
            SrcQueueFamilyIndex = Vk.QueueFamilyIgnored,
            DstQueueFamilyIndex = Vk.QueueFamilyIgnored,
            Image = image,
            SubresourceRange = new ImageSubresourceRange
            {
                AspectMask = ImageAspectFlags.ColorBit,
                BaseMipLevel = 0, LevelCount = 1,
                BaseArrayLayer = 0, LayerCount = 1,
            },
            SrcAccessMask = 0,
            DstAccessMask = AccessFlags.TransferWriteBit,
        };

        _ctx.Vk.CmdPipelineBarrier(cmd,
            PipelineStageFlags.TopOfPipeBit, PipelineStageFlags.TransferBit,
            0, 0, null, 0, null, 1, &barrier);

        // Copy buffer to image
        var region = new BufferImageCopy
        {
            BufferOffset = 0,
            BufferRowLength = 0,
            BufferImageHeight = 0,
            ImageSubresource = new ImageSubresourceLayers
            {
                AspectMask = ImageAspectFlags.ColorBit,
                MipLevel = 0, BaseArrayLayer = 0, LayerCount = 1,
            },
            ImageOffset = new Offset3D(0, 0, 0),
            ImageExtent = new Extent3D(width, height, 1),
        };

        _ctx.Vk.CmdCopyBufferToImage(cmd, buffer, image, ImageLayout.TransferDstOptimal, 1, &region);

        // Transition to ShaderReadOnlyOptimal
        barrier.OldLayout = ImageLayout.TransferDstOptimal;
        barrier.NewLayout = ImageLayout.ShaderReadOnlyOptimal;
        barrier.SrcAccessMask = AccessFlags.TransferWriteBit;
        barrier.DstAccessMask = AccessFlags.ShaderReadBit;

        _ctx.Vk.CmdPipelineBarrier(cmd,
            PipelineStageFlags.TransferBit, PipelineStageFlags.FragmentShaderBit,
            0, 0, null, 0, null, 1, &barrier);

        EndSingleTimeCommands(cmd, cmdPool);
    }

    private CommandPool CreateTransientCommandPool()
    {
        var poolInfo = new CommandPoolCreateInfo
        {
            SType = StructureType.CommandPoolCreateInfo,
            QueueFamilyIndex = _ctx.GraphicsQueueFamily,
            Flags = CommandPoolCreateFlags.TransientBit,
        };
        _ctx.Vk.CreateCommandPool(_ctx.Device, in poolInfo, null, out var pool);
        return pool;
    }

    private CommandBuffer BeginSingleTimeCommands(CommandPool pool)
    {
        var allocInfo = new CommandBufferAllocateInfo
        {
            SType = StructureType.CommandBufferAllocateInfo,
            Level = CommandBufferLevel.Primary,
            CommandPool = pool,
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

    private void EndSingleTimeCommands(CommandBuffer cmd, CommandPool pool)
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

        _ctx.Vk.FreeCommandBuffers(_ctx.Device, pool, 1, in cmd);
        _ctx.Vk.DestroyCommandPool(_ctx.Device, pool, null);
    }

    private void FreeBuffer(Buffer buffer, DeviceMemory memory)
    {
        if (buffer.Handle != 0)
            _ctx.Vk.DestroyBuffer(_ctx.Device, buffer, null);
        if (memory.Handle != 0)
            _ctx.Vk.FreeMemory(_ctx.Device, memory, null);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        FreeBuffer(QuadVertexBuffer, QuadVertexMemory);
        FreeBuffer(QuadIndexBuffer, QuadIndexMemory);
        FreeBuffer(TextVertexBuffer, TextVertexMemory);
        FreeBuffer(TextIndexBuffer, TextIndexMemory);
    }
}
