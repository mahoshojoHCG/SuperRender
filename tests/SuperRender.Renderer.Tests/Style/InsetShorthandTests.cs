using DomDocument = SuperRender.Document.Dom.Document;
using SuperRender.Document.Css;
using SuperRender.Document.Dom;
using SuperRender.Renderer.Rendering.Style;
using Xunit;

namespace SuperRender.Renderer.Tests.Style;

public class InsetShorthandTests
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
    public void Inset_SingleValue_AllSidesEqual()
    {
        var style = ResolveStyle("div { position: absolute; inset: 10px; }", "<div>test</div>");
        Assert.Equal(10f, style.Top, 0.1f);
        Assert.Equal(10f, style.Right, 0.1f);
        Assert.Equal(10f, style.Bottom, 0.1f);
        Assert.Equal(10f, style.Left, 0.1f);
    }

    [Fact]
    public void Inset_TwoValues_TopBottomAndLeftRight()
    {
        var style = ResolveStyle("div { position: absolute; inset: 10px 20px; }", "<div>test</div>");
        Assert.Equal(10f, style.Top, 0.1f);
        Assert.Equal(20f, style.Right, 0.1f);
        Assert.Equal(10f, style.Bottom, 0.1f);
        Assert.Equal(20f, style.Left, 0.1f);
    }

    [Fact]
    public void Inset_ThreeValues_TopLeftRightBottom()
    {
        var style = ResolveStyle("div { position: absolute; inset: 10px 20px 30px; }", "<div>test</div>");
        Assert.Equal(10f, style.Top, 0.1f);
        Assert.Equal(20f, style.Right, 0.1f);
        Assert.Equal(30f, style.Bottom, 0.1f);
        Assert.Equal(20f, style.Left, 0.1f);
    }

    [Fact]
    public void Inset_FourValues_AllSidesIndividual()
    {
        var style = ResolveStyle("div { position: absolute; inset: 10px 20px 30px 40px; }", "<div>test</div>");
        Assert.Equal(10f, style.Top, 0.1f);
        Assert.Equal(20f, style.Right, 0.1f);
        Assert.Equal(30f, style.Bottom, 0.1f);
        Assert.Equal(40f, style.Left, 0.1f);
    }

    [Fact]
    public void Inset_Zero_AllSidesZero()
    {
        var style = ResolveStyle("div { position: absolute; inset: 0; }", "<div>test</div>");
        Assert.Equal(0f, style.Top, 0.1f);
        Assert.Equal(0f, style.Right, 0.1f);
        Assert.Equal(0f, style.Bottom, 0.1f);
        Assert.Equal(0f, style.Left, 0.1f);
    }

    [Fact]
    public void InsetBlockStart_SetsTop()
    {
        var style = ResolveStyle("div { position: absolute; inset-block-start: 15px; }", "<div>test</div>");
        Assert.Equal(15f, style.Top, 0.1f);
    }

    [Fact]
    public void InsetBlockEnd_SetsBottom()
    {
        var style = ResolveStyle("div { position: absolute; inset-block-end: 25px; }", "<div>test</div>");
        Assert.Equal(25f, style.Bottom, 0.1f);
    }

    [Fact]
    public void InsetInlineStart_SetsLeft()
    {
        var style = ResolveStyle("div { position: absolute; inset-inline-start: 35px; }", "<div>test</div>");
        Assert.Equal(35f, style.Left, 0.1f);
    }

    [Fact]
    public void InsetInlineEnd_SetsRight()
    {
        var style = ResolveStyle("div { position: absolute; inset-inline-end: 45px; }", "<div>test</div>");
        Assert.Equal(45f, style.Right, 0.1f);
    }

    [Fact]
    public void InsetBlock_SingleValue_SetsBothTopAndBottom()
    {
        var style = ResolveStyle("div { position: absolute; inset-block: 20px; }", "<div>test</div>");
        Assert.Equal(20f, style.Top, 0.1f);
        Assert.Equal(20f, style.Bottom, 0.1f);
    }

    [Fact]
    public void InsetBlock_TwoValues_SetsStartAndEnd()
    {
        var style = ResolveStyle("div { position: absolute; inset-block: 10px 30px; }", "<div>test</div>");
        Assert.Equal(10f, style.Top, 0.1f);
        Assert.Equal(30f, style.Bottom, 0.1f);
    }

    [Fact]
    public void InsetInline_SingleValue_SetsBothLeftAndRight()
    {
        var style = ResolveStyle("div { position: absolute; inset-inline: 15px; }", "<div>test</div>");
        Assert.Equal(15f, style.Left, 0.1f);
        Assert.Equal(15f, style.Right, 0.1f);
    }

    [Fact]
    public void InsetInline_TwoValues_SetsStartAndEnd()
    {
        var style = ResolveStyle("div { position: absolute; inset-inline: 5px 25px; }", "<div>test</div>");
        Assert.Equal(5f, style.Left, 0.1f);
        Assert.Equal(25f, style.Right, 0.1f);
    }

    [Fact]
    public void Inset_OverridesIndividualProperties()
    {
        var style = ResolveStyle("div { position: absolute; top: 99px; inset: 5px; }", "<div>test</div>");
        // inset comes after top, so it should override
        Assert.Equal(5f, style.Top, 0.1f);
    }

    [Fact]
    public void Overflow_Clip_Parsed()
    {
        var style = ResolveStyle("div { overflow: clip; }", "<div>test</div>");
        Assert.Equal(OverflowType.Clip, style.Overflow);
        Assert.Equal(OverflowType.Clip, style.OverflowX);
        Assert.Equal(OverflowType.Clip, style.OverflowY);
    }

    [Fact]
    public void OverflowX_Hidden_Parsed()
    {
        var style = ResolveStyle("div { overflow-x: hidden; }", "<div>test</div>");
        Assert.Equal(OverflowType.Hidden, style.OverflowX);
    }

    [Fact]
    public void OverflowY_Scroll_Parsed()
    {
        var style = ResolveStyle("div { overflow-y: scroll; }", "<div>test</div>");
        Assert.Equal(OverflowType.Scroll, style.OverflowY);
    }

    [Fact]
    public void Overflow_SetsAllAxes()
    {
        var style = ResolveStyle("div { overflow: hidden; }", "<div>test</div>");
        Assert.Equal(OverflowType.Hidden, style.Overflow);
        Assert.Equal(OverflowType.Hidden, style.OverflowX);
        Assert.Equal(OverflowType.Hidden, style.OverflowY);
    }

    [Fact]
    public void OverflowXY_Independent()
    {
        var style = ResolveStyle("div { overflow-x: scroll; overflow-y: hidden; }", "<div>test</div>");
        Assert.Equal(OverflowType.Scroll, style.OverflowX);
        Assert.Equal(OverflowType.Hidden, style.OverflowY);
    }

    [Fact]
    public void OverflowX_Clip_Parsed()
    {
        var style = ResolveStyle("div { overflow-x: clip; }", "<div>test</div>");
        Assert.Equal(OverflowType.Clip, style.OverflowX);
    }

    [Fact]
    public void Position_Fixed_Parsed()
    {
        var style = ResolveStyle("div { position: fixed; }", "<div>test</div>");
        Assert.Equal(PositionType.Fixed, style.Position);
    }

    [Fact]
    public void Position_Sticky_Parsed()
    {
        var style = ResolveStyle("div { position: sticky; }", "<div>test</div>");
        Assert.Equal(PositionType.Sticky, style.Position);
    }

    [Fact]
    public void OverflowType_Default_Visible()
    {
        var style = new ComputedStyle();
        Assert.Equal(OverflowType.Visible, style.OverflowX);
        Assert.Equal(OverflowType.Visible, style.OverflowY);
    }
}
