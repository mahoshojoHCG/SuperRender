using Xunit;

namespace SuperRender.EcmaScript.NodeSimulator.Tests;

public class GlobalsTests
{
    [Fact]
    public void Install_Defines_Process_Global()
    {
        var (engine, _) = TestHost.Create();
        Assert.Equal("object", engine.Execute("typeof process").ToJsString());
    }

    [Fact]
    public void Install_Defines_GlobalThis_Alias()
    {
        var (engine, _) = TestHost.Create();
        Assert.Equal("object", engine.Execute("typeof globalThis").ToJsString());
        Assert.Equal("object", engine.Execute("typeof global").ToJsString());
    }

    [Fact]
    public void Install_Defines_Buffer_Global()
    {
        var (engine, _) = TestHost.Create();
        Assert.Equal("function", engine.Execute("typeof Buffer").ToJsString());
    }

    [Fact]
    public void Require_BuiltinModule_ReturnsObject()
    {
        var (engine, _) = TestHost.Create();
        Assert.Equal("object", engine.Execute("typeof require('path')").ToJsString());
        Assert.Equal("object", engine.Execute("typeof require('node:os')").ToJsString());
    }

    [Fact]
    public void Require_UnknownModule_Throws()
    {
        var (engine, _) = TestHost.Create();
        Assert.Throws<Runtime.Errors.JsErrorBase>(() => engine.Execute("require('does-not-exist')"));
    }
}
