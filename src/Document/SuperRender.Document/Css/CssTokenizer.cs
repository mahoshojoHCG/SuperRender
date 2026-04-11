using System.Globalization;
using System.Text;

namespace SuperRender.Document.Css;

public sealed class CssTokenizer
{
    private readonly string _input;
    private int _pos;

    public CssTokenizer(string input)
    {
        _input = input;
    }

    public IEnumerable<CssToken> Tokenize()
    {
        while (_pos < _input.Length)
        {
            // Skip comments
            if (Peek() == '/' && _pos + 1 < _input.Length && _input[_pos + 1] == '*')
            {
                SkipComment();
                continue;
            }

            char c = Peek();

            // Whitespace
            if (char.IsWhiteSpace(c))
            {
                ConsumeWhitespace();
                yield return new CssToken { Type = CssTokenType.Whitespace, Value = " " };
                continue;
            }

            // Strings
            if (c == '"' || c == '\'')
            {
                yield return ConsumeString(c);
                continue;
            }

            // Hash (#identifier or #hex color)
            if (c == '#')
            {
                Advance();
                var name = ConsumeIdentChars();
                yield return new CssToken { Type = CssTokenType.Hash, Value = name };
                continue;
            }

            // Numbers (and dimensions/percentages)
            // Handles: 123, 1.5, .5, +1, -1, +1.5, -1.5
            if (IsNumberStart(c))
            {
                yield return ConsumeNumeric();
                continue;
            }

            // Identifiers and functions
            if (IsIdentStart(c))
            {
                var ident = ConsumeIdentChars();
                if (_pos < _input.Length && Peek() == '(')
                {
                    Advance(); // consume '('
                    yield return new CssToken { Type = CssTokenType.Function, Value = ident };
                }
                else
                {
                    yield return new CssToken { Type = CssTokenType.Ident, Value = ident };
                }
                continue;
            }

            // Single-character tokens
            Advance();
            switch (c)
            {
                case '.':
                    // Check if this is the start of a number like .5
                    if (_pos < _input.Length && char.IsAsciiDigit(Peek()))
                    {
                        _pos--; // back up to the dot
                        yield return ConsumeNumeric();
                    }
                    else
                    {
                        yield return new CssToken { Type = CssTokenType.Dot, Value = "." };
                    }
                    break;
                case ':':
                    yield return new CssToken { Type = CssTokenType.Colon, Value = ":" };
                    break;
                case ';':
                    yield return new CssToken { Type = CssTokenType.Semicolon, Value = ";" };
                    break;
                case '{':
                    yield return new CssToken { Type = CssTokenType.LeftBrace, Value = "{" };
                    break;
                case '}':
                    yield return new CssToken { Type = CssTokenType.RightBrace, Value = "}" };
                    break;
                case ',':
                    yield return new CssToken { Type = CssTokenType.Comma, Value = "," };
                    break;
                case '(':
                    yield return new CssToken { Type = CssTokenType.LeftParen, Value = "(" };
                    break;
                case ')':
                    yield return new CssToken { Type = CssTokenType.RightParen, Value = ")" };
                    break;
                default:
                    yield return new CssToken { Type = CssTokenType.Delim, Value = c.ToString() };
                    break;
            }
        }

        yield return new CssToken { Type = CssTokenType.EndOfFile, Value = "" };
    }

    private char Peek() => _input[_pos];

    private char Advance() => _input[_pos++];

    private static bool IsIdentStart(char c)
        => char.IsLetter(c) || c == '_' || c == '-';

    private static bool IsIdentChar(char c)
        => char.IsLetterOrDigit(c) || c == '_' || c == '-';

    private bool IsNumberStart(char c)
    {
        if (char.IsAsciiDigit(c)) return true;
        // +/- followed by digit or dot+digit
        if ((c == '+' || c == '-') && _pos + 1 < _input.Length)
        {
            char next = _input[_pos + 1];
            if (char.IsAsciiDigit(next)) return true;
            if (next == '.' && _pos + 2 < _input.Length && char.IsAsciiDigit(_input[_pos + 2]))
                return true;
        }
        // . followed by digit (handled in single-char section, but guard here too)
        if (c == '.' && _pos + 1 < _input.Length && char.IsAsciiDigit(_input[_pos + 1]))
            return true;
        return false;
    }

    private void SkipComment()
    {
        _pos += 2; // skip /*
        while (_pos + 1 < _input.Length)
        {
            if (_input[_pos] == '*' && _input[_pos + 1] == '/')
            {
                _pos += 2;
                return;
            }
            _pos++;
        }
        _pos = _input.Length; // unterminated comment
    }

    private void ConsumeWhitespace()
    {
        while (_pos < _input.Length && char.IsWhiteSpace(Peek()))
            _pos++;
    }

    private string ConsumeIdentChars()
    {
        var sb = new StringBuilder();
        while (_pos < _input.Length && IsIdentChar(Peek()))
            sb.Append(Advance());
        return sb.ToString();
    }

    private CssToken ConsumeString(char quote)
    {
        Advance(); // skip opening quote
        var sb = new StringBuilder();
        while (_pos < _input.Length && Peek() != quote)
        {
            if (Peek() == '\\' && _pos + 1 < _input.Length)
            {
                Advance(); // skip backslash
                sb.Append(Advance());
            }
            else
            {
                sb.Append(Advance());
            }
        }
        if (_pos < _input.Length) Advance(); // skip closing quote
        return new CssToken { Type = CssTokenType.StringLiteral, Value = sb.ToString() };
    }

    private CssToken ConsumeNumeric()
    {
        var sb = new StringBuilder();
        // optional sign
        if (_pos < _input.Length && (Peek() == '+' || Peek() == '-'))
            sb.Append(Advance());

        // integer part
        while (_pos < _input.Length && char.IsAsciiDigit(Peek()))
            sb.Append(Advance());

        // decimal part
        if (_pos < _input.Length && Peek() == '.' && _pos + 1 < _input.Length && char.IsAsciiDigit(_input[_pos + 1]))
        {
            sb.Append(Advance()); // dot
            while (_pos < _input.Length && char.IsAsciiDigit(Peek()))
                sb.Append(Advance());
        }

        double value = double.Parse(sb.ToString(), CultureInfo.InvariantCulture);

        // Check for percentage
        if (_pos < _input.Length && Peek() == '%')
        {
            Advance();
            return new CssToken
            {
                Type = CssTokenType.Percentage,
                Value = sb.ToString() + "%",
                NumericValue = value,
                Unit = "%"
            };
        }

        // Check for dimension (unit)
        if (_pos < _input.Length && IsIdentStart(Peek()))
        {
            string unit = ConsumeIdentChars();
            return new CssToken
            {
                Type = CssTokenType.Dimension,
                Value = sb.ToString() + unit,
                NumericValue = value,
                Unit = unit
            };
        }

        return new CssToken
        {
            Type = CssTokenType.Number,
            Value = sb.ToString(),
            NumericValue = value
        };
    }
}
