using Xunit;

namespace SuperRender.Browser.Tests;

public class ScrollStateTests
{
    [Fact]
    public void Initial_ScrollY_IsZero()
    {
        var state = new ScrollState();
        Assert.Equal(0f, state.ScrollY);
    }

    [Fact]
    public void Initial_CanScroll_IsFalse()
    {
        var state = new ScrollState();
        Assert.False(state.CanScroll);
    }

    [Fact]
    public void Update_SetsContentAndViewportHeight()
    {
        var state = new ScrollState();
        state.Update(1000f, 600f);

        Assert.Equal(1000f, state.ContentHeight);
        Assert.Equal(600f, state.ViewportHeight);
    }

    [Fact]
    public void Update_ComputesMaxScrollY()
    {
        var state = new ScrollState();
        state.Update(1000f, 600f);

        Assert.Equal(400f, state.MaxScrollY);
    }

    [Fact]
    public void ContentFitsViewport_CanScroll_IsFalse()
    {
        var state = new ScrollState();
        state.Update(400f, 600f);

        Assert.False(state.CanScroll);
        Assert.Equal(0f, state.MaxScrollY);
    }

    [Fact]
    public void ContentExceedsViewport_CanScroll_IsTrue()
    {
        var state = new ScrollState();
        state.Update(1000f, 600f);

        Assert.True(state.CanScroll);
    }

    [Fact]
    public void ScrollBy_Positive_IncreasesScrollY()
    {
        var state = new ScrollState();
        state.Update(1000f, 600f);
        state.ScrollBy(100f);

        Assert.Equal(100f, state.ScrollY);
    }

    [Fact]
    public void ScrollBy_Negative_DecreasesScrollY()
    {
        var state = new ScrollState();
        state.Update(1000f, 600f);
        state.ScrollBy(200f);
        state.ScrollBy(-50f);

        Assert.Equal(150f, state.ScrollY);
    }

    [Fact]
    public void ScrollBy_ClampedToZero()
    {
        var state = new ScrollState();
        state.Update(1000f, 600f);
        state.ScrollBy(-100f);

        Assert.Equal(0f, state.ScrollY);
    }

    [Fact]
    public void ScrollBy_ClampedToMaxScrollY()
    {
        var state = new ScrollState();
        state.Update(1000f, 600f);
        state.ScrollBy(999f);

        Assert.Equal(400f, state.ScrollY);
    }

    [Fact]
    public void ScrollToTop_ResetsToZero()
    {
        var state = new ScrollState();
        state.Update(1000f, 600f);
        state.ScrollBy(200f);
        state.ScrollToTop();

        Assert.Equal(0f, state.ScrollY);
    }

    [Fact]
    public void ScrollToBottom_GoesToMaxScrollY()
    {
        var state = new ScrollState();
        state.Update(1000f, 600f);
        state.ScrollToBottom();

        Assert.Equal(400f, state.ScrollY);
    }

    [Fact]
    public void PageUp_ScrollsByNinetyPercentOfViewport()
    {
        var state = new ScrollState();
        state.Update(2000f, 600f);
        state.ScrollToBottom();
        float before = state.ScrollY;

        state.PageUp();

        Assert.Equal(before - 600f * 0.9f, state.ScrollY, 0.1f);
    }

    [Fact]
    public void PageDown_ScrollsByNinetyPercentOfViewport()
    {
        var state = new ScrollState();
        state.Update(2000f, 600f);

        state.PageDown();

        Assert.Equal(600f * 0.9f, state.ScrollY, 0.1f);
    }

    [Fact]
    public void GetScrollBarGeometry_ContentFits_ReturnsNull()
    {
        var state = new ScrollState();
        state.Update(400f, 600f);

        Assert.Null(state.GetScrollBarGeometry(68f));
    }

    [Fact]
    public void GetScrollBarGeometry_Scrollable_ReturnsValidTuple()
    {
        var state = new ScrollState();
        state.Update(1200f, 600f);

        var result = state.GetScrollBarGeometry(68f);
        Assert.NotNull(result);

        var (trackY, trackHeight, thumbY, thumbHeight) = result!.Value;
        Assert.Equal(68f, trackY);
        Assert.Equal(600f, trackHeight);
        Assert.True(thumbHeight > 0);
        Assert.True(thumbY >= trackY);
    }

    [Fact]
    public void ScrollStep_IsForty()
    {
        Assert.Equal(40f, ScrollState.ScrollStep);
    }

    [Fact]
    public void BarWidth_IsEight()
    {
        Assert.Equal(8f, ScrollState.BarWidth);
    }
}
