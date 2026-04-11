using System.Globalization;
using System.Text;

namespace SuperRender.EcmaScript.Compiler.Lexing;

public sealed class Lexer
{
    private readonly string _input;
    private int _pos;
    private int _line;
    private int _column;
    private bool _hadLineTerminator;
    private int _templateDepth;
    private readonly Stack<int> _braceStack = new();
    private Token? _lastToken;

    private static readonly Dictionary<string, TokenType> Keywords = new(StringComparer.Ordinal)
    {
        ["var"] = TokenType.Var,
        ["let"] = TokenType.Let,
        ["const"] = TokenType.Const,
        ["function"] = TokenType.Function,
        ["return"] = TokenType.Return,
        ["if"] = TokenType.If,
        ["else"] = TokenType.Else,
        ["for"] = TokenType.For,
        ["while"] = TokenType.While,
        ["do"] = TokenType.Do,
        ["switch"] = TokenType.Switch,
        ["case"] = TokenType.Case,
        ["default"] = TokenType.Default,
        ["break"] = TokenType.Break,
        ["continue"] = TokenType.Continue,
        ["throw"] = TokenType.Throw,
        ["try"] = TokenType.Try,
        ["catch"] = TokenType.Catch,
        ["finally"] = TokenType.Finally,
        ["new"] = TokenType.New,
        ["delete"] = TokenType.Delete,
        ["typeof"] = TokenType.Typeof,
        ["void"] = TokenType.Void,
        ["in"] = TokenType.In,
        ["of"] = TokenType.Of,
        ["instanceof"] = TokenType.Instanceof,
        ["this"] = TokenType.This,
        ["super"] = TokenType.Super,
        ["class"] = TokenType.Class,
        ["extends"] = TokenType.Extends,
        ["import"] = TokenType.Import,
        ["export"] = TokenType.Export,
        ["from"] = TokenType.From,
        ["as"] = TokenType.As,
        ["yield"] = TokenType.Yield,
        ["async"] = TokenType.Async,
        ["await"] = TokenType.Await,
        ["static"] = TokenType.Static,
        ["get"] = TokenType.Get,
        ["set"] = TokenType.Set,
        ["debugger"] = TokenType.Debugger,
        ["with"] = TokenType.With,
        ["true"] = TokenType.TrueLiteral,
        ["false"] = TokenType.FalseLiteral,
        ["null"] = TokenType.NullLiteral,
    };

    public Lexer(string source)
    {
        _input = source ?? throw new ArgumentNullException(nameof(source));
        _pos = 0;
        _line = 1;
        _column = 0;
        _hadLineTerminator = false;
        _templateDepth = 0;
    }

    private bool IsAtEnd => _pos >= _input.Length;

    private char Peek()
    {
        return IsAtEnd ? '\0' : _input[_pos];
    }

    private char PeekAt(int offset)
    {
        int index = _pos + offset;
        return index >= _input.Length ? '\0' : _input[index];
    }

    private char Advance()
    {
        char c = _input[_pos];
        _pos++;
        _column++;
        return c;
    }

    private bool Match(char expected)
    {
        if (IsAtEnd || _input[_pos] != expected)
        {
            return false;
        }
        _pos++;
        _column++;
        return true;
    }

    public List<Token> Tokenize()
    {
        var tokens = new List<Token>();

        while (true)
        {
            Token token = NextToken();
            tokens.Add(token);
            if (token.Type == TokenType.EndOfFile)
            {
                break;
            }
        }

        return tokens;
    }

    private Token NextToken()
    {
        _hadLineTerminator = false;
        SkipWhitespaceAndComments();

        if (IsAtEnd)
        {
            return MakeToken(TokenType.EndOfFile, "", _line, _column);
        }

        int tokenLine = _line;
        int tokenColumn = _column;
        char c = Peek();

        // Template middle/tail: when we encounter '}' and we're inside a template expression
        if (c == '}' && _templateDepth > 0 && _braceStack.Count > 0 && _braceStack.Peek() == _templateDepth)
        {
            _braceStack.Pop();
            Advance(); // consume '}'
            return ScanTemplateMiddleOrTail(tokenLine, tokenColumn);
        }

        // Identifiers and keywords
        if (IsIdentifierStart(c))
        {
            return ScanIdentifierOrKeyword(tokenLine, tokenColumn);
        }

        // Numeric literals
        if (char.IsAsciiDigit(c))
        {
            return ScanNumber(tokenLine, tokenColumn);
        }

        // Dot can start a number (.5) or be an operator
        if (c == '.' && _pos + 1 < _input.Length && char.IsAsciiDigit(_input[_pos + 1]))
        {
            return ScanNumber(tokenLine, tokenColumn);
        }

        // String literals
        if (c == '\'' || c == '"')
        {
            return ScanString(tokenLine, tokenColumn);
        }

        // Template literals
        if (c == '`')
        {
            Advance(); // consume '`'
            return ScanTemplateLiteral(tokenLine, tokenColumn);
        }

        // Slash: could be division, regex, or comment (comments already handled)
        if (c == '/')
        {
            if (ShouldScanRegex())
            {
                return ScanRegExp(tokenLine, tokenColumn);
            }
        }

        // Hash for private fields
        if (c == '#')
        {
            Advance(); // consume '#'
            var hashToken = MakeToken(TokenType.Hash, "#", tokenLine, tokenColumn);
            _lastToken = hashToken;
            return hashToken;
        }

        // Punctuation and operators
        return ScanPunctuator(tokenLine, tokenColumn);
    }

    private void SkipWhitespaceAndComments()
    {
        while (!IsAtEnd)
        {
            char c = Peek();

            // Whitespace
            if (c == ' ' || c == '\t' || c == '\f' || c == '\v' || c == '\u00A0' || c == '\uFEFF')
            {
                Advance();
                continue;
            }

            // Line terminators
            if (c == '\n')
            {
                _hadLineTerminator = true;
                Advance();
                _line++;
                _column = 0;
                continue;
            }

            if (c == '\r')
            {
                _hadLineTerminator = true;
                Advance();
                if (!IsAtEnd && Peek() == '\n')
                {
                    Advance();
                }
                _line++;
                _column = 0;
                continue;
            }

            // Unicode line separator / paragraph separator
            if (c == '\u2028' || c == '\u2029')
            {
                _hadLineTerminator = true;
                Advance();
                _line++;
                _column = 0;
                continue;
            }

            // Single-line comment
            if (c == '/' && PeekAt(1) == '/')
            {
                Advance(); // '/'
                Advance(); // '/'
                while (!IsAtEnd)
                {
                    char ch = Peek();
                    if (ch == '\n' || ch == '\r' || ch == '\u2028' || ch == '\u2029')
                    {
                        break;
                    }
                    Advance();
                }
                continue;
            }

            // Multi-line comment
            if (c == '/' && PeekAt(1) == '*')
            {
                Advance(); // '/'
                Advance(); // '*'
                while (!IsAtEnd)
                {
                    if (Peek() == '*' && PeekAt(1) == '/')
                    {
                        Advance(); // '*'
                        Advance(); // '/'
                        break;
                    }
                    if (Peek() == '\n')
                    {
                        _hadLineTerminator = true;
                        Advance();
                        _line++;
                        _column = 0;
                    }
                    else if (Peek() == '\r')
                    {
                        _hadLineTerminator = true;
                        Advance();
                        if (!IsAtEnd && Peek() == '\n')
                        {
                            Advance();
                        }
                        _line++;
                        _column = 0;
                    }
                    else if (Peek() == '\u2028' || Peek() == '\u2029')
                    {
                        _hadLineTerminator = true;
                        Advance();
                        _line++;
                        _column = 0;
                    }
                    else
                    {
                        Advance();
                    }
                }
                continue;
            }

            break;
        }
    }

    private Token ScanIdentifierOrKeyword(int tokenLine, int tokenColumn)
    {
        var sb = new StringBuilder();
        sb.Append(Advance());

        while (!IsAtEnd && IsIdentifierPart(Peek()))
        {
            sb.Append(Advance());
        }

        string word = sb.ToString();

        if (Keywords.TryGetValue(word, out var keywordType))
        {
            return MakeToken(keywordType, word, tokenLine, tokenColumn);
        }

        return MakeToken(TokenType.Identifier, word, tokenLine, tokenColumn);
    }

    private Token ScanNumber(int tokenLine, int tokenColumn)
    {
        var sb = new StringBuilder();
        char c = Peek();

        // Check for hex, octal, binary prefixes
        if (c == '0' && _pos + 1 < _input.Length)
        {
            char next = _input[_pos + 1];

            if (next == 'x' || next == 'X')
            {
                return ScanHexNumber(tokenLine, tokenColumn);
            }

            if (next == 'o' || next == 'O')
            {
                return ScanOctalNumber(tokenLine, tokenColumn);
            }

            if (next == 'b' || next == 'B')
            {
                return ScanBinaryNumber(tokenLine, tokenColumn);
            }
        }

        // Decimal number
        // Integer part
        if (c != '.')
        {
            ScanDecimalDigits(sb);
        }

        // Fractional part
        if (!IsAtEnd && Peek() == '.')
        {
            // Make sure it's not `..` (like in spread) or a method call on a number
            if (PeekAt(1) != '.' && !IsIdentifierStart(PeekAt(1)))
            {
                sb.Append(Advance()); // '.'
                ScanDecimalDigits(sb);
            }
            else if (char.IsAsciiDigit(PeekAt(1)))
            {
                sb.Append(Advance()); // '.'
                ScanDecimalDigits(sb);
            }
        }

        // Exponent part
        if (!IsAtEnd && (Peek() == 'e' || Peek() == 'E'))
        {
            sb.Append(Advance()); // 'e' or 'E'
            if (!IsAtEnd && (Peek() == '+' || Peek() == '-'))
            {
                sb.Append(Advance());
            }
            ScanDecimalDigits(sb);
        }

        // BigInt suffix 'n' — we include it in the value but parse numeric without it
        bool isBigInt = !IsAtEnd && Peek() == 'n';
        if (isBigInt)
        {
            sb.Append(Advance());
        }

        string raw = sb.ToString();
        string numericPart = isBigInt ? raw[..^1] : raw;
        string stripped = numericPart.Replace("_", "", StringComparison.Ordinal);
        double numericValue = double.TryParse(stripped, NumberStyles.Float, CultureInfo.InvariantCulture, out double val) ? val : 0;

        return MakeToken(TokenType.NumericLiteral, raw, tokenLine, tokenColumn, numericValue);
    }

    private Token ScanHexNumber(int tokenLine, int tokenColumn)
    {
        var sb = new StringBuilder();
        sb.Append(Advance()); // '0'
        sb.Append(Advance()); // 'x' or 'X'

        while (!IsAtEnd && (IsHexDigit(Peek()) || Peek() == '_'))
        {
            sb.Append(Advance());
        }

        if (!IsAtEnd && Peek() == 'n')
        {
            sb.Append(Advance());
        }

        string raw = sb.ToString();
        string hexPart = raw.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? raw[2..] : raw;
        if (hexPart.EndsWith('n'))
        {
            hexPart = hexPart[..^1];
        }
        string stripped = hexPart.Replace("_", "", StringComparison.Ordinal);
        double numericValue = long.TryParse(stripped, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out long hexVal) ? hexVal : 0;

        return MakeToken(TokenType.NumericLiteral, raw, tokenLine, tokenColumn, numericValue);
    }

    private Token ScanOctalNumber(int tokenLine, int tokenColumn)
    {
        var sb = new StringBuilder();
        sb.Append(Advance()); // '0'
        sb.Append(Advance()); // 'o' or 'O'

        while (!IsAtEnd && (IsOctalDigit(Peek()) || Peek() == '_'))
        {
            sb.Append(Advance());
        }

        if (!IsAtEnd && Peek() == 'n')
        {
            sb.Append(Advance());
        }

        string raw = sb.ToString();
        string octalPart = raw[2..];
        if (octalPart.EndsWith('n'))
        {
            octalPart = octalPart[..^1];
        }
        string stripped = octalPart.Replace("_", "", StringComparison.Ordinal);
        double numericValue = 0;
        foreach (char ch in stripped)
        {
            numericValue = (numericValue * 8) + (ch - '0');
        }

        return MakeToken(TokenType.NumericLiteral, raw, tokenLine, tokenColumn, numericValue);
    }

    private Token ScanBinaryNumber(int tokenLine, int tokenColumn)
    {
        var sb = new StringBuilder();
        sb.Append(Advance()); // '0'
        sb.Append(Advance()); // 'b' or 'B'

        while (!IsAtEnd && (Peek() == '0' || Peek() == '1' || Peek() == '_'))
        {
            sb.Append(Advance());
        }

        if (!IsAtEnd && Peek() == 'n')
        {
            sb.Append(Advance());
        }

        string raw = sb.ToString();
        string binPart = raw[2..];
        if (binPart.EndsWith('n'))
        {
            binPart = binPart[..^1];
        }
        string stripped = binPart.Replace("_", "", StringComparison.Ordinal);
        double numericValue = 0;
        foreach (char ch in stripped)
        {
            numericValue = (numericValue * 2) + (ch - '0');
        }

        return MakeToken(TokenType.NumericLiteral, raw, tokenLine, tokenColumn, numericValue);
    }

    private void ScanDecimalDigits(StringBuilder sb)
    {
        while (!IsAtEnd && (char.IsAsciiDigit(Peek()) || Peek() == '_'))
        {
            sb.Append(Advance());
        }
    }

    private Token ScanString(int tokenLine, int tokenColumn)
    {
        char quote = Advance(); // consume opening quote
        var sb = new StringBuilder();

        while (!IsAtEnd)
        {
            char c = Peek();

            if (c == quote)
            {
                Advance(); // consume closing quote
                return MakeToken(TokenType.StringLiteral, sb.ToString(), tokenLine, tokenColumn);
            }

            if (c == '\n' || c == '\r')
            {
                throw new InvalidOperationException($"Unterminated string literal at {_line}:{_column}");
            }

            if (c == '\\')
            {
                Advance(); // consume backslash
                sb.Append(ScanEscapeSequenceAsString());
                continue;
            }

            sb.Append(Advance());
        }

        throw new InvalidOperationException($"Unterminated string literal at {tokenLine}:{tokenColumn}");
    }

    private string ScanEscapeSequenceAsString()
    {
        if (IsAtEnd)
        {
            throw new InvalidOperationException($"Unexpected end of input in escape sequence at {_line}:{_column}");
        }

        char c = Advance();
        return c switch
        {
            '\\' => "\\",
            '\'' => "'",
            '"' => "\"",
            'n' => "\n",
            'r' => "\r",
            't' => "\t",
            '0' => "\0",
            'b' => "\b",
            'f' => "\f",
            'v' => "\v",
            'u' => ScanUnicodeEscape().ToString(),
            'x' => ScanHexEscape().ToString(),
            '\n' => HandleLineContinuation('\n'),
            '\r' => HandleLineContinuation('\r'),
            _ => c.ToString(),
        };
    }

    private string HandleLineContinuation(char first)
    {
        if (first == '\r' && !IsAtEnd && Peek() == '\n')
        {
            Advance();
        }
        _line++;
        _column = 0;
        return ""; // line continuation produces no character
    }

    private char ScanUnicodeEscape()
    {
        if (!IsAtEnd && Peek() == '{')
        {
            // \u{XXXXX} form
            Advance(); // consume '{'
            var hex = new StringBuilder();
            while (!IsAtEnd && Peek() != '}')
            {
                hex.Append(Advance());
            }
            if (IsAtEnd)
            {
                throw new InvalidOperationException($"Unterminated unicode escape at {_line}:{_column}");
            }
            Advance(); // consume '}'
            int codePoint = int.Parse(hex.ToString(), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            return (char)codePoint; // simplified: doesn't handle surrogate pairs for codepoints > 0xFFFF
        }
        else
        {
            // \uXXXX form
            var hex = new StringBuilder(4);
            for (int i = 0; i < 4; i++)
            {
                if (IsAtEnd)
                {
                    throw new InvalidOperationException($"Invalid unicode escape at {_line}:{_column}");
                }
                hex.Append(Advance());
            }
            return (char)int.Parse(hex.ToString(), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        }
    }

    private char ScanHexEscape()
    {
        var hex = new StringBuilder(2);
        for (int i = 0; i < 2; i++)
        {
            if (IsAtEnd)
            {
                throw new InvalidOperationException($"Invalid hex escape at {_line}:{_column}");
            }
            hex.Append(Advance());
        }
        return (char)int.Parse(hex.ToString(), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
    }

    private Token ScanTemplateLiteral(int tokenLine, int tokenColumn)
    {
        var sb = new StringBuilder();

        while (!IsAtEnd)
        {
            char c = Peek();

            if (c == '`')
            {
                Advance(); // consume closing backtick
                // If we're in a template expression context, this is a TemplateTail
                // But since we entered from a fresh backtick in NextToken, this is a plain TemplateLiteral
                return MakeToken(TokenType.TemplateLiteral, sb.ToString(), tokenLine, tokenColumn);
            }

            if (c == '$' && PeekAt(1) == '{')
            {
                Advance(); // '$'
                Advance(); // '{'
                _templateDepth++;
                _braceStack.Push(_templateDepth);
                return MakeToken(TokenType.TemplateHead, sb.ToString(), tokenLine, tokenColumn);
            }

            if (c == '\\')
            {
                Advance(); // consume backslash
                string escaped = ScanTemplateEscapeSequence();
                sb.Append(escaped);
                continue;
            }

            if (c == '\n')
            {
                sb.Append(Advance());
                _line++;
                _column = 0;
                continue;
            }

            if (c == '\r')
            {
                Advance();
                if (!IsAtEnd && Peek() == '\n')
                {
                    Advance();
                }
                sb.Append('\n');
                _line++;
                _column = 0;
                continue;
            }

            sb.Append(Advance());
        }

        throw new InvalidOperationException($"Unterminated template literal at {tokenLine}:{tokenColumn}");
    }

    private Token ScanTemplateMiddleOrTail(int tokenLine, int tokenColumn)
    {
        var sb = new StringBuilder();

        while (!IsAtEnd)
        {
            char c = Peek();

            if (c == '`')
            {
                Advance(); // consume closing backtick
                _templateDepth--;
                return MakeToken(TokenType.TemplateTail, sb.ToString(), tokenLine, tokenColumn);
            }

            if (c == '$' && PeekAt(1) == '{')
            {
                Advance(); // '$'
                Advance(); // '{'
                _braceStack.Push(_templateDepth);
                return MakeToken(TokenType.TemplateMiddle, sb.ToString(), tokenLine, tokenColumn);
            }

            if (c == '\\')
            {
                Advance(); // consume backslash
                string escaped = ScanTemplateEscapeSequence();
                sb.Append(escaped);
                continue;
            }

            if (c == '\n')
            {
                sb.Append(Advance());
                _line++;
                _column = 0;
                continue;
            }

            if (c == '\r')
            {
                Advance();
                if (!IsAtEnd && Peek() == '\n')
                {
                    Advance();
                }
                sb.Append('\n');
                _line++;
                _column = 0;
                continue;
            }

            sb.Append(Advance());
        }

        throw new InvalidOperationException($"Unterminated template literal at {tokenLine}:{tokenColumn}");
    }

    private string ScanTemplateEscapeSequence()
    {
        if (IsAtEnd)
        {
            throw new InvalidOperationException($"Unexpected end of input in escape sequence at {_line}:{_column}");
        }

        char c = Peek();

        if (c == '\n')
        {
            Advance();
            _line++;
            _column = 0;
            return "\n";
        }

        if (c == '\r')
        {
            Advance();
            if (!IsAtEnd && Peek() == '\n')
            {
                Advance();
            }
            _line++;
            _column = 0;
            return "\n";
        }

        return ScanEscapeSequenceAsString();
    }

    private bool ShouldScanRegex()
    {
        if (_lastToken is null)
        {
            return true;
        }

        // If the previous token is a value-producing token, '/' is division
        return _lastToken.Type switch
        {
            TokenType.NumericLiteral => false,
            TokenType.StringLiteral => false,
            TokenType.Identifier => false,
            TokenType.TrueLiteral => false,
            TokenType.FalseLiteral => false,
            TokenType.NullLiteral => false,
            TokenType.This => false,
            TokenType.RightParen => false,
            TokenType.RightBracket => false,
            TokenType.PlusPlus => false,
            TokenType.MinusMinus => false,
            TokenType.TemplateTail => false,
            TokenType.TemplateLiteral => false,
            _ => true,
        };
    }

    private Token ScanRegExp(int tokenLine, int tokenColumn)
    {
        var sb = new StringBuilder();
        sb.Append(Advance()); // consume opening '/'

        bool inCharClass = false;

        while (!IsAtEnd)
        {
            char c = Peek();

            if (c == '\n' || c == '\r')
            {
                throw new InvalidOperationException($"Unterminated regular expression at {_line}:{_column}");
            }

            if (c == '\\')
            {
                sb.Append(Advance()); // '\'
                if (!IsAtEnd && Peek() != '\n' && Peek() != '\r')
                {
                    sb.Append(Advance()); // escaped char
                }
                continue;
            }

            if (c == '[')
            {
                inCharClass = true;
                sb.Append(Advance());
                continue;
            }

            if (c == ']')
            {
                inCharClass = false;
                sb.Append(Advance());
                continue;
            }

            if (c == '/' && !inCharClass)
            {
                sb.Append(Advance()); // consume closing '/'
                // Scan flags
                while (!IsAtEnd && IsRegExpFlag(Peek()))
                {
                    sb.Append(Advance());
                }
                return MakeToken(TokenType.RegExpLiteral, sb.ToString(), tokenLine, tokenColumn);
            }

            sb.Append(Advance());
        }

        throw new InvalidOperationException($"Unterminated regular expression at {tokenLine}:{tokenColumn}");
    }

    private Token ScanPunctuator(int tokenLine, int tokenColumn)
    {
        char c = Advance();

        switch (c)
        {
            case '(':
                return MakeToken(TokenType.LeftParen, "(", tokenLine, tokenColumn);
            case ')':
                return MakeToken(TokenType.RightParen, ")", tokenLine, tokenColumn);
            case '{':
                return MakeToken(TokenType.LeftBrace, "{", tokenLine, tokenColumn);
            case '}':
                return MakeToken(TokenType.RightBrace, "}", tokenLine, tokenColumn);
            case '[':
                return MakeToken(TokenType.LeftBracket, "[", tokenLine, tokenColumn);
            case ']':
                return MakeToken(TokenType.RightBracket, "]", tokenLine, tokenColumn);
            case ';':
                return MakeToken(TokenType.Semicolon, ";", tokenLine, tokenColumn);
            case ',':
                return MakeToken(TokenType.Comma, ",", tokenLine, tokenColumn);
            case '~':
                return MakeToken(TokenType.Tilde, "~", tokenLine, tokenColumn);
            case ':':
                return MakeToken(TokenType.Colon, ":", tokenLine, tokenColumn);

            case '.':
                if (Match('.'))
                {
                    if (Match('.'))
                    {
                        return MakeToken(TokenType.Ellipsis, "...", tokenLine, tokenColumn);
                    }
                    // Two dots is not valid JS, but put back and return single dot
                    _pos--;
                    _column--;
                    return MakeToken(TokenType.Dot, ".", tokenLine, tokenColumn);
                }
                return MakeToken(TokenType.Dot, ".", tokenLine, tokenColumn);

            case '?':
                if (Match('?'))
                {
                    if (Match('='))
                    {
                        return MakeToken(TokenType.QuestionQuestionAssign, "??=", tokenLine, tokenColumn);
                    }
                    return MakeToken(TokenType.QuestionQuestion, "??", tokenLine, tokenColumn);
                }
                if (Match('.'))
                {
                    // ?. but not ?.digit (which would be ? followed by a number)
                    if (!IsAtEnd && char.IsAsciiDigit(Peek()))
                    {
                        _pos--;
                        _column--;
                        return MakeToken(TokenType.QuestionMark, "?", tokenLine, tokenColumn);
                    }
                    return MakeToken(TokenType.QuestionDot, "?.", tokenLine, tokenColumn);
                }
                return MakeToken(TokenType.QuestionMark, "?", tokenLine, tokenColumn);

            case '+':
                if (Match('+'))
                {
                    return MakeToken(TokenType.PlusPlus, "++", tokenLine, tokenColumn);
                }
                if (Match('='))
                {
                    return MakeToken(TokenType.PlusAssign, "+=", tokenLine, tokenColumn);
                }
                return MakeToken(TokenType.Plus, "+", tokenLine, tokenColumn);

            case '-':
                if (Match('-'))
                {
                    return MakeToken(TokenType.MinusMinus, "--", tokenLine, tokenColumn);
                }
                if (Match('='))
                {
                    return MakeToken(TokenType.MinusAssign, "-=", tokenLine, tokenColumn);
                }
                return MakeToken(TokenType.Minus, "-", tokenLine, tokenColumn);

            case '*':
                if (Match('*'))
                {
                    if (Match('='))
                    {
                        return MakeToken(TokenType.StarStarAssign, "**=", tokenLine, tokenColumn);
                    }
                    return MakeToken(TokenType.StarStar, "**", tokenLine, tokenColumn);
                }
                if (Match('='))
                {
                    return MakeToken(TokenType.StarAssign, "*=", tokenLine, tokenColumn);
                }
                return MakeToken(TokenType.Star, "*", tokenLine, tokenColumn);

            case '/':
                if (Match('='))
                {
                    return MakeToken(TokenType.SlashAssign, "/=", tokenLine, tokenColumn);
                }
                return MakeToken(TokenType.Slash, "/", tokenLine, tokenColumn);

            case '%':
                if (Match('='))
                {
                    return MakeToken(TokenType.PercentAssign, "%=", tokenLine, tokenColumn);
                }
                return MakeToken(TokenType.Percent, "%", tokenLine, tokenColumn);

            case '<':
                if (Match('<'))
                {
                    if (Match('='))
                    {
                        return MakeToken(TokenType.LeftShiftAssign, "<<=", tokenLine, tokenColumn);
                    }
                    return MakeToken(TokenType.LeftShift, "<<", tokenLine, tokenColumn);
                }
                if (Match('='))
                {
                    return MakeToken(TokenType.LessThanEqual, "<=", tokenLine, tokenColumn);
                }
                return MakeToken(TokenType.LessThan, "<", tokenLine, tokenColumn);

            case '>':
                if (Match('>'))
                {
                    if (Match('>'))
                    {
                        if (Match('='))
                        {
                            return MakeToken(TokenType.UnsignedRightShiftAssign, ">>>=", tokenLine, tokenColumn);
                        }
                        return MakeToken(TokenType.UnsignedRightShift, ">>>", tokenLine, tokenColumn);
                    }
                    if (Match('='))
                    {
                        return MakeToken(TokenType.RightShiftAssign, ">>=", tokenLine, tokenColumn);
                    }
                    return MakeToken(TokenType.RightShift, ">>", tokenLine, tokenColumn);
                }
                if (Match('='))
                {
                    return MakeToken(TokenType.GreaterThanEqual, ">=", tokenLine, tokenColumn);
                }
                return MakeToken(TokenType.GreaterThan, ">", tokenLine, tokenColumn);

            case '=':
                if (Match('='))
                {
                    if (Match('='))
                    {
                        return MakeToken(TokenType.EqualEqualEqual, "===", tokenLine, tokenColumn);
                    }
                    return MakeToken(TokenType.EqualEqual, "==", tokenLine, tokenColumn);
                }
                if (Match('>'))
                {
                    return MakeToken(TokenType.Arrow, "=>", tokenLine, tokenColumn);
                }
                return MakeToken(TokenType.Assign, "=", tokenLine, tokenColumn);

            case '!':
                if (Match('='))
                {
                    if (Match('='))
                    {
                        return MakeToken(TokenType.BangEqualEqual, "!==", tokenLine, tokenColumn);
                    }
                    return MakeToken(TokenType.BangEqual, "!=", tokenLine, tokenColumn);
                }
                return MakeToken(TokenType.Bang, "!", tokenLine, tokenColumn);

            case '&':
                if (Match('&'))
                {
                    if (Match('='))
                    {
                        return MakeToken(TokenType.AmpersandAmpersandAssign, "&&=", tokenLine, tokenColumn);
                    }
                    return MakeToken(TokenType.AmpersandAmpersand, "&&", tokenLine, tokenColumn);
                }
                if (Match('='))
                {
                    return MakeToken(TokenType.AmpersandAssign, "&=", tokenLine, tokenColumn);
                }
                return MakeToken(TokenType.Ampersand, "&", tokenLine, tokenColumn);

            case '|':
                if (Match('|'))
                {
                    if (Match('='))
                    {
                        return MakeToken(TokenType.PipePipeAssign, "||=", tokenLine, tokenColumn);
                    }
                    return MakeToken(TokenType.PipePipe, "||", tokenLine, tokenColumn);
                }
                if (Match('='))
                {
                    return MakeToken(TokenType.PipeAssign, "|=", tokenLine, tokenColumn);
                }
                return MakeToken(TokenType.Pipe, "|", tokenLine, tokenColumn);

            case '^':
                if (Match('='))
                {
                    return MakeToken(TokenType.CaretAssign, "^=", tokenLine, tokenColumn);
                }
                return MakeToken(TokenType.Caret, "^", tokenLine, tokenColumn);

            default:
                throw new InvalidOperationException($"Unexpected character '{c}' at {tokenLine}:{tokenColumn}");
        }
    }

    private Token MakeToken(TokenType type, string value, int line, int column, double numericValue = 0)
    {
        var token = new Token
        {
            Type = type,
            Value = value,
            NumericValue = numericValue,
            Line = line,
            Column = column,
            PrecedingLineTerminator = _hadLineTerminator,
        };
        _lastToken = token;
        return token;
    }

    private static bool IsIdentifierStart(char c)
    {
        return char.IsLetter(c) || c == '_' || c == '$';
    }

    private static bool IsIdentifierPart(char c)
    {
        return char.IsLetterOrDigit(c) || c == '_' || c == '$';
    }

    private static bool IsHexDigit(char c)
    {
        return char.IsAsciiHexDigit(c);
    }

    private static bool IsOctalDigit(char c)
    {
        return c is >= '0' and <= '7';
    }

    private static bool IsRegExpFlag(char c)
    {
        return c is 'g' or 'i' or 'm' or 's' or 'u' or 'y' or 'd' or 'v';
    }
}
