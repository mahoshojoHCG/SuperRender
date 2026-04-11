using SuperRender.Document.Style;

namespace SuperRender.Document.Css;

public sealed class Selector
{
    public required List<SelectorComponent> Components { get; init; }
    public PseudoElementType? PseudoElement { get; init; }

    public Specificity GetSpecificity()
    {
        int ids = 0, classes = 0, elements = 0;
        foreach (var component in Components)
        {
            var s = component.Simple;
            if (s.Id != null) ids++;
            classes += s.Classes.Count;
            classes += s.Attributes.Count;
            if (s.TagName != null) elements++;

            foreach (var pc in s.PseudoClasses)
            {
                switch (pc.Type)
                {
                    case PseudoClassType.Not:
                    case PseudoClassType.Is:
                        // :not() and :is() take specificity of their most specific argument
                        if (pc.SelectorArgument != null)
                        {
                            int maxIds = 0, maxClasses = 0, maxElements = 0;
                            foreach (var arg in pc.SelectorArgument)
                            {
                                var spec = arg.GetSpecificity();
                                if (spec.CompareTo(new Specificity { Ids = maxIds, Classes = maxClasses, Elements = maxElements }) > 0)
                                {
                                    maxIds = spec.Ids;
                                    maxClasses = spec.Classes;
                                    maxElements = spec.Elements;
                                }
                            }
                            ids += maxIds;
                            classes += maxClasses;
                            elements += maxElements;
                        }
                        break;
                    case PseudoClassType.Where:
                        // :where() contributes zero specificity
                        break;
                    default:
                        // Other pseudo-classes count as one class selector
                        classes++;
                        break;
                }
            }
        }

        if (PseudoElement != null)
            elements++;

        return new Specificity { Ids = ids, Classes = classes, Elements = elements };
    }
}

public sealed class SelectorComponent
{
    public required SimpleSelector Simple { get; init; }
    public Combinator Combinator { get; init; }
}

public sealed class SimpleSelector
{
    public string? TagName { get; init; }
    public string? Id { get; init; }
    public List<string> Classes { get; init; } = [];
    public List<AttributeSelector> Attributes { get; init; } = [];
    public List<PseudoClass> PseudoClasses { get; init; } = [];
}

public enum Combinator
{
    None,
    Descendant,
    Child,
    AdjacentSibling,
    GeneralSibling
}

// --- Attribute Selectors ---

public sealed class AttributeSelector
{
    public required string Name { get; init; }
    public AttributeOp Op { get; init; }
    public string? Value { get; init; }
}

public enum AttributeOp
{
    Exists,        // [attr]
    Equals,        // [attr="val"]
    StartsWith,    // [attr^="val"]
    EndsWith,      // [attr$="val"]
    Contains,      // [attr*="val"]
    DashMatch,     // [attr|="val"]
    WordMatch      // [attr~="val"]
}

// --- Pseudo-classes ---

public sealed class PseudoClass
{
    public PseudoClassType Type { get; init; }
    public string? Argument { get; init; }
    public List<Selector>? SelectorArgument { get; init; }
}

public enum PseudoClassType
{
    FirstChild,
    LastChild,
    NthChild,
    NthLastChild,
    OnlyChild,
    FirstOfType,
    LastOfType,
    OnlyOfType,
    Root,
    Empty,
    Link,
    Visited,
    Hover,
    Focus,
    Active,
    Not,
    Is,
    Where,
}

// --- Pseudo-elements ---

public enum PseudoElementType
{
    Before,
    After,
}
