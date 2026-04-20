#pragma warning disable CA1707 // underscores in test method names (xUnit Method_Scenario_Expected)
#pragma warning disable CA1034 // nested types (test interfaces/classes intentionally nested)

using SuperRender.EcmaScript.Engine;
using SuperRender.EcmaScript.Runtime;
using SuperRender.EcmaScript.Runtime.Errors;
using SuperRender.EcmaScript.Runtime.Interop;
using Xunit;

namespace SuperRender.EcmaScript.Tests.Runtime;

public class AsInterfaceTests
{
    internal interface IFoo : IJsType
    {
        double Foo { get; set; }
    }

    internal interface IGreeter : IJsType
    {
        string Greet(string name);
    }

    internal interface IBoolView : IJsType
    {
        bool Flag { get; }
    }

    internal interface IStringBag : IJsType
    {
        string Label { get; set; }
    }

    internal interface INested : IJsType
    {
        IFoo GetFoo();
    }

    internal interface IWithObjectArg : IJsType
    {
        JsValue Echo(JsObjectBase arg);
    }

    internal interface ICustomName : IJsType
    {
        [JsName("my_prop")]
        double MyProp { get; }

        [JsName("do-the-thing")]
        string DoThing(string input);
    }

    // Bi-directional fast path: class both implements IFoo and inherits JsObjectBase.
    internal sealed class DirectFoo : JsObjectBase, IFoo
    {
        public double Foo { get; set; }
    }

    private static JsEngine Engine() => new();

    private static JsValue Eval(string expr) => Engine().Execute(expr);

    [Fact]
    public void AsInterface_NonObjectJsValue_Throws()
    {
        var n = JsNumber.Create(42);
        Assert.Throws<JsTypeError>(() => n.AsInterface<IFoo>());
    }

    [Fact]
    public void AsInterface_Undefined_Throws()
    {
        Assert.Throws<JsTypeError>(() => JsValue.Undefined.AsInterface<IFoo>());
    }

    [Fact]
    public void AsInterface_PropertyGetter_Number()
    {
        var v = Eval("({ foo: 42 })");
        var foo = v.AsInterface<IFoo>();
        Assert.Equal(42.0, foo.Foo);
    }

    [Fact]
    public void AsInterface_PropertyGetter_String()
    {
        var v = Eval("({ label: 'hello' })");
        var bag = v.AsInterface<IStringBag>();
        Assert.Equal("hello", bag.Label);
    }

    [Fact]
    public void AsInterface_PropertyGetter_Bool()
    {
        var v = Eval("({ flag: true })");
        var b = v.AsInterface<IBoolView>();
        Assert.True(b.Flag);
    }

    [Fact]
    public void AsInterface_PropertySetter_Number_WritesBack()
    {
        var v = Eval("({ foo: 1 })");
        var foo = v.AsInterface<IFoo>();
        foo.Foo = 99;
        Assert.Equal(99.0, foo.Foo);
        // Verify the JS object observed the write.
        var obj = (JsObjectBase)v;
        Assert.Equal(99.0, obj.Get("foo").ToNumber());
    }

    [Fact]
    public void AsInterface_PropertySetter_String_WritesBack()
    {
        var v = Eval("({ label: 'x' })");
        var bag = v.AsInterface<IStringBag>();
        bag.Label = "replaced";
        Assert.Equal("replaced", bag.Label);
    }

    [Fact]
    public void AsInterface_Method_ReturnsPrimitive()
    {
        var v = Eval("({ greet(n) { return 'hi ' + n; } })");
        var g = v.AsInterface<IGreeter>();
        Assert.Equal("hi world", g.Greet("world"));
    }

    [Fact]
    public void AsInterface_Method_PrimitiveArg_Coerces()
    {
        // Numbers that arrive as C# strings convert through ToJsString in the proxy.
        var v = Eval("({ greet(n) { return typeof n + ':' + n; } })");
        var g = v.AsInterface<IGreeter>();
        Assert.Equal("string:42", g.Greet("42"));
    }

    [Fact]
    public void AsInterface_Method_JsObjectBaseArg_PassesThrough()
    {
        var engine = Engine();
        var host = (JsObjectBase)engine.Execute("({ echo(x) { return x; } })");
        var payload = (JsObjectBase)engine.Execute("({ id: 7 })");
        JsValue asJs = host;
        var wrap = asJs.AsInterface<IWithObjectArg>();
        var ret = wrap.Echo(payload);
        Assert.Same(payload, ret);
    }

    [Fact]
    public void AsInterface_NestedIJsType_RecursivelyWraps()
    {
        var v = Eval("({ getFoo() { return { foo: 7 }; } })");
        var n = v.AsInterface<INested>();
        var inner = n.GetFoo();
        Assert.Equal(7.0, inner.Foo);
    }

    [Fact]
    public void AsInterface_MethodNotCallable_Throws()
    {
        var v = Eval("({ greet: 'not a function' })");
        var g = v.AsInterface<IGreeter>();
        Assert.Throws<JsTypeError>(() => g.Greet("x"));
    }

    [Fact]
    public void AsInterface_SameJsValue_SameProxyInstance()
    {
        var v = Eval("({ foo: 1 })");
        var a = v.AsInterface<IFoo>();
        var b = v.AsInterface<IFoo>();
        Assert.Same(a, b);
    }

    [Fact]
    public void AsInterface_JsNameAttribute_OverridesName()
    {
        var v = Eval("({ 'my_prop': 3.14, 'do-the-thing': s => s.toUpperCase() })");
        var c = v.AsInterface<ICustomName>();
        Assert.Equal(3.14, c.MyProp);
        Assert.Equal("HELLO", c.DoThing("hello"));
    }

    [Fact]
    public void AsInterface_BiDirectional_ClassImplementsInterface_FastPath()
    {
        var instance = new DirectFoo { Foo = 123 };
        JsValue asJs = instance;
        var view = asJs.AsInterface<IFoo>();
        Assert.Same(instance, view);
        view.Foo = 456;
        Assert.Equal(456.0, instance.Foo);
    }

    [Fact]
    public void AsInterface_EngineIntegration_RoundTrip()
    {
        var engine = Engine();
        var v = engine.Execute("({ foo: 2.5, greet(n) { return 'hi ' + n; } })");
        Assert.Equal(2.5, v.AsInterface<IFoo>().Foo);
        Assert.Equal("hi bob", v.AsInterface<IGreeter>().Greet("bob"));
    }
}
