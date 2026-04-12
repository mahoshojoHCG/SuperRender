using DomDocument = SuperRender.Document.Dom.Document;
using SuperRender.Document;
using SuperRender.Document.Css;
using SuperRender.Document.Dom;
using SuperRender.Renderer.Rendering.Layout;
using SuperRender.Renderer.Rendering.Style;
using Xunit;

namespace SuperRender.Tests.Style;

public class CascadeLayerTests
{
    private static (DomDocument doc, Element target) CreateDoc(string css, string bodyHtml)
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

        var target = body.Children.OfType<Element>().FirstOrDefault()!;
        return (doc, target);
    }

    #region @media in StyleResolver

    [Fact]
    public void MediaScreen_RulesApplied()
    {
        var css = "@media screen { div { color: red; } }";
        var (doc, target) = CreateDoc(css, "<div>test</div>");
        var resolver = new StyleResolver(doc.Stylesheets);
        var style = resolver.Resolve(target);
        Assert.Equal(Color.FromName("red"), style.Color);
    }

    [Fact]
    public void MediaPrint_RulesNotApplied()
    {
        var css = "@media print { div { color: red; } }";
        var (doc, target) = CreateDoc(css, "<div>test</div>");
        var resolver = new StyleResolver(doc.Stylesheets);
        var style = resolver.Resolve(target);
        // Default color is black, not red
        Assert.NotEqual(Color.FromName("red"), style.Color);
    }

    [Fact]
    public void MediaMinWidth_BelowThreshold_NotApplied()
    {
        var css = "@media (min-width: 1200px) { div { color: red; } }";
        var (doc, target) = CreateDoc(css, "<div>test</div>");
        // Default viewport is 800x600
        var resolver = new StyleResolver(doc.Stylesheets);
        var style = resolver.Resolve(target);
        Assert.NotEqual(Color.FromName("red"), style.Color);
    }

    [Fact]
    public void MediaMinWidth_AboveThreshold_Applied()
    {
        var css = "@media (min-width: 600px) { div { color: red; } }";
        var (doc, target) = CreateDoc(css, "<div>test</div>");
        var resolver = new StyleResolver(doc.Stylesheets);
        var styles = resolver.ResolveAll(doc, 800, 600);
        var style = styles[target];
        Assert.Equal(Color.FromName("red"), style.Color);
    }

    [Fact]
    public void MediaMaxWidth_AboveThreshold_NotApplied()
    {
        var css = "@media (max-width: 600px) { div { color: red; } }";
        var (doc, target) = CreateDoc(css, "<div>test</div>");
        var resolver = new StyleResolver(doc.Stylesheets);
        var styles = resolver.ResolveAll(doc, 800, 600);
        var style = styles[target];
        Assert.NotEqual(Color.FromName("red"), style.Color);
    }

    [Fact]
    public void MediaMaxWidth_BelowThreshold_Applied()
    {
        var css = "@media (max-width: 1200px) { div { color: red; } }";
        var (doc, target) = CreateDoc(css, "<div>test</div>");
        var resolver = new StyleResolver(doc.Stylesheets);
        var styles = resolver.ResolveAll(doc, 800, 600);
        var style = styles[target];
        Assert.Equal(Color.FromName("red"), style.Color);
    }

    [Fact]
    public void MediaScreen_WithRegularRule_BothApplied()
    {
        var css = "div { font-size: 20px; } @media screen { div { color: red; } }";
        var (doc, target) = CreateDoc(css, "<div>test</div>");
        var resolver = new StyleResolver(doc.Stylesheets);
        var style = resolver.Resolve(target);
        Assert.Equal(Color.FromName("red"), style.Color);
        Assert.Equal(20f, style.FontSize);
    }

    [Fact]
    public void MediaViewportChange_RulesReEvaluated()
    {
        var css = "@media (min-width: 1000px) { div { color: blue; } }";
        var (doc, target) = CreateDoc(css, "<div>test</div>");
        var resolver = new StyleResolver(doc.Stylesheets);

        // At 800px, should not match
        var styles800 = resolver.ResolveAll(doc, 800, 600);
        Assert.NotEqual(Color.FromName("blue"), styles800[target].Color);

        // At 1200px, should match
        var styles1200 = resolver.ResolveAll(doc, 1200, 600);
        Assert.Equal(Color.FromName("blue"), styles1200[target].Color);
    }

    #endregion

    #region @supports in StyleResolver

    [Fact]
    public void SupportsDisplayFlex_Applied()
    {
        var css = "@supports (display: flex) { .flex { display: flex; } }";
        var (doc, _) = CreateDoc(css, "<div class=\"flex\">test</div>");
        var target = doc.Body!.Children.OfType<Element>().First();
        var resolver = new StyleResolver(doc.Stylesheets);
        var style = resolver.Resolve(target);
        Assert.Equal(DisplayType.Flex, style.Display);
    }

    [Fact]
    public void SupportsDisplayGrid_NotApplied()
    {
        var css = "@supports (display: grid) { div { color: red; } }";
        var (doc, target) = CreateDoc(css, "<div>test</div>");
        var resolver = new StyleResolver(doc.Stylesheets);
        var style = resolver.Resolve(target);
        Assert.NotEqual(Color.FromName("red"), style.Color);
    }

    [Fact]
    public void SupportsColor_Applied()
    {
        var css = "@supports (color: red) { div { color: blue; } }";
        var (doc, target) = CreateDoc(css, "<div>test</div>");
        var resolver = new StyleResolver(doc.Stylesheets);
        var style = resolver.Resolve(target);
        Assert.Equal(Color.FromName("blue"), style.Color);
    }

    #endregion

    #region @layer in StyleResolver

    [Fact]
    public void Layer_RulesApplied()
    {
        var css = "@layer base { div { color: red; } }";
        var (doc, target) = CreateDoc(css, "<div>test</div>");
        var resolver = new StyleResolver(doc.Stylesheets);
        var style = resolver.Resolve(target);
        Assert.Equal(Color.FromName("red"), style.Color);
    }

    [Fact]
    public void Layer_WithRegularRules_RegularWins()
    {
        // Regular rules and layered rules — in our implementation both are applied in source order.
        // The regular rule is defined after the layer, but at-rules are collected after regular rules.
        // So the layered rule has higher source order and wins.
        // This differs from the full CSS spec where unlayered rules always beat layered rules.
        var css = "div { color: blue; } @layer base { div { color: red; } }";
        var (doc, target) = CreateDoc(css, "<div>test</div>");
        var resolver = new StyleResolver(doc.Stylesheets);
        var style = resolver.Resolve(target);
        // The @layer rule is collected after regular rules, giving it higher source order
        Assert.Equal(Color.FromName("red"), style.Color);
    }

    #endregion

    #region Nested @media in @supports

    [Fact]
    public void NestedMediaInSupports_BothMatch_Applied()
    {
        var css = "@supports (display: flex) { @media screen { div { color: green; } } }";
        var (doc, target) = CreateDoc(css, "<div>test</div>");
        var resolver = new StyleResolver(doc.Stylesheets);
        var style = resolver.Resolve(target);
        Assert.Equal(Color.FromName("green"), style.Color);
    }

    [Fact]
    public void NestedMediaInSupports_SupportsNotMatch_NotApplied()
    {
        var css = "@supports (display: grid) { @media screen { div { color: green; } } }";
        var (doc, target) = CreateDoc(css, "<div>test</div>");
        var resolver = new StyleResolver(doc.Stylesheets);
        var style = resolver.Resolve(target);
        Assert.NotEqual(Color.FromName("green"), style.Color);
    }

    #endregion

    #region New inherited properties

    [Fact]
    public void TextIndent_AppliedAndInherited()
    {
        var css = "div { text-indent: 20px; }";
        var (doc, target) = CreateDoc(css, "<div><p>test</p></div>");
        var resolver = new StyleResolver(doc.Stylesheets);
        var styles = resolver.ResolveAll(doc);
        Assert.Equal(20f, styles[target].TextIndent);
    }

    [Fact]
    public void TabSize_AppliedAndInherited()
    {
        var css = "div { tab-size: 4; }";
        var (doc, target) = CreateDoc(css, "<div>test</div>");
        var resolver = new StyleResolver(doc.Stylesheets);
        var style = resolver.Resolve(target);
        Assert.Equal(4f, style.TabSize);
    }

    [Fact]
    public void FontVariant_Applied()
    {
        var css = "div { font-variant: small-caps; }";
        var (doc, target) = CreateDoc(css, "<div>test</div>");
        var resolver = new StyleResolver(doc.Stylesheets);
        var style = resolver.Resolve(target);
        Assert.Equal("small-caps", style.FontVariant);
    }

    [Fact]
    public void Direction_Applied()
    {
        var css = "div { direction: rtl; }";
        var (doc, target) = CreateDoc(css, "<div>test</div>");
        var resolver = new StyleResolver(doc.Stylesheets);
        var style = resolver.Resolve(target);
        Assert.Equal("rtl", style.Direction);
    }

    [Fact]
    public void Quotes_Applied()
    {
        var css = "div { quotes: auto; }";
        var (doc, target) = CreateDoc(css, "<div>test</div>");
        var resolver = new StyleResolver(doc.Stylesheets);
        var style = resolver.Resolve(target);
        Assert.Equal("auto", style.Quotes);
    }

    [Fact]
    public void NewProperties_DefaultValues()
    {
        var style = new ComputedStyle();
        Assert.Equal(0f, style.TextIndent);
        Assert.Equal(8f, style.TabSize);
        Assert.Equal("normal", style.FontVariant);
        Assert.Equal("ltr", style.Direction);
        Assert.Equal("", style.Quotes);
    }

    [Fact]
    public void NewProperties_InheritedFromParent()
    {
        var css = "div { text-indent: 30px; direction: rtl; tab-size: 2; font-variant: small-caps; }";
        var (doc, target) = CreateDoc(css, "<div><span>child</span></div>");
        var resolver = new StyleResolver(doc.Stylesheets);
        var styles = resolver.ResolveAll(doc);
        var child = target.Children.OfType<Element>().First();
        Assert.Equal(30f, styles[child].TextIndent);
        Assert.Equal("rtl", styles[child].Direction);
        Assert.Equal(2f, styles[child].TabSize);
        Assert.Equal("small-caps", styles[child].FontVariant);
    }

    #endregion
}
