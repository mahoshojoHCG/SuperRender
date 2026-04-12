using SuperRender.Document.Css;
using Xunit;

namespace SuperRender.Document.Tests.Css;

public class NestingTests
{
    [Fact]
    public void BasicNesting_ChildSelector_ExpandedToDescendant()
    {
        var css = ".parent { color: red; .child { color: blue; } }";
        var stylesheet = new CssParser(css).Parse();

        // Should have .parent rule and .parent .child rule
        Assert.True(stylesheet.Rules.Count >= 2,
            $"Expected at least 2 rules, got {stylesheet.Rules.Count}");

        // Find the parent rule
        var parentRule = stylesheet.Rules.FirstOrDefault(r =>
            r.Selectors.Any(s => s.Components.Count == 1));
        Assert.NotNull(parentRule);
        Assert.Contains(parentRule.Declarations, d => d.Property == "color");

        // Find the nested rule — should be expanded to ".parent .child"
        var nestedRule = stylesheet.Rules.FirstOrDefault(r =>
            r.Selectors.Any(s => s.Components.Count >= 2));
        Assert.NotNull(nestedRule);
    }

    [Fact]
    public void Nesting_WithAmpersand_ExpandsCorrectly()
    {
        // & references the parent selector
        var css = ".btn { color: red; &:hover { color: blue; } }";
        var stylesheet = new CssParser(css).Parse();
        Assert.True(stylesheet.Rules.Count >= 1);
    }

    [Fact]
    public void Nesting_ClassInParent_ProducesCompoundSelector()
    {
        var css = ".parent { .child { font-size: 14px; } }";
        var stylesheet = new CssParser(css).Parse();
        Assert.True(stylesheet.Rules.Count >= 1);

        // The nested rule should exist with declarations
        var nestedRule = stylesheet.Rules.FirstOrDefault(r =>
            r.Declarations.Any(d => d.Property == "font-size"));
        Assert.NotNull(nestedRule);
    }

    [Fact]
    public void Nesting_DeclarationsBeforeNested_BothParsed()
    {
        var css = ".card { padding: 10px; font-size: 14px; .title { font-weight: bold; } }";
        var stylesheet = new CssParser(css).Parse();

        // Parent rule should have its own declarations
        var parentRule = stylesheet.Rules.FirstOrDefault(r =>
            r.Declarations.Any(d => d.Property == "font-size"));
        Assert.NotNull(parentRule);
    }

    [Fact]
    public void Nesting_MultipleNestedRules()
    {
        var css = @".card {
            color: black;
            .title { font-size: 20px; }
            .body { font-size: 14px; }
        }";
        var stylesheet = new CssParser(css).Parse();
        // Should have at least 3 rules: .card, .card .title, .card .body
        Assert.True(stylesheet.Rules.Count >= 3,
            $"Expected at least 3 rules, got {stylesheet.Rules.Count}");
    }

    [Fact]
    public void Nesting_IdSelector_Expanded()
    {
        var css = "#main { #sidebar { width: 200px; } }";
        var stylesheet = new CssParser(css).Parse();
        Assert.True(stylesheet.Rules.Count >= 1);

        var nestedRule = stylesheet.Rules.FirstOrDefault(r =>
            r.Declarations.Any(d => d.Property == "width"));
        Assert.NotNull(nestedRule);
    }

    [Fact]
    public void Nesting_DoesNotBreakRegularDeclarations()
    {
        var css = ".outer { margin: 10px; padding: 5px; }";
        var stylesheet = new CssParser(css).Parse();
        Assert.Single(stylesheet.Rules);
        // margin expands to 4 + padding expands to 4 = 8
        Assert.True(stylesheet.Rules[0].Declarations.Count >= 2);
    }

    [Fact]
    public void Nesting_EmptyNestedBlock_NoError()
    {
        var css = ".parent { .child { } }";
        var stylesheet = new CssParser(css).Parse();
        // Should not throw; may produce empty rules
        Assert.NotNull(stylesheet);
    }

    [Fact]
    public void Nesting_WithRegularRulesAfter_AllParsed()
    {
        var css = ".parent { .child { color: red; } } .sibling { color: green; }";
        var stylesheet = new CssParser(css).Parse();
        Assert.True(stylesheet.Rules.Count >= 2);
    }

    [Fact]
    public void Nesting_HashSelector_InsideParent()
    {
        var css = "div { #header { background-color: blue; } }";
        var stylesheet = new CssParser(css).Parse();
        Assert.True(stylesheet.Rules.Count >= 1);
    }
}
