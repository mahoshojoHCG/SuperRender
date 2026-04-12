using SuperRender.Document.Dom;
using SuperRender.Document.Css;

namespace SuperRender.Document;

public static class SelectorMatcher
{
    public static bool Matches(Selector selector, Element element)
    {
        if (selector.Components.Count == 0) return false;

        // Right-to-left matching
        return MatchFromRight(selector, selector.Components.Count - 1, element);
    }

    private static bool MatchFromRight(Selector selector, int componentIndex, Element? element)
    {
        if (element == null) return false;
        if (componentIndex < 0) return true; // All components matched

        var component = selector.Components[componentIndex];

        if (!MatchesSimple(component.Simple, element))
            return false;

        if (componentIndex == 0)
            return true; // Leftmost component matched, done

        // Navigate to the next element based on the combinator on the LEFT component
        var combinator = selector.Components[componentIndex - 1].Combinator;

        switch (combinator)
        {
            case Combinator.Descendant:
            {
                // Any ancestor must match the next component
                var ancestor = element.Parent as Element;
                while (ancestor != null)
                {
                    if (MatchFromRight(selector, componentIndex - 1, ancestor))
                        return true;
                    ancestor = ancestor.Parent as Element;
                }
                return false;
            }

            case Combinator.Child:
                return MatchFromRight(selector, componentIndex - 1, element.Parent as Element);

            case Combinator.AdjacentSibling:
                return MatchFromRight(selector, componentIndex - 1, GetPreviousSiblingElement(element));

            case Combinator.GeneralSibling:
            {
                // Any preceding sibling must match
                if (element.Parent == null) return false;
                bool foundSelf = false;
                for (int i = element.Parent.Children.Count - 1; i >= 0; i--)
                {
                    var child = element.Parent.Children[i];
                    if (child == element) { foundSelf = true; continue; }
                    if (foundSelf && child is Element el)
                    {
                        if (MatchFromRight(selector, componentIndex - 1, el))
                            return true;
                    }
                }
                return false;
            }

            default:
                return MatchFromRight(selector, componentIndex - 1, element.Parent as Element);
        }
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

        foreach (var attr in selector.Attributes)
        {
            if (!MatchesAttribute(attr, element))
                return false;
        }

        foreach (var pc in selector.PseudoClasses)
        {
            if (!MatchesPseudoClass(pc, element))
                return false;
        }

        return true;
    }

    private static bool MatchesAttribute(AttributeSelector attr, Element element)
    {
        var hasAttr = element.Attributes.TryGetValue(attr.Name, out var attrValue);

        if (attr.Op == AttributeOp.Exists)
            return hasAttr;

        if (!hasAttr || attrValue == null) return false;

        var comparison = attr.CaseSensitivity == AttributeCaseSensitivity.CaseInsensitive
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        return attr.Op switch
        {
            AttributeOp.Equals => attrValue.Equals(attr.Value, comparison),
            AttributeOp.StartsWith => attr.Value != null && attrValue.StartsWith(attr.Value, comparison),
            AttributeOp.EndsWith => attr.Value != null && attrValue.EndsWith(attr.Value, comparison),
            AttributeOp.Contains => attr.Value != null && attrValue.Contains(attr.Value, comparison),
            AttributeOp.DashMatch => attrValue.Equals(attr.Value, comparison) ||
                                    (attr.Value != null && attrValue.StartsWith(attr.Value + "-", comparison)),
            AttributeOp.WordMatch => attr.Value != null && attrValue.Split(' ').Any(w => w.Equals(attr.Value, comparison)),
            _ => false
        };
    }

    private static bool MatchesPseudoClass(PseudoClass pc, Element element)
    {
        switch (pc.Type)
        {
            case PseudoClassType.FirstChild:
                return IsFirstChildElement(element);

            case PseudoClassType.LastChild:
                return IsLastChildElement(element);

            case PseudoClassType.OnlyChild:
                return IsFirstChildElement(element) && IsLastChildElement(element);

            case PseudoClassType.NthChild:
                if (pc.Argument == null) return false;
                return NthChildParser.Matches(pc.Argument, GetElementIndex(element));

            case PseudoClassType.NthLastChild:
                if (pc.Argument == null) return false;
                return NthChildParser.Matches(pc.Argument, GetElementIndexFromEnd(element));

            case PseudoClassType.NthOfType:
                if (pc.Argument == null) return false;
                return NthChildParser.Matches(pc.Argument, GetNthOfTypeIndex(element));

            case PseudoClassType.NthLastOfType:
                if (pc.Argument == null) return false;
                return NthChildParser.Matches(pc.Argument, GetNthLastOfTypeIndex(element));

            case PseudoClassType.FirstOfType:
                return IsFirstOfType(element);

            case PseudoClassType.LastOfType:
                return IsLastOfType(element);

            case PseudoClassType.OnlyOfType:
                return IsFirstOfType(element) && IsLastOfType(element);

            case PseudoClassType.Root:
                return element.Parent is Dom.Document;

            case PseudoClassType.Empty:
                return element.Children.Count == 0 ||
                       element.Children.All(c => c is TextNode tn && string.IsNullOrEmpty(tn.Data));

            case PseudoClassType.Link:
                return element.TagName.Equals("a", StringComparison.OrdinalIgnoreCase) &&
                       element.Attributes.ContainsKey("href");

            case PseudoClassType.AnyLink:
                return (element.TagName.Equals("a", StringComparison.OrdinalIgnoreCase) ||
                        element.TagName.Equals("area", StringComparison.OrdinalIgnoreCase)) &&
                       element.Attributes.ContainsKey("href");

            case PseudoClassType.Visited:
                return false; // Never match :visited for privacy

            case PseudoClassType.Hover:
                return element.IsHovered;

            case PseudoClassType.Focus:
                return element.IsFocused;

            case PseudoClassType.Active:
                return element.IsActive;

            case PseudoClassType.FocusWithin:
                return element.IsFocused || HasFocusedDescendant(element);

            case PseudoClassType.FocusVisible:
                return element.IsFocused; // Simplified: same as :focus

            case PseudoClassType.Target:
                return false; // Would need fragment identifier tracking

            case PseudoClassType.Not:
                if (pc.SelectorArgument == null) return true;
                return !pc.SelectorArgument.Any(sel => Matches(sel, element));

            case PseudoClassType.Is:
            case PseudoClassType.Where:
                if (pc.SelectorArgument == null) return false;
                return pc.SelectorArgument.Any(sel => Matches(sel, element));

            case PseudoClassType.Has:
                if (pc.SelectorArgument == null) return false;
                return MatchesHas(element, pc.SelectorArgument);

            // Form pseudo-classes
            case PseudoClassType.Enabled:
                return IsFormElement(element) && !element.Attributes.ContainsKey("disabled");
            case PseudoClassType.Disabled:
                return IsFormElement(element) && element.Attributes.ContainsKey("disabled");
            case PseudoClassType.Checked:
                return element.Attributes.ContainsKey("checked");
            case PseudoClassType.Required:
                return element.Attributes.ContainsKey("required");
            case PseudoClassType.Optional:
                return IsFormElement(element) && !element.Attributes.ContainsKey("required");
            case PseudoClassType.ReadOnly:
                return element.Attributes.ContainsKey("readonly") || element.Attributes.ContainsKey("disabled");
            case PseudoClassType.ReadWrite:
                return IsFormElement(element) && !element.Attributes.ContainsKey("readonly") && !element.Attributes.ContainsKey("disabled");
            case PseudoClassType.PlaceholderShown:
                return element.Attributes.ContainsKey("placeholder") &&
                       (!element.Attributes.TryGetValue("value", out var val) || string.IsNullOrEmpty(val));

            // Misc
            case PseudoClassType.Lang:
                if (pc.Argument == null) return false;
                return MatchesLang(element, pc.Argument);
            case PseudoClassType.Dir:
                return pc.Argument?.Equals("ltr", StringComparison.OrdinalIgnoreCase) == true; // Default LTR
            case PseudoClassType.Defined:
                return true; // All elements are "defined" in our engine
            case PseudoClassType.Scope:
                return element.Parent is Dom.Document; // Without explicit scope, :scope matches :root

            case PseudoClassType.Indeterminate:
            case PseudoClassType.Valid:
            case PseudoClassType.Invalid:
            case PseudoClassType.InRange:
            case PseudoClassType.OutOfRange:
            case PseudoClassType.Default:
                return false; // Requires form validation state tracking

            default:
                return false;
        }
    }

    private static bool IsFirstChildElement(Element element)
    {
        if (element.Parent == null) return false;
        foreach (var child in element.Parent.Children)
        {
            if (child is Element el) return el == element;
        }
        return false;
    }

    private static bool IsLastChildElement(Element element)
    {
        if (element.Parent == null) return false;
        for (int i = element.Parent.Children.Count - 1; i >= 0; i--)
        {
            if (element.Parent.Children[i] is Element el) return el == element;
        }
        return false;
    }

    private static int GetElementIndex(Element element)
    {
        if (element.Parent == null) return 1;
        int index = 0;
        foreach (var child in element.Parent.Children)
        {
            if (child is Element)
            {
                index++;
                if (child == element) return index;
            }
        }
        return 1;
    }

    private static int GetElementIndexFromEnd(Element element)
    {
        if (element.Parent == null) return 1;
        int index = 0;
        for (int i = element.Parent.Children.Count - 1; i >= 0; i--)
        {
            if (element.Parent.Children[i] is Element)
            {
                index++;
                if (element.Parent.Children[i] == element) return index;
            }
        }
        return 1;
    }

    private static bool IsFirstOfType(Element element)
    {
        if (element.Parent == null) return false;
        foreach (var child in element.Parent.Children)
        {
            if (child is Element el && el.TagName.Equals(element.TagName, StringComparison.OrdinalIgnoreCase))
                return el == element;
        }
        return false;
    }

    private static bool IsLastOfType(Element element)
    {
        if (element.Parent == null) return false;
        for (int i = element.Parent.Children.Count - 1; i >= 0; i--)
        {
            if (element.Parent.Children[i] is Element el && el.TagName.Equals(element.TagName, StringComparison.OrdinalIgnoreCase))
                return el == element;
        }
        return false;
    }

    private static Element? GetPreviousSiblingElement(Element element)
    {
        if (element.Parent == null) return null;
        Element? prev = null;
        foreach (var child in element.Parent.Children)
        {
            if (child == element) return prev;
            if (child is Element el) prev = el;
        }
        return null;
    }

    private static int GetNthOfTypeIndex(Element element)
    {
        if (element.Parent == null) return 1;
        int index = 0;
        foreach (var child in element.Parent.Children)
        {
            if (child is Element el && el.TagName.Equals(element.TagName, StringComparison.OrdinalIgnoreCase))
            {
                index++;
                if (child == element) return index;
            }
        }
        return 1;
    }

    private static int GetNthLastOfTypeIndex(Element element)
    {
        if (element.Parent == null) return 1;
        int index = 0;
        for (int i = element.Parent.Children.Count - 1; i >= 0; i--)
        {
            if (element.Parent.Children[i] is Element el && el.TagName.Equals(element.TagName, StringComparison.OrdinalIgnoreCase))
            {
                index++;
                if (element.Parent.Children[i] == element) return index;
            }
        }
        return 1;
    }

    private static bool HasFocusedDescendant(Element element)
    {
        foreach (var child in element.Children)
        {
            if (child is Element el)
            {
                if (el.IsFocused) return true;
                if (HasFocusedDescendant(el)) return true;
            }
        }
        return false;
    }

    private static bool MatchesHas(Element element, List<Selector> selectorArgs)
    {
        // :has(selector) matches if any descendant matches the selector
        foreach (var selector in selectorArgs)
        {
            if (HasMatchingDescendant(element, selector))
                return true;
        }
        return false;
    }

    private static bool HasMatchingDescendant(Element element, Selector selector)
    {
        foreach (var child in element.Children)
        {
            if (child is Element el)
            {
                if (Matches(selector, el)) return true;
                if (HasMatchingDescendant(el, selector)) return true;
            }
        }
        return false;
    }

    private static bool IsFormElement(Element element)
    {
        return element.TagName.Equals("input", StringComparison.OrdinalIgnoreCase) ||
               element.TagName.Equals("select", StringComparison.OrdinalIgnoreCase) ||
               element.TagName.Equals("textarea", StringComparison.OrdinalIgnoreCase) ||
               element.TagName.Equals("button", StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesLang(Element element, string lang)
    {
        // Walk up the tree looking for lang attribute
        Node? current = element;
        while (current != null)
        {
            if (current is Element el && el.Attributes.TryGetValue("lang", out var langAttr))
            {
                return langAttr.Equals(lang, StringComparison.OrdinalIgnoreCase) ||
                       langAttr.StartsWith(lang + "-", StringComparison.OrdinalIgnoreCase);
            }
            current = current.Parent;
        }
        return false;
    }
}
