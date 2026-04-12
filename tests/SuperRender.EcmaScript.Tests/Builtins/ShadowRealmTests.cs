using SuperRender.EcmaScript.Engine;
using Xunit;

namespace SuperRender.EcmaScript.Tests.Builtins;

public class ShadowRealmTests
{
    private static JsEngine CreateEngine() => new();

    [Fact]
    public void ShadowRealm_Evaluate_SimpleExpression()
    {
        var engine = CreateEngine();
        var result = engine.Execute<double>(@"
            const sr = new ShadowRealm();
            sr.evaluate('1 + 2');
        ");
        Assert.Equal(3, result);
    }

    [Fact]
    public void ShadowRealm_Evaluate_StringResult()
    {
        var engine = CreateEngine();
        var result = engine.Execute<string>(@"
            const sr = new ShadowRealm();
            sr.evaluate(""'hello'"");
        ");
        Assert.Equal("hello", result);
    }

    [Fact]
    public void ShadowRealm_Evaluate_IsolatedGlobals()
    {
        var engine = CreateEngine();
        // Variable declared in shadow realm should not leak to parent
        engine.Execute(@"
            const sr = new ShadowRealm();
            sr.evaluate('var shadowVar = 42');
        ");
        Assert.Throws<SuperRender.EcmaScript.Runtime.Errors.JsReferenceError>(() =>
            engine.Execute("shadowVar"));
    }

    [Fact]
    public void ShadowRealm_CalledWithoutNew_Throws()
    {
        var engine = CreateEngine();
        Assert.Throws<SuperRender.EcmaScript.Runtime.Errors.JsTypeError>(() =>
            engine.Execute("ShadowRealm()"));
    }

    [Fact]
    public void ShadowRealm_Evaluate_NonString_Throws()
    {
        var engine = CreateEngine();
        Assert.Throws<SuperRender.EcmaScript.Runtime.Errors.JsTypeError>(() =>
            engine.Execute(@"
                const sr = new ShadowRealm();
                sr.evaluate(42);
            "));
    }

    [Fact]
    public void ShadowRealm_Evaluate_ReturnsFunction()
    {
        var engine = CreateEngine();
        var result = engine.Execute<string>(@"
            const sr = new ShadowRealm();
            const fn = sr.evaluate('(function() { return 42; })');
            typeof fn;
        ");
        Assert.Equal("function", result);
    }

    [Fact]
    public void ShadowRealm_Evaluate_MathWorks()
    {
        var engine = CreateEngine();
        var result = engine.Execute<double>(@"
            const sr = new ShadowRealm();
            sr.evaluate('Math.max(1, 2, 3)');
        ");
        Assert.Equal(3, result);
    }

    [Fact]
    public void ShadowRealm_Evaluate_BooleanResult()
    {
        var engine = CreateEngine();
        var result = engine.Execute<bool>(@"
            const sr = new ShadowRealm();
            sr.evaluate('true');
        ");
        Assert.True(result);
    }
}
