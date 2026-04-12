using SuperRender.Document.Dom;
using SuperRender.Document.Html;
using SuperRender.EcmaScript.Runtime;

namespace SuperRender.EcmaScript.Dom;

/// <summary>
/// JS wrapper for a DOM Element. Extends JsNodeWrapper with Element-specific APIs.
/// </summary>
internal class JsElementWrapper : JsNodeWrapper
{
    private readonly Element _element;

    public JsElementWrapper(Element element, NodeWrapperCache cache, Realm realm)
        : base(element, cache, realm)
    {
        _element = element;
        InstallElementProperties();
    }

    private void InstallElementProperties()
    {
        this.DefineGetter("tagName", () => new JsString(_element.TagName.ToUpperInvariant()));

        this.DefineGetterSetter("id",
            () => _element.Id is not null ? new JsString(_element.Id) : new JsString(""),
            v => _element.SetAttribute(HtmlAttributeNames.Id, v.ToJsString()));

        this.DefineGetterSetter("className",
            () => new JsString(_element.GetAttribute(HtmlAttributeNames.Class) ?? ""),
            v => _element.SetAttribute(HtmlAttributeNames.Class, v.ToJsString()));

        this.DefineGetter("classList", () =>
        {
            var list = new JsObject();
            list.DefineMethod("add", 1, args =>
            {
                if (args.Length > 0) DomMutationApi.AddClass(_element, args[0].ToJsString());
                return Undefined;
            });
            list.DefineMethod("remove", 1, args =>
            {
                if (args.Length > 0) DomMutationApi.RemoveClass(_element, args[0].ToJsString());
                return Undefined;
            });
            list.DefineMethod("toggle", 1, args =>
            {
                if (args.Length > 0) DomMutationApi.ToggleClass(_element, args[0].ToJsString());
                return Undefined;
            });
            list.DefineMethod("contains", 1, args =>
            {
                if (args.Length > 0)
                    return _element.ClassList.Any(c =>
                        c.Equals(args[0].ToJsString(), StringComparison.OrdinalIgnoreCase))
                        ? True : False;
                return False;
            });
            return list;
        });

        this.DefineGetterSetter("innerText",
            () => new JsString(_element.InnerText),
            v => _element.InnerText = v.ToJsString());

        this.DefineGetterSetter("innerHTML",
            () => new JsString(ReconstructInnerHtml(_element)),
            v =>
            {
                while (_element.Children.Count > 0)
                    _element.RemoveChild(_element.Children[0]);
                var parser = new HtmlParser(v.ToJsString());
                var doc = parser.Parse();
                if (doc.Body is not null)
                {
                    foreach (var child in doc.Body.Children.ToList())
                        _element.AppendChild(child);
                }
            });

        this.DefineGetter("children", () =>
        {
            var elements = _element.Children.OfType<Element>().ToList();
            return new JsNodeListWrapper(elements.Cast<Node>().ToList(), Cache);
        });

        this.DefineMethod("getAttribute", 1, args =>
        {
            if (args.Length > 0)
            {
                var val = _element.GetAttribute(args[0].ToJsString());
                return val is not null ? new JsString(val) : Null;
            }
            return Null;
        });

        this.DefineMethod("setAttribute", 2, args =>
        {
            if (args.Length >= 2)
                _element.SetAttribute(args[0].ToJsString(), args[1].ToJsString());
            return Undefined;
        });

        this.DefineMethod("removeAttribute", 1, args =>
        {
            if (args.Length > 0) _element.RemoveAttribute(args[0].ToJsString());
            return Undefined;
        });

        this.DefineMethod("hasAttribute", 1, args =>
        {
            if (args.Length > 0)
                return _element.GetAttribute(args[0].ToJsString()) is not null ? True : False;
            return False;
        });

        this.DefineMethod("querySelector", 1, args =>
        {
            if (args.Length > 0)
            {
                var result = DomMutationApi.QuerySelector(_element, args[0].ToJsString());
                return result is not null ? Cache.GetOrCreate(result) : Null;
            }
            return Null;
        });

        this.DefineMethod("querySelectorAll", 1, args =>
        {
            if (args.Length > 0)
            {
                var results = DomMutationApi.QuerySelectorAll(_element, args[0].ToJsString()).ToList();
                return new JsNodeListWrapper(results.Cast<Node>().ToList(), Cache);
            }
            return new JsNodeListWrapper([], Cache);
        });

        this.DefineGetter("style", () => new JsCssStyleDeclaration(_element));

        this.DefineMethod("matches", 1, args =>
        {
            if (args.Length > 0)
                return _element.Matches(args[0].ToJsString()) ? True : False;
            return False;
        });

        this.DefineMethod("closest", 1, args =>
        {
            if (args.Length > 0)
            {
                var result = _element.Closest(args[0].ToJsString());
                return Cache.WrapNullable(result);
            }
            return Null;
        });

        this.DefineGetter("dataset", () =>
        {
            var obj = new JsObject();
            foreach (var kvp in _element.Dataset)
                obj.DefineOwnProperty(kvp.Key, PropertyDescriptor.Data(new JsString(kvp.Value)));
            return obj;
        });

        this.DefineMethod("toggleAttribute", 1, args =>
        {
            if (args.Length < 1) return False;
            var name = args[0].ToJsString();
            bool? force = args.Length > 1 ? args[1].ToBoolean() : null;
            return _element.ToggleAttribute(name, force) ? True : False;
        });

        this.DefineGetter("firstElementChild", () => Cache.WrapNullable(_element.FirstElementChild));
        this.DefineGetter("lastElementChild", () => Cache.WrapNullable(_element.LastElementChild));
        this.DefineGetter("childElementCount", () => JsNumber.Create(_element.ChildElementCount));

        this.DefineMethod("after", 0, args =>
        {
            var nodes = args.OfType<JsNodeWrapper>().Select(w => w.GetNode()).ToArray();
            if (nodes.Length > 0) _element.After(nodes);
            return Undefined;
        });

        this.DefineMethod("before", 0, args =>
        {
            var nodes = args.OfType<JsNodeWrapper>().Select(w => w.GetNode()).ToArray();
            if (nodes.Length > 0) _element.Before(nodes);
            return Undefined;
        });

        this.DefineMethod("remove", 0, _ =>
        {
            _element.Remove();
            return Undefined;
        });
    }

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
