using DomDocument = SuperRender.Document.Dom.Document;
using SuperRender.Document.Dom;
using SuperRender.EcmaScript.Runtime;

namespace SuperRender.EcmaScript.Dom;

/// <summary>
/// JS wrapper for a DOM Document. Exposes the standard document API.
/// </summary>
[JsObject]
internal sealed partial class JsDocumentWrapper : JsElementWrapper
{
    private readonly DomDocument _document;

    public JsDocumentWrapper(DomDocument document, NodeWrapperCache cache, Realm realm)
        : base(document.DocumentElement ?? CreateDummyElement(document), cache, realm)
    {
        _document = document;
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

    [JsProperty("nodeType")] public override double NodeType => 9;
    [JsProperty("nodeName")] public override string NodeName => "#document";

#pragma warning disable JSGEN006 // returns wrapped DOM node — needs IJsNode/IJsElement IJsType
    [JsProperty("documentElement")] public JsValue DocumentElement => Cache.WrapNullable(_document.DocumentElement);
    [JsProperty("body")] public JsValue Body => Cache.WrapNullable(_document.Body);
    [JsProperty("head")] public JsValue Head => Cache.WrapNullable(_document.Head);
#pragma warning restore JSGEN006

    [JsProperty("title")]
    public string Title
    {
        get
        {
            var titleElement = FindElement(_document, HtmlTagNames.Title);
            return titleElement?.InnerText ?? "";
        }
        set
        {
            var titleElement = FindElement(_document, HtmlTagNames.Title);
            if (titleElement is not null)
                titleElement.InnerText = value;
        }
    }

#pragma warning disable JSGEN006 // returns wrapped DOM node — needs IJsNode/IJsElement IJsType
    [JsMethod("createElement")]
    public JsValue CreateElement(string tagName)
    {
        var el = _document.CreateElement(tagName);
        return Cache.GetOrCreate(el);
    }

    [JsMethod("createTextNode")]
    public JsValue CreateTextNode(string data)
    {
        var textNode = _document.CreateTextNode(data);
        return Cache.GetOrCreate(textNode);
    }

    [JsMethod("getElementById")]
    public JsValue GetElementById(string id)
    {
        var element = FindElementById(_document, id);
        return element is not null ? Cache.GetOrCreate(element) : JsValue.Null;
    }
#pragma warning restore JSGEN006

#pragma warning disable JSGEN006 // returns wrapped node list — needs IJsNodeList IJsType
    [JsMethod("getElementsByTagName")]
    public JsValue GetElementsByTagName(string tagName)
    {
        var results = FindElementsByTagName(_document, tagName.ToLowerInvariant()).Cast<Node>().ToList();
        return new JsNodeListWrapper(results, Cache);
    }

    [JsMethod("getElementsByClassName")]
    public JsValue GetElementsByClassName(string className)
    {
        var results = FindElementsByClassName(_document, className).Cast<Node>().ToList();
        return new JsNodeListWrapper(results, Cache);
    }
#pragma warning restore JSGEN006

#pragma warning disable JSGEN006 // returns wrapped DOM node — needs IJsNode/IJsElement IJsType
    [JsMethod("querySelector")]
    public new JsValue QuerySelector(string selector)
    {
        var result = DomMutationApi.QuerySelector(_document, selector);
        return result is not null ? Cache.GetOrCreate(result) : JsValue.Null;
    }
#pragma warning restore JSGEN006

#pragma warning disable JSGEN006 // returns wrapped node list — needs IJsNodeList IJsType
    [JsMethod("querySelectorAll")]
    public new JsValue QuerySelectorAll(string selector)
    {
        var results = DomMutationApi.QuerySelectorAll(_document, selector).Cast<Node>().ToList();
        return new JsNodeListWrapper(results, Cache);
    }
#pragma warning restore JSGEN006

    private static Element? FindFirst(Node root, Func<Element, bool> predicate)
    {
        if (root is Element el && predicate(el))
            return el;
        foreach (var child in root.Children)
        {
            var found = FindFirst(child, predicate);
            if (found is not null) return found;
        }
        return null;
    }

    private static List<Element> FindAll(Node root, Func<Element, bool> predicate)
    {
        var results = new List<Element>();
        CollectAll(root, predicate, results);
        return results;
    }

    private static void CollectAll(Node node, Func<Element, bool> predicate, List<Element> results)
    {
        if (node is Element el && predicate(el))
            results.Add(el);
        foreach (var child in node.Children)
            CollectAll(child, predicate, results);
    }

    private static Element? FindElement(Node root, string tagName)
        => FindFirst(root, el => el.TagName.Equals(tagName, StringComparison.OrdinalIgnoreCase));

    private static Element? FindElementById(Node root, string id)
        => FindFirst(root, el => id.Equals(el.Id, StringComparison.OrdinalIgnoreCase));

    private static List<Element> FindElementsByTagName(Node root, string tagName)
        => FindAll(root, el => tagName == "*" || el.TagName.Equals(tagName, StringComparison.OrdinalIgnoreCase));

    private static List<Element> FindElementsByClassName(Node root, string className)
        => FindAll(root, el => el.ClassList.Any(c => c.Equals(className, StringComparison.OrdinalIgnoreCase)));
}
