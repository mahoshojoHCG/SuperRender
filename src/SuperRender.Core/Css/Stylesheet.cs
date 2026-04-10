namespace SuperRender.Core.Css;

public sealed class Stylesheet
{
    public List<CssRule> Rules { get; } = [];
}

public sealed class CssRule
{
    public required List<Selector> Selectors { get; init; }
    public required List<Declaration> Declarations { get; init; }
}

public sealed class Declaration
{
    public required string Property { get; init; }
    public required CssValue Value { get; init; }
    public bool Important { get; init; }
}
