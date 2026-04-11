namespace SuperRender.Document.Css;

public enum CssValueType
{
    Keyword,
    Length,
    Percentage,
    Color,
    StringLiteral,
    Number
}

public sealed class CssValue
{
    public CssValueType Type { get; init; }
    public string Raw { get; init; } = "";
    public double NumericValue { get; init; }
    public string? Unit { get; init; }
    public Document.Color? ColorValue { get; init; }

    public override string ToString() => Raw;
}
