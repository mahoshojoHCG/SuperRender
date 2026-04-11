using SuperRender.Document.Dom;
using SuperRender.EcmaScript.Runtime;

namespace SuperRender.EcmaScript.Dom;

/// <summary>
/// JS wrapper for a DOM TextNode.
/// </summary>
internal sealed class JsTextNodeWrapper : JsNodeWrapper
{
    private readonly TextNode _textNode;

    public JsTextNodeWrapper(TextNode textNode, NodeWrapperCache cache, Realm realm)
        : base(textNode, cache, realm)
    {
        _textNode = textNode;
        InstallTextProperties();
    }

    private void InstallTextProperties()
    {
        DefineOwnProperty("data", PropertyDescriptor.Accessor(
            Getter(() => new JsString(_textNode.Data)),
            Setter(v => _textNode.Data = v.ToJsString()),
            enumerable: true, configurable: true));

        DefineOwnProperty("nodeValue", PropertyDescriptor.Accessor(
            Getter(() => new JsString(_textNode.Data)),
            Setter(v => _textNode.Data = v.ToJsString()),
            enumerable: true, configurable: true));

        DefineOwnProperty("length", PropertyDescriptor.Accessor(
            Getter(() => JsNumber.Create(_textNode.Data.Length)),
            null, enumerable: true, configurable: true));
    }
}
