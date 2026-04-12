namespace SuperRender.Document.Css;

/// <summary>
/// A single color stop in a CSS gradient.
/// Position is in the range [0, 1] (0% to 100%). null means auto-distributed.
/// </summary>
public sealed class ColorStop
{
    public Color Color { get; init; }
    /// <summary>Position as a fraction [0,1]. null = auto-distributed by the renderer.</summary>
    public float? Position { get; init; }

    public ColorStop(Color color, float? position = null)
    {
        Color = color;
        Position = position;
    }
}

/// <summary>Base class for CSS gradient values.</summary>
public abstract class CssGradient;

/// <summary>
/// CSS linear-gradient(). Angle is in degrees (0 = to top, 90 = to right, etc.).
/// </summary>
public sealed class LinearGradient : CssGradient
{
    /// <summary>Gradient angle in degrees. 180 = top to bottom (default).</summary>
    public float AngleDeg { get; init; } = 180f;
    public IReadOnlyList<ColorStop> ColorStops { get; init; } = [];
}

/// <summary>Shape type for radial gradients.</summary>
public enum RadialGradientShape { Ellipse, Circle }

/// <summary>Size keyword for radial gradients.</summary>
public enum RadialGradientSize { FarthestCorner, ClosestSide, FarthestSide, ClosestCorner }

/// <summary>
/// CSS radial-gradient().
/// </summary>
public sealed class RadialGradient : CssGradient
{
    public RadialGradientShape Shape { get; init; } = RadialGradientShape.Ellipse;
    public RadialGradientSize Size { get; init; } = RadialGradientSize.FarthestCorner;
    /// <summary>Center X as a fraction [0,1]. 0.5 = center.</summary>
    public float CenterX { get; init; } = 0.5f;
    /// <summary>Center Y as a fraction [0,1]. 0.5 = center.</summary>
    public float CenterY { get; init; } = 0.5f;
    public IReadOnlyList<ColorStop> ColorStops { get; init; } = [];
}

/// <summary>
/// CSS conic-gradient().
/// </summary>
public sealed class ConicGradient : CssGradient
{
    /// <summary>Starting angle in degrees (from the top, clockwise).</summary>
    public float FromAngleDeg { get; init; }
    /// <summary>Center X as a fraction [0,1].</summary>
    public float CenterX { get; init; } = 0.5f;
    /// <summary>Center Y as a fraction [0,1].</summary>
    public float CenterY { get; init; } = 0.5f;
    public IReadOnlyList<ColorStop> ColorStops { get; init; } = [];
}
