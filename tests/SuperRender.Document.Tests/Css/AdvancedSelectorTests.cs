using SuperRender.Document.Css;
using SuperRender.Document.Dom;
using Xunit;

namespace SuperRender.Document.Tests.Css;

public class AdvancedSelectorTests
{
    private static Element CreateTree(string html)
    {
        var parser = new SuperRender.Document.Html.HtmlParser(html);
        var doc = parser.Parse();
        return doc.Body!;
    }

    private static Selector ParseSelector(string sel)
    {
        var tokens = new CssTokenizer(sel).Tokenize().ToList();
        var parser = new SelectorParser(tokens);
        var list = parser.ParseSelectorList();
        Assert.NotEmpty(list);
        return list[0];
    }

    // ===== Case-insensitive attribute selectors =====

    [Fact]
    public void AttributeSelector_CaseInsensitiveFlag_Matches()
    {
        var body = CreateTree("<div data-role=\"Admin\">test</div>");
        var div = body.Children.OfType<Element>().First();
        var sel = ParseSelector("[data-role=\"admin\" i]");
        Assert.True(SelectorMatcher.Matches(sel, div));
    }

    [Fact]
    public void AttributeSelector_CaseInsensitiveFlag_ParsedCorrectly()
    {
        var sel = ParseSelector("[type=\"text\" i]");
        var attr = sel.Components[0].Simple.Attributes[0];
        Assert.Equal(AttributeCaseSensitivity.CaseInsensitive, attr.CaseSensitivity);
    }

    [Fact]
    public void AttributeSelector_CaseSensitiveFlag_ParsedCorrectly()
    {
        var sel = ParseSelector("[type=\"text\" s]");
        var attr = sel.Components[0].Simple.Attributes[0];
        Assert.Equal(AttributeCaseSensitivity.CaseSensitive, attr.CaseSensitivity);
    }

    [Fact]
    public void AttributeSelector_DefaultCaseSensitivity()
    {
        var sel = ParseSelector("[type=\"text\"]");
        var attr = sel.Components[0].Simple.Attributes[0];
        Assert.Equal(AttributeCaseSensitivity.Default, attr.CaseSensitivity);
    }

    // ===== :nth-of-type =====

    [Fact]
    public void NthOfType_First_Matches()
    {
        var body = CreateTree("<div><p>1</p><span>2</span><p>3</p></div>");
        var div = body.Children.OfType<Element>().First();
        var p1 = div.Children.OfType<Element>().First(e => e.TagName == "p");
        var sel = ParseSelector("p:nth-of-type(1)");
        Assert.True(SelectorMatcher.Matches(sel, p1));
    }

    [Fact]
    public void NthOfType_Second_Matches()
    {
        var body = CreateTree("<div><p>1</p><span>2</span><p>3</p></div>");
        var div = body.Children.OfType<Element>().First();
        var p2 = div.Children.OfType<Element>().Where(e => e.TagName == "p").Skip(1).First();
        var sel = ParseSelector("p:nth-of-type(2)");
        Assert.True(SelectorMatcher.Matches(sel, p2));
    }

    [Fact]
    public void NthLastOfType_Last_Matches()
    {
        var body = CreateTree("<div><p>1</p><span>2</span><p>3</p></div>");
        var div = body.Children.OfType<Element>().First();
        var p2 = div.Children.OfType<Element>().Where(e => e.TagName == "p").Skip(1).First();
        var sel = ParseSelector("p:nth-last-of-type(1)");
        Assert.True(SelectorMatcher.Matches(sel, p2));
    }

    // ===== :any-link =====

    [Fact]
    public void AnyLink_AnchorWithHref_Matches()
    {
        var body = CreateTree("<a href=\"#\">link</a>");
        var a = body.Children.OfType<Element>().First();
        var sel = ParseSelector(":any-link");
        Assert.True(SelectorMatcher.Matches(sel, a));
    }

    [Fact]
    public void AnyLink_AnchorWithoutHref_DoesNotMatch()
    {
        var body = CreateTree("<a>text</a>");
        var a = body.Children.OfType<Element>().First();
        var sel = ParseSelector(":any-link");
        Assert.False(SelectorMatcher.Matches(sel, a));
    }

    // ===== :focus-within =====

    [Fact]
    public void FocusWithin_ChildFocused_Matches()
    {
        var body = CreateTree("<div><input></div>");
        var div = body.Children.OfType<Element>().First();
        var input = div.Children.OfType<Element>().First();
        input.IsFocused = true;
        var sel = ParseSelector(":focus-within");
        Assert.True(SelectorMatcher.Matches(sel, div));
    }

    [Fact]
    public void FocusWithin_NoFocused_DoesNotMatch()
    {
        var body = CreateTree("<div><input></div>");
        var div = body.Children.OfType<Element>().First();
        var sel = ParseSelector(":focus-within");
        Assert.False(SelectorMatcher.Matches(sel, div));
    }

    // ===== :has() =====

    [Fact]
    public void Has_MatchingDescendant_Matches()
    {
        var body = CreateTree("<div><p class=\"special\">text</p></div>");
        var div = body.Children.OfType<Element>().First();
        var sel = ParseSelector("div:has(.special)");
        Assert.True(SelectorMatcher.Matches(sel, div));
    }

    [Fact]
    public void Has_NoMatchingDescendant_DoesNotMatch()
    {
        var body = CreateTree("<div><p>text</p></div>");
        var div = body.Children.OfType<Element>().First();
        var sel = ParseSelector("div:has(.special)");
        Assert.False(SelectorMatcher.Matches(sel, div));
    }

    // ===== Form pseudo-classes =====

    [Fact]
    public void Enabled_InputWithoutDisabled_Matches()
    {
        var body = CreateTree("<input type=\"text\">");
        var input = body.Children.OfType<Element>().First();
        var sel = ParseSelector(":enabled");
        Assert.True(SelectorMatcher.Matches(sel, input));
    }

    [Fact]
    public void Disabled_InputWithDisabled_Matches()
    {
        var body = CreateTree("<input type=\"text\" disabled>");
        var input = body.Children.OfType<Element>().First();
        var sel = ParseSelector(":disabled");
        Assert.True(SelectorMatcher.Matches(sel, input));
    }

    [Fact]
    public void Checked_CheckboxWithChecked_Matches()
    {
        var body = CreateTree("<input type=\"checkbox\" checked>");
        var input = body.Children.OfType<Element>().First();
        var sel = ParseSelector(":checked");
        Assert.True(SelectorMatcher.Matches(sel, input));
    }

    [Fact]
    public void Required_InputWithRequired_Matches()
    {
        var body = CreateTree("<input required>");
        var input = body.Children.OfType<Element>().First();
        var sel = ParseSelector(":required");
        Assert.True(SelectorMatcher.Matches(sel, input));
    }

    [Fact]
    public void Optional_InputWithoutRequired_Matches()
    {
        var body = CreateTree("<input type=\"text\">");
        var input = body.Children.OfType<Element>().First();
        var sel = ParseSelector(":optional");
        Assert.True(SelectorMatcher.Matches(sel, input));
    }

    [Fact]
    public void ReadOnly_InputWithReadonly_Matches()
    {
        var body = CreateTree("<input readonly>");
        var input = body.Children.OfType<Element>().First();
        var sel = ParseSelector(":read-only");
        Assert.True(SelectorMatcher.Matches(sel, input));
    }

    [Fact]
    public void ReadWrite_EditableInput_Matches()
    {
        var body = CreateTree("<input type=\"text\">");
        var input = body.Children.OfType<Element>().First();
        var sel = ParseSelector(":read-write");
        Assert.True(SelectorMatcher.Matches(sel, input));
    }

    // ===== :lang() =====

    [Fact]
    public void Lang_MatchesElementLang()
    {
        var body = CreateTree("<div lang=\"en\"><p>text</p></div>");
        var div = body.Children.OfType<Element>().First();
        var p = div.Children.OfType<Element>().First();
        var sel = ParseSelector(":lang(en)");
        Assert.True(SelectorMatcher.Matches(sel, p));
    }

    [Fact]
    public void Lang_MatchesSubtag()
    {
        var body = CreateTree("<div lang=\"en-US\"><p>text</p></div>");
        var div = body.Children.OfType<Element>().First();
        var p = div.Children.OfType<Element>().First();
        var sel = ParseSelector(":lang(en)");
        Assert.True(SelectorMatcher.Matches(sel, p));
    }

    // ===== :defined =====

    [Fact]
    public void Defined_AlwaysMatches()
    {
        var body = CreateTree("<div>test</div>");
        var div = body.Children.OfType<Element>().First();
        var sel = ParseSelector(":defined");
        Assert.True(SelectorMatcher.Matches(sel, div));
    }

    // ===== :scope =====

    [Fact]
    public void Scope_RootElement_Matches()
    {
        var parser = new SuperRender.Document.Html.HtmlParser("<html><body></body></html>");
        var doc = parser.Parse();
        var html = doc.Children.OfType<Element>().First();
        var sel = ParseSelector(":scope");
        Assert.True(SelectorMatcher.Matches(sel, html));
    }

    // ===== Pseudo-element parsing =====

    [Fact]
    public void PseudoElement_FirstLine_Parsed()
    {
        var sel = ParseSelector("p::first-line");
        Assert.Equal(PseudoElementType.FirstLine, sel.PseudoElement);
    }

    [Fact]
    public void PseudoElement_FirstLetter_Parsed()
    {
        var sel = ParseSelector("p::first-letter");
        Assert.Equal(PseudoElementType.FirstLetter, sel.PseudoElement);
    }

    [Fact]
    public void PseudoElement_Marker_Parsed()
    {
        var sel = ParseSelector("li::marker");
        Assert.Equal(PseudoElementType.Marker, sel.PseudoElement);
    }

    [Fact]
    public void PseudoElement_Placeholder_Parsed()
    {
        var sel = ParseSelector("input::placeholder");
        Assert.Equal(PseudoElementType.Placeholder, sel.PseudoElement);
    }

    [Fact]
    public void PseudoElement_Selection_Parsed()
    {
        var sel = ParseSelector("::selection");
        Assert.Equal(PseudoElementType.Selection, sel.PseudoElement);
    }

    // ===== Specificity of new pseudo-classes =====

    [Fact]
    public void Has_SpecificityCountsAsClass()
    {
        var sel = ParseSelector("div:has(.foo)");
        var spec = sel.GetSpecificity();
        Assert.True(spec.Classes >= 1);
    }

    [Fact]
    public void FocusWithin_SpecificityCountsAsClass()
    {
        var sel = ParseSelector(":focus-within");
        var spec = sel.GetSpecificity();
        Assert.Equal(1, spec.Classes);
    }
}
