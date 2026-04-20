using SuperRender.EcmaScript.Runtime;
using Xunit;

namespace SuperRender.EcmaScript.NodeSimulator.Tests;

public class QueryStringTests
{
    [Fact]
    public void Parse_SingleKey()
    {
        var (engine, _) = TestHost.Create();
        var q = engine.RunString("const qs = require('querystring'); qs.parse('a=1').a");
        Assert.Equal("1", q);
    }

    [Fact]
    public void Parse_DuplicateKeyProducesArray()
    {
        var (engine, _) = TestHost.Create();
        var code = "const qs = require('querystring'); const r = qs.parse('a=1&a=2&b=3'); Array.isArray(r.a) + ':' + r.a.length + ':' + r.b";
        Assert.Equal("true:2:3", engine.RunString(code));
    }

    [Fact]
    public void Parse_DecodesPercentAndPlus()
    {
        var (engine, _) = TestHost.Create();
        Assert.Equal("hello world", engine.RunString("require('querystring').parse('q=hello+world').q"));
        Assert.Equal("ä", engine.RunString("require('querystring').parse('x=%C3%A4').x"));
    }

    [Fact]
    public void Stringify_RoundTrip()
    {
        var (engine, _) = TestHost.Create();
        var code = "const qs=require('querystring'); qs.stringify({a:1,b:'hi there',c:true})";
        Assert.Equal("a=1&b=hi%20there&c=true", engine.RunString(code));
    }

    [Fact]
    public void Stringify_HandlesArrayValues()
    {
        var (engine, _) = TestHost.Create();
        Assert.Equal("a=1&a=2", engine.RunString("require('querystring').stringify({a:[1,2]})"));
    }

    [Fact]
    public void EscapeUnescape()
    {
        var (engine, _) = TestHost.Create();
        Assert.Equal("hello%20world", engine.RunString("require('querystring').escape('hello world')"));
        Assert.Equal("hello world", engine.RunString("require('querystring').unescape('hello%20world')"));
    }

    [Fact]
    public void EmptyInput_ReturnsEmptyObject()
    {
        var (engine, _) = TestHost.Create();
        Assert.Equal("0", engine.RunString("Object.keys(require('querystring').parse('')).length + ''"));
    }
}
