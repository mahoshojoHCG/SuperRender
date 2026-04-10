using System.Globalization;

namespace SuperRender.Core.Css;

/// <summary>
/// Parses CSS text into a Stylesheet. Handles rule blocks, declarations,
/// value parsing, and shorthand expansion for margin, padding, border-width, and border.
/// </summary>
public sealed class CssParser
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

    #region Shorthand Expansion

    private static bool IsBoxShorthand(string property)
        => property is "margin" or "padding" or "border-width";

    private static List<Declaration> ExpandBoxShorthand(string property, List<CssToken> valueTokens, bool important)
    {
        // Split value tokens by whitespace into groups
        var parts = SplitByWhitespace(valueTokens);

        string prefix = property switch
        {
            "margin" => "margin",
            "padding" => "padding",
            "border-width" => "border",
            _ => property
        };

        string topProp, rightProp, bottomProp, leftProp;
        if (property == "border-width")
        {
            topProp = "border-top-width";
            rightProp = "border-right-width";
            bottomProp = "border-bottom-width";
            leftProp = "border-left-width";
        }
        else
        {
            topProp = $"{prefix}-top";
            rightProp = $"{prefix}-right";
            bottomProp = $"{prefix}-bottom";
            leftProp = $"{prefix}-left";
        }

        CssValue top, right, bottom, left;

        switch (parts.Count)
        {
            case 1:
                top = right = bottom = left = ParseValueTokens(parts[0]);
                break;
            case 2:
                top = bottom = ParseValueTokens(parts[0]);
                right = left = ParseValueTokens(parts[1]);
                break;
            case 3:
                top = ParseValueTokens(parts[0]);
                right = left = ParseValueTokens(parts[1]);
                bottom = ParseValueTokens(parts[2]);
                break;
            default: // 4+
                top = ParseValueTokens(parts[0]);
                right = ParseValueTokens(parts[1]);
                bottom = ParseValueTokens(parts[2]);
                left = ParseValueTokens(parts[3]);
                break;
        }

        return
        [
            new Declaration { Property = topProp, Value = top, Important = important },
            new Declaration { Property = rightProp, Value = right, Important = important },
            new Declaration { Property = bottomProp, Value = bottom, Important = important },
            new Declaration { Property = leftProp, Value = left, Important = important },
        ];
    }

    private static List<Declaration> ExpandBorderShorthand(List<CssToken> valueTokens, bool important)
    {
        // border: <width> <style> <color>
        var parts = SplitByWhitespace(valueTokens);
        var declarations = new List<Declaration>();

        CssValue? width = null;
        CssValue? style = null;
        CssValue? color = null;

        var borderStyles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "none", "hidden", "dotted", "dashed", "solid", "double", "groove", "ridge", "inset", "outset"
        };

        foreach (var part in parts)
        {
            var val = ParseValueTokens(part);

            if (val.Type is CssValueType.Length or CssValueType.Number or CssValueType.Percentage)
            {
                width ??= val;
            }
            else if (val.Type == CssValueType.Color)
            {
                color ??= val;
            }
            else if (val.Type == CssValueType.Keyword)
            {
                // Check if it's a color name
                if (Core.Color.TryFromName(val.Raw, out _) && color == null)
                {
                    color = new CssValue
                    {
                        Type = CssValueType.Color,
                        Raw = val.Raw,
                        ColorValue = Core.Color.FromName(val.Raw)
                    };
                }
                else if (borderStyles.Contains(val.Raw))
                {
                    style ??= val;
                }
                else
                {
                    // fallback: treat as style
                    style ??= val;
                }
            }
        }

        if (width != null)
            declarations.Add(new Declaration { Property = "border-width", Value = width, Important = important });
        if (style != null)
            declarations.Add(new Declaration { Property = "border-style", Value = style, Important = important });
        if (color != null)
            declarations.Add(new Declaration { Property = "border-color", Value = color, Important = important });

        return declarations;
    }

    #endregion

    #region Value Parsing

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
                if (Core.Color.TryFromName(token.Value, out var namedColor))
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
                var color = Core.Color.FromHex(token.Value);
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

        if (funcName == "rgb" || funcName == "rgba")
        {
            return ParseRgbFunction(funcName, argTokens);
        }

        // Fallback: join everything as keyword
        string raw = string.Join("", tokens.Select(t => t.ToString()));
        return new CssValue { Type = CssValueType.Keyword, Raw = raw };
    }

    private static CssValue ParseRgbFunction(string funcName, List<CssToken> argTokens)
    {
        // Extract numeric values from arg tokens
        var numbers = new List<double>();
        foreach (var t in argTokens)
        {
            if (t.Type is CssTokenType.Number or CssTokenType.Percentage or CssTokenType.Dimension)
                numbers.Add(t.NumericValue);
        }

        if (numbers.Count >= 3)
        {
            byte r = ClampByte(numbers[0]);
            byte g = ClampByte(numbers[1]);
            byte b = ClampByte(numbers[2]);

            var color = numbers.Count >= 4
                ? Core.Color.FromRgba(r, g, b, ClampByte(numbers[3]))
                : Core.Color.FromRgb(r, g, b);

            string raw = numbers.Count >= 4
                ? $"{funcName}({r}, {g}, {b}, {numbers[3]})"
                : $"{funcName}({r}, {g}, {b})";

            return new CssValue
            {
                Type = CssValueType.Color,
                Raw = raw,
                ColorValue = color
            };
        }

        return new CssValue { Type = CssValueType.Keyword, Raw = funcName + "(...)" };
    }

    private static byte ClampByte(double v)
        => (byte)Math.Clamp((int)Math.Round(v), 0, 255);

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
