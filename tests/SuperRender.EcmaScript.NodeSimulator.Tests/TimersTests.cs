using Xunit;

namespace SuperRender.EcmaScript.NodeSimulator.Tests;

public class TimersTests
{
    [Fact]
    public void SetImmediate_FiresOnNextPoll()
    {
        var (engine, node) = TestHost.Create();
        engine.Execute("globalThis.__n = 0; setImmediate(() => { globalThis.__n = 1; });");
        Assert.Equal(0, (int)engine.Execute("globalThis.__n").ToNumber());
        node.Timers.Poll();
        Assert.Equal(1, (int)engine.Execute("globalThis.__n").ToNumber());
    }

    [Fact]
    public void SetTimeout_FiresAfterAdvance()
    {
        var (engine, node) = TestHost.Create();
        engine.Execute("globalThis.__n = 0; setTimeout(() => { globalThis.__n = 1; }, 50);");
        node.Timers.Poll();
        Assert.Equal(0, (int)engine.Execute("globalThis.__n").ToNumber());
        node.AdvanceTimers(100);
        node.Timers.Poll();
        Assert.Equal(1, (int)engine.Execute("globalThis.__n").ToNumber());
    }

    [Fact]
    public void ClearTimeout_CancelsPendingCallback()
    {
        var (engine, node) = TestHost.Create();
        engine.Execute("globalThis.__n = 0; const h = setTimeout(() => { globalThis.__n = 1; }, 50); clearTimeout(h);");
        node.AdvanceTimers(100);
        node.Timers.Poll();
        Assert.Equal(0, (int)engine.Execute("globalThis.__n").ToNumber());
    }

    [Fact]
    public void SetInterval_FiresRepeatedly()
    {
        var (engine, node) = TestHost.Create();
        engine.Execute("globalThis.__n = 0; globalThis.__h = setInterval(() => { globalThis.__n++; }, 10);");
        for (int i = 0; i < 3; i++)
        {
            node.AdvanceTimers(15);
            node.Timers.Poll();
        }
        engine.Execute("clearInterval(globalThis.__h)");
        Assert.InRange((int)engine.Execute("globalThis.__n").ToNumber(), 3, 10);
    }

    [Fact]
    public void QueueMicrotask_FiresOnPoll()
    {
        var (engine, node) = TestHost.Create();
        engine.Execute("globalThis.__m = 0; queueMicrotask(() => { globalThis.__m = 1; });");
        node.Timers.Poll();
        Assert.Equal(1, (int)engine.Execute("globalThis.__m").ToNumber());
    }

    [Fact]
    public void SetTimeout_ForwardsExtraArgsToCallback()
    {
        var (engine, node) = TestHost.Create();
        engine.Execute("globalThis.__sum = 0; setTimeout((a, b) => { globalThis.__sum = a + b; }, 1, 5, 7);");
        node.AdvanceTimers(10);
        node.Timers.Poll();
        Assert.Equal(12, (int)engine.Execute("globalThis.__sum").ToNumber());
    }
}
