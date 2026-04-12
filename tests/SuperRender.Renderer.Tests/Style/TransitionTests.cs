using SuperRender.Renderer.Rendering.Style;
using Xunit;

namespace SuperRender.Renderer.Tests.Style;

public class TransitionTests
{
    [Fact]
    public void TimingFunction_Linear_ReturnsInputValue()
    {
        var tf = TimingFunction.Linear;
        Assert.Equal(0.5f, tf.Evaluate(0.5f), 0.01f);
    }

    [Fact]
    public void TimingFunction_Ease_NotLinear()
    {
        var tf = TimingFunction.Ease;
        float v25 = tf.Evaluate(0.25f);
        Assert.NotEqual(0.25f, v25); // should differ from linear
    }

    [Fact]
    public void TimingFunction_Steps_3End()
    {
        var tf = TimingFunction.CreateSteps(3, "end");
        Assert.Equal(0f, tf.Evaluate(0));
        Assert.Equal(1f / 3f, tf.Evaluate(0.4f), 0.01f);
    }

    [Fact]
    public void TimingFunction_Parse_Linear()
    {
        var tf = TimingFunction.Parse("linear");
        Assert.Equal(0.5f, tf.Evaluate(0.5f), 0.01f);
    }

    [Fact]
    public void TimingFunction_Parse_CubicBezier()
    {
        var tf = TimingFunction.Parse("cubic-bezier(0.25, 0.1, 0.25, 1)");
        Assert.Equal(TimingFunctionType.CubicBezier, tf.Type);
    }

    [Fact]
    public void TimingFunction_Parse_Steps()
    {
        var tf = TimingFunction.Parse("steps(4, start)");
        Assert.Equal(TimingFunctionType.Steps, tf.Type);
        Assert.Equal(4, tf.Steps);
    }

    [Fact]
    public void TransitionEngine_StartTransition_HasActive()
    {
        var engine = new TransitionEngine();
        engine.StartTransition("elem1", "opacity", 0, 1, 1.0f, 0, TimingFunction.Linear);
        Assert.True(engine.HasActiveTransitions);
    }

    [Fact]
    public void TransitionEngine_GetValue_AtStart()
    {
        var engine = new TransitionEngine();
        engine.StartTransition("elem1", "opacity", 0, 1, 1.0f, 0, TimingFunction.Linear);
        var value = engine.GetTransitionValue("elem1", "opacity");
        Assert.NotNull(value);
        Assert.Equal(0f, value.Value, 0.01f);
    }

    [Fact]
    public void TransitionEngine_GetValue_Midway()
    {
        var engine = new TransitionEngine();
        engine.StartTransition("elem1", "opacity", 0, 1, 1.0f, 0, TimingFunction.Linear);
        engine.Update(0.5f);
        var value = engine.GetTransitionValue("elem1", "opacity");
        Assert.NotNull(value);
        Assert.Equal(0.5f, value.Value, 0.01f);
    }

    [Fact]
    public void TransitionEngine_Completed_Removed()
    {
        var engine = new TransitionEngine();
        engine.StartTransition("elem1", "opacity", 0, 1, 0.5f, 0, TimingFunction.Linear);
        engine.Update(0.6f);
        Assert.False(engine.HasActiveTransitions);
    }

    [Fact]
    public void TransitionEngine_NoElement_ReturnsNull()
    {
        var engine = new TransitionEngine();
        Assert.Null(engine.GetTransitionValue("missing", "opacity"));
    }

    [Fact]
    public void StyleResolver_TransitionProperty_Parsed()
    {
        var style = StyleTestHelper.ResolveFirst("div { transition-property: opacity; }", "<div>test</div>");
        Assert.Equal("opacity", style.TransitionProperty);
    }

    [Fact]
    public void StyleResolver_TransitionDuration_Seconds()
    {
        var style = StyleTestHelper.ResolveFirst("div { transition-duration: 0.5s; }", "<div>test</div>");
        Assert.Equal(0.5f, style.TransitionDuration, 0.01f);
    }

    [Fact]
    public void StyleResolver_TransitionDuration_Milliseconds()
    {
        var style = StyleTestHelper.ResolveFirst("div { transition-duration: 300ms; }", "<div>test</div>");
        Assert.Equal(0.3f, style.TransitionDuration, 0.01f);
    }

    [Fact]
    public void StyleResolver_AnimationName_Parsed()
    {
        var style = StyleTestHelper.ResolveFirst("div { animation-name: fadeIn; }", "<div>test</div>");
        Assert.Equal("fadeIn", style.AnimationName);
    }

    [Fact]
    public void StyleResolver_AnimationDuration_Parsed()
    {
        var style = StyleTestHelper.ResolveFirst("div { animation-duration: 2s; }", "<div>test</div>");
        Assert.Equal(2f, style.AnimationDuration, 0.01f);
    }

    [Fact]
    public void StyleResolver_AnimationIterationCount_Infinite()
    {
        var style = StyleTestHelper.ResolveFirst("div { animation-iteration-count: infinite; }", "<div>test</div>");
        Assert.True(float.IsPositiveInfinity(style.AnimationIterationCount));
    }

    [Fact]
    public void StyleResolver_AnimationDirection_Alternate()
    {
        var style = StyleTestHelper.ResolveFirst("div { animation-direction: alternate; }", "<div>test</div>");
        Assert.Equal("alternate", style.AnimationDirection);
    }

    [Fact]
    public void StyleResolver_AnimationFillMode_Forwards()
    {
        var style = StyleTestHelper.ResolveFirst("div { animation-fill-mode: forwards; }", "<div>test</div>");
        Assert.Equal("forwards", style.AnimationFillMode);
    }

    [Fact]
    public void PropertyInterpolation_LerpFloat_Midpoint()
    {
        Assert.Equal(5f, PropertyInterpolation.LerpFloat(0, 10, 0.5f));
    }

    [Fact]
    public void PropertyInterpolation_LerpColor_Midpoint()
    {
        var from = new SuperRender.Document.Color(0, 0, 0, 1);
        var to = new SuperRender.Document.Color(1, 1, 1, 1);
        var mid = PropertyInterpolation.LerpColor(from, to, 0.5f);
        Assert.Equal(0.5f, mid.R, 0.01f);
    }

    [Fact]
    public void AnimationEngine_RegisterKeyframes()
    {
        var engine = new AnimationEngine();
        engine.RegisterKeyframes("fade", [
            new Keyframe { Offset = 0, Values = { ["opacity"] = 0 } },
            new Keyframe { Offset = 1, Values = { ["opacity"] = 1 } },
        ]);
        Assert.False(engine.HasActiveAnimations);
    }
}
