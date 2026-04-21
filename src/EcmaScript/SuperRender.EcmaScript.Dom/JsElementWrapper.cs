using SuperRender.Document.Dom;
using SuperRender.Document.Html;
using SuperRender.EcmaScript.Runtime;

namespace SuperRender.EcmaScript.Dom;

/// <summary>
/// JS wrapper for a DOM Element. Extends JsNodeWrapper with Element-specific APIs.
/// </summary>
[JsObject]
internal partial class JsElementWrapper : JsNodeWrapper
{
    private readonly Element _element;

    public JsElementWrapper(Element element, NodeWrapperCache cache, Realm realm)
        : base(element, cache, realm)
    {
        _element = element;
    }

    [JsProperty("tagName")]
    public string TagName => _element.TagName.ToUpperInvariant();

    [JsProperty("id")]
    public string Id
    {
        get => _element.Id ?? "";
        set => _element.SetAttribute(HtmlAttributeNames.Id, value);
    }

    [JsProperty("className")]
    public string ClassName
    {
        get => _element.GetAttribute(HtmlAttributeNames.Class) ?? "";
        set => _element.SetAttribute(HtmlAttributeNames.Class, value);
    }

    [JsProperty("classList")]
    public JsValue ClassList
    {
        get
        {
            var list = new JsDynamicObject();
            list.DefineMethod("add", 1, args =>
            {
                if (args.Length > 0) DomMutationApi.AddClass(_element, args[0].ToJsString());
                return JsValue.Undefined;
            });
            list.DefineMethod("remove", 1, args =>
            {
                if (args.Length > 0) DomMutationApi.RemoveClass(_element, args[0].ToJsString());
                return JsValue.Undefined;
            });
            list.DefineMethod("toggle", 1, args =>
            {
                if (args.Length > 0) DomMutationApi.ToggleClass(_element, args[0].ToJsString());
                return JsValue.Undefined;
            });
            list.DefineMethod("contains", 1, args =>
            {
                if (args.Length > 0)
                    return _element.ClassList.Any(c =>
                        c.Equals(args[0].ToJsString(), StringComparison.OrdinalIgnoreCase))
                        ? JsValue.True : JsValue.False;
                return JsValue.False;
            });
            return list;
        }
    }

    [JsProperty("innerText")]
    public string InnerText
    {
        get => _element.InnerText;
        set => _element.InnerText = value;
    }

    [JsProperty("innerHTML")]
    public string InnerHTML
    {
        get => ReconstructInnerHtml(_element);
        set
        {
            while (_element.Children.Count > 0)
                _element.RemoveChild(_element.Children[0]);
            var parser = new HtmlParser(value);
            var doc = parser.Parse();
            if (doc.Body is not null)
            {
                foreach (var child in doc.Body.Children.ToList())
                    _element.AppendChild(child);
            }
        }
    }

    [JsProperty("children")]
    public JsValue Children
    {
        get
        {
            var elements = _element.Children.OfType<Element>().Cast<Node>().ToList();
            return new JsNodeListWrapper(elements, Cache);
        }
    }

    [JsProperty("style")]
    public JsValue Style => new JsCssStyleDeclaration(_element);

    [JsProperty("dataset")]
    public JsValue Dataset
    {
        get
        {
            var obj = new JsDynamicObject();
            foreach (var kvp in _element.Dataset)
                obj.DefineOwnProperty(kvp.Key, PropertyDescriptor.Data(new JsString(kvp.Value)));
            return obj;
        }
    }

    [JsProperty("firstElementChild")]
    public JsValue FirstElementChild => Cache.WrapNullable(_element.FirstElementChild);

    [JsProperty("lastElementChild")]
    public JsValue LastElementChild => Cache.WrapNullable(_element.LastElementChild);

    [JsProperty("childElementCount")]
    public int ChildElementCount => _element.ChildElementCount;

    [JsMethod("getAttribute")]
    public JsValue GetAttribute(string name)
    {
        var val = _element.GetAttribute(name);
        return val is not null ? new JsString(val) : JsValue.Null;
    }

    [JsMethod("setAttribute")]
    public void SetAttribute(string name, string value) => _element.SetAttribute(name, value);

    [JsMethod("removeAttribute")]
    public void RemoveAttribute(string name) => _element.RemoveAttribute(name);

    [JsMethod("hasAttribute")]
    public bool HasAttribute(string name) => _element.GetAttribute(name) is not null;

    [JsMethod("querySelector")]
    public JsValue QuerySelector(string selector)
    {
        var result = DomMutationApi.QuerySelector(_element, selector);
        return result is not null ? Cache.GetOrCreate(result) : JsValue.Null;
    }

    [JsMethod("querySelectorAll")]
    public JsValue QuerySelectorAll(string selector)
    {
        var results = DomMutationApi.QuerySelectorAll(_element, selector).Cast<Node>().ToList();
        return new JsNodeListWrapper(results, Cache);
    }

    [JsMethod("matches")]
    public bool Matches(string selector) => _element.Matches(selector);

    [JsMethod("closest")]
    public JsValue Closest(string selector) => Cache.WrapNullable(_element.Closest(selector));

    [JsMethod("toggleAttribute")]
    public JsValue ToggleAttribute(JsValue _, JsValue[] args)
    {
        if (args.Length < 1) return JsValue.False;
        var name = args[0].ToJsString();
        bool? force = args.Length > 1 ? args[1].ToBoolean() : null;
        return _element.ToggleAttribute(name, force) ? JsValue.True : JsValue.False;
    }

    [JsMethod("after")]
    public void After(JsValue[] args)
    {
        var nodes = args.OfType<JsNodeWrapper>().Select(w => w.GetNode()).ToArray();
        if (nodes.Length > 0) _element.After(nodes);
    }

    [JsMethod("before")]
    public void Before(JsValue[] args)
    {
        var nodes = args.OfType<JsNodeWrapper>().Select(w => w.GetNode()).ToArray();
        if (nodes.Length > 0) _element.Before(nodes);
    }

    [JsMethod("remove")]
    public void Remove() => _element.Remove();

    private static string ReconstructInnerHtml(Element element)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var child in element.Children)
            AppendNodeHtml(sb, child);
        return sb.ToString();
    }

    private static void AppendNodeHtml(System.Text.StringBuilder sb, Node node)
    {
        if (node is TextNode text)
        {
            sb.Append(text.Data);
            return;
        }

        if (node is Element el)
        {
            sb.Append('<').Append(el.TagName);
            foreach (var attr in el.Attributes)
                sb.Append(' ').Append(attr.Key).Append("=\"").Append(attr.Value).Append('"');
            sb.Append('>');

            foreach (var child in el.Children)
                AppendNodeHtml(sb, child);

            sb.Append("</").Append(el.TagName).Append('>');
        }
    }
}
