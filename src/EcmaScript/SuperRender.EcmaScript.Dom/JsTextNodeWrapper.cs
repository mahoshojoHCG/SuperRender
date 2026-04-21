using SuperRender.Document.Dom;
using SuperRender.EcmaScript.Runtime;

namespace SuperRender.EcmaScript.Dom;

/// <summary>
/// JS wrapper for a DOM TextNode.
/// </summary>
[JsObject]
internal sealed partial class JsTextNodeWrapper : JsNodeWrapper
{
    private readonly TextNode _textNode;

    public JsTextNodeWrapper(TextNode textNode, NodeWrapperCache cache, Realm realm)
        : base(textNode, cache, realm)
    {
        _textNode = textNode;
    }

    [JsProperty("data")]
    public string Data
    {
        get => _textNode.Data;
        set => _textNode.Data = value;
    }

    [JsProperty("nodeValue")]
    public string NodeValue
    {
        get => _textNode.Data;
        set => _textNode.Data = value;
    }

    [JsProperty("length")]
    public int Length => _textNode.Data.Length;
}
