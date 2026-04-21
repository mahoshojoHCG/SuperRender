#pragma warning disable CA1707 // xUnit method names use underscores
#pragma warning disable CA1034 // nested types allowed in tests
#pragma warning disable CA1822 // fixture methods needn't be static

using System.Threading.Tasks;
using SuperRender.EcmaScript.Engine;
using SuperRender.EcmaScript.Runtime;
using SuperRender.EcmaScript.Runtime.Builtins;
using Xunit;

namespace SuperRender.EcmaScript.Tests.Builtins;

[JsObject]
public sealed partial class TaskReturnFixture : JsObject
{
    [JsMethod("pingStr")]
    public Task<string> PingStr(string s) => Task.FromResult(s);

    [JsMethod("sumInt")]
    public Task<int> SumInt(int a, int b) => Task.FromResult(a + b);

    [JsMethod("truthy")]
    public Task<bool> Truthy() => Task.FromResult(true);

    [JsMethod("jsValue")]
    public Task<JsString> JsStringTask() => Task.FromResult(new JsString("via-jsvalue"));

    [JsMethod("plain")]
    public Task Plain() => Task.CompletedTask;

    [JsMethod("boom")]
    public Task<string> Boom() => Task.FromException<string>(new System.InvalidOperationException("kaboom"));
}

public class JsPromiseInteropTests
{
    [Fact]
    public void FromTask_Completed_ResolvesWithValue()
    {
        var p = JsPromise.FromTask(Task.FromResult("hello"));
        Assert.Equal(JsPromise.PromiseState.Fulfilled, p.State);
        Assert.Equal("hello", Assert.IsType<JsString>(p.Result).Value);
    }

    [Fact]
    public void FromTask_IntPrimitive_AutoBoxesToJsNumber()
    {
        var p = JsPromise.FromTask(Task.FromResult(42));
        Assert.Equal(JsPromise.PromiseState.Fulfilled, p.State);
        Assert.Equal(42.0, Assert.IsType<JsNumber>(p.Result).ToNumber());
    }

    [Fact]
    public void FromTask_JsValueT_PassesThrough()
    {
        var jv = new JsString("js");
        var p = JsPromise.FromTask<JsString>(Task.FromResult(jv));
        Assert.Same(jv, p.Result);
    }

    [Fact]
    public void FromTask_FaultedTask_RejectsWithErrorMessage()
    {
        var p = JsPromise.FromTask(Task.FromException<string>(new System.InvalidOperationException("bad")));
        Assert.Equal(JsPromise.PromiseState.Rejected, p.State);
        Assert.Equal("bad", Assert.IsType<JsString>(p.Result).Value);
    }

    [Fact]
    public void FromTask_NonGeneric_ResolvesUndefined()
    {
        var p = JsPromise.FromTask(Task.CompletedTask);
        Assert.Equal(JsPromise.PromiseState.Fulfilled, p.State);
        Assert.IsType<JsUndefined>(p.Result);
    }

    [Fact]
    public async Task GetAwaiter_Fulfilled_ReturnsJsValue()
    {
        var p = JsPromise.Resolved(new JsString("awaited"));
        var v = await p;
        Assert.Equal("awaited", Assert.IsType<JsString>(v).Value);
    }

    [Fact]
    public async Task GetAwaiter_Rejected_Throws()
    {
        var p = JsPromise.Rejected(new JsString("nope"));
        var ex = await Assert.ThrowsAsync<PromiseRejectedException>(async () => await p);
        Assert.Equal("nope", Assert.IsType<JsString>(ex.Reason).Value);
    }

    [Fact]
    public async Task GetAwaiter_Typed_UnboxesToInt()
    {
        var p = JsPromise<int>.FromTask(Task.FromResult(123));
        var v = await p;
        Assert.Equal(123, v);
    }

    [Fact]
    public async Task GetAwaiter_Typed_UnboxesToString()
    {
        var p = JsPromise<string>.FromTask(Task.FromResult("s"));
        var v = await p;
        Assert.Equal("s", v);
    }

    [Fact]
    public void SG_TaskOfString_ReturnsJsPromiseResolvingToString()
    {
        var fixture = new TaskReturnFixture();
        var result = ((JsFunction)fixture.Get("pingStr")).Call(fixture, [new JsString("pong")]);
        var promise = Assert.IsAssignableFrom<JsPromise>(result);
        Assert.Equal(JsPromise.PromiseState.Fulfilled, promise.State);
        Assert.Equal("pong", Assert.IsType<JsString>(promise.Result).Value);
    }

    [Fact]
    public void SG_TaskOfInt_ReturnsJsPromiseResolvingToNumber()
    {
        var fixture = new TaskReturnFixture();
        var result = ((JsFunction)fixture.Get("sumInt")).Call(fixture, [JsNumber.Create(2), JsNumber.Create(3)]);
        var promise = Assert.IsAssignableFrom<JsPromise>(result);
        Assert.Equal(5.0, Assert.IsType<JsNumber>(promise.Result).ToNumber());
    }

    [Fact]
    public void SG_TaskOfBool_ReturnsJsPromiseResolvingToBoolean()
    {
        var fixture = new TaskReturnFixture();
        var result = ((JsFunction)fixture.Get("truthy")).Call(fixture, []);
        var promise = Assert.IsAssignableFrom<JsPromise>(result);
        Assert.Same(JsValue.True, promise.Result);
    }

    [Fact]
    public void SG_TaskOfJsValueDerived_PassesThrough()
    {
        var fixture = new TaskReturnFixture();
        var result = ((JsFunction)fixture.Get("jsValue")).Call(fixture, []);
        var promise = Assert.IsAssignableFrom<JsPromise>(result);
        Assert.Equal("via-jsvalue", Assert.IsType<JsString>(promise.Result).Value);
    }

    [Fact]
    public void SG_PlainTask_ReturnsJsPromiseResolvingToUndefined()
    {
        var fixture = new TaskReturnFixture();
        var result = ((JsFunction)fixture.Get("plain")).Call(fixture, []);
        var promise = Assert.IsAssignableFrom<JsPromise>(result);
        Assert.Equal(JsPromise.PromiseState.Fulfilled, promise.State);
        Assert.IsType<JsUndefined>(promise.Result);
    }

    [Fact]
    public void SG_FaultedTask_PromiseRejects()
    {
        var fixture = new TaskReturnFixture();
        var result = ((JsFunction)fixture.Get("boom")).Call(fixture, []);
        var promise = Assert.IsAssignableFrom<JsPromise>(result);
        Assert.Equal(JsPromise.PromiseState.Rejected, promise.State);
        Assert.Equal("kaboom", Assert.IsType<JsString>(promise.Result).Value);
    }

    [Fact]
    public void SG_PromiseFromTaskReturn_IsUsableFromJs()
    {
        var engine = new JsEngine();
        engine.SetValue("f", new TaskReturnFixture());
        var r = engine.Execute<double>(@"
            let seen = 0;
            f.sumInt(10, 32).then(v => { seen = v; });
            seen;
        ");
        Assert.Equal(42.0, r);
    }
}
