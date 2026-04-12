using SuperRender.EcmaScript.Engine;
using Xunit;

namespace SuperRender.EcmaScript.Tests.Builtins;

public class StructuredCloneTests
{
    private static JsEngine CreateEngine() => new();

    [Fact]
    public void StructuredClone_Primitive_ReturnsIdentical()
    {
        var engine = CreateEngine();
        Assert.Equal(42.0, engine.Execute<double>("structuredClone(42)"));
        Assert.Equal("hello", engine.Execute<string>("structuredClone('hello')"));
        Assert.True(engine.Execute<bool>("structuredClone(true)"));
        Assert.True(engine.Execute<bool>("structuredClone(null) === null"));
        Assert.True(engine.Execute<bool>("structuredClone(undefined) === undefined"));
    }

    [Fact]
    public void StructuredClone_PlainObject_DeepClones()
    {
        var engine = CreateEngine();
        var result = engine.Execute<bool>(@"
            const original = { a: 1, b: { c: 2 } };
            const clone = structuredClone(original);
            clone.a === 1 && clone.b.c === 2 && clone !== original && clone.b !== original.b;
        ");
        Assert.True(result);
    }

    [Fact]
    public void StructuredClone_Array_DeepClones()
    {
        var engine = CreateEngine();
        var result = engine.Execute<bool>(@"
            const original = [1, [2, 3], 4];
            const clone = structuredClone(original);
            clone[0] === 1 && clone[1][0] === 2 && clone !== original && clone[1] !== original[1];
        ");
        Assert.True(result);
    }

    [Fact]
    public void StructuredClone_NestedObjects_IndependentCopies()
    {
        var engine = CreateEngine();
        var result = engine.Execute<bool>(@"
            const original = { nested: { value: 10 } };
            const clone = structuredClone(original);
            clone.nested.value = 20;
            original.nested.value === 10;
        ");
        Assert.True(result);
    }

    [Fact]
    public void StructuredClone_CircularReference_Handled()
    {
        var engine = CreateEngine();
        var result = engine.Execute<bool>(@"
            const obj = { a: 1 };
            obj.self = obj;
            const clone = structuredClone(obj);
            clone.self === clone && clone !== obj && clone.a === 1;
        ");
        Assert.True(result);
    }

    [Fact]
    public void StructuredClone_Function_ThrowsError()
    {
        var engine = CreateEngine();
        Assert.Throws<SuperRender.EcmaScript.Runtime.Errors.JsTypeError>(() =>
            engine.Execute("structuredClone(() => {})"));
    }

    [Fact]
    public void StructuredClone_Set_ClonesValues()
    {
        var engine = CreateEngine();
        var result = engine.Execute<bool>(@"
            const original = new Set([1, 2, 3]);
            const clone = structuredClone(original);
            clone instanceof Set && clone.size === 3 && clone !== original;
        ");
        Assert.True(result);
    }

    [Fact]
    public void StructuredClone_Map_ClonesEntries()
    {
        var engine = CreateEngine();
        var result = engine.Execute<bool>(@"
            const original = new Map([['a', 1], ['b', 2]]);
            const clone = structuredClone(original);
            clone instanceof Map && clone.size === 2 && clone.get('a') === 1 && clone !== original;
        ");
        Assert.True(result);
    }

    [Fact]
    public void StructuredClone_EmptyObject_ReturnsEmptyClone()
    {
        var engine = CreateEngine();
        var result = engine.Execute<bool>(@"
            const clone = structuredClone({});
            typeof clone === 'object' && clone !== null;
        ");
        Assert.True(result);
    }

    [Fact]
    public void StructuredClone_ArrayWithObjects_DeepClones()
    {
        var engine = CreateEngine();
        var result = engine.Execute<bool>(@"
            const original = [{ x: 1 }, { x: 2 }];
            const clone = structuredClone(original);
            clone[0].x === 1 && clone[0] !== original[0];
        ");
        Assert.True(result);
    }
}
