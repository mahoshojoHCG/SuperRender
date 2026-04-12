using SuperRender.Document.Css;
using Xunit;

namespace SuperRender.Document.Tests.Css;

public class AtRuleParserTests
{
    #region @import

    [Fact]
    public void Import_StringUrl_Parsed()
    {
        var stylesheet = new CssParser("@import \"styles.css\";").Parse();
        Assert.Single(stylesheet.AtRules);
        var rule = Assert.IsType<CssImportRule>(stylesheet.AtRules[0]);
        Assert.Equal("styles.css", rule.Url);
    }

    [Fact]
    public void Import_UrlFunction_Parsed()
    {
        var stylesheet = new CssParser("@import url(\"reset.css\");").Parse();
        Assert.Single(stylesheet.AtRules);
        var rule = Assert.IsType<CssImportRule>(stylesheet.AtRules[0]);
        Assert.Equal("reset.css", rule.Url);
    }

    [Fact]
    public void Import_SingleQuoteUrl_Parsed()
    {
        var stylesheet = new CssParser("@import 'base.css';").Parse();
        Assert.Single(stylesheet.AtRules);
        var rule = Assert.IsType<CssImportRule>(stylesheet.AtRules[0]);
        Assert.Equal("base.css", rule.Url);
    }

    #endregion

    #region @media

    [Fact]
    public void Media_SimpleRule_Parsed()
    {
        var stylesheet = new CssParser("@media screen { div { color: red; } }").Parse();
        Assert.Single(stylesheet.AtRules);
        var rule = Assert.IsType<CssMediaRule>(stylesheet.AtRules[0]);
        Assert.Equal("screen", rule.MediaQuery);
        Assert.Single(rule.Rules);
        Assert.Equal("color", rule.Rules[0].Declarations[0].Property);
    }

    [Fact]
    public void Media_WithFeatureQuery_Parsed()
    {
        var stylesheet = new CssParser("@media (min-width: 600px) { p { font-size: 18px; } }").Parse();
        var rule = Assert.IsType<CssMediaRule>(stylesheet.AtRules[0]);
        Assert.Equal("(min-width: 600px)", rule.MediaQuery);
        Assert.Single(rule.Rules);
    }

    [Fact]
    public void Media_MultipleRules_Parsed()
    {
        var css = "@media screen { h1 { color: blue; } p { color: red; } }";
        var stylesheet = new CssParser(css).Parse();
        var rule = Assert.IsType<CssMediaRule>(stylesheet.AtRules[0]);
        Assert.Equal(2, rule.Rules.Count);
    }

    [Fact]
    public void Media_ComplexQuery_Parsed()
    {
        var css = "@media screen and (min-width: 768px) { .container { width: 750px; } }";
        var stylesheet = new CssParser(css).Parse();
        var rule = Assert.IsType<CssMediaRule>(stylesheet.AtRules[0]);
        Assert.Equal("screen and (min-width: 768px)", rule.MediaQuery);
    }

    [Fact]
    public void Media_CoexistsWithRegularRules()
    {
        var css = "div { color: red; } @media screen { p { color: blue; } } span { color: green; }";
        var stylesheet = new CssParser(css).Parse();
        Assert.Equal(2, stylesheet.Rules.Count);
        Assert.Single(stylesheet.AtRules);
    }

    #endregion

    #region @supports

    [Fact]
    public void Supports_SimpleCondition_Parsed()
    {
        var css = "@supports (display: flex) { .flex { display: flex; } }";
        var stylesheet = new CssParser(css).Parse();
        Assert.Single(stylesheet.AtRules);
        var rule = Assert.IsType<CssSupportsRule>(stylesheet.AtRules[0]);
        Assert.Equal("(display: flex)", rule.Condition);
        Assert.Single(rule.Rules);
    }

    [Fact]
    public void Supports_NotCondition_Parsed()
    {
        var css = "@supports not (display: grid) { .fallback { display: block; } }";
        var stylesheet = new CssParser(css).Parse();
        var rule = Assert.IsType<CssSupportsRule>(stylesheet.AtRules[0]);
        Assert.Contains("not", rule.Condition, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Supports_AndCondition_Parsed()
    {
        var css = "@supports (display: flex) and (gap: 10px) { .grid { gap: 10px; } }";
        var stylesheet = new CssParser(css).Parse();
        var rule = Assert.IsType<CssSupportsRule>(stylesheet.AtRules[0]);
        Assert.Contains("and", rule.Condition, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region @layer

    [Fact]
    public void Layer_NamedWithRules_Parsed()
    {
        var css = "@layer base { div { color: red; } }";
        var stylesheet = new CssParser(css).Parse();
        Assert.Single(stylesheet.AtRules);
        var rule = Assert.IsType<CssLayerRule>(stylesheet.AtRules[0]);
        Assert.Equal("base", rule.Name);
        Assert.Single(rule.Rules);
    }

    [Fact]
    public void Layer_DeclarationOnly_Parsed()
    {
        var css = "@layer utilities;";
        var stylesheet = new CssParser(css).Parse();
        Assert.Single(stylesheet.AtRules);
        var rule = Assert.IsType<CssLayerRule>(stylesheet.AtRules[0]);
        Assert.Equal("utilities", rule.Name);
        Assert.Empty(rule.Rules);
    }

    [Fact]
    public void Layer_Anonymous_Parsed()
    {
        var css = "@layer { div { color: red; } }";
        var stylesheet = new CssParser(css).Parse();
        Assert.Single(stylesheet.AtRules);
        var rule = Assert.IsType<CssLayerRule>(stylesheet.AtRules[0]);
        Assert.Null(rule.Name);
        Assert.Single(rule.Rules);
    }

    #endregion

    #region @font-face

    [Fact]
    public void FontFace_Parsed()
    {
        var css = "@font-face { font-family: 'MyFont'; font-weight: bold; }";
        var stylesheet = new CssParser(css).Parse();
        Assert.Single(stylesheet.AtRules);
        var rule = Assert.IsType<CssFontFaceRule>(stylesheet.AtRules[0]);
        Assert.True(rule.Descriptors.Count >= 2);
        Assert.Contains(rule.Descriptors, d => d.Property == "font-family");
        Assert.Contains(rule.Descriptors, d => d.Property == "font-weight");
    }

    [Fact]
    public void FontFace_EmptyBlock_Parsed()
    {
        var css = "@font-face { }";
        var stylesheet = new CssParser(css).Parse();
        Assert.Single(stylesheet.AtRules);
        var rule = Assert.IsType<CssFontFaceRule>(stylesheet.AtRules[0]);
        Assert.Empty(rule.Descriptors);
    }

    #endregion

    #region @keyframes

    [Fact]
    public void Keyframes_SimpleAnimation_Parsed()
    {
        var css = "@keyframes fadeIn { from { opacity: 0; } to { opacity: 1; } }";
        var stylesheet = new CssParser(css).Parse();
        Assert.Single(stylesheet.AtRules);
        var rule = Assert.IsType<CssKeyframesRule>(stylesheet.AtRules[0]);
        Assert.Equal("fadeIn", rule.Name);
        Assert.Equal(2, rule.Keyframes.Count);
    }

    [Fact]
    public void Keyframes_PercentageSelectors_Parsed()
    {
        var css = "@keyframes slide { 0% { left: 0; } 50% { left: 50px; } 100% { left: 100px; } }";
        var stylesheet = new CssParser(css).Parse();
        var rule = Assert.IsType<CssKeyframesRule>(stylesheet.AtRules[0]);
        Assert.Equal("slide", rule.Name);
        Assert.Equal(3, rule.Keyframes.Count);
    }

    [Fact]
    public void Keyframes_WebkitPrefix_Parsed()
    {
        var css = "@-webkit-keyframes bounce { from { top: 0; } to { top: 10px; } }";
        var stylesheet = new CssParser(css).Parse();
        Assert.Single(stylesheet.AtRules);
        var rule = Assert.IsType<CssKeyframesRule>(stylesheet.AtRules[0]);
        Assert.Equal("bounce", rule.Name);
    }

    #endregion

    #region @namespace

    [Fact]
    public void Namespace_WithPrefix_Parsed()
    {
        var css = "@namespace svg \"http://www.w3.org/2000/svg\";";
        var stylesheet = new CssParser(css).Parse();
        Assert.Single(stylesheet.AtRules);
        var rule = Assert.IsType<CssNamespaceRule>(stylesheet.AtRules[0]);
        Assert.Equal("svg", rule.Prefix);
        Assert.Equal("http://www.w3.org/2000/svg", rule.Uri);
    }

    [Fact]
    public void Namespace_WithoutPrefix_Parsed()
    {
        var css = "@namespace \"http://www.w3.org/1999/xhtml\";";
        var stylesheet = new CssParser(css).Parse();
        Assert.Single(stylesheet.AtRules);
        var rule = Assert.IsType<CssNamespaceRule>(stylesheet.AtRules[0]);
        Assert.Null(rule.Prefix);
        Assert.Equal("http://www.w3.org/1999/xhtml", rule.Uri);
    }

    #endregion

    #region @scope

    [Fact]
    public void Scope_WithSelector_Parsed()
    {
        var css = "@scope (.card) { h2 { color: blue; } }";
        var stylesheet = new CssParser(css).Parse();
        Assert.Single(stylesheet.AtRules);
        var rule = Assert.IsType<CssScopeRule>(stylesheet.AtRules[0]);
        Assert.Equal(".card", rule.Scope);
        Assert.Single(rule.Rules);
    }

    [Fact]
    public void Scope_WithoutSelector_Parsed()
    {
        var css = "@scope { div { color: red; } }";
        var stylesheet = new CssParser(css).Parse();
        Assert.Single(stylesheet.AtRules);
        var rule = Assert.IsType<CssScopeRule>(stylesheet.AtRules[0]);
        Assert.Null(rule.Scope);
        Assert.Single(rule.Rules);
    }

    #endregion

    #region Multiple at-rules

    [Fact]
    public void MultipleAtRules_AllParsed()
    {
        var css = @"
            @import 'reset.css';
            @media screen { div { color: red; } }
            @supports (display: flex) { .flex { display: flex; } }
            body { margin: 0; }
        ";
        var stylesheet = new CssParser(css).Parse();
        Assert.Equal(3, stylesheet.AtRules.Count);
        Assert.Single(stylesheet.Rules);
    }

    [Fact]
    public void NestedMediaInSupports_Parsed()
    {
        var css = "@supports (display: flex) { @media screen { .flex { display: flex; } } }";
        var stylesheet = new CssParser(css).Parse();
        var supports = Assert.IsType<CssSupportsRule>(stylesheet.AtRules[0]);
        Assert.Single(supports.NestedAtRules);
        Assert.IsType<CssMediaRule>(supports.NestedAtRules[0]);
    }

    [Fact]
    public void AtRuleType_Properties_Correct()
    {
        Assert.Equal(AtRuleType.Import, new CssImportRule { Url = "x" }.Type);
        Assert.Equal(AtRuleType.Media, new CssMediaRule { MediaQuery = "all" }.Type);
        Assert.Equal(AtRuleType.Supports, new CssSupportsRule { Condition = "x" }.Type);
        Assert.Equal(AtRuleType.Layer, new CssLayerRule().Type);
        Assert.Equal(AtRuleType.FontFace, new CssFontFaceRule { Descriptors = [] }.Type);
        Assert.Equal(AtRuleType.Keyframes, new CssKeyframesRule { Name = "x", Keyframes = [] }.Type);
        Assert.Equal(AtRuleType.Namespace, new CssNamespaceRule { Uri = "x" }.Type);
        Assert.Equal(AtRuleType.Scope, new CssScopeRule().Type);
    }

    [Fact]
    public void UnknownAtRule_Skipped()
    {
        var css = "@charset 'UTF-8'; div { color: red; }";
        var stylesheet = new CssParser(css).Parse();
        Assert.Empty(stylesheet.AtRules);
        Assert.Single(stylesheet.Rules);
    }

    #endregion

    #region Tokenizer at-keyword

    [Fact]
    public void Tokenizer_AtKeyword_Produced()
    {
        var tokenizer = new CssTokenizer("@media");
        var tokens = tokenizer.Tokenize().ToList();
        Assert.Contains(tokens, t => t.Type == CssTokenType.AtKeyword && t.Value == "media");
    }

    [Fact]
    public void Tokenizer_AtSymbolAlone_ProducesDelim()
    {
        var tokenizer = new CssTokenizer("@ ");
        var tokens = tokenizer.Tokenize().ToList();
        Assert.Contains(tokens, t => t.Type == CssTokenType.Delim && t.Value == "@");
    }

    #endregion
}
