using SuperRender.Document.Css;
using SuperRender.Document.Dom;
using SuperRender.Document.Html;
using Xunit;

namespace SuperRender.Document.Tests.Css;

public class SelectorTests
{
    private static (Document.Dom.Document doc, List<Element> elements) ParseAndCollect(string html)
    {
        var doc = new HtmlParser(html).Parse();
        var elements = new List<Element>();
        CollectElements(doc, elements);
        return (doc, elements);
    }

    private static void CollectElements(Node node, List<Element> elements)
    {
        if (node is Element el) elements.Add(el);
        foreach (var child in node.Children)
            CollectElements(child, elements);
    }

    private static List<Selector> ParseSelectors(string css)
    {
        var tokenizer = new CssTokenizer(css);
        var tokens = tokenizer.Tokenize().Where(t => t.Type != CssTokenType.EndOfFile).ToList();
        return new SelectorParser(tokens).ParseSelectorList();
    }

    private static Element? FindByTag(List<Element> elements, string tag)
        => elements.FirstOrDefault(e => e.TagName.Equals(tag, StringComparison.OrdinalIgnoreCase));

    private static Element? FindById(List<Element> elements, string id)
        => elements.FirstOrDefault(e => e.Id == id);

    // === Sibling Combinators ===

    [Fact]
    public void AdjacentSibling_MatchesImmediateSibling()
    {
        var (_, elements) = ParseAndCollect("<html><body><h1>H</h1><p id='t'>P</p></body></html>");
        var sel = ParseSelectors("h1 + p");
        var p = FindById(elements, "t");
        Assert.NotNull(p);
        Assert.True(SelectorMatcher.Matches(sel[0], p!));
    }

    [Fact]
    public void AdjacentSibling_DoesNotMatchNonAdjacent()
    {
        var (_, elements) = ParseAndCollect("<html><body><h1>H</h1><div>D</div><p id='t'>P</p></body></html>");
        var sel = ParseSelectors("h1 + p");
        var p = FindById(elements, "t");
        Assert.NotNull(p);
        Assert.False(SelectorMatcher.Matches(sel[0], p!));
    }

    [Fact]
    public void GeneralSibling_MatchesAnySibling()
    {
        var (_, elements) = ParseAndCollect("<html><body><h1>H</h1><div>D</div><p id='t'>P</p></body></html>");
        var sel = ParseSelectors("h1 ~ p");
        var p = FindById(elements, "t");
        Assert.NotNull(p);
        Assert.True(SelectorMatcher.Matches(sel[0], p!));
    }

    [Fact]
    public void GeneralSibling_DoesNotMatchChild()
    {
        var (_, elements) = ParseAndCollect("<html><body><h1>H</h1><div><p id='t'>P</p></div></body></html>");
        var sel = ParseSelectors("h1 ~ p");
        var p = FindById(elements, "t");
        Assert.NotNull(p);
        Assert.False(SelectorMatcher.Matches(sel[0], p!));
    }

    // === Attribute Selectors ===

    [Fact]
    public void AttributeExists_MatchesPresence()
    {
        var (_, elements) = ParseAndCollect("<html><body><input type='text' id='t'/></body></html>");
        var sel = ParseSelectors("[type]");
        var input = FindById(elements, "t");
        Assert.NotNull(input);
        Assert.True(SelectorMatcher.Matches(sel[0], input!));
    }

    [Fact]
    public void AttributeEquals_MatchesExactValue()
    {
        var (_, elements) = ParseAndCollect("<html><body><input type='text' id='t'/></body></html>");
        var sel = ParseSelectors("[type=\"text\"]");
        var input = FindById(elements, "t");
        Assert.True(SelectorMatcher.Matches(sel[0], input!));
    }

    [Fact]
    public void AttributeEquals_DoesNotMatchDifferentValue()
    {
        var (_, elements) = ParseAndCollect("<html><body><input type='text' id='t'/></body></html>");
        var sel = ParseSelectors("[type=\"password\"]");
        var input = FindById(elements, "t");
        Assert.False(SelectorMatcher.Matches(sel[0], input!));
    }

    [Fact]
    public void AttributeStartsWith_Matches()
    {
        var (_, elements) = ParseAndCollect("<html><body><a href='https://example.com' id='t'>link</a></body></html>");
        var sel = ParseSelectors("[href^=\"https\"]");
        var a = FindById(elements, "t");
        Assert.True(SelectorMatcher.Matches(sel[0], a!));
    }

    [Fact]
    public void AttributeEndsWith_Matches()
    {
        var (_, elements) = ParseAndCollect("<html><body><a href='file.pdf' id='t'>link</a></body></html>");
        var sel = ParseSelectors("[href$=\".pdf\"]");
        var a = FindById(elements, "t");
        Assert.True(SelectorMatcher.Matches(sel[0], a!));
    }

    [Fact]
    public void AttributeContains_Matches()
    {
        var (_, elements) = ParseAndCollect("<html><body><div class='hello world' id='t'>text</div></body></html>");
        var sel = ParseSelectors("[class*=\"lo wo\"]");
        var div = FindById(elements, "t");
        Assert.True(SelectorMatcher.Matches(sel[0], div!));
    }

    [Fact]
    public void AttributeWordMatch_Matches()
    {
        var (_, elements) = ParseAndCollect("<html><body><div class='foo bar baz' id='t'>text</div></body></html>");
        var sel = ParseSelectors("[class~=\"bar\"]");
        var div = FindById(elements, "t");
        Assert.True(SelectorMatcher.Matches(sel[0], div!));
    }

    [Fact]
    public void AttributeDashMatch_Matches()
    {
        var (_, elements) = ParseAndCollect("<html><body><div lang='en-US' id='t'>text</div></body></html>");
        var sel = ParseSelectors("[lang|=\"en\"]");
        var div = FindById(elements, "t");
        Assert.True(SelectorMatcher.Matches(sel[0], div!));
    }

    // === Structural Pseudo-classes ===

    [Fact]
    public void FirstChild_MatchesFirstElement()
    {
        var (_, elements) = ParseAndCollect("<html><body><ul><li id='a'>1</li><li id='b'>2</li><li id='c'>3</li></ul></body></html>");
        var sel = ParseSelectors("li:first-child");
        Assert.True(SelectorMatcher.Matches(sel[0], FindById(elements, "a")!));
        Assert.False(SelectorMatcher.Matches(sel[0], FindById(elements, "b")!));
    }

    [Fact]
    public void LastChild_MatchesLastElement()
    {
        var (_, elements) = ParseAndCollect("<html><body><ul><li id='a'>1</li><li id='b'>2</li><li id='c'>3</li></ul></body></html>");
        var sel = ParseSelectors("li:last-child");
        Assert.False(SelectorMatcher.Matches(sel[0], FindById(elements, "a")!));
        Assert.True(SelectorMatcher.Matches(sel[0], FindById(elements, "c")!));
    }

    [Fact]
    public void NthChild_Odd_MatchesCorrectElements()
    {
        var (_, elements) = ParseAndCollect("<html><body><ul><li id='a'>1</li><li id='b'>2</li><li id='c'>3</li></ul></body></html>");
        var sel = ParseSelectors("li:nth-child(odd)");
        Assert.True(SelectorMatcher.Matches(sel[0], FindById(elements, "a")!));  // 1st
        Assert.False(SelectorMatcher.Matches(sel[0], FindById(elements, "b")!)); // 2nd
        Assert.True(SelectorMatcher.Matches(sel[0], FindById(elements, "c")!));  // 3rd
    }

    [Fact]
    public void NthChild_Even_MatchesCorrectElements()
    {
        var (_, elements) = ParseAndCollect("<html><body><ul><li id='a'>1</li><li id='b'>2</li><li id='c'>3</li></ul></body></html>");
        var sel = ParseSelectors("li:nth-child(even)");
        Assert.False(SelectorMatcher.Matches(sel[0], FindById(elements, "a")!));
        Assert.True(SelectorMatcher.Matches(sel[0], FindById(elements, "b")!));
        Assert.False(SelectorMatcher.Matches(sel[0], FindById(elements, "c")!));
    }

    [Fact]
    public void NthChild_Number_MatchesSpecificIndex()
    {
        var (_, elements) = ParseAndCollect("<html><body><ul><li id='a'>1</li><li id='b'>2</li><li id='c'>3</li></ul></body></html>");
        var sel = ParseSelectors("li:nth-child(2)");
        Assert.False(SelectorMatcher.Matches(sel[0], FindById(elements, "a")!));
        Assert.True(SelectorMatcher.Matches(sel[0], FindById(elements, "b")!));
        Assert.False(SelectorMatcher.Matches(sel[0], FindById(elements, "c")!));
    }

    [Fact]
    public void NthChild_2nPlus1_SameAsOdd()
    {
        var (_, elements) = ParseAndCollect("<html><body><ul><li id='a'>1</li><li id='b'>2</li><li id='c'>3</li><li id='d'>4</li></ul></body></html>");
        var sel = ParseSelectors("li:nth-child(2n+1)");
        Assert.True(SelectorMatcher.Matches(sel[0], FindById(elements, "a")!));
        Assert.False(SelectorMatcher.Matches(sel[0], FindById(elements, "b")!));
        Assert.True(SelectorMatcher.Matches(sel[0], FindById(elements, "c")!));
        Assert.False(SelectorMatcher.Matches(sel[0], FindById(elements, "d")!));
    }

    [Fact]
    public void Root_MatchesHtmlElement()
    {
        var (_, elements) = ParseAndCollect("<html><body><div id='t'>text</div></body></html>");
        var sel = ParseSelectors(":root");
        var html = FindByTag(elements, "html");
        Assert.NotNull(html);
        Assert.True(SelectorMatcher.Matches(sel[0], html!));
    }

    [Fact]
    public void Root_DoesNotMatchNonRootElement()
    {
        var (_, elements) = ParseAndCollect("<html><body><div id='t'>text</div></body></html>");
        var sel = ParseSelectors(":root");
        var div = FindById(elements, "t");
        Assert.False(SelectorMatcher.Matches(sel[0], div!));
    }

    [Fact]
    public void Empty_MatchesEmptyElement()
    {
        var (_, elements) = ParseAndCollect("<html><body><div id='t'></div></body></html>");
        var sel = ParseSelectors(":empty");
        var div = FindById(elements, "t");
        Assert.True(SelectorMatcher.Matches(sel[0], div!));
    }

    [Fact]
    public void Empty_DoesNotMatchNonEmptyElement()
    {
        var (_, elements) = ParseAndCollect("<html><body><div id='t'>text</div></body></html>");
        var sel = ParseSelectors(":empty");
        var div = FindById(elements, "t");
        Assert.False(SelectorMatcher.Matches(sel[0], div!));
    }

    [Fact]
    public void Link_MatchesAnchorWithHref()
    {
        var (_, elements) = ParseAndCollect("<html><body><a href='http://example.com' id='t'>link</a></body></html>");
        var sel = ParseSelectors(":link");
        var a = FindById(elements, "t");
        Assert.True(SelectorMatcher.Matches(sel[0], a!));
    }

    [Fact]
    public void OnlyChild_MatchesSingleChild()
    {
        var (_, elements) = ParseAndCollect("<html><body><div><span id='t'>only</span></div></body></html>");
        var sel = ParseSelectors("span:only-child");
        var span = FindById(elements, "t");
        Assert.True(SelectorMatcher.Matches(sel[0], span!));
    }

    [Fact]
    public void FirstOfType_MatchesFirstOfTag()
    {
        var (_, elements) = ParseAndCollect("<html><body><div><span id='a'>first</span><p>para</p><span id='b'>second</span></div></body></html>");
        var sel = ParseSelectors("span:first-of-type");
        Assert.True(SelectorMatcher.Matches(sel[0], FindById(elements, "a")!));
        Assert.False(SelectorMatcher.Matches(sel[0], FindById(elements, "b")!));
    }

    // === Functional Pseudo-classes ===

    [Fact]
    public void Not_ExcludesMatchingElements()
    {
        var (_, elements) = ParseAndCollect("<html><body><div class='a' id='x'>A</div><div class='b' id='y'>B</div></body></html>");
        var sel = ParseSelectors("div:not(.a)");
        Assert.False(SelectorMatcher.Matches(sel[0], FindById(elements, "x")!));
        Assert.True(SelectorMatcher.Matches(sel[0], FindById(elements, "y")!));
    }

    [Fact]
    public void Is_MatchesAnyArgument()
    {
        var (_, elements) = ParseAndCollect("<html><body><h1 id='a'>H1</h1><h2 id='b'>H2</h2><p id='c'>P</p></body></html>");
        var sel = ParseSelectors(":is(h1, h2)");
        Assert.True(SelectorMatcher.Matches(sel[0], FindById(elements, "a")!));
        Assert.True(SelectorMatcher.Matches(sel[0], FindById(elements, "b")!));
        Assert.False(SelectorMatcher.Matches(sel[0], FindById(elements, "c")!));
    }

    [Fact]
    public void Where_MatchesAnyArgument_ZeroSpecificity()
    {
        var sel = ParseSelectors(":where(h1, h2)");
        var specificity = sel[0].GetSpecificity();
        // :where() should have zero specificity
        Assert.Equal(0, specificity.Ids);
        Assert.Equal(0, specificity.Classes);
        Assert.Equal(0, specificity.Elements);
    }

    // === Pseudo-elements ===

    [Fact]
    public void PseudoElement_Before_Parsed()
    {
        var sel = ParseSelectors("div::before");
        Assert.Single(sel);
        Assert.Equal(PseudoElementType.Before, sel[0].PseudoElement);
    }

    [Fact]
    public void PseudoElement_After_Parsed()
    {
        var sel = ParseSelectors("p::after");
        Assert.Single(sel);
        Assert.Equal(PseudoElementType.After, sel[0].PseudoElement);
    }

    // === Specificity ===

    [Fact]
    public void Specificity_AttributeSelector_CountsAsClass()
    {
        var sel = ParseSelectors("[type=\"text\"]");
        var spec = sel[0].GetSpecificity();
        Assert.Equal(0, spec.Ids);
        Assert.Equal(1, spec.Classes);
        Assert.Equal(0, spec.Elements);
    }

    [Fact]
    public void Specificity_PseudoClass_CountsAsClass()
    {
        var sel = ParseSelectors(":first-child");
        var spec = sel[0].GetSpecificity();
        Assert.Equal(0, spec.Ids);
        Assert.Equal(1, spec.Classes);
        Assert.Equal(0, spec.Elements);
    }

    [Fact]
    public void Specificity_Not_ContributesArgumentSpecificity()
    {
        var sel = ParseSelectors(":not(#myid)");
        var spec = sel[0].GetSpecificity();
        Assert.Equal(1, spec.Ids);
    }

    [Fact]
    public void Specificity_PseudoElement_CountsAsElement()
    {
        var sel = ParseSelectors("p::before");
        var spec = sel[0].GetSpecificity();
        Assert.Equal(0, spec.Ids);
        Assert.Equal(0, spec.Classes);
        Assert.Equal(2, spec.Elements); // p + ::before
    }

    // === NthChildParser ===

    [Fact]
    public void NthChildParser_Odd_Correct()
    {
        Assert.True(NthChildParser.Matches("odd", 1));
        Assert.False(NthChildParser.Matches("odd", 2));
        Assert.True(NthChildParser.Matches("odd", 3));
    }

    [Fact]
    public void NthChildParser_Even_Correct()
    {
        Assert.False(NthChildParser.Matches("even", 1));
        Assert.True(NthChildParser.Matches("even", 2));
        Assert.False(NthChildParser.Matches("even", 3));
        Assert.True(NthChildParser.Matches("even", 4));
    }

    [Fact]
    public void NthChildParser_PlainNumber_Correct()
    {
        Assert.False(NthChildParser.Matches("2", 1));
        Assert.True(NthChildParser.Matches("2", 2));
        Assert.False(NthChildParser.Matches("2", 3));
    }

    [Fact]
    public void NthChildParser_3n_Correct()
    {
        Assert.False(NthChildParser.Matches("3n", 1));
        Assert.False(NthChildParser.Matches("3n", 2));
        Assert.True(NthChildParser.Matches("3n", 3));
        Assert.False(NthChildParser.Matches("3n", 4));
        Assert.False(NthChildParser.Matches("3n", 5));
        Assert.True(NthChildParser.Matches("3n", 6));
    }

    [Fact]
    public void NthChildParser_NegativeN_Correct()
    {
        // -n+3 matches 3, 2, 1 (indices <= 3)
        Assert.True(NthChildParser.Matches("-n+3", 1));
        Assert.True(NthChildParser.Matches("-n+3", 2));
        Assert.True(NthChildParser.Matches("-n+3", 3));
        Assert.False(NthChildParser.Matches("-n+3", 4));
    }

    // === Compound selectors ===

    [Fact]
    public void Compound_TagAndClass_Matches()
    {
        var (_, elements) = ParseAndCollect("<html><body><div class='active' id='t'>text</div><p class='active' id='p'>para</p></body></html>");
        var sel = ParseSelectors("div.active");
        Assert.True(SelectorMatcher.Matches(sel[0], FindById(elements, "t")!));
        Assert.False(SelectorMatcher.Matches(sel[0], FindById(elements, "p")!));
    }

    [Fact]
    public void Compound_TagAndAttribute_Matches()
    {
        var (_, elements) = ParseAndCollect("<html><body><a href='http://x.com' id='t'>link</a><a id='n'>no href</a></body></html>");
        var sel = ParseSelectors("a[href]");
        Assert.True(SelectorMatcher.Matches(sel[0], FindById(elements, "t")!));
        Assert.False(SelectorMatcher.Matches(sel[0], FindById(elements, "n")!));
    }

    [Fact]
    public void Compound_TagAndPseudoClass_Matches()
    {
        var (_, elements) = ParseAndCollect("<html><body><ul><li id='a'>1</li><li id='b'>2</li></ul></body></html>");
        var sel = ParseSelectors("li:first-child");
        Assert.True(SelectorMatcher.Matches(sel[0], FindById(elements, "a")!));
        Assert.False(SelectorMatcher.Matches(sel[0], FindById(elements, "b")!));
    }
}
