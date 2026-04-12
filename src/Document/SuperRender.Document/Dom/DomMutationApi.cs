using SuperRender.Document.Css;

namespace SuperRender.Document.Dom;

public static class DomMutationApi
{
    public static void SetTextContent(Element element, string text)
    {
        element.InnerText = text;
    }

    public static void SetInlineStyle(Element element, string cssText)
    {
        element.SetAttribute(HtmlAttributeNames.Style, cssText);
    }

    public static void AddClass(Element element, string className)
    {
        var classes = GetClassList(element);
        if (!classes.Contains(className, StringComparer.OrdinalIgnoreCase))
        {
            classes.Add(className);
            SetClassList(element, classes);
        }
    }

    public static void RemoveClass(Element element, string className)
    {
        var classes = GetClassList(element)
            .Where(c => !c.Equals(className, StringComparison.OrdinalIgnoreCase))
            .ToList();
        SetClassList(element, classes);
    }

    public static void ToggleClass(Element element, string className)
    {
        var classes = GetClassList(element);
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
        var selectors = ParseSelectors(selectorText);
        return QuerySelectorInternal(root, selectors);
    }

    public static IEnumerable<Element> QuerySelectorAll(Node root, string selectorText)
    {
        var selectors = ParseSelectors(selectorText);
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
                    if (SelectorMatcher.Matches(selector, element))
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
                    if (SelectorMatcher.Matches(selector, element))
                    {
                        results.Add(element);
                        break;
                    }
                }

                QuerySelectorAllInternal(element, selectors, results);
            }
        }
    }

    private static List<CssToken> TokenizeSelector(string selectorText)
    {
        var tokenizer = new CssTokenizer(selectorText);
        return tokenizer.Tokenize()
            .Where(t => t.Type != CssTokenType.EndOfFile)
            .ToList();
    }

    private static List<Selector> ParseSelectors(string selectorText)
    {
        var parser = new SelectorParser(TokenizeSelector(selectorText));
        return parser.ParseSelectorList();
    }

    private static List<string> GetClassList(Element element)
    {
        var current = element.GetAttribute(HtmlAttributeNames.Class) ?? "";
        return current.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
    }

    private static void SetClassList(Element element, List<string> classes)
    {
        element.SetAttribute(HtmlAttributeNames.Class, string.Join(' ', classes));
    }
}
