using SuperRender.Document.Css;
using Xunit;

namespace SuperRender.Document.Tests.Css;

public class MediaQueryTests
{
    [Fact]
    public void All_AlwaysMatches()
    {
        var mq = new MediaQuery("all");
        Assert.True(mq.Evaluate(800, 600));
    }

    [Fact]
    public void Screen_Matches()
    {
        var mq = new MediaQuery("screen");
        Assert.True(mq.Evaluate(800, 600));
    }

    [Fact]
    public void Print_DoesNotMatch()
    {
        var mq = new MediaQuery("print");
        Assert.False(mq.Evaluate(800, 600));
    }

    [Fact]
    public void EmptyQuery_Matches()
    {
        var mq = new MediaQuery("");
        Assert.True(mq.Evaluate(800, 600));
    }

    [Fact]
    public void MinWidth_AboveThreshold_Matches()
    {
        var mq = new MediaQuery("(min-width: 600px)");
        Assert.True(mq.Evaluate(800, 600));
    }

    [Fact]
    public void MinWidth_BelowThreshold_DoesNotMatch()
    {
        var mq = new MediaQuery("(min-width: 1024px)");
        Assert.False(mq.Evaluate(800, 600));
    }

    [Fact]
    public void MinWidth_ExactThreshold_Matches()
    {
        var mq = new MediaQuery("(min-width: 800px)");
        Assert.True(mq.Evaluate(800, 600));
    }

    [Fact]
    public void MaxWidth_BelowThreshold_Matches()
    {
        var mq = new MediaQuery("(max-width: 1024px)");
        Assert.True(mq.Evaluate(800, 600));
    }

    [Fact]
    public void MaxWidth_AboveThreshold_DoesNotMatch()
    {
        var mq = new MediaQuery("(max-width: 600px)");
        Assert.False(mq.Evaluate(800, 600));
    }

    [Fact]
    public void MinHeight_AboveThreshold_Matches()
    {
        var mq = new MediaQuery("(min-height: 400px)");
        Assert.True(mq.Evaluate(800, 600));
    }

    [Fact]
    public void MinHeight_BelowThreshold_DoesNotMatch()
    {
        var mq = new MediaQuery("(min-height: 800px)");
        Assert.False(mq.Evaluate(800, 600));
    }

    [Fact]
    public void MaxHeight_BelowThreshold_Matches()
    {
        var mq = new MediaQuery("(max-height: 768px)");
        Assert.True(mq.Evaluate(800, 600));
    }

    [Fact]
    public void MaxHeight_AboveThreshold_DoesNotMatch()
    {
        var mq = new MediaQuery("(max-height: 400px)");
        Assert.False(mq.Evaluate(800, 600));
    }

    [Fact]
    public void ScreenAndMinWidth_BothMatch()
    {
        var mq = new MediaQuery("screen and (min-width: 600px)");
        Assert.True(mq.Evaluate(800, 600));
    }

    [Fact]
    public void ScreenAndMinWidth_WidthTooSmall()
    {
        var mq = new MediaQuery("screen and (min-width: 1024px)");
        Assert.False(mq.Evaluate(800, 600));
    }

    [Fact]
    public void Not_Print_Matches()
    {
        var mq = new MediaQuery("not print");
        Assert.True(mq.Evaluate(800, 600));
    }

    [Fact]
    public void Not_Screen_DoesNotMatch()
    {
        var mq = new MediaQuery("not screen");
        Assert.False(mq.Evaluate(800, 600));
    }

    [Fact]
    public void Only_Screen_Matches()
    {
        var mq = new MediaQuery("only screen");
        Assert.True(mq.Evaluate(800, 600));
    }

    [Fact]
    public void CommaList_AnyMatches_ReturnsTrue()
    {
        var mq = new MediaQuery("print, screen");
        Assert.True(mq.Evaluate(800, 600));
    }

    [Fact]
    public void CommaList_NoneMatch_ReturnsFalse()
    {
        var mq = new MediaQuery("print, speech");
        Assert.False(mq.Evaluate(800, 600));
    }

    [Fact]
    public void MultipleConditions_AllMustMatch()
    {
        var mq = new MediaQuery("screen and (min-width: 600px) and (max-width: 1200px)");
        Assert.True(mq.Evaluate(800, 600));
        Assert.False(mq.Evaluate(1400, 600));
    }

    [Fact]
    public void Orientation_Portrait_Matches()
    {
        var mq = new MediaQuery("(orientation: portrait)");
        Assert.True(mq.Evaluate(600, 800));
        Assert.False(mq.Evaluate(800, 600));
    }

    [Fact]
    public void Orientation_Landscape_Matches()
    {
        var mq = new MediaQuery("(orientation: landscape)");
        Assert.True(mq.Evaluate(800, 600));
        Assert.False(mq.Evaluate(600, 800));
    }

    [Fact]
    public void EmUnit_ConvertedTo16px()
    {
        // 50em = 800px
        var mq = new MediaQuery("(min-width: 50em)");
        Assert.True(mq.Evaluate(800, 600));
        Assert.False(mq.Evaluate(799, 600));
    }

    [Fact]
    public void BooleanContext_Color_Matches()
    {
        var mq = new MediaQuery("(color)");
        Assert.True(mq.Evaluate(800, 600));
    }

    [Fact]
    public void PrefersColorScheme_Light_Matches()
    {
        var mq = new MediaQuery("(prefers-color-scheme: light)");
        Assert.True(mq.Evaluate(800, 600));
    }
}
