namespace SuperRender.Renderer.Rendering.Style;

/// <summary>
/// Base class for CSS filter functions.
/// </summary>
public abstract class FilterFunction
{
    public abstract string Name { get; }
}

public sealed class BlurFilter : FilterFunction
{
    public override string Name => "blur";
    public float Radius { get; }
    public BlurFilter(float radius) => Radius = radius;
}

public sealed class BrightnessFilter : FilterFunction
{
    public override string Name => "brightness";
    public float Amount { get; }
    public BrightnessFilter(float amount) => Amount = amount;
}

public sealed class ContrastFilter : FilterFunction
{
    public override string Name => "contrast";
    public float Amount { get; }
    public ContrastFilter(float amount) => Amount = amount;
}

public sealed class DropShadowFilter : FilterFunction
{
    public override string Name => "drop-shadow";
    public float OffsetX { get; }
    public float OffsetY { get; }
    public float BlurRadius { get; }
    public Document.Color Color { get; }
    public DropShadowFilter(float offsetX, float offsetY, float blurRadius, Document.Color color)
    { OffsetX = offsetX; OffsetY = offsetY; BlurRadius = blurRadius; Color = color; }
}

public sealed class GrayscaleFilter : FilterFunction
{
    public override string Name => "grayscale";
    public float Amount { get; }
    public GrayscaleFilter(float amount) => Amount = amount;
}

public sealed class HueRotateFilter : FilterFunction
{
    public override string Name => "hue-rotate";
    public float Angle { get; }
    public HueRotateFilter(float radians) => Angle = radians;
}

public sealed class InvertFilter : FilterFunction
{
    public override string Name => "invert";
    public float Amount { get; }
    public InvertFilter(float amount) => Amount = amount;
}

public sealed class OpacityFilter : FilterFunction
{
    public override string Name => "opacity";
    public float Amount { get; }
    public OpacityFilter(float amount) => Amount = amount;
}

public sealed class SaturateFilter : FilterFunction
{
    public override string Name => "saturate";
    public float Amount { get; }
    public SaturateFilter(float amount) => Amount = amount;
}

public sealed class SepiaFilter : FilterFunction
{
    public override string Name => "sepia";
    public float Amount { get; }
    public SepiaFilter(float amount) => Amount = amount;
}
