using SuperRender.EcmaScript.Engine;
using Xunit;

namespace SuperRender.EcmaScript.Tests.Builtins;

public class SetMethodsTests
{
    private static JsEngine CreateEngine() => new();

    // === union ===

    [Fact]
    public void Union_TwoSets_ContainsAllElements()
    {
        var engine = CreateEngine();
        var result = engine.Execute<double>(@"
            const a = new Set([1, 2, 3]);
            const b = new Set([3, 4, 5]);
            a.union(b).size;
        ");
        Assert.Equal(5, result);
    }

    [Fact]
    public void Union_DisjointSets_ContainsAll()
    {
        var engine = CreateEngine();
        var result = engine.Execute<double>(@"
            const a = new Set([1, 2]);
            const b = new Set([3, 4]);
            a.union(b).size;
        ");
        Assert.Equal(4, result);
    }

    [Fact]
    public void Union_EmptySet_ReturnsOther()
    {
        var engine = CreateEngine();
        var result = engine.Execute<double>(@"
            const a = new Set();
            const b = new Set([1, 2, 3]);
            a.union(b).size;
        ");
        Assert.Equal(3, result);
    }

    [Fact]
    public void Union_IdenticalSets_NoDuplicates()
    {
        var engine = CreateEngine();
        var result = engine.Execute<double>(@"
            const a = new Set([1, 2, 3]);
            const b = new Set([1, 2, 3]);
            a.union(b).size;
        ");
        Assert.Equal(3, result);
    }

    // === intersection ===

    [Fact]
    public void Intersection_OverlappingSets_ReturnsShared()
    {
        var engine = CreateEngine();
        var result = engine.Execute<double>(@"
            const a = new Set([1, 2, 3, 4]);
            const b = new Set([3, 4, 5, 6]);
            a.intersection(b).size;
        ");
        Assert.Equal(2, result);
    }

    [Fact]
    public void Intersection_DisjointSets_ReturnsEmpty()
    {
        var engine = CreateEngine();
        var result = engine.Execute<double>(@"
            const a = new Set([1, 2]);
            const b = new Set([3, 4]);
            a.intersection(b).size;
        ");
        Assert.Equal(0, result);
    }

    [Fact]
    public void Intersection_ContainsCorrectValues()
    {
        var engine = CreateEngine();
        var result = engine.Execute<bool>(@"
            const a = new Set([1, 2, 3]);
            const b = new Set([2, 3, 4]);
            const result = a.intersection(b);
            result.has(2) && result.has(3) && !result.has(1) && !result.has(4);
        ");
        Assert.True(result);
    }

    // === difference ===

    [Fact]
    public void Difference_OverlappingSets_ReturnsOnlyFirst()
    {
        var engine = CreateEngine();
        var result = engine.Execute<double>(@"
            const a = new Set([1, 2, 3, 4]);
            const b = new Set([3, 4, 5]);
            a.difference(b).size;
        ");
        Assert.Equal(2, result);
    }

    [Fact]
    public void Difference_ContainsCorrectValues()
    {
        var engine = CreateEngine();
        var result = engine.Execute<bool>(@"
            const a = new Set([1, 2, 3]);
            const b = new Set([2, 3]);
            const result = a.difference(b);
            result.has(1) && !result.has(2) && !result.has(3);
        ");
        Assert.True(result);
    }

    [Fact]
    public void Difference_SubsetRemoved_ReturnsEmpty()
    {
        var engine = CreateEngine();
        var result = engine.Execute<double>(@"
            const a = new Set([1, 2]);
            const b = new Set([1, 2, 3]);
            a.difference(b).size;
        ");
        Assert.Equal(0, result);
    }

    // === symmetricDifference ===

    [Fact]
    public void SymmetricDifference_OverlappingSets_ReturnsExclusive()
    {
        var engine = CreateEngine();
        var result = engine.Execute<double>(@"
            const a = new Set([1, 2, 3]);
            const b = new Set([2, 3, 4]);
            a.symmetricDifference(b).size;
        ");
        Assert.Equal(2, result);
    }

    [Fact]
    public void SymmetricDifference_ContainsCorrectValues()
    {
        var engine = CreateEngine();
        var result = engine.Execute<bool>(@"
            const a = new Set([1, 2, 3]);
            const b = new Set([3, 4, 5]);
            const result = a.symmetricDifference(b);
            result.has(1) && result.has(2) && result.has(4) && result.has(5) && !result.has(3);
        ");
        Assert.True(result);
    }

    [Fact]
    public void SymmetricDifference_DisjointSets_ReturnsAll()
    {
        var engine = CreateEngine();
        var result = engine.Execute<double>(@"
            const a = new Set([1, 2]);
            const b = new Set([3, 4]);
            a.symmetricDifference(b).size;
        ");
        Assert.Equal(4, result);
    }

    [Fact]
    public void SymmetricDifference_IdenticalSets_ReturnsEmpty()
    {
        var engine = CreateEngine();
        var result = engine.Execute<double>(@"
            const a = new Set([1, 2, 3]);
            const b = new Set([1, 2, 3]);
            a.symmetricDifference(b).size;
        ");
        Assert.Equal(0, result);
    }

    // === isSubsetOf ===

    [Fact]
    public void IsSubsetOf_Subset_ReturnsTrue()
    {
        var engine = CreateEngine();
        var result = engine.Execute<bool>(@"
            const a = new Set([1, 2]);
            const b = new Set([1, 2, 3, 4]);
            a.isSubsetOf(b);
        ");
        Assert.True(result);
    }

    [Fact]
    public void IsSubsetOf_NotSubset_ReturnsFalse()
    {
        var engine = CreateEngine();
        var result = engine.Execute<bool>(@"
            const a = new Set([1, 2, 5]);
            const b = new Set([1, 2, 3]);
            a.isSubsetOf(b);
        ");
        Assert.False(result);
    }

    [Fact]
    public void IsSubsetOf_EmptySetIsSubset()
    {
        var engine = CreateEngine();
        var result = engine.Execute<bool>(@"
            const a = new Set();
            const b = new Set([1, 2, 3]);
            a.isSubsetOf(b);
        ");
        Assert.True(result);
    }

    [Fact]
    public void IsSubsetOf_EqualSets_ReturnsTrue()
    {
        var engine = CreateEngine();
        var result = engine.Execute<bool>(@"
            const a = new Set([1, 2, 3]);
            const b = new Set([1, 2, 3]);
            a.isSubsetOf(b);
        ");
        Assert.True(result);
    }

    // === isSupersetOf ===

    [Fact]
    public void IsSupersetOf_Superset_ReturnsTrue()
    {
        var engine = CreateEngine();
        var result = engine.Execute<bool>(@"
            const a = new Set([1, 2, 3, 4]);
            const b = new Set([1, 2]);
            a.isSupersetOf(b);
        ");
        Assert.True(result);
    }

    [Fact]
    public void IsSupersetOf_NotSuperset_ReturnsFalse()
    {
        var engine = CreateEngine();
        var result = engine.Execute<bool>(@"
            const a = new Set([1, 2]);
            const b = new Set([1, 2, 3]);
            a.isSupersetOf(b);
        ");
        Assert.False(result);
    }

    [Fact]
    public void IsSupersetOf_EmptyOther_ReturnsTrue()
    {
        var engine = CreateEngine();
        var result = engine.Execute<bool>(@"
            const a = new Set([1, 2, 3]);
            const b = new Set();
            a.isSupersetOf(b);
        ");
        Assert.True(result);
    }

    // === isDisjointFrom ===

    [Fact]
    public void IsDisjointFrom_NoOverlap_ReturnsTrue()
    {
        var engine = CreateEngine();
        var result = engine.Execute<bool>(@"
            const a = new Set([1, 2]);
            const b = new Set([3, 4]);
            a.isDisjointFrom(b);
        ");
        Assert.True(result);
    }

    [Fact]
    public void IsDisjointFrom_WithOverlap_ReturnsFalse()
    {
        var engine = CreateEngine();
        var result = engine.Execute<bool>(@"
            const a = new Set([1, 2, 3]);
            const b = new Set([3, 4, 5]);
            a.isDisjointFrom(b);
        ");
        Assert.False(result);
    }

    [Fact]
    public void IsDisjointFrom_EmptySets_ReturnsTrue()
    {
        var engine = CreateEngine();
        var result = engine.Execute<bool>(@"
            const a = new Set();
            const b = new Set();
            a.isDisjointFrom(b);
        ");
        Assert.True(result);
    }

    [Fact]
    public void IsDisjointFrom_OneEmpty_ReturnsTrue()
    {
        var engine = CreateEngine();
        var result = engine.Execute<bool>(@"
            const a = new Set([1, 2, 3]);
            const b = new Set();
            a.isDisjointFrom(b);
        ");
        Assert.True(result);
    }
}
