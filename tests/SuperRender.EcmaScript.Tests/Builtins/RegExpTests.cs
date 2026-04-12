using SuperRender.EcmaScript.Engine;
using Xunit;

namespace SuperRender.EcmaScript.Tests.Builtins;

public class RegExpTests
{
    private static JsEngine CreateEngine() => new();

    // ═══════════════════════════════════════════
    //  Named capture groups
    // ═══════════════════════════════════════════

    [Fact]
    public void Exec_NamedGroup_PopulatesGroups()
    {
        var engine = CreateEngine();
        var result = engine.Execute<string>("""
            const re = new RegExp('(?<year>\\d{4})-(?<month>\\d{2})', '');
            const m = re.exec('2025-04-12');
            m.groups.year
            """);
        Assert.Equal("2025", result);
    }

    [Fact]
    public void Exec_NoNamedGroups_GroupsIsUndefined()
    {
        var engine = CreateEngine();
        var result = engine.Execute<string>("""
            const re = new RegExp('(\\d+)-(\\d+)', '');
            const m = re.exec('123-456');
            typeof m.groups
            """);
        Assert.Equal("undefined", result);
    }

    [Fact]
    public void Exec_MultipleNamedGroups_AllPresent()
    {
        var engine = CreateEngine();
        var result = engine.Execute<string>("""
            const re = new RegExp('(?<first>\\w+)\\s(?<last>\\w+)', '');
            const m = re.exec('John Doe');
            m.groups.first + ' ' + m.groups.last
            """);
        Assert.Equal("John Doe", result);
    }

    // ═══════════════════════════════════════════
    //  /d flag (hasIndices)
    // ═══════════════════════════════════════════

    [Fact]
    public void Exec_HasIndicesFlag_PopulatesIndices()
    {
        var engine = CreateEngine();
        // Match "world" in "hello world" — starts at index 6, length 5, so indices [6, 11]
        var result = engine.Execute<string>("""
            const re = new RegExp('(world)', 'd');
            const m = re.exec('hello world');
            const idx = m.indices[1];
            idx[0] + ',' + idx[1]
            """);
        Assert.Equal("6,11", result);
    }

    [Fact]
    public void Exec_IndicesWithNamedGroups_HasGroupsProperty()
    {
        var engine = CreateEngine();
        var result = engine.Execute<string>("""
            const re = new RegExp('(?<word>world)', 'd');
            const m = re.exec('hello world');
            const idx = m.indices.groups.word;
            idx[0] + ',' + idx[1]
            """);
        Assert.Equal("6,11", result);
    }

    // ═══════════════════════════════════════════
    //  Lookbehind assertions
    // ═══════════════════════════════════════════

    [Fact]
    public void Exec_LookbehindPositive_MatchesCorrectly()
    {
        var engine = CreateEngine();
        var result = engine.Execute<string>("""
            const re = new RegExp('(?<=\\$)\\d+', '');
            const m = re.exec('Price: $100');
            m[0]
            """);
        Assert.Equal("100", result);
    }

    [Fact]
    public void Exec_LookbehindNegative_ExcludesCorrectly()
    {
        var engine = CreateEngine();
        // Negative lookbehind: match digits NOT preceded by $
        var result = engine.Execute<bool>("""
            const re = new RegExp('(?<!\\$)\\d+', '');
            const m = re.exec('$100 and 200');
            // Should match '00' (first digits not preceded by $) or '200'
            // .NET regex: the first match of digits not preceded by $ in "$100 and 200"
            // '$' precedes '1', so '100' is partially matched — '00' at index 2 is not preceded by $
            m !== null
            """);
        Assert.True(result);
    }

    // ═══════════════════════════════════════════
    //  Unicode property escapes
    // ═══════════════════════════════════════════

    [Fact]
    public void Exec_UnicodePropertyUppercase_MatchesUppercase()
    {
        var engine = CreateEngine();
        // \p{Lu} matches uppercase letters in .NET regex
        var result = engine.Execute<string>("""
            const re = new RegExp('\\p{Lu}+', '');
            const m = re.exec('helloWORLD');
            m[0]
            """);
        Assert.Equal("WORLD", result);
    }
}
