using DomDocument = SuperRender.Document.Dom.Document;
using SuperRender.Document.Dom;
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
        this.DefineGetter("nodeType", () => JsNumber.Create(DomNode.NodeType switch
        {
            NodeType.Element => 1,
            NodeType.Text => 3,
            NodeType.Document => 9,
            _ => 0
        }));

        this.DefineGetter("nodeName", () => DomNode switch
        {
            Element e => new JsString(e.TagName.ToUpperInvariant()),
            TextNode => new JsString("#text"),
            DomDocument => new JsString("#document"),
            _ => new JsString("")
        });

        this.DefineGetter("parentNode", () => Cache.WrapNullable(DomNode.Parent));
        this.DefineGetter("parentElement", () => Cache.WrapNullable(DomNode.Parent is Element ? DomNode.Parent : null));
        this.DefineGetter("firstChild", () => Cache.WrapNullable(DomNode.FirstChild));
        this.DefineGetter("lastChild", () => Cache.WrapNullable(DomNode.LastChild));
        this.DefineGetter("nextSibling", () => Cache.WrapNullable(DomNode.NextSibling));
        this.DefineGetter("previousSibling", () => Cache.WrapNullable(DomNode.PreviousSibling));
        this.DefineGetter("childNodes", () => new JsNodeListWrapper(DomNode.Children, Cache));

        this.DefineGetterSetter("textContent",
            () =>
            {
                if (DomNode is Element e) return new JsString(e.InnerText);
                if (DomNode is TextNode t) return new JsString(t.Data);
                return Null;
            },
            value =>
            {
                var text = value.ToJsString();
                if (DomNode is Element e) e.InnerText = text;
                else if (DomNode is TextNode t) t.Data = text;
            });

        this.DefineMethod("appendChild", 1, args =>
        {
            if (args.Length > 0 && args[0] is JsNodeWrapper child)
            {
                DomNode.AppendChild(child.DomNode);
                return child;
            }
            return Undefined;
        });

        this.DefineMethod("removeChild", 1, args =>
        {
            if (args.Length > 0 && args[0] is JsNodeWrapper child)
            {
                DomNode.RemoveChild(child.DomNode);
                return child;
            }
            return Undefined;
        });

        this.DefineMethod("insertBefore", 2, args =>
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
        });

        this.DefineMethod("hasChildNodes", 0, _ => DomNode.Children.Count > 0 ? True : False);

        this.DefineMethod("replaceChild", 2, args =>
        {
            var newChild = (args.Length > 0 ? args[0] as JsNodeWrapper : null)
                ?? throw new Runtime.Errors.JsTypeError("Argument 1 is not a node");
            var oldChild = (args.Length > 1 ? args[1] as JsNodeWrapper : null)
                ?? throw new Runtime.Errors.JsTypeError("Argument 2 is not a node");
            DomNode.ReplaceChild(newChild.GetNode(), oldChild.GetNode());
            return Cache.GetOrCreate(oldChild.GetNode());
        });

        this.DefineMethod("cloneNode", 1, args =>
        {
            bool deep = args.Length > 0 && args[0].ToBoolean();
            var clone = DomNode.CloneNode(deep);
            return Cache.GetOrCreate(clone);
        });

        this.DefineMethod("contains", 1, args =>
        {
            var other = args.Length > 0 ? (args[0] as JsNodeWrapper)?.GetNode() : null;
            if (other == null) return False;
            return DomNode.Contains(other) ? True : False;
        });

        // EventTarget methods
        DefineOwnProperty("addEventListener", PropertyDescriptor.Data(
            JsFunction.CreateNative("addEventListener", (_, args) =>
            {
                if (args.Length < 2 || args[1] is not JsFunction handler) return Undefined;
                var type = args[0].ToJsString();
                bool capture = args.Length > 2 && args[2].ToBoolean();
                Action<DomEvent> wrapper = evt =>
                    handler.Call(Undefined, [new JsEventWrapper(evt, Cache, Cache.Realm)]);
                Cache.StoreEventHandler(DomNode, type, handler, capture, wrapper);
                DomNode.AddEventListener(type, wrapper, capture);
                return Undefined;
            }, 2)));

        DefineOwnProperty("removeEventListener", PropertyDescriptor.Data(
            JsFunction.CreateNative("removeEventListener", (_, args) =>
            {
                if (args.Length < 2 || args[1] is not JsFunction handler) return Undefined;
                var type = args[0].ToJsString();
                bool capture = args.Length > 2 && args[2].ToBoolean();
                var wrapper = Cache.RetrieveEventHandler(DomNode, type, handler, capture);
                if (wrapper is not null)
                {
                    DomNode.RemoveEventListener(type, wrapper, capture);
                    Cache.RemoveEventHandler(DomNode, type, handler, capture);
                }
                return Undefined;
            }, 2)));

        this.DefineMethod("dispatchEvent", 1, _ => True);
    }

    protected static JsFunction Getter(Func<JsValue> fn)
        => JsFunction.CreateNative("get", (_, _) => fn(), 0);

    protected static JsFunction Setter(Action<JsValue> fn)
        => JsFunction.CreateNative("set", (_, args) => { fn(args.Length > 0 ? args[0] : Undefined); return Undefined; }, 1);
}
