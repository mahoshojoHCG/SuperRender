using DomDocument = SuperRender.Document.Dom.Document;
using SuperRender.Document.Dom;
using SuperRender.EcmaScript.Runtime;

namespace SuperRender.EcmaScript.Dom;

// JSGEN005/006/007: DOM Node members return JsValue-wrapped DOM nodes and accept raw JsValue for
// mixed Node|string targets. Migration to IJsNode/IJsNodeList IJsType is tracked separately;
// addEventListener/removeEventListener stay on the legacy variadic shape because the callback arg
// is a JsFunction that can't be represented as a typed interface param.
#pragma warning disable JSGEN005, JSGEN006, JSGEN007

/// <summary>
/// JS wrapper for a DOM Node. Exposes the standard DOM Node API.
/// </summary>
[JsObject]
internal partial class JsNodeWrapper : JsDynamicObject
{
    protected readonly Node DomNode;
    protected readonly NodeWrapperCache Cache;

    public JsNodeWrapper(Node node, NodeWrapperCache cache, Realm realm)
    {
        DomNode = node;
        Cache = cache;
        Prototype = realm.ObjectPrototype;
    }

    internal Node GetNode() => DomNode;

    [JsProperty("nodeType")]
    public virtual double NodeType => DomNode.NodeType switch
    {
        SuperRender.Document.Dom.NodeType.Element => 1,
        SuperRender.Document.Dom.NodeType.Text => 3,
        SuperRender.Document.Dom.NodeType.Document => 9,
        _ => 0
    };

    [JsProperty("nodeName")]
    public virtual string NodeName => DomNode switch
    {
        Element e => e.TagName.ToUpperInvariant(),
        TextNode => "#text",
        DomDocument => "#document",
        _ => ""
    };

    [JsProperty("parentNode")] public JsValue ParentNode => Cache.WrapNullable(DomNode.Parent);
    [JsProperty("parentElement")] public JsValue ParentElement => Cache.WrapNullable(DomNode.Parent is Element ? DomNode.Parent : null);
    [JsProperty("firstChild")] public JsValue FirstChild => Cache.WrapNullable(DomNode.FirstChild);
    [JsProperty("lastChild")] public JsValue LastChild => Cache.WrapNullable(DomNode.LastChild);
    [JsProperty("nextSibling")] public JsValue NextSibling => Cache.WrapNullable(DomNode.NextSibling);
    [JsProperty("previousSibling")] public JsValue PreviousSibling => Cache.WrapNullable(DomNode.PreviousSibling);
    [JsProperty("childNodes")] public JsValue ChildNodes => new JsNodeListWrapper(DomNode.Children, Cache);

    [JsProperty("textContent")]
    public JsValue TextContent
    {
        get
        {
            if (DomNode is Element e) return new JsString(e.InnerText);
            if (DomNode is TextNode t) return new JsString(t.Data);
            return JsValue.Null;
        }
        set
        {
            var text = value.ToJsString();
            if (DomNode is Element e) e.InnerText = text;
            else if (DomNode is TextNode t) t.Data = text;
        }
    }

    [JsMethod("appendChild")]
    public JsValue AppendChild(JsValue child)
    {
        if (child is JsNodeWrapper w)
        {
            DomNode.AppendChild(w.DomNode);
            return w;
        }
        return JsValue.Undefined;
    }

    [JsMethod("removeChild")]
    public JsValue RemoveChild(JsValue child)
    {
        if (child is JsNodeWrapper w)
        {
            DomNode.RemoveChild(w.DomNode);
            return w;
        }
        return JsValue.Undefined;
    }

    [JsMethod("insertBefore")]
    public JsValue InsertBefore(JsValue newChild, JsValue refChild)
    {
        if (newChild is not JsNodeWrapper n) return JsValue.Undefined;
        var r = refChild as JsNodeWrapper;
        DomNode.InsertBefore(n.DomNode, r?.DomNode);
        return n;
    }

    [JsMethod("hasChildNodes")]
    public bool HasChildNodes() => DomNode.Children.Count > 0;

    [JsMethod("replaceChild")]
    public JsValue ReplaceChild(JsValue newChild, JsValue oldChild)
    {
        var newWrap = newChild as JsNodeWrapper
            ?? throw new Runtime.Errors.JsTypeError("Argument 1 is not a node");
        var oldWrap = oldChild as JsNodeWrapper
            ?? throw new Runtime.Errors.JsTypeError("Argument 2 is not a node");
        DomNode.ReplaceChild(newWrap.GetNode(), oldWrap.GetNode());
        return Cache.GetOrCreate(oldWrap.GetNode());
    }

    [JsMethod("cloneNode")]
    public JsValue CloneNode(JsValue deep)
    {
        bool isDeep = deep.ToBoolean();
        var clone = DomNode.CloneNode(isDeep);
        return Cache.GetOrCreate(clone);
    }

    [JsMethod("contains")]
    public bool Contains(JsValue other)
    {
        var n = (other as JsNodeWrapper)?.GetNode();
        if (n == null) return false;
        return DomNode.Contains(n);
    }

    [JsMethod("addEventListener")]
    public JsValue AddEventListener(JsValue _, JsValue[] args)
    {
        if (args.Length < 2 || args[1] is not JsFunction handler) return JsValue.Undefined;
        var type = args[0].ToJsString();
        bool capture = args.Length > 2 && args[2].ToBoolean();
        Action<DomEvent> wrapper = evt =>
            handler.Call(JsValue.Undefined, [new JsEventWrapper(evt, Cache, Cache.Realm)]);
        Cache.StoreEventHandler(DomNode, type, handler, capture, wrapper);
        DomNode.AddEventListener(type, wrapper, capture);
        return JsValue.Undefined;
    }

    [JsMethod("removeEventListener")]
    public JsValue RemoveEventListener(JsValue _, JsValue[] args)
    {
        if (args.Length < 2 || args[1] is not JsFunction handler) return JsValue.Undefined;
        var type = args[0].ToJsString();
        bool capture = args.Length > 2 && args[2].ToBoolean();
        var wrapper = Cache.RetrieveEventHandler(DomNode, type, handler, capture);
        if (wrapper is not null)
        {
            DomNode.RemoveEventListener(type, wrapper, capture);
            Cache.RemoveEventHandler(DomNode, type, handler, capture);
        }
        return JsValue.Undefined;
    }

    [JsMethod("dispatchEvent")]
    public static bool DispatchEvent(JsValue _) => true;
}
