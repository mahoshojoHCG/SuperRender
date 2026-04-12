using SuperRender.EcmaScript.Engine;
using Xunit;

namespace SuperRender.EcmaScript.Tests.Builtins;

public class IntlTests
{
    private static JsEngine CreateEngine() => new();

    [Fact]
    public void Collator_Compare_ReturnsNegativeForLessThan()
    {
        var engine = CreateEngine();
        var result = engine.Execute<double>("const C = Intl.Collator; new C().compare('a', 'b')");
        Assert.True(result < 0);
    }

    [Fact]
    public void Collator_Compare_ReturnsZeroForEqual()
    {
        var engine = CreateEngine();
        var result = engine.Execute<double>("const C = Intl.Collator; new C().compare('abc', 'abc')");
        Assert.Equal(0, result);
    }

    [Fact]
    public void Collator_Compare_ReturnsPositiveForGreaterThan()
    {
        var engine = CreateEngine();
        var result = engine.Execute<double>("const C = Intl.Collator; new C().compare('z', 'a')");
        Assert.True(result > 0);
    }

    [Fact]
    public void NumberFormat_Decimal_FormatsNumber()
    {
        var engine = CreateEngine();
        var result = engine.Execute<string>("const NF = Intl.NumberFormat; new NF('en', { style: 'decimal' }).format(1234.5)");
        Assert.NotNull(result);
        Assert.Contains("1", result);
    }

    [Fact]
    public void NumberFormat_Percent_FormatsAsPercent()
    {
        var engine = CreateEngine();
        var result = engine.Execute<string>("const NF = Intl.NumberFormat; new NF('en', { style: 'percent' }).format(0.5)");
        Assert.NotNull(result);
        Assert.Contains("50", result);
    }

    [Fact]
    public void DateTimeFormat_Format_ReturnsString()
    {
        var engine = CreateEngine();
        var result = engine.Execute<string>("const DTF = Intl.DateTimeFormat; new DTF('en').format(new Date())");
        Assert.NotNull(result);
        Assert.True(result.Length > 0);
    }

    [Fact]
    public void PluralRules_Select_ReturnsOneForOne()
    {
        var engine = CreateEngine();
        var result = engine.Execute<string>("const PR = Intl.PluralRules; new PR('en').select(1)");
        Assert.Equal("one", result);
    }

    [Fact]
    public void PluralRules_Select_ReturnsOtherForTwo()
    {
        var engine = CreateEngine();
        var result = engine.Execute<string>("const PR = Intl.PluralRules; new PR('en').select(2)");
        Assert.Equal("other", result);
    }

    [Fact]
    public void PluralRules_Select_ReturnsOtherForZero()
    {
        var engine = CreateEngine();
        var result = engine.Execute<string>("const PR = Intl.PluralRules; new PR('en').select(0)");
        Assert.Equal("other", result);
    }

    [Fact]
    public void Collator_SensitivityBase_IgnoresCase()
    {
        var engine = CreateEngine();
        var result = engine.Execute<double>(@"
            const Col = Intl.Collator;
            const col = new Col('en', { sensitivity: 'base' });
            col.compare('A', 'a');
        ");
        Assert.Equal(0, result);
    }
}
