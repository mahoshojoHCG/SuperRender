using SuperRender.Renderer.Rendering;
using SuperRender.Renderer.Rendering.Layout;
using SuperRender.Renderer.Rendering.Style;
using SuperRender.Document.Dom;
using Xunit;

namespace SuperRender.Renderer.Tests.Layout;

public class GridLayoutTests
{
    private static LayoutBox LayoutHtml(string html, float viewportWidth = 800)
    {
        var pipeline = new RenderPipeline(new MonospaceTextMeasurer());
        var doc = pipeline.LoadHtml(html);
        var resolver = new StyleResolver(doc.Stylesheets);
        var styles = resolver.ResolveAll(doc);
        var engine = new LayoutEngine(new MonospaceTextMeasurer());
        return engine.BuildLayoutTree(doc, styles, viewportWidth, 600);
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

    [Fact]
    public void GridContainer_CreatesGridLayoutBox()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                .grid { display: grid; }
            </style></head><body>
                <div class=""grid""><div>A</div></div>
            </body></html>");

        var grid = FindBoxByClass(root, "grid");
        Assert.NotNull(grid);
        Assert.Equal(LayoutBoxType.GridContainer, grid.BoxType);
    }

    [Fact]
    public void Grid_TwoColumns_ChildrenPositioned()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                .grid { display: grid; grid-template-columns: 200px 200px; width: 400px; }
                .a { width: 200px; height: 50px; }
                .b { width: 200px; height: 50px; }
            </style></head><body>
                <div class=""grid"">
                    <div class=""a"">A</div>
                    <div class=""b"">B</div>
                </div>
            </body></html>");

        var a = FindBoxByClass(root, "a");
        var b = FindBoxByClass(root, "b");
        Assert.NotNull(a);
        Assert.NotNull(b);
        Assert.True(b.Dimensions.X > a.Dimensions.X);
    }

    [Fact]
    public void Grid_FrUnits_DistributeSpace()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                .grid { display: grid; grid-template-columns: 1fr 2fr; width: 300px; }
                .a { height: 50px; }
                .b { height: 50px; }
            </style></head><body>
                <div class=""grid"">
                    <div class=""a"">A</div>
                    <div class=""b"">B</div>
                </div>
            </body></html>");

        var a = FindBoxByClass(root, "a");
        var b = FindBoxByClass(root, "b");
        Assert.NotNull(a);
        Assert.NotNull(b);
        Assert.True(b.Dimensions.Width > a.Dimensions.Width);
    }

    [Fact]
    public void Grid_MultipleRows_ChildrenOnNextRow()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                .grid { display: grid; grid-template-columns: 100px 100px; width: 200px; }
                .item { height: 30px; }
            </style></head><body>
                <div class=""grid"">
                    <div class=""item a"">1</div>
                    <div class=""item b"">2</div>
                    <div class=""item c"">3</div>
                    <div class=""item d"">4</div>
                </div>
            </body></html>");

        var a = FindBoxByClass(root, "a");
        var c = FindBoxByClass(root, "c");
        Assert.NotNull(a);
        Assert.NotNull(c);
        Assert.True(c.Dimensions.Y > a.Dimensions.Y);
    }

    [Fact]
    public void ParseTrackList_PixelValues()
    {
        var tracks = GridLayout.ParseTrackList("100px 200px 300px");
        Assert.Equal(3, tracks.Count);
        Assert.Equal(100f, tracks[0].Value);
        Assert.Equal(200f, tracks[1].Value);
    }

    [Fact]
    public void ParseTrackList_FrValues()
    {
        var tracks = GridLayout.ParseTrackList("1fr 2fr");
        Assert.Equal(2, tracks.Count);
        Assert.True(tracks[0].IsFr);
        Assert.Equal(1f, tracks[0].Value);
    }

    [Fact]
    public void ParseTrackList_Repeat()
    {
        var tracks = GridLayout.ParseTrackList("repeat(3, 100px)");
        Assert.Equal(3, tracks.Count);
    }

    [Fact]
    public void ParseTrackList_None_Empty()
    {
        Assert.Empty(GridLayout.ParseTrackList("none"));
    }

    [Fact]
    public void ParseTrackList_Null_Empty()
    {
        Assert.Empty(GridLayout.ParseTrackList(null));
    }
}
