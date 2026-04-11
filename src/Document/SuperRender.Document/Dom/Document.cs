using SuperRender.Document.Css;

namespace SuperRender.Document.Dom;

public sealed class Document : Node
{
    public override NodeType NodeType => NodeType.Document;

    public Element? DocumentElement => Children.OfType<Element>().FirstOrDefault();

    public Element? Body => DocumentElement?.Children.OfType<Element>()
        .FirstOrDefault(e => e.TagName == "body");

    public Element? Head => DocumentElement?.Children.OfType<Element>()
        .FirstOrDefault(e => e.TagName == "head");

    public List<Stylesheet> Stylesheets { get; } = [];

    public bool NeedsLayout { get; set; } = true;

    public Document()
    {
        OwnerDocument = this;
    }

    public Element CreateElement(string tagName)
    {
        return new Element(tagName) { OwnerDocument = this };
    }

    public TextNode CreateTextNode(string data)
    {
        return new TextNode(data) { OwnerDocument = this };
    }
}
