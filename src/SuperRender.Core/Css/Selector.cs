using SuperRender.Core.Dom;

namespace SuperRender.Core.Css;

public sealed class Selector
{
    public required List<SelectorComponent> Components { get; init; }

    public bool Matches(Element element)
    {
        if (Components.Count == 0) return false;

        var current = element;
        for (int i = Components.Count - 1; i >= 0; i--)
        {
            var component = Components[i];
            if (current == null) return false;

            if (!component.Simple.Matches(current))
            {
                if (i < Components.Count - 1 && Components[i + 1].Combinator == Combinator.Descendant)
                {
                    current = current.Parent as Element;
                    i++;
                    if (current == null) return false;
                    continue;
                }
                return false;
            }

            if (i > 0)
            {
                var combinator = component.Combinator;
                current = combinator switch
                {
                    Combinator.Descendant => FindAncestorMatch(current, Components[i - 1].Simple),
                    Combinator.Child => current.Parent as Element,
                    _ => current.Parent as Element
                };

                if (combinator == Combinator.Descendant && current != null)
                {
                    i--;
                    continue;
                }
            }
        }

        return true;
    }

    private static Element? FindAncestorMatch(Element element, SimpleSelector selector)
    {
        var current = element.Parent as Element;
        while (current != null)
        {
            if (selector.Matches(current))
                return current;
            current = current.Parent as Element;
        }
        return null;
    }

    public Style.Specificity GetSpecificity()
    {
        int ids = 0, classes = 0, elements = 0;
        foreach (var component in Components)
        {
            var s = component.Simple;
            if (s.Id != null) ids++;
            classes += s.Classes.Count;
            if (s.TagName != null) elements++;
        }
        return new Style.Specificity { Ids = ids, Classes = classes, Elements = elements };
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

    public bool Matches(Element element)
    {
        if (TagName != null && !TagName.Equals(element.TagName, StringComparison.OrdinalIgnoreCase))
            return false;

        if (Id != null && !Id.Equals(element.Id, StringComparison.OrdinalIgnoreCase))
            return false;

        foreach (var cls in Classes)
        {
            if (!element.ClassList.Any(c => c.Equals(cls, StringComparison.OrdinalIgnoreCase)))
                return false;
        }

        return true;
    }
}

public enum Combinator
{
    None,
    Descendant,
    Child
}
