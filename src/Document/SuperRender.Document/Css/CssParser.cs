using System.Globalization;

namespace SuperRender.Document.Css;

/// <summary>
/// Parses CSS text into a Stylesheet. Handles rule blocks, declarations,
/// value parsing, and shorthand expansion for margin, padding, border-width, and border.
/// </summary>
public sealed partial class CssParser
{
    private readonly string _css;
    private List<CssToken> _tokens = [];
    private int _pos;

    public CssParser(string css)
    {
        _css = css;
    }

    public Stylesheet Parse()
    {
        var tokenizer = new CssTokenizer(_css);
        _tokens = tokenizer.Tokenize().ToList();
        _pos = 0;

        var stylesheet = new Stylesheet();

        SkipWhitespace();
        while (!IsEnd())
        {
            var rule = ParseRule();
            if (rule != null)
                stylesheet.Rules.Add(rule);
            SkipWhitespace();
        }

        return stylesheet;
    }

    private CssRule? ParseRule()
    {
        // Collect tokens for the selector (everything up to '{')
        var selectorTokens = new List<CssToken>();
        while (!IsEnd() && Current().Type != CssTokenType.LeftBrace)
        {
            selectorTokens.Add(Current());
            _pos++;
        }

        if (IsEnd()) return null;

        _pos++; // skip '{'

        // Parse selector list
        var selectorParser = new SelectorParser(selectorTokens);
        var selectors = selectorParser.ParseSelectorList();

        // Parse declaration block
        var declarations = ParseDeclarationBlock();

        if (selectors.Count == 0) return null;

        return new CssRule
        {
            Selectors = selectors,
            Declarations = declarations
        };
    }

    private List<Declaration> ParseDeclarationBlock()
    {
        var declarations = new List<Declaration>();

        SkipWhitespace();
        while (!IsEnd() && Current().Type != CssTokenType.RightBrace)
        {
            var decl = ParseDeclaration();
            if (decl != null)
                declarations.AddRange(decl);
            SkipWhitespace();
        }

        if (!IsEnd()) _pos++; // skip '}'

        return declarations;
    }

    /// <summary>
    /// Parses a single declaration (property: value [!important];) and returns
    /// one or more declarations (more than one when expanding shorthands).
    /// </summary>
    private List<Declaration>? ParseDeclaration()
    {
        SkipWhitespace();
        if (IsEnd() || Current().Type == CssTokenType.RightBrace)
            return null;

        // Property name
        if (Current().Type != CssTokenType.Ident)
        {
            // skip unexpected token
            _pos++;
            return null;
        }

        string property = Current().Value.ToLowerInvariant();
        _pos++;

        SkipWhitespace();

        // Expect colon
        if (IsEnd() || Current().Type != CssTokenType.Colon)
            return null;
        _pos++; // skip colon

        SkipWhitespace();

        // Collect value tokens until semicolon or '}'
        var valueTokens = new List<CssToken>();
        while (!IsEnd() && Current().Type != CssTokenType.Semicolon && Current().Type != CssTokenType.RightBrace)
        {
            valueTokens.Add(Current());
            _pos++;
        }

        // Skip semicolon if present
        if (!IsEnd() && Current().Type == CssTokenType.Semicolon)
            _pos++;

        // Check for !important
        bool important = false;
        var trimmed = TrimWhitespace(valueTokens);
        if (trimmed.Count >= 2)
        {
            var last = trimmed[^1];
            var secondLast = trimmed[^2];
            if (last.Type == CssTokenType.Ident
                && last.Value.Equals("important", StringComparison.OrdinalIgnoreCase)
                && secondLast.Type == CssTokenType.Delim
                && secondLast.Value == "!")
            {
                important = true;
                trimmed = TrimWhitespace(trimmed.GetRange(0, trimmed.Count - 2));
            }
        }

        // Handle shorthand expansion
        if (IsBoxShorthand(property))
        {
            return ExpandBoxShorthand(property, trimmed, important);
        }

        if (property == "border")
        {
            return ExpandBorderShorthand(trimmed, important);
        }

        if (property is "border-top" or "border-right" or "border-bottom" or "border-left")
        {
            return ExpandPerSideBorderShorthand(property, trimmed, important);
        }

        if (property == "border-radius")
        {
            return ExpandBorderRadiusShorthand(trimmed, important);
        }

        if (property == "flex")
        {
            return ExpandFlexShorthand(trimmed, important);
        }

        if (property == "flex-flow")
        {
            return ExpandFlexFlowShorthand(trimmed, important);
        }

        // Parse single value
        var value = ParseValueTokens(trimmed);
        return
        [
            new Declaration
            {
                Property = property,
                Value = value,
                Important = important
            }
        ];
    }

    #region Value Parsing

    /// <summary>
    /// Parses an inline style string (e.g. "border: 1px solid red; padding: 5px")
    /// into a list of expanded declarations, handling shorthand expansion.
    /// </summary>
    public static List<Declaration> ParseInlineStyleDeclarations(string cssText)
    {
        // Wrap in a dummy rule so the parser's declaration block parser handles it
        var parser = new CssParser($"x {{ {cssText} }}");
        parser._tokens = new CssTokenizer($"x {{ {cssText} }}").Tokenize().ToList();
        parser._pos = 0;
        var rule = parser.ParseRule();
        return rule?.Declarations ?? [];
    }

    /// <summary>
    /// Parses a raw CSS value string into a <see cref="CssValue"/> using the full
    /// tokenizer (handles functions like rgb(), hsl(), calc(), etc.).
    /// Used by inline style parsing.
    /// </summary>
    public static CssValue ParseValueText(string rawValue)
    {
        var tokenizer = new CssTokenizer(rawValue);
        var tokens = tokenizer.Tokenize()
            .Where(t => t.Type != CssTokenType.EndOfFile)
            .ToList();
        return ParseValueTokens(tokens);
    }

    private static CssValue ParseValueTokens(List<CssToken> tokens)
    {
        var trimmed = TrimWhitespace(tokens);
        if (trimmed.Count == 0)
        {
            return new CssValue { Type = CssValueType.Keyword, Raw = "" };
        }

        // Single token
        if (trimmed.Count == 1)
        {
            return SingleTokenToValue(trimmed[0]);
        }

        // Function call: e.g., rgb(255, 0, 0)
        if (trimmed.Count >= 1 && trimmed[0].Type == CssTokenType.Function)
        {
            return ParseFunction(trimmed);
        }

        // Multiple tokens — join as raw string with keyword type
        string raw = string.Join("", trimmed.Select(t =>
        {
            if (t.Type == CssTokenType.Whitespace) return " ";
            return t.ToString();
        })).Trim();

        return new CssValue { Type = CssValueType.Keyword, Raw = raw };
    }

    private static CssValue SingleTokenToValue(CssToken token)
    {
        switch (token.Type)
        {
            case CssTokenType.Ident:
                if (Document.Color.TryFromName(token.Value, out var namedColor))
                {
                    return new CssValue
                    {
                        Type = CssValueType.Color,
                        Raw = token.Value,
                        ColorValue = namedColor
                    };
                }
                return new CssValue { Type = CssValueType.Keyword, Raw = token.Value };

            case CssTokenType.Number:
                return new CssValue
                {
                    Type = CssValueType.Number,
                    Raw = token.Value,
                    NumericValue = token.NumericValue
                };

            case CssTokenType.Dimension:
                return new CssValue
                {
                    Type = CssValueType.Length,
                    Raw = token.Value,
                    NumericValue = token.NumericValue,
                    Unit = token.Unit
                };

            case CssTokenType.Percentage:
                return new CssValue
                {
                    Type = CssValueType.Percentage,
                    Raw = token.NumericValue.ToString(CultureInfo.InvariantCulture) + "%",
                    NumericValue = token.NumericValue,
                    Unit = "%"
                };

            case CssTokenType.Hash:
                var color = Document.Color.FromHex(token.Value);
                return new CssValue
                {
                    Type = CssValueType.Color,
                    Raw = "#" + token.Value,
                    ColorValue = color
                };

            case CssTokenType.StringLiteral:
                return new CssValue
                {
                    Type = CssValueType.StringLiteral,
                    Raw = token.Value
                };

            default:
                return new CssValue
                {
                    Type = CssValueType.Keyword,
                    Raw = token.Value
                };
        }
    }

    private static CssValue ParseFunction(List<CssToken> tokens)
    {
        string funcName = tokens[0].Value.ToLowerInvariant();

        // Collect argument tokens (everything after the Function token, excluding closing paren)
        var argTokens = new List<CssToken>();
        for (int i = 1; i < tokens.Count; i++)
        {
            if (tokens[i].Type == CssTokenType.RightParen) break;
            argTokens.Add(tokens[i]);
        }

        if (funcName is "rgb" or "rgba")
        {
            return ParseRgbFunction(funcName, argTokens);
        }

        if (funcName is "hsl" or "hsla")
        {
            return ParseHslFunction(funcName, argTokens);
        }

        if (funcName is "calc" or "min" or "max" or "clamp")
        {
            return ParseCalcFunction(funcName, argTokens);
        }

        // Fallback: join everything as keyword
        string raw = string.Join("", tokens.Select(t => t.ToString()));
        return new CssValue { Type = CssValueType.Keyword, Raw = raw };
    }

    private static CssValue ParseRgbFunction(string funcName, List<CssToken> argTokens)
    {
        // Support both comma-separated and space-separated syntax:
        // rgb(255, 0, 0)  or  rgb(255 0 0)  or  rgb(255 0 0 / 0.5)
        var numbers = new List<double>();
        bool hasSlashAlpha = false;
        double slashAlpha = 1.0;
        bool hasCommas = argTokens.Any(t => t.Type == CssTokenType.Comma);

        if (hasCommas)
        {
            // Legacy comma-separated: rgb(r, g, b) or rgba(r, g, b, a)
            foreach (var t in argTokens)
            {
                if (t.Type is CssTokenType.Number or CssTokenType.Percentage or CssTokenType.Dimension)
                    numbers.Add(t.NumericValue);
            }
        }
        else
        {
            // Modern space-separated: rgb(r g b) or rgb(r g b / alpha)
            for (int i = 0; i < argTokens.Count; i++)
            {
                var t = argTokens[i];
                if (t.Type == CssTokenType.Delim && t.Value == "/")
                {
                    // Everything after / is the alpha value
                    for (int j = i + 1; j < argTokens.Count; j++)
                    {
                        var at = argTokens[j];
                        if (at.Type is CssTokenType.Number)
                        {
                            hasSlashAlpha = true;
                            slashAlpha = at.NumericValue;
                            break;
                        }
                        if (at.Type is CssTokenType.Percentage)
                        {
                            hasSlashAlpha = true;
                            slashAlpha = at.NumericValue / 100.0;
                            break;
                        }
                    }
                    break;
                }
                if (t.Type is CssTokenType.Number or CssTokenType.Percentage or CssTokenType.Dimension)
                    numbers.Add(t.NumericValue);
            }
        }

        if (numbers.Count >= 3)
        {
            byte r = ClampByte(numbers[0]);
            byte g = ClampByte(numbers[1]);
            byte b = ClampByte(numbers[2]);

            Document.Color color;
            if (hasSlashAlpha)
            {
                byte a = (byte)Math.Clamp((int)Math.Round(slashAlpha * 255), 0, 255);
                color = Document.Color.FromRgba(r, g, b, a);
            }
            else if (numbers.Count >= 4)
            {
                color = Document.Color.FromRgba(r, g, b, ClampByte(numbers[3]));
            }
            else
            {
                color = Document.Color.FromRgb(r, g, b);
            }

            return new CssValue
            {
                Type = CssValueType.Color,
                Raw = funcName + "(...)",
                ColorValue = color
            };
        }

        return new CssValue { Type = CssValueType.Keyword, Raw = funcName + "(...)" };
    }

    private static CssValue ParseHslFunction(string funcName, List<CssToken> argTokens)
    {
        // hsl(h, s%, l%) or hsl(h s% l%) or hsl(h, s%, l%, a) or hsl(h s% l% / a)
        var numbers = new List<double>();
        bool hasSlashAlpha = false;
        double slashAlpha = 1.0;
        bool hasCommas = argTokens.Any(t => t.Type == CssTokenType.Comma);

        if (hasCommas)
        {
            foreach (var t in argTokens)
            {
                if (t.Type is CssTokenType.Number or CssTokenType.Percentage or CssTokenType.Dimension)
                    numbers.Add(t.Type == CssTokenType.Percentage ? t.NumericValue / 100.0 : t.NumericValue);
            }
        }
        else
        {
            for (int i = 0; i < argTokens.Count; i++)
            {
                var t = argTokens[i];
                if (t.Type == CssTokenType.Delim && t.Value == "/")
                {
                    for (int j = i + 1; j < argTokens.Count; j++)
                    {
                        var at = argTokens[j];
                        if (at.Type is CssTokenType.Number)
                        {
                            hasSlashAlpha = true;
                            slashAlpha = at.NumericValue;
                            break;
                        }
                        if (at.Type is CssTokenType.Percentage)
                        {
                            hasSlashAlpha = true;
                            slashAlpha = at.NumericValue / 100.0;
                            break;
                        }
                    }
                    break;
                }
                if (t.Type is CssTokenType.Number or CssTokenType.Percentage or CssTokenType.Dimension)
                    numbers.Add(t.Type == CssTokenType.Percentage ? t.NumericValue / 100.0 : t.NumericValue);
            }
        }

        if (numbers.Count >= 3)
        {
            double h = numbers[0]; // degrees
            double s = numbers[1]; // 0-1 (already normalized from %)
            double l = numbers[2]; // 0-1 (already normalized from %)

            double alpha;
            if (hasSlashAlpha)
                alpha = slashAlpha;
            else if (numbers.Count >= 4)
                alpha = numbers[3]; // comma-separated alpha
            else
                alpha = 1.0;

            var color = Document.Color.FromHsla(h, s, l, alpha);

            return new CssValue
            {
                Type = CssValueType.Color,
                Raw = funcName + "(...)",
                ColorValue = color
            };
        }

        return new CssValue { Type = CssValueType.Keyword, Raw = funcName + "(...)" };
    }

    private static byte ClampByte(double v)
        => (byte)Math.Clamp((int)Math.Round(v), 0, 255);

    private static CssValue ParseCalcFunction(string funcName, List<CssToken> argTokens)
    {
        var node = funcName switch
        {
            "calc" => ParseCalcExpression(argTokens, 0, out _),
            "min" => ParseMinMaxClamp(CalcMinMaxType.Min, argTokens),
            "max" => ParseMinMaxClamp(CalcMinMaxType.Max, argTokens),
            "clamp" => ParseMinMaxClamp(CalcMinMaxType.Clamp, argTokens),
            _ => null
        };

        if (node != null)
        {
            return new CssValue
            {
                Type = CssValueType.Calc,
                Raw = funcName + "(...)",
                CalcExpr = node
            };
        }

        return new CssValue { Type = CssValueType.Keyword, Raw = funcName + "(...)" };
    }

    private static CalcNode? ParseCalcExpression(List<CssToken> tokens, int start, out int end)
    {
        end = start;
        var node = ParseCalcTerm(tokens, start, out end);
        if (node == null) return null;

        while (end < tokens.Count)
        {
            SkipWhitespaceInList(tokens, ref end);
            if (end >= tokens.Count) break;

            var tok = tokens[end];
            CalcOp? op = null;
            if (tok.Type == CssTokenType.Delim && tok.Value == "+") op = CalcOp.Add;
            else if ((tok.Type == CssTokenType.Delim || tok.Type == CssTokenType.Ident) && tok.Value == "-") op = CalcOp.Sub;
            else if (tok.Type == CssTokenType.Ident && tok.Value == "+") op = CalcOp.Add;

            if (op == null) break;

            end++;
            SkipWhitespaceInList(tokens, ref end);

            var right = ParseCalcTerm(tokens, end, out end);
            if (right == null) break;

            node = new CalcBinaryNode(node, op.Value, right);
        }

        return node;
    }

    private static CalcNode? ParseCalcTerm(List<CssToken> tokens, int start, out int end)
    {
        end = start;
        var node = ParseCalcAtom(tokens, start, out end);
        if (node == null) return null;

        while (end < tokens.Count)
        {
            SkipWhitespaceInList(tokens, ref end);
            if (end >= tokens.Count) break;

            var tok = tokens[end];
            CalcOp? op = null;
            if (tok.Type == CssTokenType.Delim && tok.Value == "*") op = CalcOp.Mul;
            else if ((tok.Type == CssTokenType.Delim || tok.Type == CssTokenType.Ident) && tok.Value == "/") op = CalcOp.Div;

            if (op == null) break;

            end++;
            SkipWhitespaceInList(tokens, ref end);

            var right = ParseCalcAtom(tokens, end, out end);
            if (right == null) break;

            node = new CalcBinaryNode(node, op.Value, right);
        }

        return node;
    }

    private static CalcNode? ParseCalcAtom(List<CssToken> tokens, int start, out int end)
    {
        end = start;
        if (start >= tokens.Count) return null;

        SkipWhitespaceInList(tokens, ref end);
        if (end >= tokens.Count) return null;

        var tok = tokens[end];

        if (tok.Type == CssTokenType.LeftParen)
        {
            end++;
            var inner = ParseCalcExpression(tokens, end, out end);
            SkipWhitespaceInList(tokens, ref end);
            if (end < tokens.Count && tokens[end].Type == CssTokenType.RightParen)
                end++;
            return inner;
        }

        if (tok.Type == CssTokenType.Function)
        {
            var fname = tok.Value.ToLowerInvariant();
            end++;
            var nestedArgs = new List<CssToken>();
            int depth = 1;
            while (end < tokens.Count && depth > 0)
            {
                if (tokens[end].Type == CssTokenType.LeftParen) depth++;
                else if (tokens[end].Type == CssTokenType.RightParen)
                {
                    depth--;
                    if (depth == 0) { end++; break; }
                }
                nestedArgs.Add(tokens[end]);
                end++;
            }

            if (fname is "min" or "max" or "clamp")
                return ParseMinMaxClamp(fname == "min" ? CalcMinMaxType.Min : fname == "max" ? CalcMinMaxType.Max : CalcMinMaxType.Clamp, nestedArgs);
            if (fname == "calc")
                return ParseCalcExpression(nestedArgs, 0, out _);
            return null;
        }

        if (tok.Type is CssTokenType.Number or CssTokenType.Dimension or CssTokenType.Percentage)
        {
            end++;
            var value = SingleTokenToValue(tok);
            return new CalcValueNode(value);
        }

        return null;
    }

    private static CalcMinMaxNode? ParseMinMaxClamp(CalcMinMaxType type, List<CssToken> argTokens)
    {
        var args = new List<CalcNode>();
        var current = new List<CssToken>();

        foreach (var t in argTokens)
        {
            if (t.Type == CssTokenType.Comma)
            {
                if (current.Count > 0)
                {
                    var node = ParseCalcExpression(current, 0, out _);
                    if (node != null) args.Add(node);
                    current = [];
                }
            }
            else
            {
                current.Add(t);
            }
        }

        if (current.Count > 0)
        {
            var node = ParseCalcExpression(current, 0, out _);
            if (node != null) args.Add(node);
        }

        if (args.Count == 0) return null;

        return new CalcMinMaxNode(type, args);
    }

    private static void SkipWhitespaceInList(List<CssToken> tokens, ref int pos)
    {
        while (pos < tokens.Count && tokens[pos].Type == CssTokenType.Whitespace)
            pos++;
    }

    #endregion

    #region Token Helpers

    private CssToken Current() => _tokens[_pos];

    private bool IsEnd()
        => _pos >= _tokens.Count
           || _tokens[_pos].Type == CssTokenType.EndOfFile;

    private void SkipWhitespace()
    {
        while (!IsEnd() && Current().Type == CssTokenType.Whitespace)
            _pos++;
    }

    private static List<CssToken> TrimWhitespace(List<CssToken> tokens)
    {
        int start = 0;
        while (start < tokens.Count && tokens[start].Type == CssTokenType.Whitespace)
            start++;
        int end = tokens.Count - 1;
        while (end >= start && tokens[end].Type == CssTokenType.Whitespace)
            end--;
        if (start > end) return [];
        return tokens.GetRange(start, end - start + 1);
    }

    /// <summary>
    /// Splits a list of tokens into groups separated by whitespace tokens.
    /// </summary>
    private static List<List<CssToken>> SplitByWhitespace(List<CssToken> tokens)
    {
        var result = new List<List<CssToken>>();
        var current = new List<CssToken>();

        foreach (var token in tokens)
        {
            if (token.Type == CssTokenType.Whitespace)
            {
                if (current.Count > 0)
                {
                    result.Add(current);
                    current = [];
                }
            }
            else
            {
                current.Add(token);
            }
        }

        if (current.Count > 0)
            result.Add(current);

        return result;
    }

    #endregion
}
