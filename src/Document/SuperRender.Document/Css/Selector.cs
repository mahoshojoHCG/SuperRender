using SuperRender.Document.Style;

namespace SuperRender.Document.Css;

public sealed class Selector
{
    public required List<SelectorComponent> Components { get; init; }

    public Specificity GetSpecificity()
    {
        int ids = 0, classes = 0, elements = 0;
        foreach (var component in Components)
        {
            var s = component.Simple;
            if (s.Id != null) ids++;
            classes += s.Classes.Count;
            if (s.TagName != null) elements++;
        }
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
}

public enum Combinator
{
    None,
    Descendant,
    Child
}
