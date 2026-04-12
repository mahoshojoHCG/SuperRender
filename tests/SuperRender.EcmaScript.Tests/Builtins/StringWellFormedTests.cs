using SuperRender.EcmaScript.Engine;
using Xunit;

namespace SuperRender.EcmaScript.Tests.Builtins;

public class StringWellFormedTests
{
    private static JsEngine CreateEngine() => new();

    // === isWellFormed ===

    [Fact]
    public void IsWellFormed_AsciiString_ReturnsTrue()
    {
        var engine = CreateEngine();
        var result = engine.Execute<bool>("'hello world'.isWellFormed()");
        Assert.True(result);
    }

    [Fact]
    public void IsWellFormed_EmptyString_ReturnsTrue()
    {
        var engine = CreateEngine();
        var result = engine.Execute<bool>("''.isWellFormed()");
        Assert.True(result);
    }

    [Fact]
    public void IsWellFormed_ValidSurrogatePair_ReturnsTrue()
    {
        var engine = CreateEngine();
        // \uD83D\uDE00 is the surrogate pair for emoji U+1F600
        var result = engine.Execute<bool>(@"'\uD83D\uDE00'.isWellFormed()");
        Assert.True(result);
    }

    [Fact]
    public void IsWellFormed_LoneHighSurrogate_ReturnsFalse()
    {
        var engine = CreateEngine();
        var result = engine.Execute<bool>(@"'\uD800'.isWellFormed()");
        Assert.False(result);
    }

    [Fact]
    public void IsWellFormed_LoneLowSurrogate_ReturnsFalse()
    {
        var engine = CreateEngine();
        var result = engine.Execute<bool>(@"'\uDC00'.isWellFormed()");
        Assert.False(result);
    }

    [Fact]
    public void IsWellFormed_HighSurrogateAtEnd_ReturnsFalse()
    {
        var engine = CreateEngine();
        var result = engine.Execute<bool>(@"'abc\uD800'.isWellFormed()");
        Assert.False(result);
    }

    [Fact]
    public void IsWellFormed_TwoHighSurrogates_ReturnsFalse()
    {
        var engine = CreateEngine();
        var result = engine.Execute<bool>(@"'\uD800\uD801'.isWellFormed()");
        Assert.False(result);
    }

    // === toWellFormed ===

    [Fact]
    public void ToWellFormed_AsciiString_ReturnsSame()
    {
        var engine = CreateEngine();
        var result = engine.Execute<string>("'hello'.toWellFormed()");
        Assert.Equal("hello", result);
    }

    [Fact]
    public void ToWellFormed_ValidSurrogatePair_ReturnsSame()
    {
        var engine = CreateEngine();
        var result = engine.Execute<bool>(@"'\uD83D\uDE00'.toWellFormed() === '\uD83D\uDE00'");
        Assert.True(result);
    }

    [Fact]
    public void ToWellFormed_LoneHighSurrogate_ReplacesWithFFFD()
    {
        var engine = CreateEngine();
        var result = engine.Execute<bool>(@"'\uD800'.toWellFormed() === '\uFFFD'");
        Assert.True(result);
    }

    [Fact]
    public void ToWellFormed_LoneLowSurrogate_ReplacesWithFFFD()
    {
        var engine = CreateEngine();
        var result = engine.Execute<bool>(@"'\uDC00'.toWellFormed() === '\uFFFD'");
        Assert.True(result);
    }

    [Fact]
    public void ToWellFormed_MixedContent_ReplacesOnlyLoneSurrogates()
    {
        var engine = CreateEngine();
        var result = engine.Execute<double>(@"'abc\uD800def'.toWellFormed().length");
        Assert.Equal(7, result);
    }

    [Fact]
    public void ToWellFormed_EmptyString_ReturnsEmpty()
    {
        var engine = CreateEngine();
        var result = engine.Execute<string>("''.toWellFormed()");
        Assert.Equal("", result);
    }
}
