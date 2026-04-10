using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;

namespace SuperRender.Gpu;

public sealed unsafe class SwapchainManager : IDisposable
{
    private readonly VulkanContext _ctx;

    public SwapchainKHR Swapchain { get; private set; }
    public Format ImageFormat { get; private set; }
    public Extent2D Extent { get; private set; }
    public ImageView[] ImageViews { get; private set; } = [];
    public Framebuffer[] Framebuffers { get; private set; } = [];
    public RenderPass RenderPass { get; private set; }
    public KhrSwapchain KhrSwapchainApi { get; }

    private Image[] _swapchainImages = [];
    private bool _disposed;

    public SwapchainManager(VulkanContext ctx, uint width, uint height)
    {
        _ctx = ctx;

        if (!ctx.Vk.TryGetDeviceExtension(ctx.Instance, ctx.Device, out KhrSwapchain khrSwapchain))
            throw new InvalidOperationException("Failed to get KHR_swapchain extension.");
        KhrSwapchainApi = khrSwapchain;

        CreateSwapchain(width, height);
        CreateImageViews();
        CreateRenderPass();
        CreateFramebuffers();
    }

    public void Recreate(uint width, uint height)
    {
        _ctx.Vk.DeviceWaitIdle(_ctx.Device);

        CleanupSwapchain();

        CreateSwapchain(width, height);
        CreateImageViews();
        CreateFramebuffers();
    }

    private void CreateSwapchain(uint width, uint height)
    {
        _ctx.KhrSurfaceApi.GetPhysicalDeviceSurfaceCapabilities(
            _ctx.PhysicalDevice, _ctx.Surface, out var capabilities);

        var format = ChooseSurfaceFormat();
        var presentMode = ChoosePresentMode();
        var extent = ChooseExtent(capabilities, width, height);

        var imageCount = capabilities.MinImageCount + 1;
        if (capabilities.MaxImageCount > 0 && imageCount > capabilities.MaxImageCount)
            imageCount = capabilities.MaxImageCount;

        var createInfo = new SwapchainCreateInfoKHR
        {
            SType = StructureType.SwapchainCreateInfoKhr,
            Surface = _ctx.Surface,
            MinImageCount = imageCount,
            ImageFormat = format.Format,
            ImageColorSpace = format.ColorSpace,
            ImageExtent = extent,
            ImageArrayLayers = 1,
            ImageUsage = ImageUsageFlags.ColorAttachmentBit,
            PreTransform = capabilities.CurrentTransform,
            CompositeAlpha = CompositeAlphaFlagsKHR.OpaqueBitKhr,
            PresentMode = presentMode,
            Clipped = true,
            OldSwapchain = default,
        };

        if (_ctx.GraphicsQueueFamily != _ctx.PresentQueueFamily)
        {
            var queueFamilyIndices = stackalloc uint[] { _ctx.GraphicsQueueFamily, _ctx.PresentQueueFamily };
            createInfo.ImageSharingMode = SharingMode.Concurrent;
            createInfo.QueueFamilyIndexCount = 2;
            createInfo.PQueueFamilyIndices = queueFamilyIndices;
        }
        else
        {
            createInfo.ImageSharingMode = SharingMode.Exclusive;
        }

        var result = KhrSwapchainApi.CreateSwapchain(_ctx.Device, in createInfo, null, out var swapchain);
        if (result != Result.Success)
            throw new InvalidOperationException($"Failed to create swapchain: {result}");

        Swapchain = swapchain;
        ImageFormat = format.Format;
        Extent = extent;

        // Get swapchain images
        var swapImageCount = 0u;
        KhrSwapchainApi.GetSwapchainImages(_ctx.Device, Swapchain, &swapImageCount, null);
        _swapchainImages = new Image[swapImageCount];
        fixed (Image* pImages = _swapchainImages)
        {
            KhrSwapchainApi.GetSwapchainImages(_ctx.Device, Swapchain, &swapImageCount, pImages);
        }
    }

    private SurfaceFormatKHR ChooseSurfaceFormat()
    {
        var formatCount = 0u;
        _ctx.KhrSurfaceApi.GetPhysicalDeviceSurfaceFormats(
            _ctx.PhysicalDevice, _ctx.Surface, &formatCount, null);

        var formats = new SurfaceFormatKHR[formatCount];
        fixed (SurfaceFormatKHR* pFormats = formats)
        {
            _ctx.KhrSurfaceApi.GetPhysicalDeviceSurfaceFormats(
                _ctx.PhysicalDevice, _ctx.Surface, &formatCount, pFormats);
        }

        foreach (var format in formats)
        {
            if (format.Format == Format.B8G8R8A8Srgb &&
                format.ColorSpace == ColorSpaceKHR.SpaceSrgbNonlinearKhr)
            {
                return format;
            }
        }

        return formats[0];
    }

    private PresentModeKHR ChoosePresentMode()
    {
        var modeCount = 0u;
        _ctx.KhrSurfaceApi.GetPhysicalDeviceSurfacePresentModes(
            _ctx.PhysicalDevice, _ctx.Surface, &modeCount, null);

        var modes = new PresentModeKHR[modeCount];
        fixed (PresentModeKHR* pModes = modes)
        {
            _ctx.KhrSurfaceApi.GetPhysicalDeviceSurfacePresentModes(
                _ctx.PhysicalDevice, _ctx.Surface, &modeCount, pModes);
        }

        foreach (var mode in modes)
        {
            if (mode == PresentModeKHR.MailboxKhr)
                return mode;
        }

        return PresentModeKHR.FifoKhr;
    }

    private static Extent2D ChooseExtent(SurfaceCapabilitiesKHR capabilities, uint width, uint height)
    {
        if (capabilities.CurrentExtent.Width != uint.MaxValue)
        {
            return capabilities.CurrentExtent;
        }

        return new Extent2D
        {
            Width = Math.Clamp(width, capabilities.MinImageExtent.Width, capabilities.MaxImageExtent.Width),
            Height = Math.Clamp(height, capabilities.MinImageExtent.Height, capabilities.MaxImageExtent.Height),
        };
    }

    private void CreateImageViews()
    {
        ImageViews = new ImageView[_swapchainImages.Length];

        for (var i = 0; i < _swapchainImages.Length; i++)
        {
            var viewInfo = new ImageViewCreateInfo
            {
                SType = StructureType.ImageViewCreateInfo,
                Image = _swapchainImages[i],
                ViewType = ImageViewType.Type2D,
                Format = ImageFormat,
                Components = new ComponentMapping
                {
                    R = ComponentSwizzle.Identity,
                    G = ComponentSwizzle.Identity,
                    B = ComponentSwizzle.Identity,
                    A = ComponentSwizzle.Identity,
                },
                SubresourceRange = new ImageSubresourceRange
                {
                    AspectMask = ImageAspectFlags.ColorBit,
                    BaseMipLevel = 0,
                    LevelCount = 1,
                    BaseArrayLayer = 0,
                    LayerCount = 1,
                },
            };

            var result = _ctx.Vk.CreateImageView(_ctx.Device, in viewInfo, null, out ImageViews[i]);
            if (result != Result.Success)
                throw new InvalidOperationException($"Failed to create image view: {result}");
        }
    }

    private void CreateRenderPass()
    {
        var colorAttachment = new AttachmentDescription
        {
            Format = ImageFormat,
            Samples = SampleCountFlags.Count1Bit,
            LoadOp = AttachmentLoadOp.Clear,
            StoreOp = AttachmentStoreOp.Store,
            StencilLoadOp = AttachmentLoadOp.DontCare,
            StencilStoreOp = AttachmentStoreOp.DontCare,
            InitialLayout = ImageLayout.Undefined,
            FinalLayout = ImageLayout.PresentSrcKhr,
        };

        var colorAttachmentRef = new AttachmentReference
        {
            Attachment = 0,
            Layout = ImageLayout.ColorAttachmentOptimal,
        };

        var subpass = new SubpassDescription
        {
            PipelineBindPoint = PipelineBindPoint.Graphics,
            ColorAttachmentCount = 1,
            PColorAttachments = &colorAttachmentRef,
        };

        var dependency = new SubpassDependency
        {
            SrcSubpass = Vk.SubpassExternal,
            DstSubpass = 0,
            SrcStageMask = PipelineStageFlags.ColorAttachmentOutputBit,
            SrcAccessMask = 0,
            DstStageMask = PipelineStageFlags.ColorAttachmentOutputBit,
            DstAccessMask = AccessFlags.ColorAttachmentWriteBit,
        };

        var renderPassInfo = new RenderPassCreateInfo
        {
            SType = StructureType.RenderPassCreateInfo,
            AttachmentCount = 1,
            PAttachments = &colorAttachment,
            SubpassCount = 1,
            PSubpasses = &subpass,
            DependencyCount = 1,
            PDependencies = &dependency,
        };

        var result = _ctx.Vk.CreateRenderPass(_ctx.Device, in renderPassInfo, null, out var renderPass);
        if (result != Result.Success)
            throw new InvalidOperationException($"Failed to create render pass: {result}");

        RenderPass = renderPass;
    }

    private void CreateFramebuffers()
    {
        Framebuffers = new Framebuffer[ImageViews.Length];

        for (var i = 0; i < ImageViews.Length; i++)
        {
            var attachment = ImageViews[i];
            var framebufferInfo = new FramebufferCreateInfo
            {
                SType = StructureType.FramebufferCreateInfo,
                RenderPass = RenderPass,
                AttachmentCount = 1,
                PAttachments = &attachment,
                Width = Extent.Width,
                Height = Extent.Height,
                Layers = 1,
            };

            var result = _ctx.Vk.CreateFramebuffer(_ctx.Device, in framebufferInfo, null, out Framebuffers[i]);
            if (result != Result.Success)
                throw new InvalidOperationException($"Failed to create framebuffer: {result}");
        }
    }

    private void CleanupSwapchain()
    {
        foreach (var fb in Framebuffers)
            _ctx.Vk.DestroyFramebuffer(_ctx.Device, fb, null);

        foreach (var iv in ImageViews)
            _ctx.Vk.DestroyImageView(_ctx.Device, iv, null);

        KhrSwapchainApi.DestroySwapchain(_ctx.Device, Swapchain, null);

        Framebuffers = [];
        ImageViews = [];
        _swapchainImages = [];
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _ctx.Vk.DeviceWaitIdle(_ctx.Device);

        CleanupSwapchain();
        _ctx.Vk.DestroyRenderPass(_ctx.Device, RenderPass, null);
    }
}
