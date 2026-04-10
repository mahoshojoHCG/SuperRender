using System.Numerics;
using Silk.NET.Vulkan;
using SuperRender.Core.Painting;

namespace SuperRender.Demo;

public sealed unsafe class VulkanRenderer : IDisposable
{
    private readonly VulkanContext _ctx;
    private SwapchainManager _swapchain;
    private PipelineManager _pipelines;
    private readonly BufferManager _buffers;
    private readonly FontAtlas _fontAtlas;
    private readonly QuadRenderer _quadRenderer;
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

    public FontAtlas FontAtlasData => _fontAtlas;

    public VulkanRenderer(Silk.NET.Windowing.IWindow window)
    {
        _ctx = new VulkanContext(window);
        _fontAtlas = new FontAtlas();
        _quadRenderer = new QuadRenderer();
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

        // Build GPU data from paint list
        var (quadVerts, quadIndices) = _quadRenderer.BuildQuadBatch(paintList);
        var (textVerts, textIndices) = _textRenderer.BuildTextBatch(paintList);

        _buffers.UploadQuads(quadVerts, quadIndices);
        _buffers.UploadTextQuads(textVerts, textIndices);

        // Record command buffer
        var cmd = _commandBuffers[_currentFrame];
        vk.ResetCommandBuffer(cmd, 0);
        RecordCommandBuffer(cmd, imageIndex);

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

    private void RecordCommandBuffer(CommandBuffer cmd, uint imageIndex)
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

        // Push projection matrix (orthographic, top-left origin, Y-down)
        // Vulkan NDC: Y=-1 is top, Y=+1 is bottom (opposite of OpenGL)
        // CreateOrthographicOffCenter params: (left, right, bottom, top, near, far)
        // We want Y=0 at top → NDC Y=-1, Y=height at bottom → NDC Y=+1
        var projection = Matrix4x4.CreateOrthographicOffCenter(
            0, _swapchain.Extent.Width, 0, _swapchain.Extent.Height, -1, 1);

        // Draw quads
        if (_pipelines.QuadPipeline.Handle != 0 && _buffers.QuadIndexCount > 0)
        {
            vk.CmdBindPipeline(cmd, PipelineBindPoint.Graphics, _pipelines.QuadPipeline);
            vk.CmdPushConstants(cmd, _pipelines.QuadPipelineLayout,
                ShaderStageFlags.VertexBit, 0, 64, &projection);

            var vbuf = _buffers.QuadVertexBuffer;
            ulong offset = 0;
            vk.CmdBindVertexBuffers(cmd, 0, 1, &vbuf, &offset);
            vk.CmdBindIndexBuffer(cmd, _buffers.QuadIndexBuffer, 0, IndexType.Uint32);
            vk.CmdDrawIndexed(cmd, _buffers.QuadIndexCount, 1, 0, 0, 0);
        }

        // Draw text
        if (_pipelines.TextPipeline.Handle != 0 && _buffers.TextIndexCount > 0)
        {
            vk.CmdBindPipeline(cmd, PipelineBindPoint.Graphics, _pipelines.TextPipeline);
            vk.CmdPushConstants(cmd, _pipelines.TextPipelineLayout,
                ShaderStageFlags.VertexBit, 0, 64, &projection);

            var descSet = _textDescriptorSet;
            vk.CmdBindDescriptorSets(cmd, PipelineBindPoint.Graphics,
                _pipelines.TextPipelineLayout, 0, 1, &descSet, 0, null);

            var vbuf = _buffers.TextVertexBuffer;
            ulong offset = 0;
            vk.CmdBindVertexBuffers(cmd, 0, 1, &vbuf, &offset);
            vk.CmdBindIndexBuffer(cmd, _buffers.TextIndexBuffer, 0, IndexType.Uint32);
            vk.CmdDrawIndexed(cmd, _buffers.TextIndexCount, 1, 0, 0, 0);
        }

        vk.CmdEndRenderPass(cmd);
        vk.EndCommandBuffer(cmd);
    }

    private void CreateFontAtlasTexture()
    {
        (_atlasImage, _atlasMemory) = _buffers.CreateTextureImage(
            _fontAtlas.PixelData, _fontAtlas.AtlasWidth, _fontAtlas.AtlasHeight);

        // Image view
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

        // Sampler
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

        // Descriptor pool
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

        // Allocate descriptor set
        var layout = _pipelines.TextDescriptorSetLayout;
        var allocInfo = new DescriptorSetAllocateInfo
        {
            SType = StructureType.DescriptorSetAllocateInfo,
            DescriptorPool = _descriptorPool,
            DescriptorSetCount = 1,
            PSetLayouts = &layout,
        };
        _ctx.Vk.AllocateDescriptorSets(_ctx.Device, in allocInfo, out _textDescriptorSet);

        // Update descriptor set with font atlas
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

        // Re-create descriptor resources for the text pipeline
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
        _ctx.Dispose();
    }
}
