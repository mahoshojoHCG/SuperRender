using SuperRender.Document.Dom;
using SuperRender.Document.Css;

namespace SuperRender.Document;

public static class SelectorMatcher
{
    public static bool Matches(Selector selector, Element element)
    {
        if (selector.Components.Count == 0) return false;

        var current = element;
        for (int i = selector.Components.Count - 1; i >= 0; i--)
        {
            var component = selector.Components[i];
            if (current == null) return false;

            if (!MatchesSimple(component.Simple, current))
            {
                if (i < selector.Components.Count - 1 && selector.Components[i + 1].Combinator == Combinator.Descendant)
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
                    Combinator.Descendant => FindAncestorMatch(current, selector.Components[i - 1].Simple),
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

    public static bool MatchesSimple(SimpleSelector selector, Element element)
    {
        if (selector.TagName != null && !selector.TagName.Equals(element.TagName, StringComparison.OrdinalIgnoreCase))
            return false;

        if (selector.Id != null && !selector.Id.Equals(element.Id, StringComparison.OrdinalIgnoreCase))
            return false;

        foreach (var cls in selector.Classes)
        {
            if (!element.ClassList.Any(c => c.Equals(cls, StringComparison.OrdinalIgnoreCase)))
                return false;
        }

        return true;
    }

    private static Element? FindAncestorMatch(Element element, SimpleSelector selector)
    {
        var current = element.Parent as Element;
        while (current != null)
        {
            if (MatchesSimple(selector, current))
                return current;
            current = current.Parent as Element;
        }
        return null;
    }
}
