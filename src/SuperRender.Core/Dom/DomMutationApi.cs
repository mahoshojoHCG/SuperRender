using SuperRender.Core.Css;

namespace SuperRender.Core.Dom;

public static class DomMutationApi
{
    public static void SetTextContent(Element element, string text)
    {
        element.InnerText = text;
    }

    public static void SetInlineStyle(Element element, string cssText)
    {
        element.SetAttribute("style", cssText);
    }

    public static void AddClass(Element element, string className)
    {
        var current = element.GetAttribute("class") ?? "";
        var classes = current.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
        if (!classes.Contains(className, StringComparer.OrdinalIgnoreCase))
        {
            classes.Add(className);
            element.SetAttribute("class", string.Join(' ', classes));
        }
    }

    public static void RemoveClass(Element element, string className)
    {
        var current = element.GetAttribute("class") ?? "";
        var classes = current.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(c => !c.Equals(className, StringComparison.OrdinalIgnoreCase))
            .ToList();
        element.SetAttribute("class", string.Join(' ', classes));
    }

    public static void ToggleClass(Element element, string className)
    {
        var current = element.GetAttribute("class") ?? "";
        var classes = current.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
        if (classes.Any(c => c.Equals(className, StringComparison.OrdinalIgnoreCase)))
            RemoveClass(element, className);
        else
            AddClass(element, className);
    }

    public static Element CloneElement(Element element, bool deep = false)
    {
        var clone = new Element(element.TagName, new Dictionary<string, string>(element.Attributes, StringComparer.OrdinalIgnoreCase));
        clone.OwnerDocument = element.OwnerDocument;

        if (deep)
        {
            foreach (var child in element.Children)
            {
                if (child is Element childElement)
                    clone.AppendChild(CloneElement(childElement, true));
                else if (child is TextNode textNode)
                    clone.AppendChild(new TextNode(textNode.Data) { OwnerDocument = element.OwnerDocument });
            }
        }

        return clone;
    }

    public static Element? QuerySelector(Node root, string selectorText)
    {
        var parser = new SelectorParser(TokenizeSelector(selectorText));
        var selectors = parser.ParseSelectorList();
        return QuerySelectorInternal(root, selectors);
    }

    public static IEnumerable<Element> QuerySelectorAll(Node root, string selectorText)
    {
        var parser = new SelectorParser(TokenizeSelector(selectorText));
        var selectors = parser.ParseSelectorList();
        var results = new List<Element>();
        QuerySelectorAllInternal(root, selectors, results);
        return results;
    }

    private static Element? QuerySelectorInternal(Node node, List<Selector> selectors)
    {
        foreach (var child in node.Children)
        {
            if (child is Element element)
            {
                foreach (var selector in selectors)
                {
                    if (selector.Matches(element))
                        return element;
                }

                var found = QuerySelectorInternal(element, selectors);
                if (found != null) return found;
            }
        }
        return null;
    }

    private static void QuerySelectorAllInternal(Node node, List<Selector> selectors, List<Element> results)
    {
        foreach (var child in node.Children)
        {
            if (child is Element element)
            {
                foreach (var selector in selectors)
                {
                    if (selector.Matches(element))
                    {
                        results.Add(element);
                        break;
                    }
                }

                QuerySelectorAllInternal(element, selectors, results);
            }
        }
    }

    private static IReadOnlyList<CssToken> TokenizeSelector(string selectorText)
    {
        var tokenizer = new CssTokenizer(selectorText);
        return tokenizer.Tokenize()
            .Where(t => t.Type != CssTokenType.EndOfFile)
            .ToList();
    }
}
