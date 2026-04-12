using SuperRender.EcmaScript.Engine;
using Xunit;

namespace SuperRender.EcmaScript.Tests.Runtime;

public class PipelineTests
{
    private static JsEngine CreateEngine() => new();

    [Fact]
    public void Pipeline_SimpleFunction_CallsWithLeftAsArg()
    {
        var engine = CreateEngine();
        var result = engine.Execute<double>(@"
            function double(x) { return x * 2; }
            5 |> double;
        ");
        Assert.Equal(10, result);
    }

    [Fact]
    public void Pipeline_Chained_LeftToRight()
    {
        var engine = CreateEngine();
        var result = engine.Execute<double>(@"
            function add1(x) { return x + 1; }
            function mul3(x) { return x * 3; }
            2 |> add1 |> mul3;
        ");
        Assert.Equal(9, result);
    }

    [Fact]
    public void Pipeline_ArrowFunction_Works()
    {
        var engine = CreateEngine();
        var result = engine.Execute<double>("10 |> (x => x + 5)");
        Assert.Equal(15, result);
    }

    [Fact]
    public void Pipeline_StringFunction_Works()
    {
        var engine = CreateEngine();
        var result = engine.Execute<string>(@"
            function greet(name) { return 'Hello, ' + name; }
            'World' |> greet;
        ");
        Assert.Equal("Hello, World", result);
    }

    [Fact]
    public void Pipeline_WithMathFunction_Works()
    {
        var engine = CreateEngine();
        var result = engine.Execute<double>("(-5) |> Math.abs");
        Assert.Equal(5, result);
    }

    [Fact]
    public void Pipeline_MultipleSteps_ProcessesCorrectly()
    {
        var engine = CreateEngine();
        var result = engine.Execute<double>(@"
            function square(x) { return x * x; }
            function negate(x) { return -x; }
            function addTen(x) { return x + 10; }
            3 |> square |> negate |> addTen;
        ");
        Assert.Equal(1, result);
    }

    [Fact]
    public void Pipeline_WithLiteral_Works()
    {
        var engine = CreateEngine();
        var result = engine.Execute<double>(@"
            function identity(x) { return x; }
            42 |> identity;
        ");
        Assert.Equal(42, result);
    }
}
