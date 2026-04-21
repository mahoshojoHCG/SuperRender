using DomDocument = SuperRender.Document.Dom.Document;
using SuperRender.Document.Dom;
using SuperRender.EcmaScript.Runtime;

namespace SuperRender.EcmaScript.Dom;

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

#pragma warning disable JSGEN006 // returns wrapped DOM node — needs IJsNode/IJsElement IJsType
    [JsProperty("parentNode")] public JsValue ParentNode => Cache.WrapNullable(DomNode.Parent);
    [JsProperty("parentElement")] public JsValue ParentElement => Cache.WrapNullable(DomNode.Parent is Element ? DomNode.Parent : null);
    [JsProperty("firstChild")] public JsValue FirstChild => Cache.WrapNullable(DomNode.FirstChild);
    [JsProperty("lastChild")] public JsValue LastChild => Cache.WrapNullable(DomNode.LastChild);
    [JsProperty("nextSibling")] public JsValue NextSibling => Cache.WrapNullable(DomNode.NextSibling);
    [JsProperty("previousSibling")] public JsValue PreviousSibling => Cache.WrapNullable(DomNode.PreviousSibling);
#pragma warning restore JSGEN006

#pragma warning disable JSGEN006 // returns wrapped node list — needs IJsNodeList IJsType
    [JsProperty("childNodes")] public JsValue ChildNodes => new JsNodeListWrapper(DomNode.Children, Cache);
#pragma warning restore JSGEN006

#pragma warning disable JSGEN005 // JsValue param: accepts Node|string union
#pragma warning disable JSGEN006 // returns wrapped DOM node — needs IJsNode/IJsElement IJsType
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
#pragma warning restore JSGEN006
#pragma warning restore JSGEN005

#pragma warning disable JSGEN005 // JsValue param: accepts Node|string union
#pragma warning disable JSGEN006 // returns wrapped DOM node — needs IJsNode/IJsElement IJsType
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
#pragma warning restore JSGEN006
#pragma warning restore JSGEN005

    [JsMethod("hasChildNodes")]
    public bool HasChildNodes() => DomNode.Children.Count > 0;

#pragma warning disable JSGEN005 // JsValue param: accepts Node|string union
#pragma warning disable JSGEN006 // returns wrapped DOM node — needs IJsNode/IJsElement IJsType
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
#pragma warning restore JSGEN006
#pragma warning restore JSGEN005

#pragma warning disable JSGEN006 // returns wrapped DOM node — needs IJsNode/IJsElement IJsType
    [JsMethod("cloneNode")]
    public JsValue CloneNode(bool deep)
    {
        var clone = DomNode.CloneNode(deep);
        return Cache.GetOrCreate(clone);
    }
#pragma warning restore JSGEN006

#pragma warning disable JSGEN005 // JsValue param: caller may pass null/primitive
    [JsMethod("contains")]
    public bool Contains(JsValue other)
    {
        var n = (other as JsNodeWrapper)?.GetNode();
        if (n == null) return false;
        return DomNode.Contains(n);
    }
#pragma warning restore JSGEN005

#pragma warning disable JSGEN005 // variadic: type + handler + optional capture
    [JsMethod("addEventListener")]
    public void AddEventListener(JsValue[] args)
    {
        if (args.Length < 2 || args[1] is not JsFunction handler) return;
        var type = args[0].ToJsString();
        bool capture = args.Length > 2 && args[2].ToBoolean();
        Action<DomEvent> wrapper = evt =>
            handler.Call(JsValue.Undefined, [new JsEventWrapper(evt, Cache, Cache.Realm)]);
        Cache.StoreEventHandler(DomNode, type, handler, capture, wrapper);
        DomNode.AddEventListener(type, wrapper, capture);
    }

    [JsMethod("removeEventListener")]
    public void RemoveEventListener(JsValue[] args)
    {
        if (args.Length < 2 || args[1] is not JsFunction handler) return;
        var type = args[0].ToJsString();
        bool capture = args.Length > 2 && args[2].ToBoolean();
        var wrapper = Cache.RetrieveEventHandler(DomNode, type, handler, capture);
        if (wrapper is not null)
        {
            DomNode.RemoveEventListener(type, wrapper, capture);
            Cache.RemoveEventHandler(DomNode, type, handler, capture);
        }
    }
#pragma warning restore JSGEN005

#pragma warning disable JSGEN005 // JsValue param: caller may pass null/primitive
    [JsMethod("dispatchEvent")]
    public static bool DispatchEvent(JsValue _) => true;
#pragma warning restore JSGEN005
}
