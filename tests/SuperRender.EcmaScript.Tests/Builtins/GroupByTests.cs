using SuperRender.EcmaScript.Engine;
using Xunit;

namespace SuperRender.EcmaScript.Tests.Builtins;

public class GroupByTests
{
    private static JsEngine CreateEngine() => new();

    // === Object.groupBy ===

    [Fact]
    public void ObjectGroupBy_GroupsByCallback_ReturnsNullProtoObject()
    {
        var engine = CreateEngine();
        var result = engine.Execute<double>(@"
            const grouped = Object.groupBy([1, 2, 3, 4, 5], v => v % 2 === 0 ? 'even' : 'odd');
            grouped.odd.length;
        ");
        Assert.Equal(3, result);
    }

    [Fact]
    public void ObjectGroupBy_EvenGroup_HasCorrectElements()
    {
        var engine = CreateEngine();
        var result = engine.Execute<double>(@"
            const grouped = Object.groupBy([1, 2, 3, 4, 5], v => v % 2 === 0 ? 'even' : 'odd');
            grouped.even.length;
        ");
        Assert.Equal(2, result);
    }

    [Fact]
    public void ObjectGroupBy_EmptyArray_ReturnsEmptyObject()
    {
        var engine = CreateEngine();
        var result = engine.Execute<string>(@"
            const grouped = Object.groupBy([], v => v);
            JSON.stringify(grouped);
        ");
        Assert.Equal("{}", result);
    }

    [Fact]
    public void ObjectGroupBy_AllSameGroup_SingleGroup()
    {
        var engine = CreateEngine();
        var result = engine.Execute<double>(@"
            const grouped = Object.groupBy([1, 2, 3], () => 'all');
            grouped.all.length;
        ");
        Assert.Equal(3, result);
    }

    [Fact]
    public void ObjectGroupBy_UsesStringKey()
    {
        var engine = CreateEngine();
        var result = engine.Execute<double>(@"
            const grouped = Object.groupBy(['a', 'bb', 'ccc'], s => s.length);
            grouped['1'][0];
        ");
        // Note: key is string "1" since we call .ToJsString() on the callback result
        Assert.Equal("a", engine.Execute<string>(@"
            const g = Object.groupBy(['a', 'bb', 'ccc'], s => s.length);
            g['1'][0];
        "));
    }

    [Fact]
    public void ObjectGroupBy_CallbackReceivesIndex()
    {
        var engine = CreateEngine();
        var result = engine.Execute<string>(@"
            const indices = [];
            Object.groupBy([10, 20, 30], (v, i) => { indices.push(i); return 'g'; });
            indices.join(',');
        ");
        Assert.Equal("0,1,2", result);
    }

    [Fact]
    public void ObjectGroupBy_NonFunctionCallback_ThrowsTypeError()
    {
        var engine = CreateEngine();
        Assert.Throws<SuperRender.EcmaScript.Runtime.Errors.JsTypeError>(() =>
            engine.Execute("Object.groupBy([1, 2], 'not a function')"));
    }

    // === Map.groupBy ===

    [Fact]
    public void MapGroupBy_GroupsByCallback_ReturnsMap()
    {
        var engine = CreateEngine();
        var result = engine.Execute<double>(@"
            const map = Map.groupBy([1, 2, 3, 4], v => v % 2 === 0 ? 'even' : 'odd');
            map.get('odd').length;
        ");
        Assert.Equal(2, result);
    }

    [Fact]
    public void MapGroupBy_EvenGroup_HasCorrectLength()
    {
        var engine = CreateEngine();
        var result = engine.Execute<double>(@"
            const map = Map.groupBy([1, 2, 3, 4], v => v % 2 === 0 ? 'even' : 'odd');
            map.get('even').length;
        ");
        Assert.Equal(2, result);
    }

    [Fact]
    public void MapGroupBy_EmptyArray_ReturnsEmptyMap()
    {
        var engine = CreateEngine();
        var result = engine.Execute<double>(@"
            const map = Map.groupBy([], v => v);
            map.size;
        ");
        Assert.Equal(0, result);
    }

    [Fact]
    public void MapGroupBy_ObjectKeys_PreservesIdentity()
    {
        var engine = CreateEngine();
        var result = engine.Execute<bool>(@"
            const key1 = { type: 'a' };
            const key2 = { type: 'b' };
            const map = Map.groupBy([1, 2, 3], v => v <= 2 ? key1 : key2);
            map.get(key1).length === 2 && map.get(key2).length === 1;
        ");
        Assert.True(result);
    }

    [Fact]
    public void MapGroupBy_Size_MatchesGroupCount()
    {
        var engine = CreateEngine();
        var result = engine.Execute<double>(@"
            const map = Map.groupBy(['a', 'b', 'c', 'd'], s => s < 'c' ? 'low' : 'high');
            map.size;
        ");
        Assert.Equal(2, result);
    }
}
