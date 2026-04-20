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

// IJsType interface used as [JsMethod] parameter/return — user-declared so it's visible
// to the consumer class's parameter analysis during the same generator pass.
public interface IBoxThing : IJsType
{
    string Name { get; }
}

public sealed class BoxThing : JsObjectBase, IBoxThing
{
    public string Name { get; set; } = "box";

    public override JsValue Get(string name) =>
        name == "name" ? new JsString(Name) : base.Get(name);

    public override bool HasProperty(string name) =>
        name == "name" || base.HasProperty(name);
}

[JsObject]
public sealed partial class FixtureGenConsumer : JsObjectBase
{
    public IBoxThing? Captured { get; private set; }

    [JsMethod("takeBox")]
    public string TakeBox(IBoxThing box)
    {
        Captured = box;
        return box.Name;
    }

    [JsMethod("makeBox")]
    public IBoxThing MakeBox(string name) => new BoxThing { Name = name };
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

    [Fact]
    public void JsMethod_IJsTypeParameter_CallsAsInterface()
    {
        var consumer = new FixtureGenConsumer();
        var takeBox = consumer.Get("takeBox") as JsFunction;
        Assert.NotNull(takeBox);

        var box = new BoxThing { Name = "alpha" };
        var result = takeBox!.Call(consumer, [box]);

        Assert.Equal("alpha", Assert.IsType<JsString>(result).Value);
        Assert.Same(box, consumer.Captured);
    }

    [Fact]
    public void JsMethod_IJsTypeReturn_UnwrapsToJsValue()
    {
        var consumer = new FixtureGenConsumer();
        var makeBox = consumer.Get("makeBox") as JsFunction;
        Assert.NotNull(makeBox);

        var result = makeBox!.Call(consumer, [new JsString("beta")]);
        var asBox = Assert.IsType<BoxThing>(result);
        Assert.Equal("beta", asBox.Name);
    }

    [Fact]
    public void JsMethod_IJsTypeParameter_ThrowsOnNonObject()
    {
        var consumer = new FixtureGenConsumer();
        var takeBox = consumer.Get("takeBox") as JsFunction;
        Assert.NotNull(takeBox);

        Assert.Throws<SuperRender.EcmaScript.Runtime.Errors.JsTypeError>(
            () => takeBox!.Call(consumer, [new JsString("not-an-object")]));
    }
}
