using SuperRender.EcmaScript.Ast;
using SuperRender.EcmaScript.Errors;
using SuperRender.EcmaScript.Lexing;

namespace SuperRender.EcmaScript.Parsing;

public sealed partial class Parser
{
    private readonly List<Token> _tokens;
    private int _pos;

    // Precedence levels
    private const int PrecComma = 1;
    private const int PrecAssignment = 2;
    private const int PrecConditional = 3;
    private const int PrecNullishCoalescing = 4;
    private const int PrecLogicalOr = 5;
    private const int PrecLogicalAnd = 6;
    private const int PrecBitwiseOr = 7;
    private const int PrecBitwiseXor = 8;
    private const int PrecBitwiseAnd = 9;
    private const int PrecEquality = 10;
    private const int PrecRelational = 11;
    private const int PrecShift = 12;
    private const int PrecAdditive = 13;
    private const int PrecMultiplicative = 14;
    private const int PrecExponentiation = 15;
    private const int PrecUnary = 16;
    private const int PrecPostfix = 17;
    private const int PrecCall = 18;
    private const int PrecMember = 19;

    private bool _inForInit;

    public Parser(string source)
    {
        var lexer = new Lexer(source);
        _tokens = lexer.Tokenize();
    }

    public Program Parse()
    {
        var body = new List<SyntaxNode>();
        bool isModule = false;

        while (!IsAtEnd)
        {
            if (Current.Type == TokenType.Import || Current.Type == TokenType.Export)
                isModule = true;
            body.Add(ParseStatementOrDeclaration());
        }

        return new Program { Body = body, IsModule = isModule, Location = new SourceLocation(1, 0) };
    }

    // ═══════════════════════════════════════════
    //  Token helpers
    // ═══════════════════════════════════════════

    private Token Current => _pos < _tokens.Count ? _tokens[_pos] : _tokens[^1];

    private Token Peek(int offset = 1)
    {
        int idx = _pos + offset;
        return idx < _tokens.Count ? _tokens[idx] : _tokens[^1];
    }

    private bool IsAtEnd => _pos >= _tokens.Count || Current.Type == TokenType.EndOfFile;

    private Token Advance()
    {
        var token = Current;
        if (_pos < _tokens.Count) _pos++;
        return token;
    }

    private bool Match(TokenType type)
    {
        if (Current.Type != type) return false;
        Advance();
        return true;
    }

    private Token Expect(TokenType type)
    {
        if (Current.Type == type) return Advance();
        throw new JsSyntaxError($"Expected {type} but got {Current.Type} ('{Current.Value}')", Current.Line, Current.Column);
    }

    private void ExpectSemicolon()
    {
        if (Current.Type == TokenType.Semicolon) { Advance(); return; }
        if (Current.PrecedingLineTerminator) return;
        if (Current.Type == TokenType.RightBrace) return;
        if (Current.Type == TokenType.EndOfFile) return;
        throw new JsSyntaxError($"Expected semicolon but got {Current.Type}", Current.Line, Current.Column);
    }

    private SourceLocation Loc() => new(Current.Line, Current.Column);

    private static bool IsIdentifier(Token token) =>
        token.Type == TokenType.Identifier ||
        token.Type == TokenType.Async ||
        token.Type == TokenType.From ||
        token.Type == TokenType.As ||
        token.Type == TokenType.Of ||
        token.Type == TokenType.Get ||
        token.Type == TokenType.Set ||
        token.Type == TokenType.Static ||
        token.Type == TokenType.Let;

    private static bool IsIdentifierOrKeyword(Token token) =>
        IsIdentifier(token) || token.Type == TokenType.Yield || token.Type == TokenType.Await;

    private string ExpectIdentifierName()
    {
        if (IsIdentifier(Current) || IsIdentifierOrKeyword(Current))
            return Advance().Value;
        // Allow all keywords as property names
        if (Current.Type >= TokenType.Var && Current.Type <= TokenType.With)
            return Advance().Value;
        throw new JsSyntaxError($"Expected identifier but got {Current.Type}", Current.Line, Current.Column);
    }

    private SyntaxNode ParseStatementOrDeclaration()
    {
        return Current.Type switch
        {
            TokenType.Var or TokenType.Let or TokenType.Const => ParseVariableDeclaration(),
            TokenType.Function => ParseFunctionDeclaration(),
            TokenType.Class => ParseClassDeclaration(),
            TokenType.Async when Peek().Type == TokenType.Function && !Peek().PrecedingLineTerminator => ParseAsyncFunctionDeclaration(),
            TokenType.Import => ParseImportDeclaration(),
            TokenType.Export => ParseExportDeclaration(),
            _ => ParseStatement()
        };
    }

    // ═══════════════════════════════════════════
    //  Infix precedence mapping
    // ═══════════════════════════════════════════

    private int GetInfixPrecedence(Token token)
    {
        return token.Type switch
        {
            TokenType.Comma => PrecComma,

            TokenType.Assign or TokenType.PlusAssign or TokenType.MinusAssign or
            TokenType.StarAssign or TokenType.SlashAssign or TokenType.PercentAssign or
            TokenType.StarStarAssign or TokenType.LeftShiftAssign or TokenType.RightShiftAssign or
            TokenType.UnsignedRightShiftAssign or TokenType.AmpersandAssign or TokenType.PipeAssign or
            TokenType.CaretAssign or TokenType.AmpersandAmpersandAssign or TokenType.PipePipeAssign or
            TokenType.QuestionQuestionAssign => PrecAssignment,

            TokenType.QuestionMark => PrecConditional,
            TokenType.QuestionQuestion => PrecNullishCoalescing,
            TokenType.PipePipe => PrecLogicalOr,
            TokenType.AmpersandAmpersand => PrecLogicalAnd,
            TokenType.Pipe => PrecBitwiseOr,
            TokenType.Caret => PrecBitwiseXor,
            TokenType.Ampersand => PrecBitwiseAnd,

            TokenType.EqualEqual or TokenType.BangEqual or
            TokenType.EqualEqualEqual or TokenType.BangEqualEqual => PrecEquality,

            TokenType.LessThan or TokenType.GreaterThan or
            TokenType.LessThanEqual or TokenType.GreaterThanEqual or
            TokenType.Instanceof => PrecRelational,

            TokenType.In when !_inForInit => PrecRelational,

            TokenType.LeftShift or TokenType.RightShift or
            TokenType.UnsignedRightShift => PrecShift,

            TokenType.Plus or TokenType.Minus => PrecAdditive,

            TokenType.Star or TokenType.Slash or TokenType.Percent => PrecMultiplicative,

            TokenType.StarStar => PrecExponentiation,

            TokenType.PlusPlus or TokenType.MinusMinus => PrecPostfix,

            TokenType.Dot or TokenType.LeftBracket or TokenType.QuestionDot => PrecMember,

            TokenType.LeftParen => PrecCall,

            TokenType.TemplateLiteral or TokenType.TemplateHead => PrecCall,

            _ => 0
        };
    }

    private static bool IsAssignment(TokenType type) =>
        type is TokenType.Assign or TokenType.PlusAssign or TokenType.MinusAssign or
        TokenType.StarAssign or TokenType.SlashAssign or TokenType.PercentAssign or
        TokenType.StarStarAssign or TokenType.LeftShiftAssign or TokenType.RightShiftAssign or
        TokenType.UnsignedRightShiftAssign or TokenType.AmpersandAssign or TokenType.PipeAssign or
        TokenType.CaretAssign or TokenType.AmpersandAmpersandAssign or TokenType.PipePipeAssign or
        TokenType.QuestionQuestionAssign;
}
