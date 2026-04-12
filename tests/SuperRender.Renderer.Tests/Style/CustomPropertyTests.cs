using DomDocument = SuperRender.Document.Dom.Document;
using SuperRender.Document.Css;
using SuperRender.Document.Dom;
using SuperRender.Renderer.Rendering.Style;
using Xunit;

namespace SuperRender.Renderer.Tests.Style;

public class CustomPropertyTests
{
    private static (ComputedStyle style, StyleResolver resolver, DomDocument doc) ResolveWithCustomProps(string css, string bodyHtml)
    {
        var doc = new DomDocument();
        var html = new Element("html");
        var head = new Element("head");
        var body = new Element("body");
        doc.AppendChild(html);
        html.AppendChild(head);
        html.AppendChild(body);

        if (!string.IsNullOrEmpty(css))
        {
            var stylesheet = new CssParser(css).Parse();
            doc.Stylesheets.Add(stylesheet);
        }

        var parser = new SuperRender.Document.Html.HtmlParser(bodyHtml);
        var parsedDoc = parser.Parse();
        if (parsedDoc.Body != null)
        {
            foreach (var child in parsedDoc.Body.Children.ToList())
            {
                child.Parent?.RemoveChild(child);
                body.AppendChild(child);
            }
        }

        var resolver = new StyleResolver(doc.Stylesheets);
        var styles = resolver.ResolveAll(doc);
        var target = body.Children.OfType<Element>().FirstOrDefault()!;
        return (styles[target], resolver, doc);
    }

    private static ComputedStyle ResolveStyle(string css, string bodyHtml)
        => ResolveWithCustomProps(css, bodyHtml).style;

    // ===== Basic var() substitution =====

    [Fact]
    public void Var_BasicColor_Resolved()
    {
        var style = ResolveStyle(
            ":root { --main-color: red; } div { color: var(--main-color); }",
            "<div>test</div>");
        Assert.Equal(Document.Color.FromRgb(255, 0, 0), style.Color);
    }

    [Fact]
    public void Var_BasicLength_Resolved()
    {
        var style = ResolveStyle(
            ":root { --spacing: 20px; } div { padding-top: var(--spacing); }",
            "<div>test</div>");
        Assert.Equal(20f, style.Padding.Top, 0.1f);
    }

    [Fact]
    public void Var_WithFallback_UsedWhenVarMissing()
    {
        var style = ResolveStyle(
            "div { color: var(--undefined, blue); }",
            "<div>test</div>");
        Assert.Equal(Document.Color.FromRgb(0, 0, 255), style.Color);
    }

    [Fact]
    public void Var_WithFallback_NotUsedWhenVarDefined()
    {
        var style = ResolveStyle(
            ":root { --color: green; } div { color: var(--color, blue); }",
            "<div>test</div>");
        Assert.Equal(Document.Color.FromRgb(0, 128, 0), style.Color);
    }

    // ===== Custom property inheritance =====

    [Fact]
    public void CustomProperty_InheritsFromParent()
    {
        var doc = new DomDocument();
        var html = new Element("html");
        var body = new Element("body");
        var parent = new Element("div");
        parent.SetAttribute("class", "parent");
        var child = new Element("span");
        child.SetAttribute("class", "child");
        doc.AppendChild(html);
        html.AppendChild(body);
        body.AppendChild(parent);
        parent.AppendChild(child);

        var stylesheet = new CssParser(
            ".parent { --text-color: red; } .child { color: var(--text-color); }").Parse();
        doc.Stylesheets.Add(stylesheet);

        var resolver = new StyleResolver(doc.Stylesheets);
        var styles = resolver.ResolveAll(doc);

        Assert.Equal(Document.Color.FromRgb(255, 0, 0), styles[child].Color);
    }

    // ===== Var referencing another var =====

    [Fact]
    public void Var_ReferencesAnotherVar()
    {
        var style = ResolveStyle(
            ":root { --base: 10px; --double: var(--base); } div { padding-top: var(--double); }",
            "<div>test</div>");
        Assert.Equal(10f, style.Padding.Top, 0.1f);
    }

    // ===== Cycle detection =====

    [Fact]
    public void Var_CyclicReference_UsesFallback()
    {
        // --a references --b, --b references --a → cycle
        var style = ResolveStyle(
            ":root { --a: var(--b); --b: var(--a); } div { color: var(--a, black); }",
            "<div>test</div>");
        // Should fall back to black (the fallback) rather than infinite loop
        Assert.Equal(Document.Color.Black, style.Color);
    }

    [Fact]
    public void Var_SelfReference_UsesFallback()
    {
        var style = ResolveStyle(
            ":root { --x: var(--x); } div { color: var(--x, red); }",
            "<div>test</div>");
        Assert.Equal(Document.Color.FromRgb(255, 0, 0), style.Color);
    }

    // ===== Custom property in inline style =====

    [Fact]
    public void Var_InInlineStyle_Resolved()
    {
        var doc = new DomDocument();
        var html = new Element("html");
        var body = new Element("body");
        var div = new Element("div");
        doc.AppendChild(html);
        html.AppendChild(body);
        body.AppendChild(div);

        var stylesheet = new CssParser(":root { --bg: blue; }").Parse();
        doc.Stylesheets.Add(stylesheet);
        div.SetAttribute("style", "background-color: var(--bg)");

        var resolver = new StyleResolver(doc.Stylesheets);
        var styles = resolver.ResolveAll(doc);

        Assert.Equal(Document.Color.FromRgb(0, 0, 255), styles[div].BackgroundColor);
    }

    // ===== CSS parsing of custom properties =====

    [Fact]
    public void CssParser_CustomProperty_Parsed()
    {
        var stylesheet = new CssParser(":root { --color: #ff0000; }").Parse();
        Assert.Single(stylesheet.Rules);
        var decl = stylesheet.Rules[0].Declarations.FirstOrDefault(d => d.Property == "--color");
        Assert.NotNull(decl);
        Assert.Equal("#ff0000", decl.Value.Raw);
    }

    [Fact]
    public void CssParser_VarFunction_Parsed()
    {
        var value = CssParser.ParseValueText("var(--color)");
        Assert.Equal("--color", value.VarName);
        Assert.Null(value.VarFallback);
    }

    [Fact]
    public void CssParser_VarFunctionWithFallback_Parsed()
    {
        var value = CssParser.ParseValueText("var(--color, red)");
        Assert.Equal("--color", value.VarName);
        Assert.Equal("red", value.VarFallback);
    }

    // ===== Custom property overriding =====

    [Fact]
    public void CustomProperty_Override_InChild()
    {
        var doc = new DomDocument();
        var html = new Element("html");
        var body = new Element("body");
        var parent = new Element("div");
        parent.SetAttribute("class", "parent");
        var child = new Element("div");
        child.SetAttribute("class", "child");
        doc.AppendChild(html);
        html.AppendChild(body);
        body.AppendChild(parent);
        parent.AppendChild(child);

        var stylesheet = new CssParser(
            ".parent { --color: red; } .child { --color: blue; color: var(--color); }").Parse();
        doc.Stylesheets.Add(stylesheet);

        var resolver = new StyleResolver(doc.Stylesheets);
        var styles = resolver.ResolveAll(doc);

        // Child overrides --color to blue
        Assert.Equal(Document.Color.FromRgb(0, 0, 255), styles[child].Color);
    }

    // ===== Multiple properties using same variable =====

    [Fact]
    public void Var_UsedInMultipleProperties()
    {
        var style = ResolveStyle(
            ":root { --size: 10px; } div { padding-top: var(--size); margin-top: var(--size); }",
            "<div>test</div>");
        Assert.Equal(10f, style.Padding.Top, 0.1f);
        Assert.Equal(10f, style.Margin.Top, 0.1f);
    }
}
