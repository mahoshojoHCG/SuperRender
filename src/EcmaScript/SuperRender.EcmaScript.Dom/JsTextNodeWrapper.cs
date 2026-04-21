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
    public string Data => _textNode.Data;

    [JsProperty("data", IsSetter = true)]
    public void SetData(string value) => _textNode.Data = value;

    [JsProperty("nodeValue")]
    public string NodeValue => _textNode.Data;

    [JsProperty("nodeValue", IsSetter = true)]
    public void SetNodeValue(string value) => _textNode.Data = value;

    [JsProperty("length")]
    public int Length => _textNode.Data.Length;
}
