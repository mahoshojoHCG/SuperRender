using SuperRender.Document.Css;

namespace SuperRender.Document.Dom;

public sealed class Element : Node
{
    public override NodeType NodeType => NodeType.Element;

    public string TagName { get; }
    public Dictionary<string, string> Attributes { get; }

    public string? Id => Attributes.GetValueOrDefault("id");

    private List<string>? _classList;
    public IReadOnlyList<string> ClassList
    {
        get
        {
            if (_classList == null)
                RebuildClassList();
            return _classList!;
        }
    }

    public Element(string tagName, Dictionary<string, string>? attributes = null)
    {
        TagName = tagName.ToLowerInvariant();
        Attributes = attributes ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    public string? GetAttribute(string name)
        => Attributes.GetValueOrDefault(name);

    public void SetAttribute(string name, string value)
    {
        Attributes[name] = value;
        if (name.Equals("class", StringComparison.OrdinalIgnoreCase))
            _classList = null;
        MarkDirty();
    }

    public void RemoveAttribute(string name)
    {
        Attributes.Remove(name);
        if (name.Equals("class", StringComparison.OrdinalIgnoreCase))
            _classList = null;
        MarkDirty();
    }

    /// <summary>
    /// Returns true if the element has an attribute with the given name (case-insensitive).
    /// </summary>
    public bool HasAttribute(string name)
        => Attributes.ContainsKey(name);

    /// <summary>
    /// Toggles a boolean attribute. If <paramref name="force"/> is true, ensures the attribute
    /// exists. If false, removes it. If null, toggles it. Returns whether the attribute is
    /// present after the call.
    /// </summary>
    public bool ToggleAttribute(string name, bool? force = null)
    {
        bool exists = HasAttribute(name);

        if (force == true || (force == null && !exists))
        {
            SetAttribute(name, "");
            return true;
        }

        if (force == false || (force == null && exists))
        {
            RemoveAttribute(name);
            return false;
        }

        return exists;
    }

    public string InnerText
    {
        get
        {
            var sb = new System.Text.StringBuilder();
            CollectText(this, sb);
            return sb.ToString();
        }
        set
        {
            Children.Clear();
            MarkDirty();
            if (!string.IsNullOrEmpty(value))
            {
                var textNode = new TextNode(value) { OwnerDocument = OwnerDocument };
                AppendChild(textNode);
            }
        }
    }

    private static void CollectText(Node node, System.Text.StringBuilder sb)
    {
        foreach (var child in node.Children)
        {
            if (child is TextNode text)
                sb.Append(text.Data);
            else
                CollectText(child, sb);
        }
    }

    // --- Element traversal properties ---

    /// <summary>
    /// Returns the first child that is an Element, or null if none.
    /// </summary>
    public Element? FirstElementChild =>
        Children.OfType<Element>().FirstOrDefault();

    /// <summary>
    /// Returns the last child that is an Element, or null if none.
    /// </summary>
    public Element? LastElementChild =>
        Children.OfType<Element>().LastOrDefault();

    /// <summary>
    /// Returns the count of child nodes that are Elements.
    /// </summary>
    public int ChildElementCount =>
        Children.OfType<Element>().Count();

    // --- DOM manipulation methods ---

    /// <summary>
    /// Creates a clone of this element. If <paramref name="deep"/> is true, all children
    /// are cloned recursively.
    /// </summary>
    public override Node CloneNode(bool deep)
    {
        var clone = new Element(TagName, new Dictionary<string, string>(Attributes, StringComparer.OrdinalIgnoreCase))
        {
            OwnerDocument = OwnerDocument
        };

        if (deep)
        {
            foreach (var child in Children)
                clone.AppendChild(child.CloneNode(true));
        }

        return clone;
    }

    /// <summary>
    /// Tests whether this element matches the given CSS selector string.
    /// </summary>
    public bool Matches(string selectorText)
    {
        var selectors = ParseSelectors(selectorText);
        foreach (var selector in selectors)
        {
            if (SelectorMatcher.Matches(selector, this))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Walks up the ancestor chain (including self) and returns the first element
    /// matching the given CSS selector, or null if none matches.
    /// </summary>
    public Element? Closest(string selectorText)
    {
        var selectors = ParseSelectors(selectorText);
        Element? current = this;
        while (current != null)
        {
            foreach (var selector in selectors)
            {
                if (SelectorMatcher.Matches(selector, current))
                    return current;
            }
            current = current.Parent as Element;
        }
        return null;
    }

    /// <summary>
    /// Returns a dictionary of data-* attributes with the "data-" prefix stripped and
    /// kebab-case names converted to camelCase.
    /// </summary>
    public IReadOnlyDictionary<string, string> Dataset
    {
        get
        {
            var result = new Dictionary<string, string>();
            foreach (var kvp in Attributes)
            {
                if (kvp.Key.StartsWith("data-", StringComparison.OrdinalIgnoreCase) && kvp.Key.Length > 5)
                {
                    var key = KebabToCamelCase(kvp.Key[5..]);
                    result[key] = kvp.Value;
                }
            }
            return result;
        }
    }

    /// <summary>
    /// Inserts the given nodes after this element in its parent's children list.
    /// </summary>
    public void After(params Node[] nodes)
    {
        if (Parent is null) return;

        var nextSib = NextSibling;
        foreach (var node in nodes)
        {
            Parent.InsertBefore(node, nextSib);
        }
    }

    /// <summary>
    /// Inserts the given nodes before this element in its parent's children list.
    /// </summary>
    public void Before(params Node[] nodes)
    {
        if (Parent is null) return;

        foreach (var node in nodes)
        {
            Parent.InsertBefore(node, this);
        }
    }

    /// <summary>
    /// Removes this element from its parent.
    /// </summary>
    public void Remove()
    {
        Parent?.RemoveChild(this);
    }

    // --- Helpers ---

    private static List<Selector> ParseSelectors(string selectorText)
    {
        var tokenizer = new CssTokenizer(selectorText);
        var tokens = tokenizer.Tokenize()
            .Where(t => t.Type != CssTokenType.EndOfFile)
            .ToList();
        var parser = new SelectorParser(tokens);
        return parser.ParseSelectorList();
    }

    private static string KebabToCamelCase(string kebab)
    {
        if (string.IsNullOrEmpty(kebab)) return kebab;

        var sb = new System.Text.StringBuilder(kebab.Length);
        bool capitalizeNext = false;
        foreach (char c in kebab)
        {
            if (c == '-')
            {
                capitalizeNext = true;
            }
            else if (capitalizeNext)
            {
                sb.Append(char.ToUpperInvariant(c));
                capitalizeNext = false;
            }
            else
            {
                sb.Append(c);
            }
        }
        return sb.ToString();
    }

    // --- Dynamic state flags (set by browser/InputHandler) ---

    /// <summary>Whether the mouse is currently over this element.</summary>
    public bool IsHovered { get; set; }

    /// <summary>Whether this element currently has keyboard focus.</summary>
    public bool IsFocused { get; set; }

    /// <summary>Whether a mouse button is currently pressed on this element.</summary>
    public bool IsActive { get; set; }

    internal void RebuildClassList()
    {
        if (Attributes.TryGetValue("class", out var cls) && !string.IsNullOrWhiteSpace(cls))
            _classList = [.. cls.Split(' ', StringSplitOptions.RemoveEmptyEntries)];
        else
            _classList = [];
    }

    public override string ToString() => $"<{TagName}>";
}
