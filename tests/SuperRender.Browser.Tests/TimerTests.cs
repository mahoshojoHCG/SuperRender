using DomDocument = SuperRender.Document.Dom.Document;
using SuperRender.EcmaScript.Dom;
using SuperRender.EcmaScript.Engine;
using Xunit;

namespace SuperRender.Browser.Tests;

public class TimerTests
{
    private static (JsEngine engine, DomDocument doc, DomBridge bridge) CreateTestEnvironment(string html)
        => TestEnvironmentHelper.Create(html);

    [Fact]
    public void SetTimeout_ReturnsTimerId()
    {
        var (engine, _, _) = CreateTestEnvironment("<html><body></body></html>");
        var result = engine.Execute("window.setTimeout(function() {}, 0)");
        Assert.True(result.ToNumber() > 0);
    }

    [Fact]
    public void SetTimeout_CallbackFires_AfterDrain()
    {
        var (engine, _, bridge) = CreateTestEnvironment("<html><body></body></html>");
        engine.Execute(@"
            var called = false;
            window.setTimeout(function() { called = true; }, 0);
        ");

        // Drain the timer queue
        bridge.TimerQueue.DrainReady();

        var result = engine.Execute("called");
        Assert.True(result.ToBoolean());
    }

    [Fact]
    public void ClearTimeout_CancelsTimer()
    {
        var (engine, _, bridge) = CreateTestEnvironment("<html><body></body></html>");
        engine.Execute(@"
            var called = false;
            var id = window.setTimeout(function() { called = true; }, 0);
            window.clearTimeout(id);
        ");

        bridge.TimerQueue.DrainReady();

        var result = engine.Execute("called");
        Assert.False(result.ToBoolean());
    }

    [Fact]
    public void SetInterval_ReturnsTimerId()
    {
        var (engine, _, _) = CreateTestEnvironment("<html><body></body></html>");
        var result = engine.Execute("window.setInterval(function() {}, 100)");
        Assert.True(result.ToNumber() > 0);
    }

    [Fact]
    public void ClearInterval_CancelsInterval()
    {
        var (engine, _, bridge) = CreateTestEnvironment("<html><body></body></html>");
        engine.Execute(@"
            var count = 0;
            var id = window.setInterval(function() { count++; }, 4);
            window.clearInterval(id);
        ");

        bridge.TimerQueue.DrainReady();

        var result = engine.Execute("count");
        Assert.Equal(0.0, result.ToNumber());
    }

    [Fact]
    public void RequestAnimationFrame_ReturnsId()
    {
        var (engine, _, _) = CreateTestEnvironment("<html><body></body></html>");
        var result = engine.Execute("window.requestAnimationFrame(function() {})");
        Assert.True(result.ToNumber() > 0);
    }

    [Fact]
    public void RequestAnimationFrame_CallbackFires_AfterDrain()
    {
        var (engine, _, bridge) = CreateTestEnvironment("<html><body></body></html>");
        engine.Execute(@"
            var rafCalled = false;
            window.requestAnimationFrame(function() { rafCalled = true; });
        ");

        bridge.TimerQueue.DrainReady();

        var result = engine.Execute("rafCalled");
        Assert.True(result.ToBoolean());
    }

    [Fact]
    public void CancelAnimationFrame_CancelsRaf()
    {
        var (engine, _, bridge) = CreateTestEnvironment("<html><body></body></html>");
        engine.Execute(@"
            var rafCalled = false;
            var id = window.requestAnimationFrame(function() { rafCalled = true; });
            window.cancelAnimationFrame(id);
        ");

        bridge.TimerQueue.DrainReady();

        var result = engine.Execute("rafCalled");
        Assert.False(result.ToBoolean());
    }
}
