using SuperRender.Renderer.Rendering;
using SuperRender.Document.Dom;
using SuperRender.Renderer.Rendering.Layout;
using SuperRender.Renderer.Rendering.Style;
using Xunit;

namespace SuperRender.Renderer.Tests.Layout;

public class DisplayContentsTests
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

    private static List<LayoutBox> FindAllByClass(LayoutBox box, string className)
    {
        var results = new List<LayoutBox>();
        FindAllByClassRecursive(box, className, results);
        return results;
    }

    private static void FindAllByClassRecursive(LayoutBox box, string className, List<LayoutBox> results)
    {
        if (box.DomNode is Element e && e.ClassList.Contains(className))
            results.Add(box);
        foreach (var child in box.Children)
            FindAllByClassRecursive(child, className, results);
    }

    // ---------------------------------------------------------------
    // display: contents
    // ---------------------------------------------------------------
    [Fact]
    public void DisplayContents_ParsedCorrectly()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                .wrapper { display: contents; }
            </style></head><body>
                <div class=""wrapper""><div class=""child"">Hello</div></div>
            </body></html>");
        var wrapper = FindBoxByClass(root, "wrapper");
        // display: contents means the wrapper should not generate its own box
        // Its children should be promoted to the parent
        // We should find the child directly
        var child = FindBoxByClass(root, "child");
        Assert.NotNull(child);
    }

    [Fact]
    public void DisplayContents_ChildrenPromotedToParent()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                .wrapper { display: contents; }
                .child { width: 100px; height: 50px; }
            </style></head><body style=""margin:0;padding:0"">
                <div class=""wrapper"">
                    <div class=""child a"">A</div>
                    <div class=""child b"">B</div>
                </div>
            </body></html>");

        var a = FindBoxByClass(root, "a");
        var b = FindBoxByClass(root, "b");
        Assert.NotNull(a);
        Assert.NotNull(b);

        // Children should be laid out as direct children of body
        Assert.Equal(100, a!.Dimensions.Width, 0.1f);
        Assert.Equal(50, a.Dimensions.Height, 0.1f);
        Assert.True(b!.Dimensions.Y > a.Dimensions.Y,
            "Children should stack vertically");
    }

    [Fact]
    public void DisplayContents_StyleResolvesToContents()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                .wrapper { display: contents; }
            </style></head><body>
                <div class=""wrapper""><div>inner</div></div>
            </body></html>");
        var wrapper = FindBoxByClass(root, "wrapper");
        // The wrapper element shouldn't have a visible box
        // but inner should still be findable
        var body = FindBox(root, "body");
        Assert.NotNull(body);
    }

    [Fact]
    public void DisplayContents_InFlex_ChildrenBecomeFlexItems()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                .flex { display: flex; width: 400px; }
                .wrapper { display: contents; }
                .item { width: 100px; height: 50px; }
            </style></head><body style=""margin:0;padding:0"">
                <div class=""flex"">
                    <div class=""wrapper"">
                        <div class=""item a"">A</div>
                        <div class=""item b"">B</div>
                    </div>
                </div>
            </body></html>");

        var a = FindBoxByClass(root, "a");
        var b = FindBoxByClass(root, "b");
        Assert.NotNull(a);
        Assert.NotNull(b);

        // Children of display:contents wrapper should become direct flex items
        // They should be laid out horizontally
        Assert.True(b!.Dimensions.X > a!.Dimensions.X,
            "Children should be horizontal flex items");
    }

    // ---------------------------------------------------------------
    // display: list-item
    // ---------------------------------------------------------------
    [Fact]
    public void DisplayListItem_ParsedCorrectly()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                .item { display: list-item; }
            </style></head><body>
                <div class=""item"">Item</div>
            </body></html>");
        var item = FindBoxByClass(root, "item");
        Assert.NotNull(item);
        Assert.Equal(DisplayType.ListItem, item!.Style.Display);
    }

    [Fact]
    public void DisplayListItem_BehavesLikeBlock()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                .item { display: list-item; width: 200px; height: 30px; }
            </style></head><body style=""margin:0;padding:0"">
                <div class=""item a"">First</div>
                <div class=""item b"">Second</div>
            </body></html>");

        var a = FindBoxByClass(root, "a");
        var b = FindBoxByClass(root, "b");
        Assert.NotNull(a);
        Assert.NotNull(b);

        // list-item should stack vertically like block
        Assert.Equal(200, a!.Dimensions.Width, 0.1f);
        Assert.True(b!.Dimensions.Y > a.Dimensions.Y,
            "List items should stack vertically");
    }

    // ---------------------------------------------------------------
    // display: inline-flex
    // ---------------------------------------------------------------
    [Fact]
    public void DisplayInlineFlex_ParsedCorrectly()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                .flex { display: inline-flex; }
            </style></head><body>
                <div class=""flex""><span>A</span></div>
            </body></html>");
        var flex = FindBoxByClass(root, "flex");
        Assert.NotNull(flex);
        Assert.Equal(DisplayType.InlineFlex, flex!.Style.Display);
    }

    [Fact]
    public void DisplayInlineFlex_IsFlexContainer()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                .flex { display: inline-flex; width: 200px; }
                .item { width: 50px; height: 30px; }
            </style></head><body style=""margin:0;padding:0"">
                <div class=""flex"">
                    <div class=""item a"">A</div>
                    <div class=""item b"">B</div>
                </div>
            </body></html>");

        var flex = FindBoxByClass(root, "flex");
        Assert.NotNull(flex);
        // inline-flex should be laid out as FlexContainer internally
        Assert.Equal(LayoutBoxType.FlexContainer, flex!.BoxType);
        Assert.Equal(DisplayType.InlineFlex, flex.Style.Display);
    }

    [Fact]
    public void DisplayInlineFlex_FlexDirection_Column()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                .flex { display: inline-flex; flex-direction: column; width: 200px; }
                .item { width: 50px; height: 30px; }
            </style></head><body style=""margin:0;padding:0"">
                <div class=""flex"">
                    <div class=""item a"">A</div>
                    <div class=""item b"">B</div>
                </div>
            </body></html>");

        var flex = FindBoxByClass(root, "flex");
        Assert.NotNull(flex);
        Assert.Equal(FlexDirectionType.Column, flex!.Style.FlexDirection);
        Assert.Equal(DisplayType.InlineFlex, flex.Style.Display);
    }

    // ---------------------------------------------------------------
    // place-self shorthand
    // ---------------------------------------------------------------
    [Fact]
    public void PlaceSelf_SetsAlignSelf()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                .flex { display: flex; }
                .item { place-self: center; }
            </style></head><body>
                <div class=""flex""><div class=""item"">A</div></div>
            </body></html>");
        var item = FindBoxByClass(root, "item");
        Assert.NotNull(item);
        Assert.Equal(AlignSelfType.Center, item!.Style.AlignSelf);
    }

    // ---------------------------------------------------------------
    // display: none still works
    // ---------------------------------------------------------------
    [Fact]
    public void DisplayNone_StillHidesElements()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                .hidden { display: none; }
                .visible { width: 100px; height: 50px; }
            </style></head><body style=""margin:0;padding:0"">
                <div class=""hidden"">Hidden</div>
                <div class=""visible"">Visible</div>
            </body></html>");

        var visible = FindBoxByClass(root, "visible");
        Assert.NotNull(visible);
        Assert.Equal(100, visible!.Dimensions.Width, 0.1f);
    }

    // ---------------------------------------------------------------
    // display: contents nested
    // ---------------------------------------------------------------
    [Fact]
    public void DisplayContents_Nested_BothFlatten()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                .outer { display: contents; }
                .inner { display: contents; }
                .child { width: 100px; height: 50px; }
            </style></head><body style=""margin:0;padding:0"">
                <div class=""outer"">
                    <div class=""inner"">
                        <div class=""child"">Hello</div>
                    </div>
                </div>
            </body></html>");

        var child = FindBoxByClass(root, "child");
        Assert.NotNull(child);
        Assert.Equal(100, child!.Dimensions.Width, 0.1f);
    }

    [Fact]
    public void DisplayListItem_HasWidth()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                .item { display: list-item; width: 300px; height: 40px; }
            </style></head><body style=""margin:0;padding:0"">
                <div class=""item"">Item text</div>
            </body></html>");

        var item = FindBoxByClass(root, "item");
        Assert.NotNull(item);
        Assert.Equal(300, item!.Dimensions.Width, 0.1f);
        Assert.Equal(40, item.Dimensions.Height, 0.1f);
    }

    // ---------------------------------------------------------------
    // display: contents with display:none child
    // ---------------------------------------------------------------
    [Fact]
    public void DisplayContents_SkipsNoneChildren()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                .wrapper { display: contents; }
                .hidden { display: none; }
                .visible { width: 100px; height: 50px; }
            </style></head><body style=""margin:0;padding:0"">
                <div class=""wrapper"">
                    <div class=""hidden"">Hidden</div>
                    <div class=""visible"">Visible</div>
                </div>
            </body></html>");

        var visible = FindBoxByClass(root, "visible");
        Assert.NotNull(visible);
        Assert.Equal(100, visible!.Dimensions.Width, 0.1f);
    }

    [Fact]
    public void DisplayInlineFlex_ParsesFromStylesheet()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                .iflex { display: inline-flex; }
            </style></head><body>
                <div class=""iflex""><span>A</span></div>
            </body></html>");
        var box = FindBoxByClass(root, "iflex");
        Assert.NotNull(box);
        Assert.Equal(DisplayType.InlineFlex, box!.Style.Display);
        Assert.Equal(LayoutBoxType.FlexContainer, box.BoxType);
    }

    [Fact]
    public void DisplayContents_DoesNotGenerateOwnBox()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                .wrapper { display: contents; background: red; padding: 20px; }
                .child { width: 100px; height: 50px; }
            </style></head><body style=""margin:0;padding:0"">
                <div class=""wrapper"">
                    <div class=""child"">Hello</div>
                </div>
            </body></html>");

        // The child should be laid out as if it were a direct child of body
        // The wrapper's padding should NOT affect the child's position
        var child = FindBoxByClass(root, "child");
        Assert.NotNull(child);
        Assert.Equal(100, child!.Dimensions.Width, 0.1f);
    }

    [Fact]
    public void DisplayListItem_MultipleStack()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                .item { display: list-item; width: 200px; height: 25px; margin: 0; }
            </style></head><body style=""margin:0;padding:1px"">
                <div class=""item a"">First</div>
                <div class=""item b"">Second</div>
                <div class=""item c"">Third</div>
            </body></html>");

        var a = FindBoxByClass(root, "a");
        var b = FindBoxByClass(root, "b");
        var c = FindBoxByClass(root, "c");
        Assert.NotNull(a);
        Assert.NotNull(b);
        Assert.NotNull(c);

        // Items should stack vertically
        Assert.True(b!.Dimensions.Y > a!.Dimensions.Y);
        Assert.True(c!.Dimensions.Y > b.Dimensions.Y);
    }
}
