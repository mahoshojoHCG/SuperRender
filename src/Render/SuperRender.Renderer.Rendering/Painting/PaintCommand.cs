using SuperRender.Document;
using SuperRender.Renderer.Rendering.Layout;
using SuperRender.Renderer.Rendering.Style;

namespace SuperRender.Renderer.Rendering.Painting;

public abstract class PaintCommand;

public sealed class FillRectCommand : PaintCommand
{
    public required RectF Rect { get; init; }
    public required Color Color { get; init; }
}

public sealed class StrokeRectCommand : PaintCommand
{
    public required RectF Rect { get; init; }
    public required Color Color { get; init; }
    public float LineWidth { get; init; } = 1f;
}

public sealed class DrawTextCommand : PaintCommand
{
    public required string Text { get; init; }
    public required float X { get; init; }
    public required float Y { get; init; }
    public required float FontSize { get; init; }
    public required Color Color { get; init; }
    public int FontWeight { get; init; } = 400;
    public FontStyleType FontStyle { get; init; } = FontStyleType.Normal;
    public string FontFamily { get; init; } = "";
    public IReadOnlyList<string> FontFamilies { get; init; } = [];
    public float LetterSpacing { get; init; }
    public float WordSpacing { get; init; }
}

public sealed class PushClipCommand : PaintCommand
{
    public required RectF Rect { get; init; }
}

public sealed class PopClipCommand : PaintCommand;

public sealed class DrawImageCommand : PaintCommand
{
    /// <summary>URL or key identifying the image in the image cache.</summary>
    public required string ImageUrl { get; init; }
    /// <summary>Destination rectangle in logical (CSS) pixels.</summary>
    public required RectF Rect { get; init; }
    /// <summary>Opacity (0-1) applied to the image.</summary>
    public float Opacity { get; init; } = 1f;
}

public sealed class PaintList
{
    public List<PaintCommand> Commands { get; } = [];

    public void Add(PaintCommand command) => Commands.Add(command);
}
