using SuperRender.Core.Dom;
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
        DefineOwnProperty("tagName", PropertyDescriptor.Accessor(
            Getter(() => new JsString(_element.TagName.ToUpperInvariant())),
            null, enumerable: true, configurable: true));

        DefineOwnProperty("id", PropertyDescriptor.Accessor(
            Getter(() => _element.Id is not null ? new JsString(_element.Id) : new JsString("")),
            Setter(v => _element.SetAttribute("id", v.ToJsString())),
            enumerable: true, configurable: true));

        DefineOwnProperty("className", PropertyDescriptor.Accessor(
            Getter(() => new JsString(_element.GetAttribute("class") ?? "")),
            Setter(v => _element.SetAttribute("class", v.ToJsString())),
            enumerable: true, configurable: true));

        DefineOwnProperty("classList", PropertyDescriptor.Accessor(
            Getter(() =>
            {
                var list = new JsObject();
                list.DefineOwnProperty("add", PropertyDescriptor.Data(
                    JsFunction.CreateNative("add", (_, args) =>
                    {
                        if (args.Length > 0)
                            DomMutationApi.AddClass(_element, args[0].ToJsString());
                        return Undefined;
                    }, 1)));
                list.DefineOwnProperty("remove", PropertyDescriptor.Data(
                    JsFunction.CreateNative("remove", (_, args) =>
                    {
                        if (args.Length > 0)
                            DomMutationApi.RemoveClass(_element, args[0].ToJsString());
                        return Undefined;
                    }, 1)));
                list.DefineOwnProperty("toggle", PropertyDescriptor.Data(
                    JsFunction.CreateNative("toggle", (_, args) =>
                    {
                        if (args.Length > 0)
                            DomMutationApi.ToggleClass(_element, args[0].ToJsString());
                        return Undefined;
                    }, 1)));
                list.DefineOwnProperty("contains", PropertyDescriptor.Data(
                    JsFunction.CreateNative("contains", (_, args) =>
                    {
                        if (args.Length > 0)
                            return _element.ClassList.Any(c =>
                                c.Equals(args[0].ToJsString(), StringComparison.OrdinalIgnoreCase))
                                ? True : False;
                        return False;
                    }, 1)));
                return list;
            }), null, enumerable: true, configurable: true));

        DefineOwnProperty("innerText", PropertyDescriptor.Accessor(
            Getter(() => new JsString(_element.InnerText)),
            Setter(v => _element.InnerText = v.ToJsString()),
            enumerable: true, configurable: true));

        DefineOwnProperty("innerHTML", PropertyDescriptor.Accessor(
            Getter(() => new JsString(ReconstructInnerHtml(_element))),
            Setter(v =>
            {
                // Clear existing children
                while (_element.Children.Count > 0)
                    _element.RemoveChild(_element.Children[0]);
                // Parse the fragment and append children
                var parser = new Core.Html.HtmlParser(v.ToJsString());
                var doc = parser.Parse();
                if (doc.Body is not null)
                {
                    foreach (var child in doc.Body.Children.ToList())
                        _element.AppendChild(child);
                }
            }),
            enumerable: true, configurable: true));

        DefineOwnProperty("children", PropertyDescriptor.Accessor(
            Getter(() =>
            {
                var elements = _element.Children.OfType<Element>().ToList();
                return new JsNodeListWrapper(elements.Cast<Node>().ToList(), Cache);
            }), null, enumerable: true, configurable: true));

        DefineOwnProperty("getAttribute", PropertyDescriptor.Data(
            JsFunction.CreateNative("getAttribute", (_, args) =>
            {
                if (args.Length > 0)
                {
                    var val = _element.GetAttribute(args[0].ToJsString());
                    return val is not null ? new JsString(val) : Null;
                }
                return Null;
            }, 1)));

        DefineOwnProperty("setAttribute", PropertyDescriptor.Data(
            JsFunction.CreateNative("setAttribute", (_, args) =>
            {
                if (args.Length >= 2)
                    _element.SetAttribute(args[0].ToJsString(), args[1].ToJsString());
                return Undefined;
            }, 2)));

        DefineOwnProperty("removeAttribute", PropertyDescriptor.Data(
            JsFunction.CreateNative("removeAttribute", (_, args) =>
            {
                if (args.Length > 0)
                    _element.RemoveAttribute(args[0].ToJsString());
                return Undefined;
            }, 1)));

        DefineOwnProperty("hasAttribute", PropertyDescriptor.Data(
            JsFunction.CreateNative("hasAttribute", (_, args) =>
            {
                if (args.Length > 0)
                    return _element.GetAttribute(args[0].ToJsString()) is not null ? True : False;
                return False;
            }, 1)));

        DefineOwnProperty("querySelector", PropertyDescriptor.Data(
            JsFunction.CreateNative("querySelector", (_, args) =>
            {
                if (args.Length > 0)
                {
                    var result = DomMutationApi.QuerySelector(_element, args[0].ToJsString());
                    return result is not null ? Cache.GetOrCreate(result) : Null;
                }
                return Null;
            }, 1)));

        DefineOwnProperty("querySelectorAll", PropertyDescriptor.Data(
            JsFunction.CreateNative("querySelectorAll", (_, args) =>
            {
                if (args.Length > 0)
                {
                    var results = DomMutationApi.QuerySelectorAll(_element, args[0].ToJsString()).ToList();
                    return new JsNodeListWrapper(results.Cast<Node>().ToList(), Cache);
                }
                return new JsNodeListWrapper([], Cache);
            }, 1)));

        DefineOwnProperty("style", PropertyDescriptor.Accessor(
            Getter(() => new JsCssStyleDeclaration(_element)),
            null, enumerable: true, configurable: true));
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
