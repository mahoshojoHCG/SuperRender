using SuperRender.EcmaScript.Compiler.Lexing;
using Xunit;

namespace SuperRender.EcmaScript.Tests.Lexing;

public class LexerTests
{
    private static List<Token> Tokenize(string source) => new Lexer(source).Tokenize();

    private static Token Single(string source)
    {
        var tokens = Tokenize(source);
        // Return the first non-EOF token
        Assert.True(tokens.Count >= 2, "Expected at least one token plus EOF");
        return tokens[0];
    }

    // ═══════════════════════════════════════════
    //  Keywords
    // ═══════════════════════════════════════════

    [Theory]
    [InlineData("var", TokenType.Var)]
    [InlineData("let", TokenType.Let)]
    [InlineData("const", TokenType.Const)]
    [InlineData("function", TokenType.Function)]
    [InlineData("return", TokenType.Return)]
    [InlineData("if", TokenType.If)]
    [InlineData("else", TokenType.Else)]
    [InlineData("for", TokenType.For)]
    [InlineData("while", TokenType.While)]
    [InlineData("do", TokenType.Do)]
    [InlineData("switch", TokenType.Switch)]
    [InlineData("case", TokenType.Case)]
    [InlineData("default", TokenType.Default)]
    [InlineData("break", TokenType.Break)]
    [InlineData("continue", TokenType.Continue)]
    [InlineData("throw", TokenType.Throw)]
    [InlineData("try", TokenType.Try)]
    [InlineData("catch", TokenType.Catch)]
    [InlineData("finally", TokenType.Finally)]
    [InlineData("new", TokenType.New)]
    [InlineData("delete", TokenType.Delete)]
    [InlineData("typeof", TokenType.Typeof)]
    [InlineData("void", TokenType.Void)]
    [InlineData("in", TokenType.In)]
    [InlineData("of", TokenType.Of)]
    [InlineData("instanceof", TokenType.Instanceof)]
    [InlineData("this", TokenType.This)]
    [InlineData("super", TokenType.Super)]
    [InlineData("class", TokenType.Class)]
    [InlineData("extends", TokenType.Extends)]
    [InlineData("import", TokenType.Import)]
    [InlineData("export", TokenType.Export)]
    [InlineData("from", TokenType.From)]
    [InlineData("as", TokenType.As)]
    [InlineData("yield", TokenType.Yield)]
    [InlineData("async", TokenType.Async)]
    [InlineData("await", TokenType.Await)]
    [InlineData("static", TokenType.Static)]
    [InlineData("get", TokenType.Get)]
    [InlineData("set", TokenType.Set)]
    [InlineData("debugger", TokenType.Debugger)]
    [InlineData("with", TokenType.With)]
    public void Tokenize_Keyword_ReturnsCorrectType(string source, TokenType expected)
    {
        var token = Single(source);
        Assert.Equal(expected, token.Type);
        Assert.Equal(source, token.Value);
    }

    [Theory]
    [InlineData("true", TokenType.TrueLiteral)]
    [InlineData("false", TokenType.FalseLiteral)]
    [InlineData("null", TokenType.NullLiteral)]
    public void Tokenize_BooleanAndNull_ReturnsCorrectType(string source, TokenType expected)
    {
        var token = Single(source);
        Assert.Equal(expected, token.Type);
    }

    // ═══════════════════════════════════════════
    //  Identifiers
    // ═══════════════════════════════════════════

    [Theory]
    [InlineData("x")]
    [InlineData("foo")]
    [InlineData("myVariable")]
    [InlineData("_private")]
    [InlineData("$dollar")]
    [InlineData("camelCase")]
    [InlineData("PascalCase")]
    [InlineData("_")]
    [InlineData("$$")]
    [InlineData("abc123")]
    public void Tokenize_Identifier_ReturnsIdentifierToken(string source)
    {
        var token = Single(source);
        Assert.Equal(TokenType.Identifier, token.Type);
        Assert.Equal(source, token.Value);
    }

    // ═══════════════════════════════════════════
    //  Numeric literals
    // ═══════════════════════════════════════════

    [Fact]
    public void Tokenize_Integer_ReturnsNumericLiteral()
    {
        var token = Single("42");
        Assert.Equal(TokenType.NumericLiteral, token.Type);
        Assert.Equal(42.0, token.NumericValue);
    }

    [Fact]
    public void Tokenize_Float_ReturnsNumericLiteral()
    {
        var token = Single("3.14");
        Assert.Equal(TokenType.NumericLiteral, token.Type);
        Assert.Equal(3.14, token.NumericValue, 5);
    }

    [Fact]
    public void Tokenize_FloatStartingWithDot_ReturnsNumericLiteral()
    {
        var token = Single(".5");
        Assert.Equal(TokenType.NumericLiteral, token.Type);
        Assert.Equal(0.5, token.NumericValue, 5);
    }

    [Fact]
    public void Tokenize_HexLiteral_ReturnsNumericLiteral()
    {
        var token = Single("0xFF");
        Assert.Equal(TokenType.NumericLiteral, token.Type);
        Assert.Equal(255.0, token.NumericValue);
    }

    [Fact]
    public void Tokenize_OctalLiteral_ReturnsNumericLiteral()
    {
        var token = Single("0o77");
        Assert.Equal(TokenType.NumericLiteral, token.Type);
        Assert.Equal(63.0, token.NumericValue);
    }

    [Fact]
    public void Tokenize_BinaryLiteral_ReturnsNumericLiteral()
    {
        var token = Single("0b1010");
        Assert.Equal(TokenType.NumericLiteral, token.Type);
        Assert.Equal(10.0, token.NumericValue);
    }

    [Fact]
    public void Tokenize_ScientificNotation_ReturnsNumericLiteral()
    {
        var token = Single("1e3");
        Assert.Equal(TokenType.NumericLiteral, token.Type);
        Assert.Equal(1000.0, token.NumericValue);
    }

    [Fact]
    public void Tokenize_ScientificNotationWithSign_ReturnsNumericLiteral()
    {
        var token = Single("2.5e-2");
        Assert.Equal(TokenType.NumericLiteral, token.Type);
        Assert.Equal(0.025, token.NumericValue, 10);
    }

    [Fact]
    public void Tokenize_NegativeNumber_ParsesAsMinusAndNumber()
    {
        var tokens = Tokenize("-5");
        Assert.Equal(TokenType.Minus, tokens[0].Type);
        Assert.Equal(TokenType.NumericLiteral, tokens[1].Type);
        Assert.Equal(5.0, tokens[1].NumericValue);
    }

    [Fact]
    public void Tokenize_Zero_ReturnsNumericLiteral()
    {
        var token = Single("0");
        Assert.Equal(TokenType.NumericLiteral, token.Type);
        Assert.Equal(0.0, token.NumericValue);
    }

    [Fact]
    public void Tokenize_HexUpperCase_ReturnsNumericLiteral()
    {
        var token = Single("0XAB");
        Assert.Equal(TokenType.NumericLiteral, token.Type);
        Assert.Equal(171.0, token.NumericValue);
    }

    // ═══════════════════════════════════════════
    //  String literals
    // ═══════════════════════════════════════════

    [Fact]
    public void Tokenize_DoubleQuotedString_ReturnsStringLiteral()
    {
        var token = Single("\"hello\"");
        Assert.Equal(TokenType.StringLiteral, token.Type);
        Assert.Equal("hello", token.Value);
    }

    [Fact]
    public void Tokenize_SingleQuotedString_ReturnsStringLiteral()
    {
        var token = Single("'world'");
        Assert.Equal(TokenType.StringLiteral, token.Type);
        Assert.Equal("world", token.Value);
    }

    [Fact]
    public void Tokenize_EmptyString_ReturnsStringLiteral()
    {
        var token = Single("\"\"");
        Assert.Equal(TokenType.StringLiteral, token.Type);
        Assert.Equal("", token.Value);
    }

    [Fact]
    public void Tokenize_StringWithEscapeNewline_ReturnsCorrectValue()
    {
        var token = Single("\"hello\\nworld\"");
        Assert.Equal(TokenType.StringLiteral, token.Type);
        Assert.Equal("hello\nworld", token.Value);
    }

    [Fact]
    public void Tokenize_StringWithEscapeTab_ReturnsCorrectValue()
    {
        var token = Single("\"tab\\there\"");
        Assert.Equal(TokenType.StringLiteral, token.Type);
        Assert.Equal("tab\there", token.Value);
    }

    [Fact]
    public void Tokenize_StringWithEscapedQuote_ReturnsCorrectValue()
    {
        var token = Single("\"say \\\"hi\\\"\"");
        Assert.Equal(TokenType.StringLiteral, token.Type);
        Assert.Equal("say \"hi\"", token.Value);
    }

    [Fact]
    public void Tokenize_StringWithEscapedBackslash_ReturnsCorrectValue()
    {
        var token = Single("\"path\\\\file\"");
        Assert.Equal(TokenType.StringLiteral, token.Type);
        Assert.Equal("path\\file", token.Value);
    }

    [Fact]
    public void Tokenize_StringWithUnicodeEscape_ReturnsCorrectValue()
    {
        var token = Single("\"\\u0041\"");
        Assert.Equal(TokenType.StringLiteral, token.Type);
        Assert.Equal("A", token.Value);
    }

    [Fact]
    public void Tokenize_StringWithHexEscape_ReturnsCorrectValue()
    {
        var token = Single("\"\\x41\"");
        Assert.Equal(TokenType.StringLiteral, token.Type);
        Assert.Equal("A", token.Value);
    }

    // ═══════════════════════════════════════════
    //  Template literals
    // ═══════════════════════════════════════════

    [Fact]
    public void Tokenize_TemplateLiteralNoSubstitution_ReturnsTemplateLiteral()
    {
        var token = Single("`hello world`");
        Assert.Equal(TokenType.TemplateLiteral, token.Type);
        Assert.Equal("hello world", token.Value);
    }

    [Fact]
    public void Tokenize_TemplateLiteralWithSubstitution_ReturnsCorrectTokens()
    {
        var tokens = Tokenize("`hello ${name}!`");
        Assert.Equal(TokenType.TemplateHead, tokens[0].Type);
        Assert.Equal("hello ", tokens[0].Value);
        Assert.Equal(TokenType.Identifier, tokens[1].Type);
        Assert.Equal("name", tokens[1].Value);
        Assert.Equal(TokenType.TemplateTail, tokens[2].Type);
        Assert.Equal("!", tokens[2].Value);
    }

    [Fact]
    public void Tokenize_TemplateLiteralWithMultipleSubstitutions_ReturnsCorrectTokens()
    {
        var tokens = Tokenize("`${a} and ${b}`");
        Assert.Equal(TokenType.TemplateHead, tokens[0].Type);
        Assert.Equal("", tokens[0].Value);
        Assert.Equal(TokenType.Identifier, tokens[1].Type);
        Assert.Equal("a", tokens[1].Value);
        Assert.Equal(TokenType.TemplateMiddle, tokens[2].Type);
        Assert.Equal(" and ", tokens[2].Value);
        Assert.Equal(TokenType.Identifier, tokens[3].Type);
        Assert.Equal("b", tokens[3].Value);
        Assert.Equal(TokenType.TemplateTail, tokens[4].Type);
        Assert.Equal("", tokens[4].Value);
    }

    // ═══════════════════════════════════════════
    //  Arithmetic operators
    // ═══════════════════════════════════════════

    [Theory]
    [InlineData("+", TokenType.Plus)]
    [InlineData("-", TokenType.Minus)]
    [InlineData("*", TokenType.Star)]
    [InlineData("/", TokenType.Slash)]
    [InlineData("%", TokenType.Percent)]
    [InlineData("**", TokenType.StarStar)]
    public void Tokenize_ArithmeticOperator_ReturnsCorrectType(string op, TokenType expected)
    {
        // Prefix with a value so operators like / are not treated as regex
        var tokens = Tokenize("x " + op + " y");
        var opToken = tokens[1];
        Assert.Equal(expected, opToken.Type);
    }

    // ═══════════════════════════════════════════
    //  Comparison operators
    // ═══════════════════════════════════════════

    [Theory]
    [InlineData("<", TokenType.LessThan)]
    [InlineData(">", TokenType.GreaterThan)]
    [InlineData("<=", TokenType.LessThanEqual)]
    [InlineData(">=", TokenType.GreaterThanEqual)]
    [InlineData("==", TokenType.EqualEqual)]
    [InlineData("!=", TokenType.BangEqual)]
    [InlineData("===", TokenType.EqualEqualEqual)]
    [InlineData("!==", TokenType.BangEqualEqual)]
    public void Tokenize_ComparisonOperator_ReturnsCorrectType(string op, TokenType expected)
    {
        var tokens = Tokenize("x " + op + " y");
        var opToken = tokens[1];
        Assert.Equal(expected, opToken.Type);
    }

    // ═══════════════════════════════════════════
    //  Assignment operators
    // ═══════════════════════════════════════════

    [Theory]
    [InlineData("=", TokenType.Assign)]
    [InlineData("+=", TokenType.PlusAssign)]
    [InlineData("-=", TokenType.MinusAssign)]
    [InlineData("*=", TokenType.StarAssign)]
    [InlineData("/=", TokenType.SlashAssign)]
    [InlineData("%=", TokenType.PercentAssign)]
    [InlineData("**=", TokenType.StarStarAssign)]
    [InlineData("<<=", TokenType.LeftShiftAssign)]
    [InlineData(">>=", TokenType.RightShiftAssign)]
    [InlineData(">>>=", TokenType.UnsignedRightShiftAssign)]
    [InlineData("&=", TokenType.AmpersandAssign)]
    [InlineData("|=", TokenType.PipeAssign)]
    [InlineData("^=", TokenType.CaretAssign)]
    [InlineData("&&=", TokenType.AmpersandAmpersandAssign)]
    [InlineData("||=", TokenType.PipePipeAssign)]
    [InlineData("??=", TokenType.QuestionQuestionAssign)]
    public void Tokenize_AssignmentOperator_ReturnsCorrectType(string op, TokenType expected)
    {
        var tokens = Tokenize("x " + op + " y");
        var opToken = tokens[1];
        Assert.Equal(expected, opToken.Type);
    }

    // ═══════════════════════════════════════════
    //  Logical operators
    // ═══════════════════════════════════════════

    [Theory]
    [InlineData("&&", TokenType.AmpersandAmpersand)]
    [InlineData("||", TokenType.PipePipe)]
    [InlineData("??", TokenType.QuestionQuestion)]
    [InlineData("!", TokenType.Bang)]
    public void Tokenize_LogicalOperator_ReturnsCorrectType(string op, TokenType expected)
    {
        // Use prefix for unary operators
        if (op == "!")
        {
            var token = Single("!x");
            Assert.Equal(expected, token.Type);
        }
        else
        {
            var tokens = Tokenize("x " + op + " y");
            var opToken = tokens[1];
            Assert.Equal(expected, opToken.Type);
        }
    }

    // ═══════════════════════════════════════════
    //  Punctuation
    // ═══════════════════════════════════════════

    [Theory]
    [InlineData("(", TokenType.LeftParen)]
    [InlineData(")", TokenType.RightParen)]
    [InlineData("{", TokenType.LeftBrace)]
    [InlineData("}", TokenType.RightBrace)]
    [InlineData("[", TokenType.LeftBracket)]
    [InlineData("]", TokenType.RightBracket)]
    [InlineData(";", TokenType.Semicolon)]
    [InlineData(",", TokenType.Comma)]
    [InlineData(":", TokenType.Colon)]
    public void Tokenize_Punctuation_ReturnsCorrectType(string source, TokenType expected)
    {
        var token = Single(source);
        Assert.Equal(expected, token.Type);
    }

    [Fact]
    public void Tokenize_Dot_ReturnsDot()
    {
        var tokens = Tokenize("a.b");
        Assert.Equal(TokenType.Identifier, tokens[0].Type);
        Assert.Equal(TokenType.Dot, tokens[1].Type);
        Assert.Equal(TokenType.Identifier, tokens[2].Type);
    }

    [Fact]
    public void Tokenize_Ellipsis_ReturnsEllipsis()
    {
        var tokens = Tokenize("...x");
        Assert.Equal(TokenType.Ellipsis, tokens[0].Type);
        Assert.Equal("...", tokens[0].Value);
    }

    [Fact]
    public void Tokenize_Arrow_ReturnsArrow()
    {
        var tokens = Tokenize("x => y");
        Assert.Equal(TokenType.Identifier, tokens[0].Type);
        Assert.Equal(TokenType.Arrow, tokens[1].Type);
        Assert.Equal("=>", tokens[1].Value);
    }

    [Fact]
    public void Tokenize_QuestionDot_ReturnsQuestionDot()
    {
        var tokens = Tokenize("a?.b");
        Assert.Equal(TokenType.Identifier, tokens[0].Type);
        Assert.Equal(TokenType.QuestionDot, tokens[1].Type);
        Assert.Equal(TokenType.Identifier, tokens[2].Type);
    }

    // ═══════════════════════════════════════════
    //  Increment / Decrement
    // ═══════════════════════════════════════════

    [Fact]
    public void Tokenize_PlusPlus_ReturnsPlusPlus()
    {
        var tokens = Tokenize("x++");
        Assert.Equal(TokenType.Identifier, tokens[0].Type);
        Assert.Equal(TokenType.PlusPlus, tokens[1].Type);
    }

    [Fact]
    public void Tokenize_MinusMinus_ReturnsMinusMinus()
    {
        var tokens = Tokenize("x--");
        Assert.Equal(TokenType.Identifier, tokens[0].Type);
        Assert.Equal(TokenType.MinusMinus, tokens[1].Type);
    }

    // ═══════════════════════════════════════════
    //  Comments
    // ═══════════════════════════════════════════

    [Fact]
    public void Tokenize_SingleLineComment_IsSkipped()
    {
        var tokens = Tokenize("x // this is a comment\ny");
        Assert.Equal(TokenType.Identifier, tokens[0].Type);
        Assert.Equal("x", tokens[0].Value);
        Assert.Equal(TokenType.Identifier, tokens[1].Type);
        Assert.Equal("y", tokens[1].Value);
        Assert.Equal(TokenType.EndOfFile, tokens[2].Type);
    }

    [Fact]
    public void Tokenize_MultiLineComment_IsSkipped()
    {
        var tokens = Tokenize("x /* comment */ y");
        Assert.Equal(TokenType.Identifier, tokens[0].Type);
        Assert.Equal("x", tokens[0].Value);
        Assert.Equal(TokenType.Identifier, tokens[1].Type);
        Assert.Equal("y", tokens[1].Value);
        Assert.Equal(TokenType.EndOfFile, tokens[2].Type);
    }

    [Fact]
    public void Tokenize_MultiLineCommentSpanningLines_IsSkipped()
    {
        var tokens = Tokenize("a /* multi\nline\ncomment */ b");
        Assert.Equal(TokenType.Identifier, tokens[0].Type);
        Assert.Equal("a", tokens[0].Value);
        Assert.Equal(TokenType.Identifier, tokens[1].Type);
        Assert.Equal("b", tokens[1].Value);
    }

    // ═══════════════════════════════════════════
    //  Line/column tracking
    // ═══════════════════════════════════════════

    [Fact]
    public void Tokenize_LineTracking_ReportsCorrectLine()
    {
        var tokens = Tokenize("x\ny\nz");
        Assert.Equal(1, tokens[0].Line);
        Assert.Equal(2, tokens[1].Line);
        Assert.Equal(3, tokens[2].Line);
    }

    [Fact]
    public void Tokenize_ColumnTracking_ReportsCorrectColumn()
    {
        var tokens = Tokenize("let x = 5");
        // "let" starts at column 0 after advancing through it
        Assert.Equal(0, tokens[0].Column);
    }

    // ═══════════════════════════════════════════
    //  End of file
    // ═══════════════════════════════════════════

    [Fact]
    public void Tokenize_EmptySource_ReturnsOnlyEof()
    {
        var tokens = Tokenize("");
        Assert.Single(tokens);
        Assert.Equal(TokenType.EndOfFile, tokens[0].Type);
    }

    [Fact]
    public void Tokenize_WhitespaceOnly_ReturnsOnlyEof()
    {
        var tokens = Tokenize("   \t\n  ");
        Assert.Single(tokens);
        Assert.Equal(TokenType.EndOfFile, tokens[0].Type);
    }

    // ═══════════════════════════════════════════
    //  Multiple tokens
    // ═══════════════════════════════════════════

    [Fact]
    public void Tokenize_VariableDeclaration_ReturnsCorrectSequence()
    {
        var tokens = Tokenize("let x = 42;");
        Assert.Equal(TokenType.Let, tokens[0].Type);
        Assert.Equal(TokenType.Identifier, tokens[1].Type);
        Assert.Equal("x", tokens[1].Value);
        Assert.Equal(TokenType.Assign, tokens[2].Type);
        Assert.Equal(TokenType.NumericLiteral, tokens[3].Type);
        Assert.Equal(42.0, tokens[3].NumericValue);
        Assert.Equal(TokenType.Semicolon, tokens[4].Type);
        Assert.Equal(TokenType.EndOfFile, tokens[5].Type);
    }

    [Fact]
    public void Tokenize_FunctionDeclaration_ReturnsCorrectSequence()
    {
        var tokens = Tokenize("function add(a, b) { return a + b; }");
        Assert.Equal(TokenType.Function, tokens[0].Type);
        Assert.Equal(TokenType.Identifier, tokens[1].Type);
        Assert.Equal("add", tokens[1].Value);
        Assert.Equal(TokenType.LeftParen, tokens[2].Type);
        Assert.Equal(TokenType.Identifier, tokens[3].Type);
        Assert.Equal("a", tokens[3].Value);
        Assert.Equal(TokenType.Comma, tokens[4].Type);
        Assert.Equal(TokenType.Identifier, tokens[5].Type);
        Assert.Equal("b", tokens[5].Value);
        Assert.Equal(TokenType.RightParen, tokens[6].Type);
    }
}
