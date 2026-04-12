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
        this.DefineGetterSetter("cookie",
            () => new JsString(getCookies()),
            v => setCookie(v.ToJsString()));
    }

    private void InstallDocumentProperties()
    {
        this.DefineGetter("nodeType", () => JsNumber.Create(9));
        this.DefineGetter("nodeName", () => new JsString("#document"));
        this.DefineGetter("documentElement", () => Cache.WrapNullable(_document.DocumentElement));
        this.DefineGetter("body", () => Cache.WrapNullable(_document.Body));
        this.DefineGetter("head", () => Cache.WrapNullable(_document.Head));

        this.DefineGetterSetter("title",
            () =>
            {
                var titleElement = FindElement(_document, HtmlTagNames.Title);
                return new JsString(titleElement?.InnerText ?? "");
            },
            v =>
            {
                var titleElement = FindElement(_document, HtmlTagNames.Title);
                if (titleElement is not null)
                    titleElement.InnerText = v.ToJsString();
            });

        this.DefineMethod("createElement", 1, args =>
        {
            if (args.Length > 0)
            {
                var el = _document.CreateElement(args[0].ToJsString());
                return Cache.GetOrCreate(el);
            }
            return Undefined;
        });

        this.DefineMethod("createTextNode", 1, args =>
        {
            var data = args.Length > 0 ? args[0].ToJsString() : "";
            var textNode = _document.CreateTextNode(data);
            return Cache.GetOrCreate(textNode);
        });

        this.DefineMethod("getElementById", 1, args =>
        {
            if (args.Length > 0)
            {
                var id = args[0].ToJsString();
                var element = FindElementById(_document, id);
                return element is not null ? Cache.GetOrCreate(element) : Null;
            }
            return Null;
        });

        this.DefineMethod("getElementsByTagName", 1, args =>
        {
            if (args.Length > 0)
            {
                var tagName = args[0].ToJsString().ToLowerInvariant();
                var results = FindElementsByTagName(_document, tagName);
                return new JsNodeListWrapper(results.Cast<Node>().ToList(), Cache);
            }
            return new JsNodeListWrapper([], Cache);
        });

        this.DefineMethod("getElementsByClassName", 1, args =>
        {
            if (args.Length > 0)
            {
                var className = args[0].ToJsString();
                var results = FindElementsByClassName(_document, className);
                return new JsNodeListWrapper(results.Cast<Node>().ToList(), Cache);
            }
            return new JsNodeListWrapper([], Cache);
        });

        this.DefineMethod("querySelector", 1, args =>
        {
            if (args.Length > 0)
            {
                var result = DomMutationApi.QuerySelector(_document, args[0].ToJsString());
                return result is not null ? Cache.GetOrCreate(result) : Null;
            }
            return Null;
        });

        this.DefineMethod("querySelectorAll", 1, args =>
        {
            if (args.Length > 0)
            {
                var results = DomMutationApi.QuerySelectorAll(_document, args[0].ToJsString()).ToList();
                return new JsNodeListWrapper(results.Cast<Node>().ToList(), Cache);
            }
            return new JsNodeListWrapper([], Cache);
        });
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
