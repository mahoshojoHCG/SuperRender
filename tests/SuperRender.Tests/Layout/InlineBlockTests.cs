using SuperRender.Core;
using SuperRender.Core.Dom;
using SuperRender.Core.Layout;
using SuperRender.Core.Style;
using Xunit;

namespace SuperRender.Tests.Layout;

public class InlineBlockTests
{
    private static LayoutBox LayoutHtml(string html, float viewportWidth = 800)
    {
        var measurer = new MonospaceTextMeasurer();
        var pipeline = new RenderPipeline(measurer);
        var doc = pipeline.LoadHtml(html);
        var resolver = new StyleResolver(doc.Stylesheets);
        var styles = resolver.ResolveAll(doc);
        var engine = new LayoutEngine(measurer);
        return engine.BuildLayoutTree(doc, styles, viewportWidth, 600);
    }

    [Fact]
    public void InlineBlock_ParsedCorrectly()
    {
        var html = @"<html><head><style>.ib { display: inline-block; }</style></head>
            <body><div class=""ib"">test</div></body></html>";
        var pipeline = new RenderPipeline(new MonospaceTextMeasurer());
        var doc = pipeline.LoadHtml(html);
        var resolver = new StyleResolver(doc.Stylesheets);
        var styles = resolver.ResolveAll(doc);

        var div = doc.Body!.Children.OfType<Element>().First();
        Assert.Equal(DisplayType.InlineBlock, styles[div].Display);
    }

    [Fact]
    public void FlowRoot_ParsedCorrectly()
    {
        var html = @"<html><head><style>.fr { display: flow-root; }</style></head>
            <body><div class=""fr"">test</div></body></html>";
        var pipeline = new RenderPipeline(new MonospaceTextMeasurer());
        var doc = pipeline.LoadHtml(html);
        var resolver = new StyleResolver(doc.Stylesheets);
        var styles = resolver.ResolveAll(doc);

        var div = doc.Body!.Children.OfType<Element>().First();
        Assert.Equal(DisplayType.FlowRoot, styles[div].Display);
    }

    [Fact]
    public void FlowRoot_LaysOutAsBlock()
    {
        var root = LayoutHtml(@"<html><head><style>
            .fr { display: flow-root; width: 300px; }
        </style></head><body><div class=""fr"">test</div></body></html>");

        var div = FindBoxByClass(root, "fr");
        Assert.NotNull(div);
        Assert.Equal(300, div!.Dimensions.Width, 0.1f);
    }

    [Fact]
    public void InlineBlock_WithExplicitWidth_RespectsWidth()
    {
        var root = LayoutHtml(@"<html><head><style>
            span { display: inline-block; width: 100px; height: 50px; }
        </style></head><body><div><span>A</span></div></body></html>");

        var span = FindBox(root, "span");
        Assert.NotNull(span);
        Assert.Equal(100, span!.Dimensions.Width, 0.1f);
        Assert.Equal(50, span!.Dimensions.Height, 0.1f);
    }

    private static LayoutBox? FindBox(LayoutBox box, string tagName)
    {
        if (box.DomNode is Element e && e.TagName == tagName)
            return box;
        foreach (var child in box.Children)
        {
            var found = FindBox(child, tagName);
            if (found != null) return found;
        }
        return null;
    }

    private static LayoutBox? FindBoxByClass(LayoutBox box, string className)
    {
        if (box.DomNode is Element e && e.ClassList.Contains(className))
            return box;
        foreach (var child in box.Children)
        {
            var found = FindBoxByClass(child, className);
            if (found != null) return found;
        }
        return null;
    }
}
