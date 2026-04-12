using SuperRender.EcmaScript.Engine;
using Xunit;

namespace SuperRender.EcmaScript.Tests.Runtime;

public class EvalTests
{
    private static JsEngine CreateEngine() => new();

    [Fact]
    public void Eval_SimpleExpression_ReturnsResult()
    {
        var engine = CreateEngine();
        var result = engine.Execute<double>("eval('1 + 2')");
        Assert.Equal(3, result);
    }

    [Fact]
    public void Eval_StringExpression_ReturnsString()
    {
        var engine = CreateEngine();
        var result = engine.Execute<string>("eval(\"'hello'\")");
        Assert.Equal("hello", result);
    }

    [Fact]
    public void Eval_NonString_ReturnsArgument()
    {
        var engine = CreateEngine();
        var result = engine.Execute<double>("eval(42)");
        Assert.Equal(42, result);
    }

    [Fact]
    public void Eval_VariableDeclaration_AccessibleInScope()
    {
        var engine = CreateEngine();
        var result = engine.Execute<double>(@"
            eval('var x = 10');
            x;
        ");
        Assert.Equal(10, result);
    }

    [Fact]
    public void Eval_FunctionDeclaration_Callable()
    {
        var engine = CreateEngine();
        var result = engine.Execute<double>(@"
            eval('function add(a, b) { return a + b; }');
            add(3, 4);
        ");
        Assert.Equal(7, result);
    }

    [Fact]
    public void Eval_SyntaxError_Throws()
    {
        var engine = CreateEngine();
        Assert.ThrowsAny<Exception>(() => engine.Execute("eval('if (')"));
    }

    [Fact]
    public void FunctionConstructor_CreatesFunction()
    {
        var engine = CreateEngine();
        var result = engine.Execute<double>(@"
            const add = new Function('a', 'b', 'return a + b');
            add(5, 3);
        ");
        Assert.Equal(8, result);
    }

    [Fact]
    public void FunctionConstructor_NoArgs_CreatesEmptyFunction()
    {
        var engine = CreateEngine();
        var result = engine.Execute<string>(@"
            const f = new Function('return 42');
            typeof f;
        ");
        Assert.Equal("function", result);
    }

    [Fact]
    public void FunctionConstructor_WithoutNew_Works()
    {
        var engine = CreateEngine();
        var result = engine.Execute<double>(@"
            const f = Function('x', 'return x * 2');
            f(5);
        ");
        Assert.Equal(10, result);
    }

    [Fact]
    public void Eval_ObjectLiteral_Works()
    {
        var engine = CreateEngine();
        var result = engine.Execute<double>("eval('({x: 42})').x");
        Assert.Equal(42, result);
    }
}
