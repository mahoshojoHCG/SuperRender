using SuperRender.EcmaScript.NodeSimulator.Modules;
using SuperRender.EcmaScript.Runtime;
using Xunit;

namespace SuperRender.EcmaScript.NodeSimulator.Tests;

public class UtilTests
{
    [Fact]
    public void Format_S_SubstitutesString()
    {
        Assert.Equal("hello world", UtilModule.Format([new JsString("hello %s"), new JsString("world")]));
    }

    [Fact]
    public void Format_D_FormatsInteger()
    {
        Assert.Equal("count 7", UtilModule.Format([new JsString("count %d"), JsNumber.Create(7)]));
    }

    [Fact]
    public void Format_ExtraArgs_AppendedWithSpaces()
    {
        var s = UtilModule.Format([new JsString("a"), new JsString("b"), new JsString("c")]);
        Assert.StartsWith("a", s);
        Assert.Contains("b", s);
        Assert.Contains("c", s);
    }

    [Fact]
    public void Inspect_Object_ProducesBraces()
    {
        var o = new JsDynamicObject();
        o.DefineOwnProperty("a", PropertyDescriptor.Data(JsNumber.Create(1)));
        Assert.Contains("a: 1", UtilModule.Inspect(o, 2), System.StringComparison.Ordinal);
    }

    [Fact]
    public void DeepEqual_Arrays_Structurally()
    {
        var a = new JsArray(); a.Push(JsNumber.Create(1)); a.Push(JsNumber.Create(2));
        var b = new JsArray(); b.Push(JsNumber.Create(1)); b.Push(JsNumber.Create(2));
        Assert.True(UtilModule.DeepEqual(a, b, strict: true));
    }

    [Fact]
    public void DeepEqual_Objects_Structurally()
    {
        var (engine, _) = TestHost.Create();
        var result = engine.Execute("require('util').isDeepStrictEqual({a:1,b:[2,3]}, {a:1,b:[2,3]})").ToBoolean();
        Assert.True(result);
    }

    [Fact]
    public void Util_FormatFn_InvokableFromJs()
    {
        var (engine, _) = TestHost.Create();
        Assert.Equal("x=5", engine.RunString("require('util').format('x=%d', 5)"));
    }

    [Fact]
    public void Promisify_WrapsCallbackFunction()
    {
        var (engine, node) = TestHost.Create();
        engine.Execute("""
            const util = require('util');
            function cbFn(val, cb) { cb(null, val * 2); }
            const p = util.promisify(cbFn);
            globalThis.__result = null;
            p(21).then(v => { globalThis.__result = v; });
        """);
        node.DrainOnce();
        Assert.Equal(42, (int)engine.Execute("globalThis.__result").ToNumber());
    }
}
