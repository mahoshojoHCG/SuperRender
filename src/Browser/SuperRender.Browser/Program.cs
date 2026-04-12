using Microsoft.Extensions.Logging;
using Silk.NET.Maths;
using Silk.NET.Windowing;
using SuperRender.Renderer.Gpu;

namespace SuperRender.Browser;

public static class Program
{
    public static void Main(string[] args)
    {
        if (OperatingSystem.IsMacOS())
            VulkanContext.EnsureMoltenVK();

        using var loggerFactory = LoggerFactory.Create(builder =>
            builder.AddConsole().SetMinimumLevel(LogLevel.Information));

        var opts = WindowOptions.DefaultVulkan with
        {
            Size = new Vector2D<int>(1280, 900),
            Title = "SuperRenderer Browser",
            IsEventDriven = false,
        };

        var window = Window.Create(opts);
        var browser = new BrowserWindow(window, loggerFactory);

        window.Load += browser.OnLoad;
        window.Render += browser.OnRender;
        window.FramebufferResize += browser.OnResize;
        window.Closing += browser.OnClosing;
        window.Run();
        window.Dispose();
    }
}
