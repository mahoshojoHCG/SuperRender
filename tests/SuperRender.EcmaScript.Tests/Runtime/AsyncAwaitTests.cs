using SuperRender.EcmaScript.Interop;
using Xunit;

namespace SuperRender.EcmaScript.Tests.Runtime;

public class AsyncAwaitTests
{
    private static JsEngine CreateEngine() => new();

    [Fact]
    public void AsyncFunction_ReturnsPromise()
    {
        var engine = CreateEngine();
        var result = engine.Execute(@"
            async function foo() { return 42; }
            var p = foo();
            typeof p.then
        ");

        Assert.Equal("function", result.ToJsString());
    }

    [Fact]
    public void Await_ResolvedValue()
    {
        var engine = CreateEngine();
        engine.Execute(@"
            var resolved = null;
            async function foo() {
                var x = await 42;
                return x;
            }
            foo().then(function(v) { resolved = v; });
        ");

        Assert.Equal(42.0, engine.Execute("resolved").ToNumber());
    }

    [Fact]
    public void AsyncAwait_WithTryCatch()
    {
        var engine = CreateEngine();
        engine.Execute(@"
            var caught = null;
            async function foo() {
                try {
                    var val = await Promise.reject('oops');
                    return val;
                } catch (e) {
                    caught = e;
                    return 'caught';
                }
            }
            var result = null;
            foo().then(function(v) { result = v; });
        ");

        Assert.Equal("caught", engine.Execute("result").ToJsString());
        Assert.Equal("oops", engine.Execute("caught").ToJsString());
    }

    [Fact]
    public void MultipleSequentialAwaits()
    {
        var engine = CreateEngine();
        engine.Execute(@"
            var result = null;
            async function add() {
                var a = await 10;
                var b = await 20;
                var c = await 30;
                return a + b + c;
            }
            add().then(function(v) { result = v; });
        ");

        Assert.Equal(60.0, engine.Execute("result").ToNumber());
    }
}
