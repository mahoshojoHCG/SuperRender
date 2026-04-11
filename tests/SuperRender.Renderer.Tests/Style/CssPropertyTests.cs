using DomDocument = SuperRender.Document.Dom.Document;
using SuperRender.Document.Css;
using SuperRender.Document.Dom;
using SuperRender.Renderer.Rendering.Layout;
using SuperRender.Renderer.Rendering.Style;
using Xunit;

namespace SuperRender.Renderer.Tests.Style;

public class CssPropertyTests
{
    private static ComputedStyle ResolveStyle(string css, string bodyHtml)
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
        var resolver = new StyleResolver(doc.Stylesheets);
        return resolver.Resolve(target);
    }

    [Fact]
    public void BoxSizing_ContentBox_Parsing()
    {
        var style = ResolveStyle("div { box-sizing: content-box; }", "<div>test</div>");
        Assert.Equal(BoxSizingType.ContentBox, style.BoxSizing);
    }

    [Fact]
    public void BoxSizing_BorderBox_Parsing()
    {
        var style = ResolveStyle("div { box-sizing: border-box; }", "<div>test</div>");
        Assert.Equal(BoxSizingType.BorderBox, style.BoxSizing);
    }

    [Fact]
    public void MinWidth_Parsing()
    {
        var style = ResolveStyle("div { min-width: 100px; }", "<div>test</div>");
        Assert.Equal(100f, style.MinWidth, 0.1f);
    }

    [Fact]
    public void MaxWidth_Parsing()
    {
        var style = ResolveStyle("div { max-width: 500px; }", "<div>test</div>");
        Assert.Equal(500f, style.MaxWidth, 0.1f);
    }

    [Fact]
    public void MaxWidth_None_Parsing()
    {
        var style = ResolveStyle("div { max-width: none; }", "<div>test</div>");
        Assert.True(float.IsPositiveInfinity(style.MaxWidth));
    }

    [Fact]
    public void MinHeight_Parsing()
    {
        var style = ResolveStyle("div { min-height: 50px; }", "<div>test</div>");
        Assert.Equal(50f, style.MinHeight, 0.1f);
    }

    [Fact]
    public void MaxHeight_Parsing()
    {
        var style = ResolveStyle("div { max-height: 400px; }", "<div>test</div>");
        Assert.Equal(400f, style.MaxHeight, 0.1f);
    }

    [Fact]
    public void Overflow_Hidden_Parsing()
    {
        var style = ResolveStyle("div { overflow: hidden; }", "<div>test</div>");
        Assert.Equal(OverflowType.Hidden, style.Overflow);
    }

    [Fact]
    public void Overflow_Visible_Parsing()
    {
        var style = ResolveStyle("div { overflow: visible; }", "<div>test</div>");
        Assert.Equal(OverflowType.Visible, style.Overflow);
    }

    [Fact]
    public void Overflow_Scroll_Parsing()
    {
        var style = ResolveStyle("div { overflow: scroll; }", "<div>test</div>");
        Assert.Equal(OverflowType.Scroll, style.Overflow);
    }

    [Fact]
    public void Overflow_Auto_Parsing()
    {
        var style = ResolveStyle("div { overflow: auto; }", "<div>test</div>");
        Assert.Equal(OverflowType.Auto, style.Overflow);
    }

    [Fact]
    public void TextOverflow_Ellipsis_Parsing()
    {
        var style = ResolveStyle("div { text-overflow: ellipsis; }", "<div>test</div>");
        Assert.Equal(TextOverflowType.Ellipsis, style.TextOverflow);
    }

    [Fact]
    public void TextOverflow_Clip_Parsing()
    {
        var style = ResolveStyle("div { text-overflow: clip; }", "<div>test</div>");
        Assert.Equal(TextOverflowType.Clip, style.TextOverflow);
    }

    [Fact]
    public void WhiteSpace_Pre_Parsing()
    {
        var style = ResolveStyle("div { white-space: pre; }", "<div>test</div>");
        Assert.Equal(WhiteSpaceType.Pre, style.WhiteSpace);
    }

    [Fact]
    public void WhiteSpace_Nowrap_Parsing()
    {
        var style = ResolveStyle("div { white-space: nowrap; }", "<div>test</div>");
        Assert.Equal(WhiteSpaceType.Nowrap, style.WhiteSpace);
    }

    [Fact]
    public void WhiteSpace_PreWrap_Parsing()
    {
        var style = ResolveStyle("div { white-space: pre-wrap; }", "<div>test</div>");
        Assert.Equal(WhiteSpaceType.PreWrap, style.WhiteSpace);
    }

    [Fact]
    public void WhiteSpace_Inherits()
    {
        var doc = new DomDocument();
        var html = new Element("html");
        var head = new Element("head");
        var body = new Element("body");
        doc.AppendChild(html);
        html.AppendChild(head);
        html.AppendChild(body);

        var stylesheet = new CssParser("div { white-space: pre; }").Parse();
        doc.Stylesheets.Add(stylesheet);

        var parser = new SuperRender.Document.Html.HtmlParser("<div><span>child</span></div>");
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

        var span = body.Children.OfType<Element>().First()
            .Children.OfType<Element>().First();
        Assert.Equal(WhiteSpaceType.Pre, styles[span].WhiteSpace);
    }

    [Fact]
    public void ZIndex_Parsing()
    {
        var style = ResolveStyle("div { z-index: 5; }", "<div>test</div>");
        Assert.Equal(5, style.ZIndex);
        Assert.False(style.ZIndexIsAuto);
    }

    [Fact]
    public void ZIndex_Auto_Parsing()
    {
        var style = ResolveStyle("div { z-index: auto; }", "<div>test</div>");
        Assert.True(style.ZIndexIsAuto);
    }

    [Fact]
    public void Display_InlineBlock_Parsing()
    {
        var style = ResolveStyle("div { display: inline-block; }", "<div>test</div>");
        Assert.Equal(DisplayType.InlineBlock, style.Display);
    }

    [Fact]
    public void Display_FlowRoot_Parsing()
    {
        var style = ResolveStyle("div { display: flow-root; }", "<div>test</div>");
        Assert.Equal(DisplayType.FlowRoot, style.Display);
    }

    [Fact]
    public void HiddenAttribute_SetsDisplayNone()
    {
        var doc = new DomDocument();
        var html = new Element("html");
        var head = new Element("head");
        var body = new Element("body");
        doc.AppendChild(html);
        html.AppendChild(head);
        html.AppendChild(body);

        var div = new Element("div");
        div.SetAttribute("hidden", "");
        body.AppendChild(div);

        var resolver = new StyleResolver(doc.Stylesheets);
        var style = resolver.Resolve(div);
        Assert.Equal(DisplayType.None, style.Display);
    }
}
