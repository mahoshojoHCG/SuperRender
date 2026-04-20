using SuperRender.EcmaScript.NodeSimulator.Modules;
using Xunit;

namespace SuperRender.EcmaScript.NodeSimulator.Tests;

public class AssertTests
{
    [Fact]
    public void Ok_Truthy_NoThrow()
    {
        var (engine, _) = TestHost.Create();
        engine.Execute("require('assert').ok(1)");
    }

    [Fact]
    public void Ok_Falsy_Throws()
    {
        var (engine, _) = TestHost.Create();
        Assert.ThrowsAny<Runtime.Errors.JsErrorBase>(() => engine.Execute("require('assert').ok(0)"));
    }

    [Fact]
    public void StrictEqual_MatchesTypesAndValues()
    {
        var (engine, _) = TestHost.Create();
        engine.Execute("require('assert').strictEqual(1, 1)");
        Assert.Throws<AssertionError>(() => engine.Execute("require('assert').strictEqual(1, '1')"));
    }

    [Fact]
    public void DeepStrictEqual_ForObjects()
    {
        var (engine, _) = TestHost.Create();
        engine.Execute("require('assert').deepStrictEqual({a:1, b:[2,3]}, {a:1, b:[2,3]})");
        Assert.Throws<AssertionError>(() => engine.Execute("require('assert').deepStrictEqual({a:1}, {a:2})"));
    }

    [Fact]
    public void Throws_MatchesThrowingFunction()
    {
        var (engine, _) = TestHost.Create();
        engine.Execute("require('assert').throws(() => { throw new Error('x'); })");
    }

    [Fact]
    public void DoesNotThrow_NoError_NoFailure()
    {
        var (engine, _) = TestHost.Create();
        engine.Execute("require('assert').doesNotThrow(() => 1)");
    }

    [Fact]
    public void AssertCallable_BehavesAsOk()
    {
        var (engine, _) = TestHost.Create();
        engine.Execute("const a = require('assert'); a(true);");
        Assert.ThrowsAny<Runtime.Errors.JsErrorBase>(() => engine.Execute("require('assert')(false)"));
    }

    [Fact]
    public void Match_AcceptsRegexp()
    {
        var (engine, _) = TestHost.Create();
        engine.Execute("require('assert').match('abc', new RegExp('b'))");
        Assert.Throws<AssertionError>(() => engine.Execute("require('assert').match('abc', new RegExp('z'))"));
    }
}
