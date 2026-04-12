using SuperRender.EcmaScript.Engine;
using Xunit;

namespace SuperRender.EcmaScript.Tests.Builtins;

public class WeakRefTests
{
    private static JsEngine CreateEngine() => new();

    // === WeakRef ===

    [Fact]
    public void WeakRef_DerefAfterCreate_ReturnsTarget()
    {
        var engine = CreateEngine();
        var result = engine.Execute<double>(@"
            const target = { value: 42 };
            const ref = new WeakRef(target);
            ref.deref().value;
        ");
        Assert.Equal(42, result);
    }

    [Fact]
    public void WeakRef_DerefReturnsSameObject()
    {
        var engine = CreateEngine();
        var result = engine.Execute<bool>(@"
            const target = { x: 1 };
            const ref = new WeakRef(target);
            ref.deref() === target;
        ");
        Assert.True(result);
    }

    [Fact]
    public void WeakRef_NonObjectTarget_ThrowsTypeError()
    {
        var engine = CreateEngine();
        Assert.Throws<SuperRender.EcmaScript.Runtime.Errors.JsTypeError>(() =>
            engine.Execute("new WeakRef(42)"));
    }

    [Fact]
    public void WeakRef_CalledWithoutNew_ThrowsTypeError()
    {
        var engine = CreateEngine();
        Assert.Throws<SuperRender.EcmaScript.Runtime.Errors.JsTypeError>(() =>
            engine.Execute("WeakRef({})"));
    }

    [Fact]
    public void WeakRef_MultipleDeref_ReturnsSame()
    {
        var engine = CreateEngine();
        var result = engine.Execute<bool>(@"
            const obj = { a: 1 };
            const wr = new WeakRef(obj);
            wr.deref() === wr.deref();
        ");
        Assert.True(result);
    }

    // === FinalizationRegistry ===

    [Fact]
    public void FinalizationRegistry_Register_DoesNotThrow()
    {
        var engine = CreateEngine();
        engine.Execute(@"
            const registry = new FinalizationRegistry(heldValue => {});
            const target = {};
            registry.register(target, 'held');
        ");
        // Just verifies no exception
    }

    [Fact]
    public void FinalizationRegistry_NonFunctionCallback_ThrowsTypeError()
    {
        var engine = CreateEngine();
        Assert.Throws<SuperRender.EcmaScript.Runtime.Errors.JsTypeError>(() =>
            engine.Execute("new FinalizationRegistry('not a function')"));
    }

    [Fact]
    public void FinalizationRegistry_RegisterNonObject_ThrowsTypeError()
    {
        var engine = CreateEngine();
        Assert.Throws<SuperRender.EcmaScript.Runtime.Errors.JsTypeError>(() =>
            engine.Execute(@"
                const registry = new FinalizationRegistry(() => {});
                registry.register(42, 'held');
            "));
    }

    [Fact]
    public void FinalizationRegistry_Unregister_WithToken()
    {
        var engine = CreateEngine();
        var result = engine.Execute<bool>(@"
            const registry = new FinalizationRegistry(() => {});
            const target = {};
            const token = {};
            registry.register(target, 'held', token);
            registry.unregister(token);
        ");
        Assert.True(result);
    }

    [Fact]
    public void FinalizationRegistry_UnregisterNoToken_ReturnsFalse()
    {
        var engine = CreateEngine();
        var result = engine.Execute<bool>(@"
            const registry = new FinalizationRegistry(() => {});
            const target = {};
            registry.register(target, 'held');
            const noMatchToken = {};
            registry.unregister(noMatchToken);
        ");
        Assert.False(result);
    }

    [Fact]
    public void FinalizationRegistry_CalledWithoutNew_ThrowsTypeError()
    {
        var engine = CreateEngine();
        Assert.Throws<SuperRender.EcmaScript.Runtime.Errors.JsTypeError>(() =>
            engine.Execute("FinalizationRegistry(() => {})"));
    }
}
