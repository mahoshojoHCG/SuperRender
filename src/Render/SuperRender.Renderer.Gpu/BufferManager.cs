using System.Runtime.InteropServices;
using Silk.NET.Vulkan;
using Buffer = Silk.NET.Vulkan.Buffer;

namespace SuperRender.Renderer.Gpu;

public sealed unsafe class BufferManager : IDisposable
{
    private readonly VulkanContext _ctx;
    private readonly PersistentBuffer _quadVertexBuf;
    private readonly PersistentBuffer _quadIndexBuf;
    private readonly PersistentBuffer _textVertexBuf;
    private readonly PersistentBuffer _textIndexBuf;
    private bool _disposed;

    public BufferManager(VulkanContext ctx)
    {
        _ctx = ctx;
        _quadVertexBuf = new PersistentBuffer(ctx, BufferUsageFlags.VertexBufferBit);
        _quadIndexBuf = new PersistentBuffer(ctx, BufferUsageFlags.IndexBufferBit);
        _textVertexBuf = new PersistentBuffer(ctx, BufferUsageFlags.VertexBufferBit);
        _textIndexBuf = new PersistentBuffer(ctx, BufferUsageFlags.IndexBufferBit);
    }

    public void UploadQuads(uint frameIndex, ReadOnlySpan<QuadVertex> vertices, ReadOnlySpan<uint> indices)
    {
        if (vertices.IsEmpty || indices.IsEmpty) return;

        var vertBytes = MemoryMarshal.AsBytes(vertices);
        _quadVertexBuf.EnsureCapacity((ulong)vertBytes.Length);
        _quadVertexBuf.WriteSlot(frameIndex, vertBytes);

        var idxBytes = MemoryMarshal.AsBytes(indices);
        _quadIndexBuf.EnsureCapacity((ulong)idxBytes.Length);
        _quadIndexBuf.WriteSlot(frameIndex, idxBytes);
    }

    public void UploadTextQuads(uint frameIndex, ReadOnlySpan<TextVertex> vertices, ReadOnlySpan<uint> indices)
    {
        if (vertices.IsEmpty || indices.IsEmpty) return;

        var vertBytes = MemoryMarshal.AsBytes(vertices);
        _textVertexBuf.EnsureCapacity((ulong)vertBytes.Length);
        _textVertexBuf.WriteSlot(frameIndex, vertBytes);

        var idxBytes = MemoryMarshal.AsBytes(indices);
        _textIndexBuf.EnsureCapacity((ulong)idxBytes.Length);
        _textIndexBuf.WriteSlot(frameIndex, idxBytes);
    }

    public (Buffer Buffer, ulong Offset) GetQuadVertexBinding(uint frameIndex) =>
        _quadVertexBuf.GetBinding(frameIndex);

    public (Buffer Buffer, ulong Offset) GetQuadIndexBinding(uint frameIndex) =>
        _quadIndexBuf.GetBinding(frameIndex);

    public (Buffer Buffer, ulong Offset) GetTextVertexBinding(uint frameIndex) =>
        _textVertexBuf.GetBinding(frameIndex);

    public (Buffer Buffer, ulong Offset) GetTextIndexBinding(uint frameIndex) =>
        _textIndexBuf.GetBinding(frameIndex);

    public bool HasQuadData => _quadVertexBuf.IsAllocated;
    public bool HasTextData => _textVertexBuf.IsAllocated;

    #region Texture operations (staging pattern, unchanged)

    public (Image image, DeviceMemory memory) CreateTextureImage(byte[] pixels, int width, int height)
    {
        // Create staging buffer
        var (stagingBuffer, stagingMemory) = CreateStagingBuffer(pixels.AsSpan());

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

    /// <summary>
    /// Re-uploads pixel data to an existing GPU texture image.
    /// The image must have been created with TransferDst usage.
    /// </summary>
    public void UpdateTextureImage(Image image, byte[] pixels, int width, int height)
    {
        var (stagingBuffer, stagingMemory) = CreateStagingBuffer(pixels.AsSpan());

        var cmdPool = CreateTransientCommandPool();
        var cmd = BeginSingleTimeCommands(cmdPool);

        // Transition from ShaderReadOnly to TransferDst
        var barrier = new ImageMemoryBarrier
        {
            SType = StructureType.ImageMemoryBarrier,
            OldLayout = ImageLayout.ShaderReadOnlyOptimal,
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
            SrcAccessMask = AccessFlags.ShaderReadBit,
            DstAccessMask = AccessFlags.TransferWriteBit,
        };

        _ctx.Vk.CmdPipelineBarrier(cmd,
            PipelineStageFlags.FragmentShaderBit, PipelineStageFlags.TransferBit,
            0, 0, null, 0, null, 1, &barrier);

        // Copy staging buffer to image
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
            ImageExtent = new Extent3D((uint)width, (uint)height, 1),
        };

        _ctx.Vk.CmdCopyBufferToImage(cmd, stagingBuffer, image, ImageLayout.TransferDstOptimal, 1, &region);

        // Transition back to ShaderReadOnly
        barrier.OldLayout = ImageLayout.TransferDstOptimal;
        barrier.NewLayout = ImageLayout.ShaderReadOnlyOptimal;
        barrier.SrcAccessMask = AccessFlags.TransferWriteBit;
        barrier.DstAccessMask = AccessFlags.ShaderReadBit;

        _ctx.Vk.CmdPipelineBarrier(cmd,
            PipelineStageFlags.TransferBit, PipelineStageFlags.FragmentShaderBit,
            0, 0, null, 0, null, 1, &barrier);

        EndSingleTimeCommands(cmd, cmdPool);

        // Cleanup staging
        _ctx.Vk.DestroyBuffer(_ctx.Device, stagingBuffer, null);
        _ctx.Vk.FreeMemory(_ctx.Device, stagingMemory, null);
    }

    /// <summary>
    /// Re-uploads only the specified dirty regions to an existing GPU texture image.
    /// Much more efficient than a full re-upload when only a few new glyphs were rendered.
    /// </summary>
    public void UpdateTextureImageRegions(Image image, byte[] pixels, int atlasWidth,
        IReadOnlyList<(int X, int Y, int Width, int Height)> regions)
    {
        if (regions.Count == 0) return;

        // Calculate total staging buffer size (sum of all region pixel counts)
        long totalBytes = 0;
        foreach (var r in regions)
            totalBytes += r.Width * r.Height;

        // Build staging data: pack dirty region pixels contiguously
        var stagingData = new byte[totalBytes];
        long offset = 0;
        foreach (var r in regions)
        {
            for (int row = 0; row < r.Height; row++)
            {
                int srcStart = (r.Y + row) * atlasWidth + r.X;
                Array.Copy(pixels, srcStart, stagingData, offset, r.Width);
                offset += r.Width;
            }
        }

        var (stagingBuffer, stagingMemory) = CreateStagingBuffer(stagingData.AsSpan());

        var cmdPool = CreateTransientCommandPool();
        var cmd = BeginSingleTimeCommands(cmdPool);

        // Transition from ShaderReadOnly to TransferDst
        var barrier = new ImageMemoryBarrier
        {
            SType = StructureType.ImageMemoryBarrier,
            OldLayout = ImageLayout.ShaderReadOnlyOptimal,
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
            SrcAccessMask = AccessFlags.ShaderReadBit,
            DstAccessMask = AccessFlags.TransferWriteBit,
        };

        _ctx.Vk.CmdPipelineBarrier(cmd,
            PipelineStageFlags.FragmentShaderBit, PipelineStageFlags.TransferBit,
            0, 0, null, 0, null, 1, &barrier);

        // Build one BufferImageCopy per dirty region
        var copyRegions = new BufferImageCopy[regions.Count];
        long bufferOffset = 0;
        for (int i = 0; i < regions.Count; i++)
        {
            var r = regions[i];
            copyRegions[i] = new BufferImageCopy
            {
                BufferOffset = (ulong)bufferOffset,
                BufferRowLength = (uint)r.Width,
                BufferImageHeight = (uint)r.Height,
                ImageSubresource = new ImageSubresourceLayers
                {
                    AspectMask = ImageAspectFlags.ColorBit,
                    MipLevel = 0, BaseArrayLayer = 0, LayerCount = 1,
                },
                ImageOffset = new Offset3D(r.X, r.Y, 0),
                ImageExtent = new Extent3D((uint)r.Width, (uint)r.Height, 1),
            };
            bufferOffset += r.Width * r.Height;
        }

        fixed (BufferImageCopy* pRegions = copyRegions)
        {
            _ctx.Vk.CmdCopyBufferToImage(cmd, stagingBuffer, image,
                ImageLayout.TransferDstOptimal, (uint)copyRegions.Length, pRegions);
        }

        // Transition back to ShaderReadOnly
        barrier.OldLayout = ImageLayout.TransferDstOptimal;
        barrier.NewLayout = ImageLayout.ShaderReadOnlyOptimal;
        barrier.SrcAccessMask = AccessFlags.TransferWriteBit;
        barrier.DstAccessMask = AccessFlags.ShaderReadBit;

        _ctx.Vk.CmdPipelineBarrier(cmd,
            PipelineStageFlags.TransferBit, PipelineStageFlags.FragmentShaderBit,
            0, 0, null, 0, null, 1, &barrier);

        EndSingleTimeCommands(cmd, cmdPool);

        // Cleanup staging
        _ctx.Vk.DestroyBuffer(_ctx.Device, stagingBuffer, null);
        _ctx.Vk.FreeMemory(_ctx.Device, stagingMemory, null);
    }

    #endregion

    #region Staging buffer helpers

    private (Buffer buffer, DeviceMemory memory) CreateStagingBuffer(ReadOnlySpan<byte> data)
    {
        var size = (ulong)data.Length;

        var bufferInfo = new BufferCreateInfo
        {
            SType = StructureType.BufferCreateInfo,
            Size = size,
            Usage = BufferUsageFlags.TransferSrcBit,
            SharingMode = SharingMode.Exclusive,
        };

        var result = _ctx.Vk.CreateBuffer(_ctx.Device, in bufferInfo, null, out var buffer);
        if (result != Result.Success)
            throw new InvalidOperationException($"Failed to create staging buffer: {result}");

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
            throw new InvalidOperationException($"Failed to allocate staging buffer memory: {result}");

        _ctx.Vk.BindBufferMemory(_ctx.Device, buffer, memory, 0);

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

    #endregion

    #region Command buffer helpers

    private void TransitionAndCopyBufferToImage(Buffer buffer, Image image, uint width, uint height)
    {
        var cmdPool = CreateTransientCommandPool();
        var cmd = BeginSingleTimeCommands(cmdPool);

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

    #endregion

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _quadVertexBuf.Dispose();
        _quadIndexBuf.Dispose();
        _textVertexBuf.Dispose();
        _textIndexBuf.Dispose();
    }

    /// <summary>
    /// A persistently mapped Vulkan buffer with per-frame ring slots.
    /// Eliminates per-frame vkAllocateMemory/vkFreeMemory/vkMapMemory calls.
    /// Two slots (matching MaxFramesInFlight=2): slot 0 and slot 1 are used by
    /// alternating frames so the GPU can read one while the CPU writes the other.
    /// </summary>
    private sealed class PersistentBuffer : IDisposable
    {
        private readonly VulkanContext _ctx;
        private readonly BufferUsageFlags _usage;

        private Buffer _buffer;
        private DeviceMemory _memory;
        private void* _mappedPtr;
        private ulong _slotCapacity;

        private const int SlotCount = 2; // matches VulkanRenderer.MaxFramesInFlight

        public bool IsAllocated => _buffer.Handle != 0;

        public PersistentBuffer(VulkanContext ctx, BufferUsageFlags usage)
        {
            _ctx = ctx;
            _usage = usage;
        }

        /// <summary>
        /// Ensures each ring slot can hold at least <paramref name="perSlotBytes"/> bytes.
        /// If the buffer needs to grow, waits for all GPU work to complete first.
        /// </summary>
        public void EnsureCapacity(ulong perSlotBytes)
        {
            if (perSlotBytes == 0 || perSlotBytes <= _slotCapacity) return;

            // Wait for all GPU work before destroying the buffer that may be in use
            if (_buffer.Handle != 0)
            {
                _ctx.Vk.DeviceWaitIdle(_ctx.Device);
                _ctx.Vk.UnmapMemory(_ctx.Device, _memory);
                _ctx.Vk.DestroyBuffer(_ctx.Device, _buffer, null);
                _ctx.Vk.FreeMemory(_ctx.Device, _memory, null);
            }

            // Grow to next power of 2, minimum 64KB per slot
            _slotCapacity = NextPowerOf2(Math.Max(perSlotBytes, 65536));
            var totalSize = _slotCapacity * SlotCount;

            var bufferInfo = new BufferCreateInfo
            {
                SType = StructureType.BufferCreateInfo,
                Size = totalSize,
                Usage = _usage,
                SharingMode = SharingMode.Exclusive,
            };

            var result = _ctx.Vk.CreateBuffer(_ctx.Device, in bufferInfo, null, out _buffer);
            if (result != Result.Success)
                throw new InvalidOperationException($"Failed to create persistent buffer: {result}");

            _ctx.Vk.GetBufferMemoryRequirements(_ctx.Device, _buffer, out var memReqs);

            var allocInfo = new MemoryAllocateInfo
            {
                SType = StructureType.MemoryAllocateInfo,
                AllocationSize = memReqs.Size,
                MemoryTypeIndex = FindHostVisibleMemoryType(memReqs.MemoryTypeBits),
            };

            result = _ctx.Vk.AllocateMemory(_ctx.Device, in allocInfo, null, out _memory);
            if (result != Result.Success)
                throw new InvalidOperationException($"Failed to allocate persistent buffer memory: {result}");

            _ctx.Vk.BindBufferMemory(_ctx.Device, _buffer, _memory, 0);

            // Map once, keep mapped until Dispose
            void* ptr;
            _ctx.Vk.MapMemory(_ctx.Device, _memory, 0, totalSize, 0, &ptr);
            _mappedPtr = ptr;
        }

        /// <summary>
        /// Writes data into the specified frame slot's region of the persistent buffer.
        /// </summary>
        public void WriteSlot(uint slotIndex, ReadOnlySpan<byte> data)
        {
            if (data.IsEmpty || _mappedPtr == null) return;
            var byteOffset = slotIndex * _slotCapacity;
            data.CopyTo(new Span<byte>((byte*)_mappedPtr + byteOffset, data.Length));
        }

        /// <summary>
        /// Returns the buffer handle and byte offset for the specified frame slot,
        /// suitable for vkCmdBindVertexBuffers / vkCmdBindIndexBuffer.
        /// </summary>
        public (Buffer Buffer, ulong Offset) GetBinding(uint slotIndex)
        {
            return (_buffer, slotIndex * _slotCapacity);
        }

        private uint FindHostVisibleMemoryType(uint typeFilter)
        {
            _ctx.Vk.GetPhysicalDeviceMemoryProperties(_ctx.PhysicalDevice, out var memProps);
            var flags = MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit;
            for (var i = 0u; i < memProps.MemoryTypeCount; i++)
            {
                if ((typeFilter & (1u << (int)i)) != 0 &&
                    (memProps.MemoryTypes[(int)i].PropertyFlags & flags) == flags)
                    return i;
            }
            throw new InvalidOperationException("No suitable host-visible memory type.");
        }

        private static ulong NextPowerOf2(ulong v)
        {
            v--;
            v |= v >> 1;
            v |= v >> 2;
            v |= v >> 4;
            v |= v >> 8;
            v |= v >> 16;
            v |= v >> 32;
            return v + 1;
        }

        public void Dispose()
        {
            if (_buffer.Handle != 0)
            {
                _ctx.Vk.UnmapMemory(_ctx.Device, _memory);
                _ctx.Vk.DestroyBuffer(_ctx.Device, _buffer, null);
                _ctx.Vk.FreeMemory(_ctx.Device, _memory, null);
                _buffer = default;
                _memory = default;
                _mappedPtr = null;
            }
        }
    }
}
