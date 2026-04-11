using SuperRender.Document.Html;
using Xunit;

namespace SuperRender.Document.Tests.Html;

public class HtmlEntityTests
{
    // --- Common / Basic entities ---

    [Theory]
    [InlineData("&amp;", "&")]
    [InlineData("&lt;", "<")]
    [InlineData("&gt;", ">")]
    [InlineData("&quot;", "\"")]
    [InlineData("&apos;", "'")]
    [InlineData("&nbsp;", "\u00A0")]
    public void Decode_CommonEntities_ReturnsCorrectCharacter(string input, string expected)
    {
        Assert.Equal(expected, HtmlEntities.Decode(input));
    }

    // --- Extended entities ---

    [Theory]
    [InlineData("&mdash;", "\u2014")]
    [InlineData("&ndash;", "\u2013")]
    [InlineData("&copy;", "\u00A9")]
    [InlineData("&reg;", "\u00AE")]
    [InlineData("&trade;", "\u2122")]
    [InlineData("&hellip;", "\u2026")]
    [InlineData("&laquo;", "\u00AB")]
    [InlineData("&raquo;", "\u00BB")]
    [InlineData("&euro;", "\u20AC")]
    [InlineData("&pound;", "\u00A3")]
    [InlineData("&yen;", "\u00A5")]
    [InlineData("&cent;", "\u00A2")]
    public void Decode_ExtendedEntities_ReturnsCorrectCharacter(string input, string expected)
    {
        Assert.Equal(expected, HtmlEntities.Decode(input));
    }

    // --- Punctuation entities ---

    [Theory]
    [InlineData("&lsquo;", "\u2018")]
    [InlineData("&rsquo;", "\u2019")]
    [InlineData("&ldquo;", "\u201C")]
    [InlineData("&rdquo;", "\u201D")]
    [InlineData("&bull;", "\u2022")]
    [InlineData("&prime;", "\u2032")]
    [InlineData("&Prime;", "\u2033")]
    [InlineData("&lsaquo;", "\u2039")]
    [InlineData("&rsaquo;", "\u203A")]
    public void Decode_PunctuationEntities_ReturnsCorrectCharacter(string input, string expected)
    {
        Assert.Equal(expected, HtmlEntities.Decode(input));
    }

    // --- Mathematical entities ---

    [Theory]
    [InlineData("&forall;", "\u2200")]
    [InlineData("&part;", "\u2202")]
    [InlineData("&exist;", "\u2203")]
    [InlineData("&empty;", "\u2205")]
    [InlineData("&nabla;", "\u2207")]
    [InlineData("&isin;", "\u2208")]
    [InlineData("&notin;", "\u2209")]
    [InlineData("&prod;", "\u220F")]
    [InlineData("&sum;", "\u2211")]
    [InlineData("&minus;", "\u2212")]
    [InlineData("&radic;", "\u221A")]
    [InlineData("&infin;", "\u221E")]
    [InlineData("&int;", "\u222B")]
    public void Decode_MathEntities_ReturnsCorrectCharacter(string input, string expected)
    {
        Assert.Equal(expected, HtmlEntities.Decode(input));
    }

    // --- Greek letter entities ---

    [Theory]
    [InlineData("&Alpha;", "\u0391")]
    [InlineData("&Beta;", "\u0392")]
    [InlineData("&Gamma;", "\u0393")]
    [InlineData("&Delta;", "\u0394")]
    [InlineData("&Omega;", "\u03A9")]
    [InlineData("&alpha;", "\u03B1")]
    [InlineData("&beta;", "\u03B2")]
    [InlineData("&gamma;", "\u03B3")]
    [InlineData("&delta;", "\u03B4")]
    [InlineData("&omega;", "\u03C9")]
    [InlineData("&pi;", "\u03C0")]
    [InlineData("&sigma;", "\u03C3")]
    [InlineData("&theta;", "\u03B8")]
    public void Decode_GreekLetters_ReturnsCorrectCharacter(string input, string expected)
    {
        Assert.Equal(expected, HtmlEntities.Decode(input));
    }

    // --- Arrow entities ---

    [Theory]
    [InlineData("&larr;", "\u2190")]
    [InlineData("&uarr;", "\u2191")]
    [InlineData("&rarr;", "\u2192")]
    [InlineData("&darr;", "\u2193")]
    [InlineData("&harr;", "\u2194")]
    [InlineData("&lArr;", "\u21D0")]
    [InlineData("&rArr;", "\u21D2")]
    [InlineData("&uArr;", "\u21D1")]
    [InlineData("&dArr;", "\u21D3")]
    [InlineData("&hArr;", "\u21D4")]
    public void Decode_ArrowEntities_ReturnsCorrectCharacter(string input, string expected)
    {
        Assert.Equal(expected, HtmlEntities.Decode(input));
    }

    // --- Numeric decimal references ---

    [Theory]
    [InlineData("&#169;", "\u00A9")]   // ©
    [InlineData("&#8364;", "\u20AC")]  // €
    [InlineData("&#65;", "A")]
    [InlineData("&#8212;", "\u2014")]  // em dash
    public void Decode_NumericDecimal_ReturnsCorrectCharacter(string input, string expected)
    {
        Assert.Equal(expected, HtmlEntities.Decode(input));
    }

    // --- Numeric hex references ---

    [Theory]
    [InlineData("&#x00A9;", "\u00A9")]   // ©
    [InlineData("&#x20AC;", "\u20AC")]   // €
    [InlineData("&#x41;", "A")]
    [InlineData("&#x2014;", "\u2014")]   // em dash
    public void Decode_NumericHex_ReturnsCorrectCharacter(string input, string expected)
    {
        Assert.Equal(expected, HtmlEntities.Decode(input));
    }

    // --- Numeric overflow → U+FFFD ---

    [Theory]
    [InlineData("&#x110000;")]   // Just above max Unicode codepoint
    [InlineData("&#x1FFFFF;")]
    [InlineData("&#1114112;")]   // 0x110000 in decimal
    [InlineData("&#99999999;")]
    public void Decode_NumericOverflow_ReturnsReplacementCharacter(string input)
    {
        Assert.Equal("\uFFFD", HtmlEntities.Decode(input));
    }

    // --- Surrogates → U+FFFD ---

    [Theory]
    [InlineData("&#xD800;")]
    [InlineData("&#xDBFF;")]
    [InlineData("&#xDC00;")]
    [InlineData("&#xDFFF;")]
    [InlineData("&#55296;")]  // 0xD800 in decimal
    [InlineData("&#57343;")]  // 0xDFFF in decimal
    public void Decode_Surrogates_ReturnsReplacementCharacter(string input)
    {
        Assert.Equal("\uFFFD", HtmlEntities.Decode(input));
    }

    // --- Null character → U+FFFD ---

    [Theory]
    [InlineData("&#0;")]
    [InlineData("&#x0;")]
    [InlineData("&#x00;")]
    public void Decode_NullCharacter_ReturnsReplacementCharacter(string input)
    {
        Assert.Equal("\uFFFD", HtmlEntities.Decode(input));
    }

    // --- Case sensitivity ---

    [Fact]
    public void Decode_CaseSensitive_AmpVsAMP()
    {
        // Both &amp; and &AMP; are valid WHATWG entities (different entries)
        Assert.Equal("&", HtmlEntities.Decode("&amp;"));
        Assert.Equal("&", HtmlEntities.Decode("&AMP;"));
    }

    [Fact]
    public void Decode_CaseSensitive_DistinguishesAlphaVsAlpha()
    {
        // &Alpha; (capital Greek Alpha) vs &alpha; (lowercase Greek alpha)
        Assert.Equal("\u0391", HtmlEntities.Decode("&Alpha;"));
        Assert.Equal("\u03B1", HtmlEntities.Decode("&alpha;"));
        Assert.NotEqual(
            HtmlEntities.Decode("&Alpha;"),
            HtmlEntities.Decode("&alpha;"));
    }

    [Fact]
    public void Decode_CaseSensitive_UnknownVariantLeftAsIs()
    {
        // &Amp; is NOT a WHATWG entity (only &amp; and &AMP; are)
        Assert.Equal("&Amp;", HtmlEntities.Decode("&Amp;"));
    }

    // --- Entity table count ---

    [Fact]
    public void EntityTable_HasAtLeast2100Entries()
    {
        Assert.True(
            HtmlEntityTable.NamedEntities.Count >= 2100,
            $"Expected at least 2100 entities but found {HtmlEntityTable.NamedEntities.Count}");
    }

    // --- Entities with digits in names ---

    [Theory]
    [InlineData("&frac12;", "\u00BD")]
    [InlineData("&frac14;", "\u00BC")]
    [InlineData("&frac34;", "\u00BE")]
    [InlineData("&sup2;", "\u00B2")]
    [InlineData("&sup3;", "\u00B3")]
    public void Decode_EntitiesWithDigits_ReturnsCorrectCharacter(string input, string expected)
    {
        Assert.Equal(expected, HtmlEntities.Decode(input));
    }

    // --- Mixed content ---

    [Fact]
    public void Decode_MixedContent_DecodesAllEntities()
    {
        var input = "5 &lt; 10 &amp; 10 &gt; 5";
        Assert.Equal("5 < 10 & 10 > 5", HtmlEntities.Decode(input));
    }

    [Fact]
    public void Decode_NoEntities_ReturnsOriginal()
    {
        Assert.Equal("hello world", HtmlEntities.Decode("hello world"));
    }

    [Fact]
    public void Decode_NullOrEmpty_ReturnsOriginal()
    {
        Assert.Equal("", HtmlEntities.Decode(""));
        Assert.Null(HtmlEntities.Decode(null!));
    }

    [Fact]
    public void Decode_UnknownEntity_LeftAsIs()
    {
        Assert.Equal("&notarealentity;", HtmlEntities.Decode("&notarealentity;"));
    }

    // --- Typography entities ---

    [Theory]
    [InlineData("&ensp;", "\u2002")]
    [InlineData("&emsp;", "\u2003")]
    [InlineData("&thinsp;", "\u2009")]
    [InlineData("&zwnj;", "\u200C")]
    [InlineData("&zwj;", "\u200D")]
    [InlineData("&lrm;", "\u200E")]
    [InlineData("&rlm;", "\u200F")]
    public void Decode_TypographyEntities_ReturnsCorrectCharacter(string input, string expected)
    {
        Assert.Equal(expected, HtmlEntities.Decode(input));
    }

    // --- Symbol entities ---

    [Theory]
    [InlineData("&deg;", "\u00B0")]
    [InlineData("&plusmn;", "\u00B1")]
    [InlineData("&micro;", "\u00B5")]
    [InlineData("&para;", "\u00B6")]
    [InlineData("&middot;", "\u00B7")]
    [InlineData("&sect;", "\u00A7")]
    [InlineData("&spades;", "\u2660")]
    [InlineData("&clubs;", "\u2663")]
    [InlineData("&hearts;", "\u2665")]
    [InlineData("&diams;", "\u2666")]
    public void Decode_SymbolEntities_ReturnsCorrectCharacter(string input, string expected)
    {
        Assert.Equal(expected, HtmlEntities.Decode(input));
    }

    // --- Valid codepoints that are non-characters pass through ---

    [Fact]
    public void Decode_ValidHighCodepoint_ReturnsCharacter()
    {
        // U+10000 (Linear B syllable) - valid supplementary plane character
        Assert.Equal("\U00010000", HtmlEntities.Decode("&#x10000;"));
    }

    [Fact]
    public void Decode_MaxValidCodepoint_ReturnsCharacter()
    {
        // U+10FFFF - maximum valid Unicode codepoint
        Assert.Equal("\U0010FFFF", HtmlEntities.Decode("&#x10FFFF;"));
    }
}
