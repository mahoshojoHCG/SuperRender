using SuperRender.Document;
using SuperRender.Renderer.Rendering.Layout;
using SuperRender.Renderer.Rendering.Style;

namespace SuperRender.Renderer.Rendering.Painting;

public abstract class PaintCommand;

public sealed class FillRectCommand : PaintCommand
{
    public required RectF Rect { get; init; }
    public required Color Color { get; init; }
    public float RadiusTL { get; init; }
    public float RadiusTR { get; init; }
    public float RadiusBR { get; init; }
    public float RadiusBL { get; init; }
}

public sealed class StrokeRectCommand : PaintCommand
{
    public required RectF Rect { get; init; }
    public required Color Color { get; init; }
    public float LineWidth { get; init; } = 1f;
    public float RadiusTL { get; init; }
    public float RadiusTR { get; init; }
    public float RadiusBR { get; init; }
    public float RadiusBL { get; init; }
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

public sealed class PushTransformCommand : PaintCommand
{
    /// <summary>4x4 column-major transform matrix.</summary>
    public required float[] Matrix4x4 { get; init; }
}

public sealed class PopTransformCommand : PaintCommand;

public sealed class PushFilterCommand : PaintCommand
{
    public required List<Style.FilterFunction> Filters { get; init; }
}

public sealed class PopFilterCommand : PaintCommand;

public sealed class DrawImageCommand : PaintCommand
{
    /// <summary>URL or key identifying the image in the image cache.</summary>
    public required string ImageUrl { get; init; }
    /// <summary>Destination rectangle in logical (CSS) pixels.</summary>
    public required RectF Rect { get; init; }
    /// <summary>Opacity (0-1) applied to the image.</summary>
    public float Opacity { get; init; } = 1f;
}

/// <summary>
/// Draws a linear gradient fill within a rectangle.
/// The gradient shader interpolates between color stops along the angle direction.
/// </summary>
public sealed class DrawLinearGradientCommand : PaintCommand
{
    public required RectF Rect { get; init; }
    /// <summary>Gradient angle in degrees (0 = to top, 90 = to right, 180 = to bottom).</summary>
    public float AngleDeg { get; init; }
    public required IReadOnlyList<GradientColorStop> ColorStops { get; init; }
    public float RadiusTL { get; init; }
    public float RadiusTR { get; init; }
    public float RadiusBR { get; init; }
    public float RadiusBL { get; init; }
}

/// <summary>
/// Draws a radial gradient fill within a rectangle.
/// </summary>
public sealed class DrawRadialGradientCommand : PaintCommand
{
    public required RectF Rect { get; init; }
    /// <summary>Center X relative to rect, as fraction [0,1].</summary>
    public float CenterX { get; init; } = 0.5f;
    /// <summary>Center Y relative to rect, as fraction [0,1].</summary>
    public float CenterY { get; init; } = 0.5f;
    /// <summary>Radius X as fraction of rect width.</summary>
    public float RadiusX { get; init; } = 0.5f;
    /// <summary>Radius Y as fraction of rect height.</summary>
    public float RadiusY { get; init; } = 0.5f;
    public required IReadOnlyList<GradientColorStop> ColorStops { get; init; }
}

/// <summary>
/// Draws a box shadow (outer or inset).
/// Uses SDF-based Gaussian blur approximation in the GPU shader.
/// </summary>
public sealed class DrawBoxShadowCommand : PaintCommand
{
    /// <summary>The element's border rect (shadow is drawn relative to this).</summary>
    public required RectF Rect { get; init; }
    public float OffsetX { get; init; }
    public float OffsetY { get; init; }
    public float BlurRadius { get; init; }
    public float SpreadRadius { get; init; }
    public required Color Color { get; init; }
    public bool Inset { get; init; }
    public float RadiusTL { get; init; }
    public float RadiusTR { get; init; }
    public float RadiusBR { get; init; }
    public float RadiusBL { get; init; }
}

/// <summary>
/// Draws an outline around an element (painted after content, outside border).
/// </summary>
public sealed class DrawOutlineCommand : PaintCommand
{
    public required RectF Rect { get; init; }
    public float Width { get; init; }
    public required Color Color { get; init; }
    public string Style { get; init; } = "solid";
    public float Offset { get; init; }
}

/// <summary>Flat color stop for GPU gradient rendering.</summary>
public sealed class GradientColorStop
{
    public required Color Color { get; init; }
    /// <summary>Position along the gradient line [0, 1].</summary>
    public required float Position { get; init; }
}

public sealed class PaintList
{
    public List<PaintCommand> Commands { get; } = [];

    public void Add(PaintCommand command) => Commands.Add(command);
}
