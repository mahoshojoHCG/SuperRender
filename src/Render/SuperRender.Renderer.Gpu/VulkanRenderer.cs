using System.Numerics;
using System.Runtime.InteropServices;
using Silk.NET.Vulkan;
using SuperRender.Renderer.Rendering.Painting;

namespace SuperRender.Renderer.Gpu;

public sealed unsafe class VulkanRenderer : IDisposable
{
    private readonly VulkanContext _ctx;
    private SwapchainManager _swapchain;
    private PipelineManager _pipelines;
    private readonly BufferManager _buffers;
    private readonly SystemFontLocator _fontLocator;
    private readonly FontAtlas _fontAtlas;
    private readonly TextRenderer _textRenderer;

    // Font atlas GPU resources
    private Image _atlasImage;
    private DeviceMemory _atlasMemory;
    private ImageView _atlasImageView;
    private Sampler _atlasSampler;
    private DescriptorPool _descriptorPool;
    private DescriptorSet _textDescriptorSet;

    // Sync
    private Silk.NET.Vulkan.Semaphore[] _imageAvailableSemaphores = [];
    private Silk.NET.Vulkan.Semaphore[] _renderFinishedSemaphores = [];
    private Fence[] _inFlightFences = [];
    private CommandPool _commandPool;
    private CommandBuffer[] _commandBuffers = [];
    private uint _currentFrame;
    private const int MaxFramesInFlight = 2;

    private bool _framebufferResized;
    private uint _width, _height;
    private bool _disposed;

    /// <summary>
    /// Content scale factor for HiDPI support.
    /// 1.0 = standard display, 2.0 = Retina/HiDPI.
    /// The projection matrix uses logical (CSS) pixels; this factor maps them to physical pixels.
    /// </summary>
    public float ContentScale { get; set; } = 1.0f;

    public FontAtlas FontAtlasData => _fontAtlas;

    public VulkanRenderer(Silk.NET.Windowing.IWindow window, float contentScale = 1.0f)
    {
        ContentScale = contentScale;
        _ctx = new VulkanContext(window);
        _fontLocator = new SystemFontLocator();
        _fontAtlas = new FontAtlas(_fontLocator, contentScale);
        _textRenderer = new TextRenderer(_fontAtlas);

        var fbSize = window.FramebufferSize;
        _width = (uint)fbSize.X;
        _height = (uint)fbSize.Y;

        _swapchain = new SwapchainManager(_ctx, _width, _height);
        _pipelines = new PipelineManager(_ctx, _swapchain.RenderPass, _swapchain.Extent);
        _buffers = new BufferManager(_ctx);

        CreateFontAtlasTexture();
        CreateDescriptorResources();
        CreateCommandPool();
        AllocateCommandBuffers();
        CreateSyncObjects();
    }

    public void RenderFrame(PaintList? paintList)
    {
        if (paintList == null) return;

        var vk = _ctx.Vk;

        vk.WaitForFences(_ctx.Device, 1, in _inFlightFences[_currentFrame], true, ulong.MaxValue);

        uint imageIndex;
        var acquireResult = _swapchain.KhrSwapchainApi.AcquireNextImage(
            _ctx.Device, _swapchain.Swapchain, ulong.MaxValue,
            _imageAvailableSemaphores[_currentFrame], default, &imageIndex);

        if (acquireResult == Result.ErrorOutOfDateKhr)
        {
            RecreateSwapchain();
            return;
        }
        if (acquireResult != Result.Success && acquireResult != Result.SuboptimalKhr)
            return;

        vk.ResetFences(_ctx.Device, 1, in _inFlightFences[_currentFrame]);

        // Build draw segments from paint list (respects command order and clip rects)
        var segments = BuildDrawSegments(paintList);

        // Re-upload font atlas texture if new glyphs were rendered on demand
        if (_fontAtlas.IsDirty)
        {
            if (_fontAtlas.DirtyRegions.Count > 0)
            {
                _buffers.UpdateTextureImageRegions(_atlasImage, _fontAtlas.PixelData,
                    _fontAtlas.AtlasWidth, _fontAtlas.DirtyRegions);
            }
            else
            {
                _buffers.UpdateTextureImage(_atlasImage, _fontAtlas.PixelData,
                    _fontAtlas.AtlasWidth, _fontAtlas.AtlasHeight);
            }
            _fontAtlas.ClearDirty();
        }

        // Collect all quad and text data across segments into single buffers
        var allQuadVerts = new List<QuadVertex>();
        var allQuadIdx = new List<uint>();
        var allTextVerts = new List<TextVertex>();
        var allTextIdx = new List<uint>();

        foreach (var seg in segments)
        {
            seg.QuadIndexOffset = (uint)allQuadIdx.Count;
            seg.QuadVertexOffset = (uint)allQuadVerts.Count;
            seg.TextIndexOffset = (uint)allTextIdx.Count;
            seg.TextVertexOffset = (uint)allTextVerts.Count;

            allQuadIdx.AddRange(seg.QuadIndices);
            allQuadVerts.AddRange(seg.QuadVertices);

            allTextIdx.AddRange(seg.TextIndices);
            allTextVerts.AddRange(seg.TextVertices);
        }

        // Upload directly into persistent mapped GPU buffers (no per-frame alloc/free)
        _buffers.UploadQuads(_currentFrame,
            CollectionsMarshal.AsSpan(allQuadVerts),
            CollectionsMarshal.AsSpan(allQuadIdx));
        _buffers.UploadTextQuads(_currentFrame,
            CollectionsMarshal.AsSpan(allTextVerts),
            CollectionsMarshal.AsSpan(allTextIdx));

        // Record command buffer
        var cmd = _commandBuffers[_currentFrame];
        vk.ResetCommandBuffer(cmd, 0);
        RecordCommandBuffer(cmd, imageIndex, segments);

        // Submit
        var waitSemaphore = _imageAvailableSemaphores[_currentFrame];
        var waitStage = PipelineStageFlags.ColorAttachmentOutputBit;
        var signalSemaphore = _renderFinishedSemaphores[_currentFrame];

        var submitInfo = new SubmitInfo
        {
            SType = StructureType.SubmitInfo,
            WaitSemaphoreCount = 1,
            PWaitSemaphores = &waitSemaphore,
            PWaitDstStageMask = &waitStage,
            CommandBufferCount = 1,
            PCommandBuffers = &cmd,
            SignalSemaphoreCount = 1,
            PSignalSemaphores = &signalSemaphore,
        };

        var submitResult = vk.QueueSubmit(_ctx.GraphicsQueue, 1, in submitInfo, _inFlightFences[_currentFrame]);
        if (submitResult != Result.Success)
            return;

        // Present
        var swapchain = _swapchain.Swapchain;
        var presentInfo = new PresentInfoKHR
        {
            SType = StructureType.PresentInfoKhr,
            WaitSemaphoreCount = 1,
            PWaitSemaphores = &signalSemaphore,
            SwapchainCount = 1,
            PSwapchains = &swapchain,
            PImageIndices = &imageIndex,
        };

        var presentResult = _swapchain.KhrSwapchainApi.QueuePresent(_ctx.PresentQueue, in presentInfo);

        if (presentResult == Result.ErrorOutOfDateKhr || presentResult == Result.SuboptimalKhr || _framebufferResized)
        {
            _framebufferResized = false;
            RecreateSwapchain();
        }

        _currentFrame = (_currentFrame + 1) % MaxFramesInFlight;
    }

    public void OnResize(int width, int height)
    {
        _width = (uint)width;
        _height = (uint)height;
        _framebufferResized = true;
    }

    private void RecordCommandBuffer(CommandBuffer cmd, uint imageIndex, List<DrawSegment> segments)
    {
        var vk = _ctx.Vk;

        var beginInfo = new CommandBufferBeginInfo { SType = StructureType.CommandBufferBeginInfo };
        vk.BeginCommandBuffer(cmd, in beginInfo);

        var clearValue = new ClearValue
        {
            Color = new ClearColorValue(1f, 1f, 1f, 1f), // White background
        };

        var renderPassBegin = new RenderPassBeginInfo
        {
            SType = StructureType.RenderPassBeginInfo,
            RenderPass = _swapchain.RenderPass,
            Framebuffer = _swapchain.Framebuffers[imageIndex],
            RenderArea = new Rect2D { Offset = new Offset2D(0, 0), Extent = _swapchain.Extent },
            ClearValueCount = 1,
            PClearValues = &clearValue,
        };

        vk.CmdBeginRenderPass(cmd, in renderPassBegin, SubpassContents.Inline);

        // HiDPI: layout uses logical (CSS) pixels; projection maps them to physical framebuffer pixels.
        float logicalWidth = _swapchain.Extent.Width / ContentScale;
        float logicalHeight = _swapchain.Extent.Height / ContentScale;
        var projection = Matrix4x4.CreateOrthographicOffCenter(
            0, logicalWidth, 0, logicalHeight, -1, 1);

        // Draw each segment in order with its scissor rect
        foreach (var seg in segments)
        {
            // Set scissor (in physical pixels)
            var scissor = new Rect2D
            {
                Offset = new Offset2D(
                    (int)(seg.ScissorX * ContentScale),
                    (int)(seg.ScissorY * ContentScale)),
                Extent = new Extent2D(
                    (uint)(seg.ScissorW * ContentScale),
                    (uint)(seg.ScissorH * ContentScale)),
            };

            // Draw quads for this segment
            if (_pipelines.QuadPipeline.Handle != 0 && seg.QuadIndexCount > 0 && _buffers.HasQuadData)
            {
                vk.CmdBindPipeline(cmd, PipelineBindPoint.Graphics, _pipelines.QuadPipeline);
                vk.CmdSetScissor(cmd, 0, 1, &scissor);
                vk.CmdPushConstants(cmd, _pipelines.QuadPipelineLayout,
                    ShaderStageFlags.VertexBit, 0, 64, &projection);

                var (qvBuf, qvOff) = _buffers.GetQuadVertexBinding(_currentFrame);
                vk.CmdBindVertexBuffers(cmd, 0, 1, &qvBuf, &qvOff);
                var (qiBuf, qiOff) = _buffers.GetQuadIndexBinding(_currentFrame);
                vk.CmdBindIndexBuffer(cmd, qiBuf, qiOff, IndexType.Uint32);
                vk.CmdDrawIndexed(cmd, seg.QuadIndexCount, 1, seg.QuadIndexOffset, (int)seg.QuadVertexOffset, 0);
            }

            // Draw text for this segment
            if (_pipelines.TextPipeline.Handle != 0 && seg.TextIndexCount > 0 && _buffers.HasTextData)
            {
                vk.CmdBindPipeline(cmd, PipelineBindPoint.Graphics, _pipelines.TextPipeline);
                vk.CmdSetScissor(cmd, 0, 1, &scissor);
                vk.CmdPushConstants(cmd, _pipelines.TextPipelineLayout,
                    ShaderStageFlags.VertexBit, 0, 64, &projection);

                var descSet = _textDescriptorSet;
                vk.CmdBindDescriptorSets(cmd, PipelineBindPoint.Graphics,
                    _pipelines.TextPipelineLayout, 0, 1, &descSet, 0, null);

                var (tvBuf, tvOff) = _buffers.GetTextVertexBinding(_currentFrame);
                vk.CmdBindVertexBuffers(cmd, 0, 1, &tvBuf, &tvOff);
                var (tiBuf, tiOff) = _buffers.GetTextIndexBinding(_currentFrame);
                vk.CmdBindIndexBuffer(cmd, tiBuf, tiOff, IndexType.Uint32);
                vk.CmdDrawIndexed(cmd, seg.TextIndexCount, 1, seg.TextIndexOffset, (int)seg.TextVertexOffset, 0);
            }
        }

        vk.CmdEndRenderPass(cmd);
        vk.EndCommandBuffer(cmd);
    }

    /// <summary>
    /// A draw segment: a batch of quads and text commands that share the same scissor rect.
    /// Segments are rendered in order to preserve paint command z-ordering and clipping.
    /// </summary>
    private sealed class DrawSegment
    {
        public List<QuadVertex> QuadVertices { get; } = [];
        public List<uint> QuadIndices { get; } = [];
        public List<TextVertex> TextVertices { get; } = [];
        public List<uint> TextIndices { get; } = [];

        // Scissor rect in logical pixels
        public float ScissorX, ScissorY, ScissorW, ScissorH;

        // Offsets into the combined buffer (set during upload)
        public uint QuadIndexOffset;
        public uint QuadVertexOffset;
        public uint TextIndexOffset;
        public uint TextVertexOffset;

        public uint QuadIndexCount => (uint)QuadIndices.Count;
        public uint TextIndexCount => (uint)TextIndices.Count;
    }

    private List<DrawSegment> BuildDrawSegments(PaintList paintList)
    {
        float logicalWidth = _swapchain.Extent.Width / ContentScale;
        float logicalHeight = _swapchain.Extent.Height / ContentScale;

        var segments = new List<DrawSegment>();
        var clipStack = new Stack<(float x, float y, float w, float h)>();
        clipStack.Push((0, 0, logicalWidth, logicalHeight));

        var current = new DrawSegment();
        var (cx, cy, cw, ch) = clipStack.Peek();
        current.ScissorX = cx; current.ScissorY = cy;
        current.ScissorW = cw; current.ScissorH = ch;

        foreach (var cmd in paintList.Commands)
        {
            switch (cmd)
            {
                case PushClipCommand clip:
                {
                    // Flush current segment if it has content
                    if (current.QuadIndices.Count > 0 || current.TextIndices.Count > 0)
                    {
                        segments.Add(current);
                        current = new DrawSegment();
                    }

                    // Intersect new clip with current clip
                    var (px, py, pw, ph) = clipStack.Peek();
                    float nx = Math.Max(px, clip.Rect.X);
                    float ny = Math.Max(py, clip.Rect.Y);
                    float nr = Math.Min(px + pw, clip.Rect.X + clip.Rect.Width);
                    float nb = Math.Min(py + ph, clip.Rect.Y + clip.Rect.Height);
                    float nw = Math.Max(0, nr - nx);
                    float nh = Math.Max(0, nb - ny);

                    clipStack.Push((nx, ny, nw, nh));
                    current.ScissorX = nx; current.ScissorY = ny;
                    current.ScissorW = nw; current.ScissorH = nh;
                    break;
                }

                case PopClipCommand:
                {
                    // Flush current segment
                    if (current.QuadIndices.Count > 0 || current.TextIndices.Count > 0)
                    {
                        segments.Add(current);
                        current = new DrawSegment();
                    }

                    if (clipStack.Count > 1) clipStack.Pop();
                    var (rx, ry, rw, rh) = clipStack.Peek();
                    current.ScissorX = rx; current.ScissorY = ry;
                    current.ScissorW = rw; current.ScissorH = rh;
                    break;
                }

                case FillRectCommand fill:
                    QuadRenderer.AddFilledRect(current.QuadVertices, current.QuadIndices,
                        fill.Rect.X, fill.Rect.Y, fill.Rect.Width, fill.Rect.Height, fill.Color);
                    break;

                case StrokeRectCommand stroke:
                    QuadRenderer.AddStrokeRect(current.QuadVertices, current.QuadIndices,
                        stroke.Rect.X, stroke.Rect.Y, stroke.Rect.Width, stroke.Rect.Height,
                        stroke.LineWidth, stroke.Color);
                    break;

                case DrawTextCommand text:
                    _textRenderer.EmitTextQuads(current.TextVertices, current.TextIndices, text);
                    break;
            }
        }

        // Add final segment
        if (current.QuadIndices.Count > 0 || current.TextIndices.Count > 0)
            segments.Add(current);

        return segments;
    }

    private void CreateFontAtlasTexture()
    {
        (_atlasImage, _atlasMemory) = _buffers.CreateTextureImage(
            _fontAtlas.PixelData, _fontAtlas.AtlasWidth, _fontAtlas.AtlasHeight);

        var viewInfo = new ImageViewCreateInfo
        {
            SType = StructureType.ImageViewCreateInfo,
            Image = _atlasImage,
            ViewType = ImageViewType.Type2D,
            Format = Format.R8Unorm,
            SubresourceRange = new ImageSubresourceRange
            {
                AspectMask = ImageAspectFlags.ColorBit,
                BaseMipLevel = 0, LevelCount = 1,
                BaseArrayLayer = 0, LayerCount = 1,
            },
        };
        _ctx.Vk.CreateImageView(_ctx.Device, in viewInfo, null, out _atlasImageView);

        var samplerInfo = new SamplerCreateInfo
        {
            SType = StructureType.SamplerCreateInfo,
            MagFilter = Filter.Linear,
            MinFilter = Filter.Linear,
            AddressModeU = SamplerAddressMode.ClampToEdge,
            AddressModeV = SamplerAddressMode.ClampToEdge,
            AddressModeW = SamplerAddressMode.ClampToEdge,
            AnisotropyEnable = false,
            BorderColor = BorderColor.IntOpaqueBlack,
            UnnormalizedCoordinates = false,
            CompareEnable = false,
            MipmapMode = SamplerMipmapMode.Linear,
        };
        _ctx.Vk.CreateSampler(_ctx.Device, in samplerInfo, null, out _atlasSampler);
    }

    private void CreateDescriptorResources()
    {
        if (_pipelines.TextDescriptorSetLayout.Handle == 0) return;

        var poolSize = new DescriptorPoolSize
        {
            Type = DescriptorType.CombinedImageSampler,
            DescriptorCount = 1,
        };

        var poolInfo = new DescriptorPoolCreateInfo
        {
            SType = StructureType.DescriptorPoolCreateInfo,
            PoolSizeCount = 1,
            PPoolSizes = &poolSize,
            MaxSets = 1,
        };
        _ctx.Vk.CreateDescriptorPool(_ctx.Device, in poolInfo, null, out _descriptorPool);

        var layout = _pipelines.TextDescriptorSetLayout;
        var allocInfo = new DescriptorSetAllocateInfo
        {
            SType = StructureType.DescriptorSetAllocateInfo,
            DescriptorPool = _descriptorPool,
            DescriptorSetCount = 1,
            PSetLayouts = &layout,
        };
        _ctx.Vk.AllocateDescriptorSets(_ctx.Device, in allocInfo, out _textDescriptorSet);

        var imageInfo = new DescriptorImageInfo
        {
            ImageLayout = ImageLayout.ShaderReadOnlyOptimal,
            ImageView = _atlasImageView,
            Sampler = _atlasSampler,
        };

        var write = new WriteDescriptorSet
        {
            SType = StructureType.WriteDescriptorSet,
            DstSet = _textDescriptorSet,
            DstBinding = 0,
            DstArrayElement = 0,
            DescriptorType = DescriptorType.CombinedImageSampler,
            DescriptorCount = 1,
            PImageInfo = &imageInfo,
        };

        _ctx.Vk.UpdateDescriptorSets(_ctx.Device, 1, in write, 0, null);
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

    private void AllocateCommandBuffers()
    {
        _commandBuffers = new CommandBuffer[MaxFramesInFlight];

        var allocInfo = new CommandBufferAllocateInfo
        {
            SType = StructureType.CommandBufferAllocateInfo,
            CommandPool = _commandPool,
            Level = CommandBufferLevel.Primary,
            CommandBufferCount = MaxFramesInFlight,
        };

        fixed (CommandBuffer* pCmds = _commandBuffers)
        {
            _ctx.Vk.AllocateCommandBuffers(_ctx.Device, in allocInfo, pCmds);
        }
    }

    private void CreateSyncObjects()
    {
        _imageAvailableSemaphores = new Silk.NET.Vulkan.Semaphore[MaxFramesInFlight];
        _renderFinishedSemaphores = new Silk.NET.Vulkan.Semaphore[MaxFramesInFlight];
        _inFlightFences = new Fence[MaxFramesInFlight];

        var semaphoreInfo = new SemaphoreCreateInfo { SType = StructureType.SemaphoreCreateInfo };
        var fenceInfo = new FenceCreateInfo
        {
            SType = StructureType.FenceCreateInfo,
            Flags = FenceCreateFlags.SignaledBit,
        };

        for (int i = 0; i < MaxFramesInFlight; i++)
        {
            _ctx.Vk.CreateSemaphore(_ctx.Device, in semaphoreInfo, null, out _imageAvailableSemaphores[i]);
            _ctx.Vk.CreateSemaphore(_ctx.Device, in semaphoreInfo, null, out _renderFinishedSemaphores[i]);
            _ctx.Vk.CreateFence(_ctx.Device, in fenceInfo, null, out _inFlightFences[i]);
        }
    }

    private void RecreateSwapchain()
    {
        if (_width == 0 || _height == 0) return;

        _ctx.Vk.DeviceWaitIdle(_ctx.Device);

        _pipelines.Dispose();
        _swapchain.Recreate(_width, _height);
        _pipelines = new PipelineManager(_ctx, _swapchain.RenderPass, _swapchain.Extent);

        if (_descriptorPool.Handle != 0)
            _ctx.Vk.DestroyDescriptorPool(_ctx.Device, _descriptorPool, null);
        CreateDescriptorResources();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _ctx.Vk.DeviceWaitIdle(_ctx.Device);

        for (int i = 0; i < MaxFramesInFlight; i++)
        {
            _ctx.Vk.DestroySemaphore(_ctx.Device, _imageAvailableSemaphores[i], null);
            _ctx.Vk.DestroySemaphore(_ctx.Device, _renderFinishedSemaphores[i], null);
            _ctx.Vk.DestroyFence(_ctx.Device, _inFlightFences[i], null);
        }

        _ctx.Vk.DestroyCommandPool(_ctx.Device, _commandPool, null);

        if (_descriptorPool.Handle != 0)
            _ctx.Vk.DestroyDescriptorPool(_ctx.Device, _descriptorPool, null);

        _ctx.Vk.DestroySampler(_ctx.Device, _atlasSampler, null);
        _ctx.Vk.DestroyImageView(_ctx.Device, _atlasImageView, null);
        _ctx.Vk.DestroyImage(_ctx.Device, _atlasImage, null);
        _ctx.Vk.FreeMemory(_ctx.Device, _atlasMemory, null);

        _buffers.Dispose();
        _pipelines.Dispose();
        _swapchain.Dispose();
        _fontAtlas.Dispose();
        _fontLocator.Dispose();
        _ctx.Dispose();
    }
}
