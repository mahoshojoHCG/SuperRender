using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.Windowing;
using SuperRender.Core;
using SuperRender.Core.Dom;
using SuperRender.Core.Painting;

namespace SuperRender.Demo;

public static class Program
{
    private static IWindow _window = null!;
    private static RenderPipeline _renderPipeline = null!;
    private static VulkanRenderer _renderer = null!;
    private static PaintList? _lastPaintList;

    public static void Main(string[] args)
    {
        // Must run before Window.Create — Silk.NET's Vulkan window
        // needs the loader to find MoltenVK during surface creation.
        if (OperatingSystem.IsMacOS())
            VulkanContext.EnsureMoltenVK();

        var opts = WindowOptions.DefaultVulkan with
        {
            Size = new Vector2D<int>(1024, 768),
            Title = "SuperRenderer - HTML+CSS Rendering Engine",
            IsEventDriven = false,
        };

        _window = Window.Create(opts);
        _window.Load += OnLoad;
        _window.Render += OnRender;
        _window.FramebufferResize += OnResize;
        _window.Closing += OnClosing;
        _window.Run();
        _window.Dispose();
    }

    private static void OnLoad()
    {
        _renderer = new VulkanRenderer(_window);

        var measurer = new BitmapFontTextMeasurer(_renderer.FontAtlasData);
        _renderPipeline = new RenderPipeline(measurer);

        var doc = _renderPipeline.LoadHtml(SampleHtml);

        // Demonstrate DOM mutation API
        var body = doc!.Body;
        if (body != null)
        {
            var newDiv = doc.CreateElement("div");
            newDiv.SetAttribute("class", "dynamic");
            var text = doc.CreateTextNode("This element was dynamically added via the DOM Mutation API!");
            newDiv.AppendChild(text);
            body.AppendChild(newDiv);
        }

        // Input handling
        var input = _window.CreateInput();
        foreach (var kb in input.Keyboards)
            kb.KeyDown += OnKeyDown;

        Console.WriteLine("SuperRenderer Demo started. Press Escape to exit.");
    }

    private static void OnRender(double deltaTime)
    {
        var size = _window.FramebufferSize;
        if (size.X == 0 || size.Y == 0) return;

        var paintList = _renderPipeline.RenderIfDirty(size.X, size.Y);
        if (paintList != null)
            _lastPaintList = paintList;

        _lastPaintList ??= _renderPipeline.Render(size.X, size.Y);

        _renderer.RenderFrame(_lastPaintList);
    }

    private static void OnResize(Vector2D<int> size)
    {
        _renderer.OnResize(size.X, size.Y);
        if (_renderPipeline.Document != null)
            _renderPipeline.Document.NeedsLayout = true;
    }

    private static void OnKeyDown(IKeyboard kb, Key key, int scancode)
    {
        if (key == Key.Escape) _window.Close();
    }

    private static void OnClosing()
    {
        _renderer.Dispose();
    }

    private const string SampleHtml = """
        <!DOCTYPE html>
        <html>
        <head>
            <style>
                body {
                    background-color: #ffffff;
                    color: #333333;
                    font-size: 16px;
                    margin: 20px;
                    padding: 0;
                }
                h1 {
                    color: #1a5276;
                    font-size: 32px;
                    margin-bottom: 16px;
                }
                .container {
                    width: 600px;
                    margin-top: 0;
                    margin-bottom: 0;
                    padding: 20px;
                    background-color: #f4f6f7;
                    border-width: 1px;
                    border-color: #d5d8dc;
                    border-style: solid;
                }
                p {
                    margin-bottom: 12px;
                    line-height: 1.5;
                }
                .highlight {
                    background-color: #f9e79f;
                    padding: 4px;
                }
                a { color: #2e86c1; }
                ul {
                    margin-left: 20px;
                    margin-bottom: 12px;
                }
                li { margin-bottom: 4px; }
                .dynamic {
                    margin-top: 16px;
                    padding: 10px;
                    background-color: #d5f5e3;
                    color: #1e8449;
                }
            </style>
        </head>
        <body>
            <h1>SuperRenderer</h1>
            <div class="container">
                <p>This is a simple HTML page rendered by a custom engine built with C# and Vulkan.</p>
                <p class="highlight">CSS styling is working!</p>
                <ul>
                    <li>HTML parsing</li>
                    <li>CSS cascade and specificity</li>
                    <li>Box model layout</li>
                    <li>Vulkan rendering</li>
                </ul>
                <p>Visit <a href="#">the project</a> for more.</p>
            </div>
        </body>
        </html>
        """;
}
