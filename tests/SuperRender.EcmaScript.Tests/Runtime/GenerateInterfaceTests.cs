#pragma warning disable CA1707 // underscores in test method names (xUnit Method_Scenario_Expected)
#pragma warning disable CA1034 // nested types
#pragma warning disable CA1822 // method can be static — required to satisfy JS binding

using System.IO;
using SuperRender.EcmaScript.Runtime;
using SuperRender.EcmaScript.Runtime.Interop;
using Xunit;

namespace SuperRender.EcmaScript.Tests.Runtime;

[JsObject(GenerateInterface = true)]
public sealed partial class FixtureGen : JsObjectBase
{
    [JsMethod("ping")]
    public JsString Ping(string s) => new(s);

    [JsMethod("add")]
    public double Add(double a, double b) => a + b;

    [JsProperty("label")]
    public JsString Label => new("hello");
}

public class GenerateInterfaceTests
{
    [Fact]
    public void FixtureGen_ImplementsGeneratedInterface()
    {
        Assert.True(typeof(IFixtureGen).IsAssignableFrom(typeof(FixtureGen)));
        Assert.True(typeof(IJsType).IsAssignableFrom(typeof(IFixtureGen)));
    }

    [Fact]
    public void FixtureGen_AsInterface_UsesFastPath()
    {
        var obj = new FixtureGen();
        JsValue v = obj;
        var view = v.AsInterface<IFixtureGen>();
        Assert.Same(obj, view);
        Assert.Equal("hi", view.Ping("hi").Value);
        Assert.Equal(5.0, view.Add(2.0, 3.0));
    }

    [Fact]
    public void DtsFile_WrittenToTypesFolder()
    {
        var asmDir = Path.GetDirectoryName(typeof(GenerateInterfaceTests).Assembly.Location)!;
        var dts = Path.Combine(
            asmDir, "types", "SuperRender", "EcmaScript", "Tests", "Runtime", "IFixtureGen.d.ts");
        Assert.True(File.Exists(dts), $"expected {dts}");
        var content = File.ReadAllText(dts);
        Assert.Contains("export interface IFixtureGen", content);
        Assert.Contains("ping(", content);
        Assert.Contains("add(", content);
        Assert.Contains("readonly label:", content);
    }
}
