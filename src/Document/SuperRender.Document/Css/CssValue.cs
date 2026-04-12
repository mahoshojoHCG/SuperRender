namespace SuperRender.Document.Css;

public enum CssValueType
{
    Keyword,
    Length,
    Percentage,
    Color,
    StringLiteral,
    Number,
    Calc,
    Angle,
    Time,
    Resolution
}

public sealed class CssValue
{
    public CssValueType Type { get; init; }
    public string Raw { get; init; } = "";
    public double NumericValue { get; init; }
    public string? Unit { get; init; }
    public Document.Color? ColorValue { get; init; }
    public CalcNode? CalcExpr { get; init; }

    // Custom property var() references
    public string? VarName { get; init; }
    public string? VarFallback { get; init; }

    // Gradient value (parsed from linear-gradient, radial-gradient, conic-gradient)
    public CssGradient? Gradient { get; init; }

    // Box-shadow value list (parsed from box-shadow property)
    public IReadOnlyList<BoxShadowDescriptor>? BoxShadows { get; init; }

    public override string ToString() => Raw;
}
