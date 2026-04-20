using SuperRender.EcmaScript.Engine;
using Xunit;

namespace SuperRender.EcmaScript.Tests.Builtins;

public class PromiseWithResolversTests
{
    private static JsEngine CreateEngine() => new();

    [Fact]
    public void WithResolvers_ReturnsObjectWithThreeProperties()
    {
        var engine = CreateEngine();
        engine.Execute(@"
            const { promise, resolve, reject } = Promise.withResolvers();
        ");
        var promise = engine.GetValue("promise");
        var resolve = engine.GetValue("resolve");
        var reject = engine.GetValue("reject");
        Assert.IsAssignableFrom<SuperRender.EcmaScript.Runtime.JsDynamicObject>(promise);
        Assert.IsAssignableFrom<SuperRender.EcmaScript.Runtime.JsFunction>(resolve);
        Assert.IsAssignableFrom<SuperRender.EcmaScript.Runtime.JsFunction>(reject);
    }

    [Fact]
    public void WithResolvers_ResolveSettlesPromise()
    {
        var engine = CreateEngine();
        var result = engine.Execute<double>(@"
            const { promise, resolve } = Promise.withResolvers();
            let result = 0;
            promise.then(v => { result = v; });
            resolve(42);
            result;
        ");
        Assert.Equal(42, result);
    }

    [Fact]
    public void WithResolvers_RejectSettlesPromise()
    {
        var engine = CreateEngine();
        var result = engine.Execute<string>(@"
            const { promise, reject } = Promise.withResolvers();
            let result = '';
            promise.catch(r => { result = r; });
            reject('error');
            result;
        ");
        Assert.Equal("error", result);
    }

    [Fact]
    public void WithResolvers_PromiseIsPromiseInstance()
    {
        var engine = CreateEngine();
        var result = engine.Execute<bool>(@"
            const { promise } = Promise.withResolvers();
            promise instanceof Promise;
        ");
        Assert.True(result);
    }

    [Fact]
    public void WithResolvers_ResolveOnlyOnce()
    {
        var engine = CreateEngine();
        var result = engine.Execute<double>(@"
            const { promise, resolve } = Promise.withResolvers();
            let result = 0;
            promise.then(v => { result = v; });
            resolve(1);
            resolve(2);
            result;
        ");
        Assert.Equal(1, result);
    }

    [Fact]
    public void WithResolvers_ThenChaining()
    {
        var engine = CreateEngine();
        var result = engine.Execute<double>(@"
            const { promise, resolve } = Promise.withResolvers();
            let result = 0;
            promise.then(v => v * 2).then(v => { result = v; });
            resolve(21);
            result;
        ");
        Assert.Equal(42, result);
    }
}
