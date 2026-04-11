using DomDocument = SuperRender.Document.Dom.Document;
using SuperRender.Document.Dom;
using SuperRender.EcmaScript.Runtime;

namespace SuperRender.EcmaScript.Dom;

/// <summary>
/// JS wrapper for a DOM Document. Exposes the standard document API.
/// </summary>
internal sealed class JsDocumentWrapper : JsElementWrapper
{
    private readonly DomDocument _document;

    public JsDocumentWrapper(DomDocument document, NodeWrapperCache cache, Realm realm)
        : base(document.DocumentElement ?? CreateDummyElement(document), cache, realm)
    {
        _document = document;
        InstallDocumentProperties();
    }

    private static Element CreateDummyElement(DomDocument doc) => doc.CreateElement("html");

    /// <summary>
    /// Install document.cookie accessor. Called from DomBridge when cookies are configured.
    /// </summary>
    public void InstallCookie(Func<string> getCookies, Action<string> setCookie)
    {
        DefineOwnProperty("cookie", PropertyDescriptor.Accessor(
            Getter(() => new JsString(getCookies())),
            Setter(v => setCookie(v.ToJsString())),
            enumerable: true, configurable: true));
    }

    private void InstallDocumentProperties()
    {
        DefineOwnProperty("nodeType", PropertyDescriptor.Accessor(
            Getter(() => JsNumber.Create(9)),
            null, enumerable: true, configurable: true));

        DefineOwnProperty("nodeName", PropertyDescriptor.Accessor(
            Getter(() => new JsString("#document")),
            null, enumerable: true, configurable: true));

        DefineOwnProperty("documentElement", PropertyDescriptor.Accessor(
            Getter(() => Cache.WrapNullable(_document.DocumentElement)),
            null, enumerable: true, configurable: true));

        DefineOwnProperty("body", PropertyDescriptor.Accessor(
            Getter(() => Cache.WrapNullable(_document.Body)),
            null, enumerable: true, configurable: true));

        DefineOwnProperty("head", PropertyDescriptor.Accessor(
            Getter(() => Cache.WrapNullable(_document.Head)),
            null, enumerable: true, configurable: true));

        DefineOwnProperty("title", PropertyDescriptor.Accessor(
            Getter(() =>
            {
                var titleElement = FindElement(_document, "title");
                return new JsString(titleElement?.InnerText ?? "");
            }),
            Setter(v =>
            {
                var titleElement = FindElement(_document, "title");
                if (titleElement is not null)
                    titleElement.InnerText = v.ToJsString();
            }),
            enumerable: true, configurable: true));

        DefineOwnProperty("createElement", PropertyDescriptor.Data(
            JsFunction.CreateNative("createElement", (_, args) =>
            {
                if (args.Length > 0)
                {
                    var el = _document.CreateElement(args[0].ToJsString());
                    return Cache.GetOrCreate(el);
                }
                return Undefined;
            }, 1)));

        DefineOwnProperty("createTextNode", PropertyDescriptor.Data(
            JsFunction.CreateNative("createTextNode", (_, args) =>
            {
                var data = args.Length > 0 ? args[0].ToJsString() : "";
                var textNode = _document.CreateTextNode(data);
                return Cache.GetOrCreate(textNode);
            }, 1)));

        DefineOwnProperty("getElementById", PropertyDescriptor.Data(
            JsFunction.CreateNative("getElementById", (_, args) =>
            {
                if (args.Length > 0)
                {
                    var id = args[0].ToJsString();
                    var element = FindElementById(_document, id);
                    return element is not null ? Cache.GetOrCreate(element) : Null;
                }
                return Null;
            }, 1)));

        DefineOwnProperty("getElementsByTagName", PropertyDescriptor.Data(
            JsFunction.CreateNative("getElementsByTagName", (_, args) =>
            {
                if (args.Length > 0)
                {
                    var tagName = args[0].ToJsString().ToLowerInvariant();
                    var results = FindElementsByTagName(_document, tagName);
                    return new JsNodeListWrapper(results.Cast<Node>().ToList(), Cache);
                }
                return new JsNodeListWrapper([], Cache);
            }, 1)));

        DefineOwnProperty("getElementsByClassName", PropertyDescriptor.Data(
            JsFunction.CreateNative("getElementsByClassName", (_, args) =>
            {
                if (args.Length > 0)
                {
                    var className = args[0].ToJsString();
                    var results = FindElementsByClassName(_document, className);
                    return new JsNodeListWrapper(results.Cast<Node>().ToList(), Cache);
                }
                return new JsNodeListWrapper([], Cache);
            }, 1)));

        DefineOwnProperty("querySelector", PropertyDescriptor.Data(
            JsFunction.CreateNative("querySelector", (_, args) =>
            {
                if (args.Length > 0)
                {
                    var result = DomMutationApi.QuerySelector(_document, args[0].ToJsString());
                    return result is not null ? Cache.GetOrCreate(result) : Null;
                }
                return Null;
            }, 1)));

        DefineOwnProperty("querySelectorAll", PropertyDescriptor.Data(
            JsFunction.CreateNative("querySelectorAll", (_, args) =>
            {
                if (args.Length > 0)
                {
                    var results = DomMutationApi.QuerySelectorAll(_document, args[0].ToJsString()).ToList();
                    return new JsNodeListWrapper(results.Cast<Node>().ToList(), Cache);
                }
                return new JsNodeListWrapper([], Cache);
            }, 1)));
    }

    private static Element? FindElement(Node root, string tagName)
    {
        if (root is Element el && el.TagName.Equals(tagName, StringComparison.OrdinalIgnoreCase))
            return el;
        foreach (var child in root.Children)
        {
            var found = FindElement(child, tagName);
            if (found is not null) return found;
        }
        return null;
    }

    private static Element? FindElementById(Node root, string id)
    {
        if (root is Element el && id.Equals(el.Id, StringComparison.OrdinalIgnoreCase))
            return el;
        foreach (var child in root.Children)
        {
            var found = FindElementById(child, id);
            if (found is not null) return found;
        }
        return null;
    }

    private static List<Element> FindElementsByTagName(Node root, string tagName)
    {
        var results = new List<Element>();
        CollectByTagName(root, tagName, results);
        return results;
    }

    private static void CollectByTagName(Node node, string tagName, List<Element> results)
    {
        if (node is Element el && (tagName == "*" || el.TagName.Equals(tagName, StringComparison.OrdinalIgnoreCase)))
            results.Add(el);
        foreach (var child in node.Children)
            CollectByTagName(child, tagName, results);
    }

    private static List<Element> FindElementsByClassName(Node root, string className)
    {
        var results = new List<Element>();
        CollectByClassName(root, className, results);
        return results;
    }

    private static void CollectByClassName(Node node, string className, List<Element> results)
    {
        if (node is Element el && el.ClassList.Any(c => c.Equals(className, StringComparison.OrdinalIgnoreCase)))
            results.Add(el);
        foreach (var child in node.Children)
            CollectByClassName(child, className, results);
    }
}
