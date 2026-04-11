using SuperRender.Document.Dom;

namespace SuperRender.Document.Html;

/// <summary>
/// Builds a DOM tree from raw HTML using <see cref="HtmlTokenizer"/>.
/// </summary>
public sealed class HtmlParser
{
    private readonly string _html;

    // Void elements never have children and are never pushed onto the open-elements stack.
    private static readonly HashSet<string> VoidElements = new(StringComparer.OrdinalIgnoreCase)
    {
        "area", "base", "br", "col", "embed", "hr", "img",
        "input", "link", "meta", "param", "source", "track", "wbr",
    };

    // Block-level elements (used to decide whether to skip whitespace-only text nodes).
    private static readonly HashSet<string> BlockElements = new(StringComparer.OrdinalIgnoreCase)
    {
        "address", "article", "aside", "blockquote", "body", "dd", "details",
        "dialog", "div", "dl", "dt", "fieldset", "figcaption", "figure",
        "footer", "form", "h1", "h2", "h3", "h4", "h5", "h6", "head",
        "header", "hgroup", "hr", "html", "li", "main", "nav", "ol",
        "p", "pre", "section", "summary", "table", "tbody", "td",
        "tfoot", "th", "thead", "tr", "ul",
    };

    public HtmlParser(string html)
    {
        _html = html ?? throw new ArgumentNullException(nameof(html));
    }

    public Dom.Document Parse()
    {
        var doc = new Dom.Document();
        var tokenizer = new HtmlTokenizer(_html);
        var openElements = new Stack<Element>();

        // The document itself acts as the implicit container until we know better.
        // We'll attach nodes directly to `doc` and fix up structure afterwards.
        foreach (var token in tokenizer.Tokenize())
        {
            switch (token.Type)
            {
                case HtmlTokenType.StartTag:
                    HandleStartTag(token, doc, openElements);
                    break;

                case HtmlTokenType.EndTag:
                    HandleEndTag(token, openElements);
                    break;

                case HtmlTokenType.Text:
                    HandleText(token, doc, openElements);
                    break;

                case HtmlTokenType.Comment:
                    // Comments are ignored in this implementation.
                    break;

                case HtmlTokenType.Doctype:
                    // Doctype is acknowledged but we don't need to store it.
                    break;

                case HtmlTokenType.EndOfFile:
                    break;
            }
        }

        EnsureStructure(doc);
        return doc;
    }

    // ── token handlers ───────────────────────────────────────────────────────

    private static void HandleStartTag(HtmlToken token, Dom.Document doc, Stack<Element> openElements)
    {
        var tagName = token.TagName!;

        // Build the attributes dictionary (case-insensitive).
        Dictionary<string, string>? attrs = null;
        if (token.Attributes.Count > 0)
        {
            attrs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in token.Attributes)
                attrs[kvp.Key] = kvp.Value;
        }

        var element = new Element(tagName, attrs) { OwnerDocument = doc };

        // Append to current parent.
        Node parent = openElements.Count > 0 ? openElements.Peek() : doc;
        parent.AppendChild(element);

        // Push onto stack unless void or self-closing.
        bool isVoid = VoidElements.Contains(tagName) || token.SelfClosing;
        if (!isVoid)
            openElements.Push(element);
    }

    private static void HandleEndTag(HtmlToken token, Stack<Element> openElements)
    {
        var tagName = token.TagName!;

        // Pop elements until we find the matching open tag.
        // This gracefully handles mis-nested and unclosed tags.
        var temp = new Stack<Element>();
        bool found = false;

        while (openElements.Count > 0)
        {
            var top = openElements.Peek();
            if (string.Equals(top.TagName, tagName, StringComparison.OrdinalIgnoreCase))
            {
                openElements.Pop(); // pop the matching element
                found = true;
                break;
            }

            // Pop and remember (they remain in the tree as children, just no
            // longer on the open-elements stack).
            temp.Push(openElements.Pop());
        }

        if (!found)
        {
            // No matching open element — push everything back (stray end tag, ignore it).
            while (temp.Count > 0)
                openElements.Push(temp.Pop());
        }
        // If found, the elements that were popped from between are implicitly closed.
        // We do NOT push them back — they stay as children in the tree already.
    }

    private static void HandleText(HtmlToken token, Dom.Document doc, Stack<Element> openElements)
    {
        var text = token.Text;
        if (string.IsNullOrEmpty(text))
            return;

        Node parent = openElements.Count > 0 ? openElements.Peek() : doc;

        // Skip whitespace-only text nodes for block-level parents.
        if (parent is Element parentEl && BlockElements.Contains(parentEl.TagName))
        {
            if (string.IsNullOrWhiteSpace(text))
                return;
        }

        var textNode = new TextNode(text) { OwnerDocument = doc };
        parent.AppendChild(textNode);
    }

    // ── auto-structuring ─────────────────────────────────────────────────────

    /// <summary>
    /// Ensures the document has an html &gt; head + body structure.
    /// If the parsed tree is missing these elements, they are created and the
    /// existing nodes are moved into the appropriate container.
    /// </summary>
    private static void EnsureStructure(Dom.Document doc)
    {
        // Check if an <html> element already exists at the document level.
        var htmlElement = doc.Children.OfType<Element>()
            .FirstOrDefault(e => e.TagName == "html");

        if (htmlElement != null)
        {
            // <html> exists — ensure it has <head> and <body>.
            EnsureHeadAndBody(doc, htmlElement);
            return;
        }

        // No <html> element — wrap everything.
        htmlElement = new Element("html") { OwnerDocument = doc };

        // Move all current children of doc into the new html element temporarily.
        var children = doc.Children.ToList();
        foreach (var child in children)
            doc.RemoveChild(child);

        doc.AppendChild(htmlElement);
        EnsureHeadAndBody(doc, htmlElement);

        // Distribute the original children into head or body.
        var head = htmlElement.Children.OfType<Element>().First(e => e.TagName == "head");
        var body = htmlElement.Children.OfType<Element>().First(e => e.TagName == "body");

        foreach (var child in children)
        {
            if (child is Element el)
            {
                if (IsHeadContent(el.TagName))
                    head.AppendChild(child);
                else
                    body.AppendChild(child);
            }
            else
            {
                // Text nodes go into body (skip whitespace-only at top level).
                if (child is TextNode tn && string.IsNullOrWhiteSpace(tn.Data))
                    continue;
                body.AppendChild(child);
            }
        }
    }

    private static void EnsureHeadAndBody(Dom.Document doc, Element htmlElement)
    {
        var head = htmlElement.Children.OfType<Element>()
            .FirstOrDefault(e => e.TagName == "head");
        var body = htmlElement.Children.OfType<Element>()
            .FirstOrDefault(e => e.TagName == "body");

        if (head == null)
        {
            head = new Element("head") { OwnerDocument = doc };
            // Insert head before body or as first child.
            if (body != null)
                htmlElement.InsertBefore(head, body);
            else
                htmlElement.AppendChild(head);
        }

        if (body == null)
        {
            body = new Element("body") { OwnerDocument = doc };
            htmlElement.AppendChild(body);
        }
    }

    /// <summary>
    /// Returns true if the tag typically belongs in &lt;head&gt;.
    /// </summary>
    private static bool IsHeadContent(string tagName)
    {
        return tagName switch
        {
            "title" or "meta" or "link" or "style" or "base" or "script" or "noscript" => true,
            _ => false,
        };
    }
}
