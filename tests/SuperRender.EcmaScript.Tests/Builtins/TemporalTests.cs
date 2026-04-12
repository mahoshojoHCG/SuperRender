using SuperRender.EcmaScript.Engine;
using Xunit;

namespace SuperRender.EcmaScript.Tests.Builtins;

public class TemporalTests
{
    private static JsEngine CreateEngine() => new();

    [Fact]
    public void PlainDate_From_ParsesString()
    {
        var engine = CreateEngine();
        var result = engine.Execute<string>("Temporal.PlainDate.from('2024-06-01').toString()");
        Assert.Equal("2024-06-01", result);
    }

    [Fact]
    public void PlainDate_Constructor_CreatesDate()
    {
        var engine = CreateEngine();
        var result = engine.Execute<string>("const PD = Temporal.PlainDate; new PD(2024, 3, 15).toString()");
        Assert.Equal("2024-03-15", result);
    }

    [Fact]
    public void PlainDate_Year_ReturnsYear()
    {
        var engine = CreateEngine();
        var result = engine.Execute<double>("const PD = Temporal.PlainDate; new PD(2024, 1, 1).year");
        Assert.Equal(2024, result);
    }

    [Fact]
    public void PlainDate_Add_AddsDays()
    {
        var engine = CreateEngine();
        var result = engine.Execute<string>(@"
            const PD = Temporal.PlainDate;
            const date = new PD(2024, 1, 1);
            date.add({ years: 0, months: 0, days: 10 }).toString();
        ");
        Assert.Equal("2024-01-11", result);
    }

    [Fact]
    public void PlainDate_Equals_ReturnsTrueForSame()
    {
        var engine = CreateEngine();
        var result = engine.Execute<bool>(@"
            const PD = Temporal.PlainDate;
            const a = new PD(2024, 6, 15);
            const b = Temporal.PlainDate.from('2024-06-15');
            a.equals(b);
        ");
        Assert.True(result);
    }

    [Fact]
    public void PlainTime_Constructor_CreatesTime()
    {
        var engine = CreateEngine();
        var result = engine.Execute<string>("const PT = Temporal.PlainTime; new PT(14, 30, 0).toString()");
        Assert.Equal("14:30:00", result);
    }

    [Fact]
    public void PlainTime_Hour_ReturnsHour()
    {
        var engine = CreateEngine();
        var result = engine.Execute<double>("const PT = Temporal.PlainTime; new PT(10, 20, 30).hour");
        Assert.Equal(10, result);
    }

    [Fact]
    public void PlainDateTime_From_ParsesString()
    {
        var engine = CreateEngine();
        var result = engine.Execute<string>("Temporal.PlainDateTime.from('2024-03-15T10:30:00').toString()");
        Assert.Equal("2024-03-15T10:30:00", result);
    }

    [Fact]
    public void Instant_FromEpochMilliseconds_CreatesInstant()
    {
        var engine = CreateEngine();
        var result = engine.Execute<double>("Temporal.Instant.fromEpochMilliseconds(0).epochMilliseconds");
        Assert.Equal(0, result);
    }

    [Fact]
    public void Duration_From_ParsesIsoDuration()
    {
        var engine = CreateEngine();
        var result = engine.Execute<double>("Temporal.Duration.from('P1Y2M3D').years");
        Assert.Equal(1, result);
    }

    [Fact]
    public void Now_PlainDateISO_ReturnsCurrentDate()
    {
        var engine = CreateEngine();
        var result = engine.Execute<double>("Temporal.Now.plainDateISO().year");
        Assert.True(result >= 2024);
    }
}
