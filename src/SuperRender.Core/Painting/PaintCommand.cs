using SuperRender.Core.Layout;

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
}

public sealed class PaintList
{
    public List<PaintCommand> Commands { get; } = [];

    public void Add(PaintCommand command) => Commands.Add(command);
}
