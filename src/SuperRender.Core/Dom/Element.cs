namespace SuperRender.Core.Dom;

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

    internal void RebuildClassList()
    {
        if (Attributes.TryGetValue("class", out var cls) && !string.IsNullOrWhiteSpace(cls))
            _classList = [.. cls.Split(' ', StringSplitOptions.RemoveEmptyEntries)];
        else
            _classList = [];
    }

    public override string ToString() => $"<{TagName}>";
}
