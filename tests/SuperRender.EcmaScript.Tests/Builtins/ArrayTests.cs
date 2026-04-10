using SuperRender.EcmaScript.Interop;
using SuperRender.EcmaScript.Runtime;
using Xunit;

namespace SuperRender.EcmaScript.Tests.Builtins;

public class ArrayTests
{
    private static JsEngine CreateEngine() => new();

    // ═══════════════════════════════════════════
    //  push / pop / shift / unshift
    // ═══════════════════════════════════════════

    [Fact]
    public void Push_AddsElementToEnd_ReturnsNewLength()
    {
        var engine = CreateEngine();
        var result = engine.Execute<int>("const arr = [1, 2]; arr.push(3)");
        Assert.Equal(3, result);
    }

    [Fact]
    public void Push_MultipleElements_ReturnsNewLength()
    {
        var engine = CreateEngine();
        var result = engine.Execute<int>("const arr = []; arr.push(1, 2, 3)");
        Assert.Equal(3, result);
    }

    [Fact]
    public void Pop_RemovesLastElement_ReturnsIt()
    {
        var engine = CreateEngine();
        var result = engine.Execute<int>("const arr = [1, 2, 3]; arr.pop()");
        Assert.Equal(3, result);
    }

    [Fact]
    public void Pop_EmptyArray_ReturnsUndefined()
    {
        var engine = CreateEngine();
        var result = engine.Execute("[].pop()");
        Assert.Same(JsValue.Undefined, result);
    }

    [Fact]
    public void Shift_RemovesFirstElement_ReturnsIt()
    {
        var engine = CreateEngine();
        var result = engine.Execute<int>("const arr = [10, 20, 30]; arr.shift()");
        Assert.Equal(10, result);
    }

    [Fact]
    public void Shift_EmptyArray_ReturnsUndefined()
    {
        var engine = CreateEngine();
        var result = engine.Execute("[].shift()");
        Assert.Same(JsValue.Undefined, result);
    }

    [Fact]
    public void Unshift_AddsToBeginning_ReturnsNewLength()
    {
        var engine = CreateEngine();
        var result = engine.Execute<int>("const arr = [3, 4]; arr.unshift(1, 2)");
        Assert.Equal(4, result);
    }

    [Fact]
    public void Unshift_MaintainsOrder()
    {
        var engine = CreateEngine();
        var result = engine.Execute<int>("const arr = [3]; arr.unshift(1, 2); arr[0]");
        Assert.Equal(1, result);
    }

    // ═══════════════════════════════════════════
    //  map / filter / reduce / forEach
    // ═══════════════════════════════════════════

    [Fact]
    public void Map_TransformsElements()
    {
        var engine = CreateEngine();
        var result = engine.Execute<int>(@"
            const arr = [1, 2, 3];
            const doubled = arr.map(x => x * 2);
            doubled[1]
        ");
        Assert.Equal(4, result);
    }

    [Fact]
    public void Map_ReturnsNewArrayOfSameLength()
    {
        var engine = CreateEngine();
        var result = engine.Execute<int>("const r = [1, 2, 3].map(x => x + 1); r.length");
        Assert.Equal(3, result);
    }

    [Fact]
    public void Filter_ReturnsMatchingElements()
    {
        var engine = CreateEngine();
        var result = engine.Execute<int>(@"
            const arr = [1, 2, 3, 4, 5];
            const even = arr.filter(x => x % 2 === 0);
            even.length
        ");
        Assert.Equal(2, result);
    }

    [Fact]
    public void Filter_ReturnedArrayHasCorrectValues()
    {
        var engine = CreateEngine();
        var result = engine.Execute<int>("[1, 2, 3, 4].filter(x => x > 2)[0]");
        Assert.Equal(3, result);
    }

    [Fact]
    public void Reduce_SumsElements()
    {
        var engine = CreateEngine();
        var result = engine.Execute<int>("[1, 2, 3, 4].reduce((acc, x) => acc + x, 0)");
        Assert.Equal(10, result);
    }

    [Fact]
    public void Reduce_WithoutInitialValue_UsesFirstElement()
    {
        var engine = CreateEngine();
        var result = engine.Execute<int>("[1, 2, 3].reduce((acc, x) => acc + x)");
        Assert.Equal(6, result);
    }

    [Fact]
    public void ForEach_VisitsAllElements()
    {
        var engine = CreateEngine();
        var result = engine.Execute<int>(@"
            let sum = 0;
            [1, 2, 3].forEach(x => { sum += x; });
            sum
        ");
        Assert.Equal(6, result);
    }

    [Fact]
    public void ForEach_ProvidesIndex()
    {
        var engine = CreateEngine();
        var result = engine.Execute<int>(@"
            let indexSum = 0;
            ['a', 'b', 'c'].forEach((_, i) => { indexSum += i; });
            indexSum
        ");
        Assert.Equal(3, result); // 0 + 1 + 2
    }

    // ═══════════════════════════════════════════
    //  find / findIndex / includes / indexOf
    // ═══════════════════════════════════════════

    [Fact]
    public void Find_ReturnsFirstMatch()
    {
        var engine = CreateEngine();
        var result = engine.Execute<int>("[1, 2, 3, 4].find(x => x > 2)");
        Assert.Equal(3, result);
    }

    [Fact]
    public void Find_NoMatch_ReturnsUndefined()
    {
        var engine = CreateEngine();
        var result = engine.Execute("[1, 2, 3].find(x => x > 10)");
        Assert.Same(JsValue.Undefined, result);
    }

    [Fact]
    public void FindIndex_ReturnsIndexOfFirstMatch()
    {
        var engine = CreateEngine();
        var result = engine.Execute<int>("[10, 20, 30].findIndex(x => x === 20)");
        Assert.Equal(1, result);
    }

    [Fact]
    public void FindIndex_NoMatch_ReturnsNegativeOne()
    {
        var engine = CreateEngine();
        var result = engine.Execute<int>("[1, 2, 3].findIndex(x => x === 99)");
        Assert.Equal(-1, result);
    }

    [Fact]
    public void Includes_ExistingElement_ReturnsTrue()
    {
        var engine = CreateEngine();
        var result = engine.Execute<bool>("[1, 2, 3].includes(2)");
        Assert.True(result);
    }

    [Fact]
    public void Includes_MissingElement_ReturnsFalse()
    {
        var engine = CreateEngine();
        var result = engine.Execute<bool>("[1, 2, 3].includes(5)");
        Assert.False(result);
    }

    [Fact]
    public void IndexOf_ExistingElement_ReturnsIndex()
    {
        var engine = CreateEngine();
        var result = engine.Execute<int>("[10, 20, 30].indexOf(20)");
        Assert.Equal(1, result);
    }

    [Fact]
    public void IndexOf_MissingElement_ReturnsNegativeOne()
    {
        var engine = CreateEngine();
        var result = engine.Execute<int>("[1, 2, 3].indexOf(99)");
        Assert.Equal(-1, result);
    }

    [Fact]
    public void IndexOf_WithFromIndex_SearchesFromThatPosition()
    {
        var engine = CreateEngine();
        var result = engine.Execute<int>("[1, 2, 3, 2, 1].indexOf(2, 2)");
        Assert.Equal(3, result);
    }

    // ═══════════════════════════════════════════
    //  slice / splice / concat / join
    // ═══════════════════════════════════════════

    [Fact]
    public void Slice_ReturnsSubArray()
    {
        var engine = CreateEngine();
        var result = engine.Execute<int>("[1, 2, 3, 4, 5].slice(1, 3).length");
        Assert.Equal(2, result);
    }

    [Fact]
    public void Slice_CorrectElements()
    {
        var engine = CreateEngine();
        var result = engine.Execute<int>("const s = [10, 20, 30, 40].slice(1, 3); s[0]");
        Assert.Equal(20, result);
    }

    [Fact]
    public void Slice_NegativeIndex_CountsFromEnd()
    {
        var engine = CreateEngine();
        var result = engine.Execute<int>("[1, 2, 3, 4, 5].slice(-2).length");
        Assert.Equal(2, result);
    }

    [Fact]
    public void Splice_RemovesElements_ReturnsRemoved()
    {
        var engine = CreateEngine();
        var result = engine.Execute<int>(@"
            const arr = [1, 2, 3, 4, 5];
            const removed = arr.splice(1, 2);
            removed.length
        ");
        Assert.Equal(2, result);
    }

    [Fact]
    public void Splice_InsertsElements()
    {
        var engine = CreateEngine();
        var result = engine.Execute<int>(@"
            const arr = [1, 4, 5];
            arr.splice(1, 0, 2, 3);
            arr.length
        ");
        Assert.Equal(5, result);
    }

    [Fact]
    public void Concat_CombinesArrays()
    {
        var engine = CreateEngine();
        var result = engine.Execute<int>("[1, 2].concat([3, 4]).length");
        Assert.Equal(4, result);
    }

    [Fact]
    public void Concat_DoesNotModifyOriginal()
    {
        var engine = CreateEngine();
        var result = engine.Execute<int>("const a = [1, 2]; a.concat([3]); a.length");
        Assert.Equal(2, result);
    }

    [Fact]
    public void Join_DefaultSeparator()
    {
        var engine = CreateEngine();
        var result = engine.Execute<string>("[1, 2, 3].join()");
        Assert.Equal("1,2,3", result);
    }

    [Fact]
    public void Join_CustomSeparator()
    {
        var engine = CreateEngine();
        var result = engine.Execute<string>("[1, 2, 3].join(' - ')");
        Assert.Equal("1 - 2 - 3", result);
    }

    // ═══════════════════════════════════════════
    //  sort / reverse
    // ═══════════════════════════════════════════

    [Fact]
    public void Sort_DefaultLexicographic()
    {
        var engine = CreateEngine();
        var result = engine.Execute<string>("[3, 1, 2].sort().join(',')");
        Assert.Equal("1,2,3", result);
    }

    [Fact]
    public void Sort_WithComparator_NumericAscending()
    {
        var engine = CreateEngine();
        var result = engine.Execute<int>("[30, 10, 20].sort((a, b) => a - b)[0]");
        Assert.Equal(10, result);
    }

    [Fact]
    public void Sort_WithComparator_NumericDescending()
    {
        var engine = CreateEngine();
        var result = engine.Execute<int>("[10, 30, 20].sort((a, b) => b - a)[0]");
        Assert.Equal(30, result);
    }

    [Fact]
    public void Reverse_ReversesInPlace()
    {
        var engine = CreateEngine();
        var result = engine.Execute<string>("[1, 2, 3].reverse().join(',')");
        Assert.Equal("3,2,1", result);
    }

    // ═══════════════════════════════════════════
    //  flat / flatMap
    // ═══════════════════════════════════════════

    [Fact]
    public void Flat_FlattensOneLevelByDefault()
    {
        var engine = CreateEngine();
        var result = engine.Execute<int>("[[1, 2], [3, 4]].flat().length");
        Assert.Equal(4, result);
    }

    [Fact]
    public void Flat_FlattensToSpecifiedDepth()
    {
        var engine = CreateEngine();
        var result = engine.Execute<int>("[1, [2, [3]]].flat(1).length");
        Assert.Equal(3, result); // [1, 2, [3]]
    }

    [Fact]
    public void Flat_DeepFlatten()
    {
        var engine = CreateEngine();
        var result = engine.Execute<int>("[1, [2, [3, [4]]]].flat(Infinity).length");
        // Infinity depth should flatten completely
        Assert.Equal(4, result);
    }

    [Fact]
    public void FlatMap_MapsAndFlattensOneLevel()
    {
        var engine = CreateEngine();
        var result = engine.Execute<int>("[1, 2, 3].flatMap(x => [x, x * 2]).length");
        Assert.Equal(6, result);
    }

    [Fact]
    public void FlatMap_CorrectValues()
    {
        var engine = CreateEngine();
        var result = engine.Execute<string>("[1, 2].flatMap(x => [x, x * 10]).join(',')");
        Assert.Equal("1,10,2,20", result);
    }

    // ═══════════════════════════════════════════
    //  Array.isArray / Array.from / Array.of
    // ═══════════════════════════════════════════

    [Fact]
    public void IsArray_WithArray_ReturnsTrue()
    {
        var engine = CreateEngine();
        var result = engine.Execute<bool>("Array.isArray([1, 2, 3])");
        Assert.True(result);
    }

    [Fact]
    public void IsArray_WithNonArray_ReturnsFalse()
    {
        var engine = CreateEngine();
        Assert.False(engine.Execute<bool>("Array.isArray('hello')"));
        Assert.False(engine.Execute<bool>("Array.isArray(42)"));
        Assert.False(engine.Execute<bool>("Array.isArray({})"));
    }

    [Fact]
    public void ArrayFrom_String_CreatesCharArray()
    {
        var engine = CreateEngine();
        var result = engine.Execute<int>("Array.from('abc').length");
        Assert.Equal(3, result);
    }

    [Fact]
    public void ArrayFrom_String_CorrectValues()
    {
        var engine = CreateEngine();
        var result = engine.Execute<string>("Array.from('abc')[0]");
        Assert.Equal("a", result);
    }

    [Fact]
    public void ArrayFrom_WithMapFn_TransformsElements()
    {
        var engine = CreateEngine();
        var result = engine.Execute<int>("Array.from([1, 2, 3], x => x * 2)[2]");
        Assert.Equal(6, result);
    }

    [Fact]
    public void ArrayOf_CreatesArrayFromArguments()
    {
        var engine = CreateEngine();
        var result = engine.Execute<int>("Array.of(1, 2, 3).length");
        Assert.Equal(3, result);
    }

    [Fact]
    public void ArrayOf_CorrectElements()
    {
        var engine = CreateEngine();
        var result = engine.Execute<int>("Array.of(10, 20, 30)[1]");
        Assert.Equal(20, result);
    }

    // ═══════════════════════════════════════════
    //  Additional methods: some, every, at
    // ═══════════════════════════════════════════

    [Fact]
    public void Some_ReturnsTrueWhenAnyMatch()
    {
        var engine = CreateEngine();
        var result = engine.Execute<bool>("[1, 2, 3].some(x => x > 2)");
        Assert.True(result);
    }

    [Fact]
    public void Some_ReturnsFalseWhenNoneMatch()
    {
        var engine = CreateEngine();
        var result = engine.Execute<bool>("[1, 2, 3].some(x => x > 10)");
        Assert.False(result);
    }

    [Fact]
    public void Every_ReturnsTrueWhenAllMatch()
    {
        var engine = CreateEngine();
        var result = engine.Execute<bool>("[2, 4, 6].every(x => x % 2 === 0)");
        Assert.True(result);
    }

    [Fact]
    public void Every_ReturnsFalseWhenAnyDoesNotMatch()
    {
        var engine = CreateEngine();
        var result = engine.Execute<bool>("[2, 3, 6].every(x => x % 2 === 0)");
        Assert.False(result);
    }

    [Fact]
    public void ReduceRight_ReducesFromRight()
    {
        var engine = CreateEngine();
        var result = engine.Execute<string>("['a', 'b', 'c'].reduceRight((acc, x) => acc + x, '')");
        Assert.Equal("cba", result);
    }

    [Fact]
    public void LastIndexOf_FindsFromEnd()
    {
        var engine = CreateEngine();
        var result = engine.Execute<int>("[1, 2, 3, 2, 1].lastIndexOf(2)");
        Assert.Equal(3, result);
    }

    [Fact]
    public void Fill_FillsRange()
    {
        var engine = CreateEngine();
        var result = engine.Execute<string>("[1, 2, 3, 4].fill(0, 1, 3).join(',')");
        Assert.Equal("1,0,0,4", result);
    }
}
