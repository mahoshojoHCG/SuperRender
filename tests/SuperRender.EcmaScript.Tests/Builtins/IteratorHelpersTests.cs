using SuperRender.EcmaScript.Engine;
using Xunit;

namespace SuperRender.EcmaScript.Tests.Builtins;

public class IteratorHelpersTests
{
    private static JsEngine CreateEngine() => new();

    // === map ===

    [Fact]
    public void Map_TransformsElements()
    {
        var engine = CreateEngine();
        var result = engine.Execute<string>(@"
            Iterator.from([1, 2, 3]).map(x => x * 2).toArray().join(',');
        ");
        Assert.Equal("2,4,6", result);
    }

    [Fact]
    public void Map_IsLazy_DoesNotConsumeEagerly()
    {
        var engine = CreateEngine();
        var result = engine.Execute<double>(@"
            let calls = 0;
            const iter = Iterator.from([1, 2, 3, 4, 5]).map(x => { calls++; return x * 2; });
            iter.next();
            iter.next();
            calls;
        ");
        Assert.Equal(2, result);
    }

    [Fact]
    public void Map_ReceivesIndex()
    {
        var engine = CreateEngine();
        var result = engine.Execute<string>(@"
            Iterator.from(['a', 'b', 'c']).map((v, i) => i + ':' + v).toArray().join(',');
        ");
        Assert.Equal("0:a,1:b,2:c", result);
    }

    // === filter ===

    [Fact]
    public void Filter_SelectsMatching()
    {
        var engine = CreateEngine();
        var result = engine.Execute<string>(@"
            Iterator.from([1, 2, 3, 4, 5]).filter(x => x % 2 === 0).toArray().join(',');
        ");
        Assert.Equal("2,4", result);
    }

    [Fact]
    public void Filter_NoMatches_ReturnsEmpty()
    {
        var engine = CreateEngine();
        var result = engine.Execute<double>(@"
            Iterator.from([1, 2, 3]).filter(x => x > 10).toArray().length;
        ");
        Assert.Equal(0, result);
    }

    // === take ===

    [Fact]
    public void Take_LimitsCount()
    {
        var engine = CreateEngine();
        var result = engine.Execute<string>(@"
            Iterator.from([1, 2, 3, 4, 5]).take(3).toArray().join(',');
        ");
        Assert.Equal("1,2,3", result);
    }

    [Fact]
    public void Take_Zero_ReturnsEmpty()
    {
        var engine = CreateEngine();
        var result = engine.Execute<double>(@"
            Iterator.from([1, 2, 3]).take(0).toArray().length;
        ");
        Assert.Equal(0, result);
    }

    [Fact]
    public void Take_MoreThanAvailable_ReturnsAll()
    {
        var engine = CreateEngine();
        var result = engine.Execute<double>(@"
            Iterator.from([1, 2]).take(10).toArray().length;
        ");
        Assert.Equal(2, result);
    }

    // === drop ===

    [Fact]
    public void Drop_SkipsCount()
    {
        var engine = CreateEngine();
        var result = engine.Execute<string>(@"
            Iterator.from([1, 2, 3, 4, 5]).drop(2).toArray().join(',');
        ");
        Assert.Equal("3,4,5", result);
    }

    [Fact]
    public void Drop_Zero_ReturnsAll()
    {
        var engine = CreateEngine();
        var result = engine.Execute<double>(@"
            Iterator.from([1, 2, 3]).drop(0).toArray().length;
        ");
        Assert.Equal(3, result);
    }

    [Fact]
    public void Drop_MoreThanAvailable_ReturnsEmpty()
    {
        var engine = CreateEngine();
        var result = engine.Execute<double>(@"
            Iterator.from([1, 2]).drop(10).toArray().length;
        ");
        Assert.Equal(0, result);
    }

    // === flatMap ===

    [Fact]
    public void FlatMap_FlattensOneLevel()
    {
        var engine = CreateEngine();
        var result = engine.Execute<string>(@"
            Iterator.from([1, 2, 3]).flatMap(x => [x, x * 10]).toArray().join(',');
        ");
        Assert.Equal("1,10,2,20,3,30", result);
    }

    [Fact]
    public void FlatMap_NonIterablePassedThrough()
    {
        var engine = CreateEngine();
        var result = engine.Execute<string>(@"
            Iterator.from([1, 2, 3]).flatMap(x => x * 2).toArray().join(',');
        ");
        Assert.Equal("2,4,6", result);
    }

    // === reduce ===

    [Fact]
    public void Reduce_AccumulatesValue()
    {
        var engine = CreateEngine();
        var result = engine.Execute<double>(@"
            Iterator.from([1, 2, 3, 4]).reduce((acc, v) => acc + v, 0);
        ");
        Assert.Equal(10, result);
    }

    [Fact]
    public void Reduce_WithoutInitial_UsesFirstElement()
    {
        var engine = CreateEngine();
        var result = engine.Execute<double>(@"
            Iterator.from([1, 2, 3]).reduce((acc, v) => acc + v);
        ");
        Assert.Equal(6, result);
    }

    [Fact]
    public void Reduce_EmptyWithNoInitial_ThrowsTypeError()
    {
        var engine = CreateEngine();
        Assert.Throws<SuperRender.EcmaScript.Runtime.Errors.JsTypeError>(() =>
            engine.Execute("Iterator.from([]).reduce((acc, v) => acc + v)"));
    }

    // === toArray ===

    [Fact]
    public void ToArray_CollectsAll()
    {
        var engine = CreateEngine();
        var result = engine.Execute<double>(@"
            Iterator.from([1, 2, 3]).toArray().length;
        ");
        Assert.Equal(3, result);
    }

    // === forEach ===

    [Fact]
    public void ForEach_CallsForEachElement()
    {
        var engine = CreateEngine();
        var result = engine.Execute<double>(@"
            let sum = 0;
            Iterator.from([1, 2, 3]).forEach(v => { sum += v; });
            sum;
        ");
        Assert.Equal(6, result);
    }

    // === some ===

    [Fact]
    public void Some_MatchExists_ReturnsTrue()
    {
        var engine = CreateEngine();
        var result = engine.Execute<bool>(@"
            Iterator.from([1, 2, 3, 4]).some(x => x > 3);
        ");
        Assert.True(result);
    }

    [Fact]
    public void Some_NoMatch_ReturnsFalse()
    {
        var engine = CreateEngine();
        var result = engine.Execute<bool>(@"
            Iterator.from([1, 2, 3]).some(x => x > 10);
        ");
        Assert.False(result);
    }

    [Fact]
    public void Some_ShortCircuits()
    {
        var engine = CreateEngine();
        var result = engine.Execute<double>(@"
            let calls = 0;
            Iterator.from([1, 2, 3, 4, 5]).some(x => { calls++; return x === 2; });
            calls;
        ");
        Assert.Equal(2, result);
    }

    // === every ===

    [Fact]
    public void Every_AllMatch_ReturnsTrue()
    {
        var engine = CreateEngine();
        var result = engine.Execute<bool>(@"
            Iterator.from([2, 4, 6]).every(x => x % 2 === 0);
        ");
        Assert.True(result);
    }

    [Fact]
    public void Every_OneDoesNotMatch_ReturnsFalse()
    {
        var engine = CreateEngine();
        var result = engine.Execute<bool>(@"
            Iterator.from([2, 3, 6]).every(x => x % 2 === 0);
        ");
        Assert.False(result);
    }

    [Fact]
    public void Every_ShortCircuitsOnFalse()
    {
        var engine = CreateEngine();
        var result = engine.Execute<double>(@"
            let calls = 0;
            Iterator.from([2, 4, 5, 6, 8]).every(x => { calls++; return x % 2 === 0; });
            calls;
        ");
        Assert.Equal(3, result);
    }

    // === find ===

    [Fact]
    public void Find_ReturnsFirstMatch()
    {
        var engine = CreateEngine();
        var result = engine.Execute<double>(@"
            Iterator.from([1, 2, 3, 4, 5]).find(x => x > 3);
        ");
        Assert.Equal(4, result);
    }

    [Fact]
    public void Find_NoMatch_ReturnsUndefined()
    {
        var engine = CreateEngine();
        var result = engine.Execute<bool>(@"
            Iterator.from([1, 2, 3]).find(x => x > 10) === undefined;
        ");
        Assert.True(result);
    }

    // === Iterator.from ===

    [Fact]
    public void From_WrapsIterable()
    {
        var engine = CreateEngine();
        var result = engine.Execute<string>(@"
            Iterator.from([1, 2, 3]).toArray().join(',');
        ");
        Assert.Equal("1,2,3", result);
    }

    [Fact]
    public void From_NonIterable_ThrowsTypeError()
    {
        var engine = CreateEngine();
        Assert.Throws<SuperRender.EcmaScript.Runtime.Errors.JsTypeError>(() =>
            engine.Execute("Iterator.from(42)"));
    }

    // === chaining ===

    [Fact]
    public void Chaining_MapFilter_Works()
    {
        var engine = CreateEngine();
        var result = engine.Execute<string>(@"
            Iterator.from([1, 2, 3, 4, 5])
                .map(x => x * 2)
                .filter(x => x > 4)
                .toArray()
                .join(',');
        ");
        Assert.Equal("6,8,10", result);
    }

    [Fact]
    public void Chaining_FilterTake_Works()
    {
        var engine = CreateEngine();
        var result = engine.Execute<string>(@"
            Iterator.from([1, 2, 3, 4, 5, 6, 7, 8, 9, 10])
                .filter(x => x % 2 === 0)
                .take(3)
                .toArray()
                .join(',');
        ");
        Assert.Equal("2,4,6", result);
    }

    [Fact]
    public void Chaining_DropMap_Works()
    {
        var engine = CreateEngine();
        var result = engine.Execute<string>(@"
            Iterator.from([1, 2, 3, 4, 5])
                .drop(2)
                .map(x => x * 10)
                .toArray()
                .join(',');
        ");
        Assert.Equal("30,40,50", result);
    }

    // === generators ===

    [Fact]
    public void Generator_MapToArray_Works()
    {
        var engine = CreateEngine();
        var result = engine.Execute<string>(@"
            function* gen() { yield 1; yield 2; yield 3; }
            gen().map(x => x * 2).toArray().join(',');
        ");
        Assert.Equal("2,4,6", result);
    }

    [Fact]
    public void Generator_FilterReduce_Works()
    {
        var engine = CreateEngine();
        var result = engine.Execute<double>(@"
            function* range(n) { for (let i = 0; i < n; i++) yield i; }
            range(10).filter(x => x % 2 === 0).reduce((acc, v) => acc + v, 0);
        ");
        Assert.Equal(20, result);
    }
}
