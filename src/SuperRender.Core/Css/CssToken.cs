namespace SuperRender.Core.Css;

public enum CssTokenType
{
    Ident,
    Hash,
    Dot,
    Colon,
    Semicolon,
    LeftBrace,
    RightBrace,
    Comma,
    Whitespace,
    Number,
    Dimension,
    Percentage,
    StringLiteral,
    Function,
    LeftParen,
    RightParen,
    Delim,
    EndOfFile
}

public sealed class CssToken
{
    public CssTokenType Type { get; init; }
    public string Value { get; init; } = "";
    public double NumericValue { get; init; }
    public string? Unit { get; init; }

    public override string ToString() => Type switch
    {
        CssTokenType.Dimension => $"{NumericValue}{Unit}",
        CssTokenType.Percentage => $"{NumericValue}%",
        CssTokenType.Number => NumericValue.ToString(System.Globalization.CultureInfo.InvariantCulture),
        _ => Value
    };
}
