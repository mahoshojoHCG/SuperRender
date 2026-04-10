namespace SuperRender.Core.Dom;

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

    public override string ToString() => $"Text(\"{(Data.Length > 20 ? Data[..20] + "..." : Data)}\")";
}
