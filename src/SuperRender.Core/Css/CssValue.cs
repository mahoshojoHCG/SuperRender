namespace SuperRender.Core.Css;

public enum CssValueType
{
    Keyword,
    Length,
    Percentage,
    Color,
    String,
    Number
}

public sealed class CssValue
{
    public CssValueType Type { get; init; }
    public string Raw { get; init; } = "";
    public double NumericValue { get; init; }
    public string? Unit { get; init; }
    public Core.Color? ColorValue { get; init; }

    public override string ToString() => Raw;
}
