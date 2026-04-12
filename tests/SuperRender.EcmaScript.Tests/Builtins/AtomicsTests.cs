using SuperRender.EcmaScript.Engine;
using Xunit;

namespace SuperRender.EcmaScript.Tests.Builtins;

public class AtomicsTests
{
    private static JsEngine CreateEngine() => new();

    [Fact]
    public void Atomics_Store_And_Load_Works()
    {
        var engine = CreateEngine();
        var result = engine.Execute<double>(@"
            const buf = new SharedArrayBuffer(16);
            const arr = new Int32Array(buf);
            Atomics.store(arr, 0, 42);
            Atomics.load(arr, 0);
        ");
        Assert.Equal(42, result);
    }

    [Fact]
    public void Atomics_Add_ReturnsOldValue()
    {
        var engine = CreateEngine();
        var result = engine.Execute<double>(@"
            const buf = new SharedArrayBuffer(16);
            const arr = new Int32Array(buf);
            Atomics.store(arr, 0, 10);
            Atomics.add(arr, 0, 5);
        ");
        Assert.Equal(10, result);
    }

    [Fact]
    public void Atomics_Add_UpdatesValue()
    {
        var engine = CreateEngine();
        var result = engine.Execute<double>(@"
            const buf = new SharedArrayBuffer(16);
            const arr = new Int32Array(buf);
            Atomics.store(arr, 0, 10);
            Atomics.add(arr, 0, 5);
            Atomics.load(arr, 0);
        ");
        Assert.Equal(15, result);
    }

    [Fact]
    public void Atomics_Exchange_SwapsValues()
    {
        var engine = CreateEngine();
        var result = engine.Execute<double>(@"
            const buf = new SharedArrayBuffer(16);
            const arr = new Int32Array(buf);
            Atomics.store(arr, 0, 100);
            const old = Atomics.exchange(arr, 0, 200);
            old;
        ");
        Assert.Equal(100, result);
    }

    [Fact]
    public void Atomics_CompareExchange_MatchingExpected_Swaps()
    {
        var engine = CreateEngine();
        var result = engine.Execute<double>(@"
            const buf = new SharedArrayBuffer(16);
            const arr = new Int32Array(buf);
            Atomics.store(arr, 0, 50);
            Atomics.compareExchange(arr, 0, 50, 99);
            Atomics.load(arr, 0);
        ");
        Assert.Equal(99, result);
    }

    [Fact]
    public void Atomics_CompareExchange_NonMatching_DoesNotSwap()
    {
        var engine = CreateEngine();
        var result = engine.Execute<double>(@"
            const buf = new SharedArrayBuffer(16);
            const arr = new Int32Array(buf);
            Atomics.store(arr, 0, 50);
            Atomics.compareExchange(arr, 0, 99, 200);
            Atomics.load(arr, 0);
        ");
        Assert.Equal(50, result);
    }

    [Fact]
    public void Atomics_IsLockFree_ReturnsTrue_For4Bytes()
    {
        var engine = CreateEngine();
        var result = engine.Execute<bool>("Atomics.isLockFree(4)");
        Assert.True(result);
    }

    [Fact]
    public void Atomics_IsLockFree_ReturnsFalse_For3Bytes()
    {
        var engine = CreateEngine();
        var result = engine.Execute<bool>("Atomics.isLockFree(3)");
        Assert.False(result);
    }

    [Fact]
    public void Atomics_Sub_SubtractsValue()
    {
        var engine = CreateEngine();
        var result = engine.Execute<double>(@"
            const buf = new SharedArrayBuffer(16);
            const arr = new Int32Array(buf);
            Atomics.store(arr, 0, 20);
            Atomics.sub(arr, 0, 5);
            Atomics.load(arr, 0);
        ");
        Assert.Equal(15, result);
    }

    [Fact]
    public void Atomics_Notify_ReturnsZero()
    {
        var engine = CreateEngine();
        var result = engine.Execute<double>(@"
            const buf = new SharedArrayBuffer(16);
            const arr = new Int32Array(buf);
            Atomics.notify(arr, 0, 1);
        ");
        Assert.Equal(0, result);
    }
}
