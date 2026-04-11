namespace SuperRender.Document.Dom;

public sealed class TextNode : Node
{
    public override NodeType NodeType => NodeType.Text;

    private string _data;
    public string Data
    {
        get => _data;
        set
        {
            _data = value;
            MarkDirty();
        }
    }

    public TextNode(string data)
    {
        _data = data;
    }

    /// <summary>
    /// Returns a new TextNode with the same text content and owner document.
    /// The <paramref name="deep"/> parameter is accepted for API consistency but
    /// has no effect since text nodes have no children.
    /// </summary>
    public override Node CloneNode(bool deep)
    {
        return new TextNode(Data) { OwnerDocument = OwnerDocument };
    }

    /// <summary>
    /// For a TextNode, TextContent returns and sets the Data property directly.
    /// </summary>
    public override string TextContent
    {
        get => Data;
        set => Data = value;
    }

    public override string ToString() => $"Text(\"{(Data.Length > 20 ? Data[..20] + "..." : Data)}\")";
}
