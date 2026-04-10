using SuperRender.Core.Layout;
using SuperRender.Core.Style;

namespace SuperRender.Core.Painting;

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
    public float FontWeight { get; init; } = 400f;
    public FontStyleType FontStyle { get; init; } = FontStyleType.Normal;
    public TextDecorationLine TextDecoration { get; init; } = TextDecorationLine.None;
}

public sealed class ClipRectCommand : PaintCommand
{
    public required RectF Rect { get; init; }
}

public sealed class RestoreClipCommand : PaintCommand;

public sealed class PaintList
{
    public List<PaintCommand> Commands { get; } = [];

    public void Add(PaintCommand command) => Commands.Add(command);
}
