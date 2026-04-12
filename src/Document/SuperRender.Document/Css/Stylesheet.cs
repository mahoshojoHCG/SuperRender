namespace SuperRender.Document.Css;

public sealed class Stylesheet
{
    public List<CssRule> Rules { get; } = [];
    public List<CssAtRule> AtRules { get; } = [];
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

// --- At-rule model ---

public enum AtRuleType
{
    Import,
    Media,
    Supports,
    Layer,
    FontFace,
    Keyframes,
    Namespace,
    Scope
}

public abstract class CssAtRule
{
    public abstract AtRuleType Type { get; }
}

public sealed class CssImportRule : CssAtRule
{
    public override AtRuleType Type => AtRuleType.Import;
    public required string Url { get; init; }
}

public sealed class CssMediaRule : CssAtRule
{
    public override AtRuleType Type => AtRuleType.Media;
    public required string MediaQuery { get; init; }
    public List<CssRule> Rules { get; init; } = [];
    public List<CssAtRule> NestedAtRules { get; init; } = [];
}

public sealed class CssSupportsRule : CssAtRule
{
    public override AtRuleType Type => AtRuleType.Supports;
    public required string Condition { get; init; }
    public List<CssRule> Rules { get; init; } = [];
    public List<CssAtRule> NestedAtRules { get; init; } = [];
}

public sealed class CssLayerRule : CssAtRule
{
    public override AtRuleType Type => AtRuleType.Layer;
    public string? Name { get; init; }
    public List<CssRule> Rules { get; init; } = [];
}

public sealed class CssFontFaceRule : CssAtRule
{
    public override AtRuleType Type => AtRuleType.FontFace;
    public required List<Declaration> Descriptors { get; init; }
}

public sealed class CssKeyframesRule : CssAtRule
{
    public override AtRuleType Type => AtRuleType.Keyframes;
    public required string Name { get; init; }
    public required List<CssKeyframe> Keyframes { get; init; }
}

public sealed class CssKeyframe
{
    public required string Selector { get; init; }
    public required List<Declaration> Declarations { get; init; }
}

public sealed class CssNamespaceRule : CssAtRule
{
    public override AtRuleType Type => AtRuleType.Namespace;
    public string? Prefix { get; init; }
    public required string Uri { get; init; }
}

public sealed class CssScopeRule : CssAtRule
{
    public override AtRuleType Type => AtRuleType.Scope;
    public string? Scope { get; init; }
    public List<CssRule> Rules { get; init; } = [];
}
