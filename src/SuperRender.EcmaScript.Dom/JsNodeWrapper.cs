using SuperRender.Core.Dom;
using SuperRender.EcmaScript.Runtime;

namespace SuperRender.EcmaScript.Dom;

/// <summary>
/// JS wrapper for a DOM Node. Exposes the standard DOM Node API.
/// </summary>
internal class JsNodeWrapper : JsObject
{
    protected readonly Node DomNode;
    protected readonly NodeWrapperCache Cache;

    public JsNodeWrapper(Node node, NodeWrapperCache cache, Realm realm)
    {
        DomNode = node;
        Cache = cache;
        Prototype = realm.ObjectPrototype;
        InstallProperties();
    }

    internal Node GetNode() => DomNode;

    private void InstallProperties()
    {
        DefineOwnProperty("nodeType", PropertyDescriptor.Accessor(
            Getter(() => JsNumber.Create(DomNode.NodeType switch
            {
                NodeType.Element => 1,
                NodeType.Text => 3,
                NodeType.Document => 9,
                _ => 0
            })), null, enumerable: true, configurable: true));

        DefineOwnProperty("nodeName", PropertyDescriptor.Accessor(
            Getter(() => DomNode switch
            {
                Element e => new JsString(e.TagName.ToUpperInvariant()),
                TextNode => new JsString("#text"),
                Document => new JsString("#document"),
                _ => new JsString("")
            }), null, enumerable: true, configurable: true));

        DefineOwnProperty("parentNode", PropertyDescriptor.Accessor(
            Getter(() => Cache.WrapNullable(DomNode.Parent)),
            null, enumerable: true, configurable: true));

        DefineOwnProperty("parentElement", PropertyDescriptor.Accessor(
            Getter(() => Cache.WrapNullable(DomNode.Parent is Element ? DomNode.Parent : null)),
            null, enumerable: true, configurable: true));

        DefineOwnProperty("firstChild", PropertyDescriptor.Accessor(
            Getter(() => Cache.WrapNullable(DomNode.FirstChild)),
            null, enumerable: true, configurable: true));

        DefineOwnProperty("lastChild", PropertyDescriptor.Accessor(
            Getter(() => Cache.WrapNullable(DomNode.LastChild)),
            null, enumerable: true, configurable: true));

        DefineOwnProperty("nextSibling", PropertyDescriptor.Accessor(
            Getter(() => Cache.WrapNullable(DomNode.NextSibling)),
            null, enumerable: true, configurable: true));

        DefineOwnProperty("previousSibling", PropertyDescriptor.Accessor(
            Getter(() => Cache.WrapNullable(DomNode.PreviousSibling)),
            null, enumerable: true, configurable: true));

        DefineOwnProperty("childNodes", PropertyDescriptor.Accessor(
            Getter(() => new JsNodeListWrapper(DomNode.Children, Cache)),
            null, enumerable: true, configurable: true));

        DefineOwnProperty("textContent", PropertyDescriptor.Accessor(
            Getter(() =>
            {
                if (DomNode is Element e)
                    return new JsString(e.InnerText);
                if (DomNode is TextNode t)
                    return new JsString(t.Data);
                return Null;
            }),
            Setter(value =>
            {
                var text = value.ToJsString();
                if (DomNode is Element e)
                    e.InnerText = text;
                else if (DomNode is TextNode t)
                    t.Data = text;
            }), enumerable: true, configurable: true));

        DefineOwnProperty("appendChild", PropertyDescriptor.Data(
            JsFunction.CreateNative("appendChild", (_, args) =>
            {
                if (args.Length > 0 && args[0] is JsNodeWrapper child)
                {
                    DomNode.AppendChild(child.DomNode);
                    return child;
                }
                return Undefined;
            }, 1)));

        DefineOwnProperty("removeChild", PropertyDescriptor.Data(
            JsFunction.CreateNative("removeChild", (_, args) =>
            {
                if (args.Length > 0 && args[0] is JsNodeWrapper child)
                {
                    DomNode.RemoveChild(child.DomNode);
                    return child;
                }
                return Undefined;
            }, 1)));

        DefineOwnProperty("insertBefore", PropertyDescriptor.Data(
            JsFunction.CreateNative("insertBefore", (_, args) =>
            {
                if (args.Length < 1) return Undefined;
                var newChild = args[0] as JsNodeWrapper;
                var refChild = args.Length > 1 ? args[1] as JsNodeWrapper : null;
                if (newChild is not null)
                {
                    DomNode.InsertBefore(newChild.DomNode, refChild?.DomNode);
                    return newChild;
                }
                return Undefined;
            }, 2)));

        DefineOwnProperty("hasChildNodes", PropertyDescriptor.Data(
            JsFunction.CreateNative("hasChildNodes", (_, _) =>
                DomNode.Children.Count > 0 ? True : False, 0)));
    }

    protected static JsFunction Getter(Func<JsValue> fn)
        => JsFunction.CreateNative("get", (_, _) => fn(), 0);

    protected static JsFunction Setter(Action<JsValue> fn)
        => JsFunction.CreateNative("set", (_, args) => { fn(args.Length > 0 ? args[0] : Undefined); return Undefined; }, 1);
}
