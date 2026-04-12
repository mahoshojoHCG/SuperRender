namespace SuperRender.Document.Css;

/// <summary>
/// Describes a single CSS box-shadow value.
/// Multiple box-shadows are stored as a list.
/// </summary>
public sealed class BoxShadowDescriptor
{
    /// <summary>Horizontal offset in pixels.</summary>
    public float OffsetX { get; init; }
    /// <summary>Vertical offset in pixels.</summary>
    public float OffsetY { get; init; }
    /// <summary>Blur radius in pixels (>= 0).</summary>
    public float BlurRadius { get; init; }
    /// <summary>Spread radius in pixels (can be negative).</summary>
    public float SpreadRadius { get; init; }
    /// <summary>Shadow color.</summary>
    public Color Color { get; init; } = new(0, 0, 0, 1);
    /// <summary>True for inset shadows.</summary>
    public bool Inset { get; init; }
}
