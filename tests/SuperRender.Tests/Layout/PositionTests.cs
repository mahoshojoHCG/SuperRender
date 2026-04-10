using SuperRender.Core;
using SuperRender.Core.Dom;
using SuperRender.Core.Layout;
using SuperRender.Core.Style;
using Xunit;

namespace SuperRender.Tests.Layout;

public class PositionTests
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
    public void Relative_OffsetsFromNormalPosition()
    {
        // First get normal-flow position
        var rootNormal = LayoutHtml(@"<html><head><style>
            .rel { width: 100px; height: 50px; }
        </style></head><body><div class=""rel"">test</div></body></html>");
        var normalDiv = FindBoxByClass(rootNormal, "rel");
        float normalX = normalDiv!.Dimensions.X;
        float normalY = normalDiv.Dimensions.Y;

        var root = LayoutHtml(@"<html><head><style>
            .rel { position: relative; top: 10px; left: 20px; width: 100px; height: 50px; }
        </style></head><body><div class=""rel"">test</div></body></html>");

        var div = FindBoxByClass(root, "rel");
        Assert.NotNull(div);
        Assert.Equal(normalX + 20, div!.Dimensions.X, 0.1f);
        Assert.Equal(normalY + 10, div.Dimensions.Y, 0.1f);
    }

    [Fact]
    public void Relative_BottomRight_OffsetsNegatively()
    {
        // Get normal position first
        var rootNormal = LayoutHtml(@"<html><head><style>
            .box { width: 100px; height: 50px; }
        </style></head><body><div class=""box"">test</div></body></html>");
        var normalDiv = FindBoxByClass(rootNormal, "box");
        float normalX = normalDiv!.Dimensions.X;
        float normalY = normalDiv.Dimensions.Y;

        var rootRel = LayoutHtml(@"<html><head><style>
            .box { position: relative; right: 10px; bottom: 5px; width: 100px; height: 50px; }
        </style></head><body><div class=""box"">test</div></body></html>");
        var relDiv = FindBoxByClass(rootRel, "box");
        Assert.NotNull(relDiv);
        Assert.Equal(normalX - 10, relDiv!.Dimensions.X, 0.1f);
        Assert.Equal(normalY - 5, relDiv.Dimensions.Y, 0.1f);
    }

    [Fact]
    public void Absolute_PositionedRelativeToContainer()
    {
        var root = LayoutHtml(@"<html><head><style>
            .container { position: relative; width: 400px; height: 300px; }
            .abs { position: absolute; top: 10px; left: 20px; width: 100px; height: 50px; }
        </style></head><body>
            <div class=""container"">
                <div class=""abs"">positioned</div>
            </div>
        </body></html>");

        var abs = FindBoxByClass(root, "abs");
        Assert.NotNull(abs);
        Assert.Equal(100, abs!.Dimensions.Width, 0.1f);
        Assert.Equal(50, abs.Dimensions.Height, 0.1f);
    }

    [Fact]
    public void Absolute_DoesNotAffectNormalFlow()
    {
        var root = LayoutHtml(@"<html><head><style>
            .abs { position: absolute; top: 0; left: 0; width: 100px; height: 100px; }
            .normal { width: 200px; height: 50px; }
        </style></head><body>
            <div class=""abs"">abs</div>
            <div class=""normal"">normal</div>
        </body></html>");

        var normal = FindBoxByClass(root, "normal");
        Assert.NotNull(normal);
        // Normal-flow element should start at top (after body margin), not pushed down by absolute
        Assert.Equal(200, normal!.Dimensions.Width, 0.1f);
    }

    [Fact]
    public void ZIndex_Parsed()
    {
        var html = @"<html><head><style>.z { z-index: 5; }</style></head>
            <body><div class=""z"">test</div></body></html>";
        var pipeline = new RenderPipeline(new MonospaceTextMeasurer());
        var doc = pipeline.LoadHtml(html);
        var resolver = new StyleResolver(doc.Stylesheets);
        var styles = resolver.ResolveAll(doc);

        var div = doc.Body!.Children.OfType<Element>().First();
        Assert.Equal(5, styles[div].ZIndex);
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
