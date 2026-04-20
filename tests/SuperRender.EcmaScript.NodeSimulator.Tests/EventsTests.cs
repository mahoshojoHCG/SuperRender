using Xunit;

namespace SuperRender.EcmaScript.NodeSimulator.Tests;

public class EventsTests
{
    [Fact]
    public void EventEmitter_EmitsFiresListener()
    {
        var (engine, _) = TestHost.Create();
        engine.Execute("""
            const EE = require('events');
            const e = new EE();
            globalThis.__out = 0;
            e.on('x', v => { globalThis.__out += v; });
            e.emit('x', 1);
            e.emit('x', 2);
        """);
        Assert.Equal(3, (int)engine.Execute("globalThis.__out").ToNumber());
    }

    [Fact]
    public void Once_FiresOnlyOnce()
    {
        var (engine, _) = TestHost.Create();
        engine.Execute("""
            const EE = require('events');
            const e = new EE();
            globalThis.__n = 0;
            e.once('x', () => { globalThis.__n++; });
            e.emit('x'); e.emit('x');
        """);
        Assert.Equal(1, (int)engine.Execute("globalThis.__n").ToNumber());
    }

    [Fact]
    public void Off_RemovesListener()
    {
        var (engine, _) = TestHost.Create();
        engine.Execute("""
            const EE = require('events');
            const e = new EE();
            globalThis.__n = 0;
            const fn = () => { globalThis.__n++; };
            e.on('x', fn);
            e.off('x', fn);
            e.emit('x');
        """);
        Assert.Equal(0, (int)engine.Execute("globalThis.__n").ToNumber());
    }

    [Fact]
    public void ListenerCount_ReturnsRegistered()
    {
        var (engine, _) = TestHost.Create();
        engine.Execute("""
            const EE = require('events');
            const e = new EE();
            e.on('x', () => {});
            e.on('x', () => {});
            globalThis.__c = e.listenerCount('x');
        """);
        Assert.Equal(2, (int)engine.Execute("globalThis.__c").ToNumber());
    }

    [Fact]
    public void Emit_WithNoListeners_ReturnsFalse()
    {
        var (engine, _) = TestHost.Create();
        Assert.False(engine.Execute("const EE=require('events'); new EE().emit('x')").ToBoolean());
    }
}
