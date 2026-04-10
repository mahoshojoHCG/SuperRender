using Silk.NET.Core.Native;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;
using Silk.NET.Windowing;

namespace SuperRender.Gpu;

public sealed unsafe class VulkanContext : IDisposable
{
    public Vk Vk { get; }
    public Instance Instance { get; }
    public PhysicalDevice PhysicalDevice { get; }
    public Device Device { get; }
    public Queue GraphicsQueue { get; }
    public Queue PresentQueue { get; }
    public uint GraphicsQueueFamily { get; }
    public uint PresentQueueFamily { get; }
    public KhrSurface KhrSurfaceApi { get; }
    public SurfaceKHR Surface { get; }

    private bool _disposed;

    public VulkanContext(IWindow window)
    {
        Vk = Vk.GetApi();

        // --- Create Instance ---
        Instance = CreateInstance(window);

        // --- Get KhrSurface extension ---
        if (!Vk.TryGetInstanceExtension(Instance, out KhrSurface khrSurface))
            throw new InvalidOperationException("Failed to get KHR_surface extension.");
        KhrSurfaceApi = khrSurface;

        // --- Create Surface ---
        Surface = CreateSurface(window);

        // --- Pick Physical Device ---
        PhysicalDevice = PickPhysicalDevice();

        // --- Find Queue Families ---
        (GraphicsQueueFamily, PresentQueueFamily) = FindQueueFamilies(PhysicalDevice);

        // --- Create Logical Device ---
        Device = CreateLogicalDevice();

        // --- Get Queues ---
        Vk.GetDeviceQueue(Device, GraphicsQueueFamily, 0, out var graphicsQueue);
        GraphicsQueue = graphicsQueue;

        Vk.GetDeviceQueue(Device, PresentQueueFamily, 0, out var presentQueue);
        PresentQueue = presentQueue;
    }

    private Instance CreateInstance(IWindow window)
    {
        var appInfo = new ApplicationInfo
        {
            SType = StructureType.ApplicationInfo,
            ApiVersion = Vk.Version12,
        };

        // Discover available instance extensions
        var availableExtensions = GetAvailableInstanceExtensions();

        // Gather required surface extensions from the window
        var vkSurface = window.VkSurface!;
        var requiredExtensions = vkSurface.GetRequiredExtensions(out var requiredExtCount);

        // Build extension list: required + portability enumeration (if available)
        var extList = new List<nint>();
        for (var i = 0u; i < requiredExtCount; i++)
            extList.Add((nint)requiredExtensions[i]);

        bool hasPortabilityEnum = availableExtensions.Contains("VK_KHR_portability_enumeration");
        nint portabilityEnumStr = 0;
        if (hasPortabilityEnum)
        {
            portabilityEnumStr = SilkMarshal.StringToPtr("VK_KHR_portability_enumeration");
            extList.Add(portabilityEnumStr);
        }

        var extensions = (byte**)SilkMarshal.Allocate(extList.Count * sizeof(byte*));
        for (int i = 0; i < extList.Count; i++)
            extensions[i] = (byte*)extList[i];

        var createInfo = new InstanceCreateInfo
        {
            SType = StructureType.InstanceCreateInfo,
            PApplicationInfo = &appInfo,
            EnabledExtensionCount = (uint)extList.Count,
            PpEnabledExtensionNames = extensions,
            Flags = hasPortabilityEnum ? InstanceCreateFlags.EnumeratePortabilityBitKhr : 0,
        };

        var result = Vk.CreateInstance(in createInfo, null, out var instance);
        if (result != Result.Success)
            throw new InvalidOperationException($"Failed to create Vulkan instance: {result}");

        if (portabilityEnumStr != 0) SilkMarshal.Free(portabilityEnumStr);
        SilkMarshal.Free((nint)extensions);

        return instance;
    }

    private HashSet<string> GetAvailableInstanceExtensions()
    {
        var count = 0u;
        Vk.EnumerateInstanceExtensionProperties((byte*)null, &count, null);

        var props = new ExtensionProperties[count];
        fixed (ExtensionProperties* pProps = props)
            Vk.EnumerateInstanceExtensionProperties((byte*)null, &count, pProps);

        var set = new HashSet<string>();
        foreach (var p in props)
        {
            var name = SilkMarshal.PtrToString((nint)p.ExtensionName);
            if (name != null) set.Add(name);
        }
        return set;
    }

    private SurfaceKHR CreateSurface(IWindow window)
    {
        var vkSurface = window.VkSurface!;
        var handle = vkSurface.Create<AllocationCallbacks>(Instance.ToHandle(), null);
        return handle.ToSurface();
    }

    private PhysicalDevice PickPhysicalDevice()
    {
        var deviceCount = 0u;
        Vk.EnumeratePhysicalDevices(Instance, &deviceCount, null);

        if (deviceCount == 0)
            throw new InvalidOperationException("No Vulkan physical devices found.");

        var devices = new PhysicalDevice[deviceCount];
        fixed (PhysicalDevice* pDevices = devices)
        {
            Vk.EnumeratePhysicalDevices(Instance, &deviceCount, pDevices);
        }

        // Prefer discrete GPU
        PhysicalDevice chosen = devices[0];
        foreach (var device in devices)
        {
            Vk.GetPhysicalDeviceProperties(device, out var props);
            if (props.DeviceType == PhysicalDeviceType.DiscreteGpu)
            {
                chosen = device;
                break;
            }
        }

        return chosen;
    }

    private (uint graphics, uint present) FindQueueFamilies(PhysicalDevice physicalDevice)
    {
        var queueFamilyCount = 0u;
        Vk.GetPhysicalDeviceQueueFamilyProperties(physicalDevice, &queueFamilyCount, null);

        var queueFamilies = new QueueFamilyProperties[queueFamilyCount];
        fixed (QueueFamilyProperties* pFamilies = queueFamilies)
        {
            Vk.GetPhysicalDeviceQueueFamilyProperties(physicalDevice, &queueFamilyCount, pFamilies);
        }

        uint? graphicsFamily = null;
        uint? presentFamily = null;

        for (var i = 0u; i < queueFamilyCount; i++)
        {
            if (queueFamilies[i].QueueFlags.HasFlag(QueueFlags.GraphicsBit))
            {
                graphicsFamily = i;
            }

            KhrSurfaceApi.GetPhysicalDeviceSurfaceSupport(physicalDevice, i, Surface, out var presentSupport);
            if (presentSupport)
            {
                presentFamily = i;
            }

            if (graphicsFamily.HasValue && presentFamily.HasValue)
                break;
        }

        if (!graphicsFamily.HasValue || !presentFamily.HasValue)
            throw new InvalidOperationException("Could not find suitable queue families.");

        return (graphicsFamily.Value, presentFamily.Value);
    }

    private Device CreateLogicalDevice()
    {
        var uniqueFamilies = GraphicsQueueFamily == PresentQueueFamily
            ? new[] { GraphicsQueueFamily }
            : new[] { GraphicsQueueFamily, PresentQueueFamily };

        var queueCreateInfos = new DeviceQueueCreateInfo[uniqueFamilies.Length];
        var priority = 1.0f;

        for (var i = 0; i < uniqueFamilies.Length; i++)
        {
            queueCreateInfos[i] = new DeviceQueueCreateInfo
            {
                SType = StructureType.DeviceQueueCreateInfo,
                QueueFamilyIndex = uniqueFamilies[i],
                QueueCount = 1,
                PQueuePriorities = &priority,
            };
        }

        // Build device extensions: swapchain + portability subset if available
        var deviceExtensions = new List<string> { "VK_KHR_swapchain" };

        if (IsDeviceExtensionAvailable("VK_KHR_portability_subset"))
        {
            deviceExtensions.Add("VK_KHR_portability_subset");
        }

        var extensionPtrs = new nint[deviceExtensions.Count];
        for (var i = 0; i < deviceExtensions.Count; i++)
        {
            extensionPtrs[i] = SilkMarshal.StringToPtr(deviceExtensions[i]);
        }

        var features = new PhysicalDeviceFeatures();

        fixed (DeviceQueueCreateInfo* pQueueCreateInfos = queueCreateInfos)
        fixed (nint* pExtPtrs = extensionPtrs)
        {
            var createInfo = new DeviceCreateInfo
            {
                SType = StructureType.DeviceCreateInfo,
                QueueCreateInfoCount = (uint)queueCreateInfos.Length,
                PQueueCreateInfos = pQueueCreateInfos,
                EnabledExtensionCount = (uint)deviceExtensions.Count,
                PpEnabledExtensionNames = (byte**)pExtPtrs,
                PEnabledFeatures = &features,
            };

            var result = Vk.CreateDevice(PhysicalDevice, in createInfo, null, out var device);
            if (result != Result.Success)
                throw new InvalidOperationException($"Failed to create logical device: {result}");

            // Free extension string allocations
            foreach (var ptr in extensionPtrs)
            {
                SilkMarshal.Free(ptr);
            }

            return device;
        }
    }

    private bool IsDeviceExtensionAvailable(string extensionName)
    {
        var extCount = 0u;
        Vk.EnumerateDeviceExtensionProperties(PhysicalDevice, (byte*)null, &extCount, null);

        if (extCount == 0) return false;

        var extensions = new ExtensionProperties[extCount];
        fixed (ExtensionProperties* pExtensions = extensions)
        {
            Vk.EnumerateDeviceExtensionProperties(PhysicalDevice, (byte*)null, &extCount, pExtensions);
        }

        foreach (var ext in extensions)
        {
            var name = SilkMarshal.PtrToString((nint)ext.ExtensionName);
            if (name == extensionName)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Make MoltenVK discoverable by both GLFW and the Vulkan loader.
    /// Must be called before any Silk.NET Vulkan/Window usage.
    /// </summary>
    public static void EnsureMoltenVK()
    {
        var appDir = AppContext.BaseDirectory;

        // Find the NuGet-provided native directory
        string? nativeDir = null;
        foreach (var candidate in new[] { "osx", "osx-arm64", "osx-x64" })
        {
            var dir = Path.Combine(appDir, "runtimes", candidate, "native");
            if (File.Exists(Path.Combine(dir, "libMoltenVK.dylib")))
            {
                nativeDir = dir;
                break;
            }
        }

        if (nativeDir == null)
        {
            if (!File.Exists(Path.Combine(appDir, "libMoltenVK.dylib")))
                Console.WriteLine("Warning: libMoltenVK.dylib not found. Vulkan may not work on macOS.");
            return;
        }

        CreateSymlinkIfMissing(
            Path.Combine(appDir, "libMoltenVK.dylib"),
            Path.Combine(nativeDir, "libMoltenVK.dylib"));

        CreateSymlinkIfMissing(
            Path.Combine(appDir, "libvulkan.1.dylib"),
            Path.Combine(nativeDir, "libMoltenVK.dylib"));

        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("VK_DRIVER_FILES"))
            && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("VK_ICD_FILENAMES")))
        {
            var icdSrc = Path.Combine(nativeDir, "MoltenVK_icd.json");
            if (File.Exists(icdSrc))
            {
                var icdDst = Path.Combine(appDir, "MoltenVK_icd.json");
                if (!File.Exists(icdDst))
                    File.Copy(icdSrc, icdDst);
                Environment.SetEnvironmentVariable("VK_DRIVER_FILES", icdDst);
            }
        }

        Console.WriteLine($"MoltenVK activated from {nativeDir}");
    }

    private static void CreateSymlinkIfMissing(string link, string target)
    {
        if (File.Exists(link)) return;
        try
        {
            File.CreateSymbolicLink(link, target);
        }
        catch
        {
            // Symlink may fail (permissions); fall back to copy
            try { File.Copy(target, link); } catch { /* best effort */ }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        Vk.DestroyDevice(Device, null);
        KhrSurfaceApi.DestroySurface(Instance, Surface, null);
        Vk.DestroyInstance(Instance, null);
        Vk.Dispose();
    }
}
