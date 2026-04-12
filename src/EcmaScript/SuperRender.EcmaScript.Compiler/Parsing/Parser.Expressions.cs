using System.Numerics;
using SuperRender.EcmaScript.Compiler.Ast;
using SuperRender.EcmaScript.Runtime.Errors;
using SuperRender.EcmaScript.Compiler.Lexing;

namespace SuperRender.EcmaScript.Compiler.Parsing;

public sealed partial class Parser
{
    // ═══════════════════════════════════════════
    //  Expression parsing (Pratt)
    // ═══════════════════════════════════════════

    private SyntaxNode ParseExpression(int minPrec = 0)
    {
        var left = ParsePrefix();

        while (!IsAtEnd)
        {
            int prec = GetInfixPrecedence(Current);
            if (prec <= minPrec) break;

            left = ParseInfix(left, prec);
        }

        return left;
    }

    private SyntaxNode ParseAssignmentExpression()
    {
        return ParseExpression(PrecComma);
    }

    // ═══════════════════════════════════════════
    //  Prefix parsing
    // ═══════════════════════════════════════════

    private SyntaxNode ParsePrefix()
    {
        var loc = Loc();
        var token = Current;

        switch (token.Type)
        {
            case TokenType.Identifier:
            case TokenType.Async:
            case TokenType.From:
            case TokenType.As:
            case TokenType.Of:
            case TokenType.Get:
            case TokenType.Set:
            case TokenType.Static:
            case TokenType.Let:
                return ParseIdentifierOrArrow();

            case TokenType.Yield:
                return ParseYieldExpression();

            case TokenType.Await:
                return ParseAwaitExpression();

            case TokenType.NumericLiteral:
                Advance();
                return new Literal { Value = token.NumericValue, Raw = token.Value, Location = loc };

            case TokenType.BigIntLiteral:
                Advance();
                return new Literal { Value = ParseBigIntLiteral(token.Value), Raw = token.Value, Location = loc };

            case TokenType.StringLiteral:
                Advance();
                return new Literal { Value = token.Value, Raw = token.Value, Location = loc };

            case TokenType.TrueLiteral:
                Advance();
                return new Literal { Value = true, Raw = "true", Location = loc };

            case TokenType.FalseLiteral:
                Advance();
                return new Literal { Value = false, Raw = "false", Location = loc };

            case TokenType.NullLiteral:
                Advance();
                return new Literal { Value = null, Raw = "null", Location = loc };

            case TokenType.RegExpLiteral:
                Advance();
                return new Literal { Value = token.Value, Raw = token.Value, Location = loc };

            case TokenType.This:
                Advance();
                return new ThisExpression { Location = loc };

            case TokenType.Super:
                Advance();
                return new Identifier { Name = "super", Location = loc };

            case TokenType.LeftParen:
                return ParseParenthesizedOrArrow();

            case TokenType.LeftBracket:
                return ParseArrayLiteral();

            case TokenType.LeftBrace:
                return ParseObjectLiteral();

            case TokenType.Function:
                return ParseFunctionExpression(false);

            case TokenType.Class:
                return ParseClassExpression();

            case TokenType.New:
                return ParseNewExpression();

            case TokenType.TemplateLiteral:
                return ParseTemplateLiteralExpr();

            case TokenType.TemplateHead:
                return ParseTemplateLiteralExpr();

            case TokenType.Ellipsis:
                Advance();
                var arg = ParseAssignmentExpression();
                return new SpreadElement { Argument = arg, Location = loc };

            // Unary operators
            case TokenType.Bang:
            case TokenType.Tilde:
            case TokenType.Typeof:
            case TokenType.Void:
            case TokenType.Delete:
                Advance();
                var unaryArg = ParseExpression(PrecUnary);
                return new UnaryExpression { Operator = token.Value, Argument = unaryArg, Prefix = true, Location = loc };

            case TokenType.Plus:
                Advance();
                var posArg = ParseExpression(PrecUnary);
                return new UnaryExpression { Operator = "+", Argument = posArg, Prefix = true, Location = loc };

            case TokenType.Minus:
                Advance();
                var negArg = ParseExpression(PrecUnary);
                return new UnaryExpression { Operator = "-", Argument = negArg, Prefix = true, Location = loc };

            // Prefix increment/decrement
            case TokenType.PlusPlus:
                Advance();
                var incArg = ParseExpression(PrecUnary);
                return new UpdateExpression { Operator = "++", Argument = incArg, Prefix = true, Location = loc };

            case TokenType.MinusMinus:
                Advance();
                var decArg = ParseExpression(PrecUnary);
                return new UpdateExpression { Operator = "--", Argument = decArg, Prefix = true, Location = loc };

            case TokenType.Hash:
                Advance();
                var privName = ExpectIdentifierName();
                return new Identifier { Name = "#" + privName, Location = loc };

            case TokenType.Import:
                // import.meta or import()
                if (Peek().Type == TokenType.Dot)
                {
                    Advance(); // import
                    Advance(); // .
                    var meta = ExpectIdentifierName();
                    return new MemberExpression
                    {
                        Object = new Identifier { Name = "import", Location = loc },
                        Property = new Identifier { Name = meta },
                        Location = loc
                    };
                }
                // Dynamic import()
                if (Peek().Type == TokenType.LeftParen)
                {
                    Advance(); // import
                    Advance(); // (
                    var source = ParseAssignmentExpression();
                    Expect(TokenType.RightParen);
                    return new CallExpression
                    {
                        Callee = new Identifier { Name = "import", Location = loc },
                        Arguments = [source],
                        Location = loc
                    };
                }
                throw new JsSyntaxError("Unexpected 'import'", loc.Line, loc.Column);

            default:
                throw new JsSyntaxError($"Unexpected token: {token.Type} ('{token.Value}')", loc.Line, loc.Column);
        }
    }

    // ═══════════════════════════════════════════
    //  Infix parsing
    // ═══════════════════════════════════════════

    private SyntaxNode ParseInfix(SyntaxNode left, int prec)
    {
        var loc = left.Location ?? Loc();
        var token = Current;

        // Postfix ++/--
        if (token.Type is TokenType.PlusPlus or TokenType.MinusMinus && !token.PrecedingLineTerminator)
        {
            Advance();
            return new UpdateExpression { Operator = token.Value, Argument = left, Prefix = false, Location = loc };
        }

        // Comma (sequence)
        if (token.Type == TokenType.Comma && prec == PrecComma)
        {
            var expressions = new List<SyntaxNode> { left };
            while (Match(TokenType.Comma))
            {
                expressions.Add(ParseAssignmentExpression());
            }
            return expressions.Count == 1 ? expressions[0] : new SequenceExpression { Expressions = expressions, Location = loc };
        }

        // Assignment operators (right-associative)
        if (IsAssignment(token.Type))
        {
            var op = Advance().Value;
            var right = ParseExpression(PrecAssignment - 1); // right-associative
            return new AssignmentExpression { Left = ToAssignmentTarget(left), Operator = op, Right = right, Location = loc };
        }

        // Pipeline operator: |>
        if (token.Type == TokenType.Pipeline)
        {
            Advance();
            var right = ParseExpression(prec);
            return new BinaryExpression { Left = left, Operator = "|>", Right = right, Location = loc };
        }

        // Conditional ternary
        if (token.Type == TokenType.QuestionMark)
        {
            Advance();
            var consequent = ParseAssignmentExpression();
            Expect(TokenType.Colon);
            var alternate = ParseAssignmentExpression();
            return new ConditionalExpression { Test = left, Consequent = consequent, Alternate = alternate, Location = loc };
        }

        // Logical operators (&&, ||, ??)
        if (token.Type is TokenType.AmpersandAmpersand or TokenType.PipePipe or TokenType.QuestionQuestion)
        {
            var op = Advance().Value;
            var right = ParseExpression(prec);
            return new LogicalExpression { Left = left, Operator = op, Right = right, Location = loc };
        }

        // Binary operators
        if (token.Type is TokenType.Plus or TokenType.Minus or TokenType.Star or TokenType.Slash or
            TokenType.Percent or TokenType.Pipe or TokenType.Caret or TokenType.Ampersand or
            TokenType.EqualEqual or TokenType.BangEqual or TokenType.EqualEqualEqual or TokenType.BangEqualEqual or
            TokenType.LessThan or TokenType.GreaterThan or TokenType.LessThanEqual or TokenType.GreaterThanEqual or
            TokenType.LeftShift or TokenType.RightShift or TokenType.UnsignedRightShift or
            TokenType.Instanceof or TokenType.In)
        {
            var op = Advance().Value;
            // Exponentiation is right-associative
            int nextPrec = token.Type == TokenType.StarStar ? prec - 1 : prec;
            var right = ParseExpression(nextPrec);
            return new BinaryExpression { Left = left, Operator = op, Right = right, Location = loc };
        }

        if (token.Type == TokenType.StarStar)
        {
            var op = Advance().Value;
            var right = ParseExpression(prec - 1); // right-associative
            return new BinaryExpression { Left = left, Operator = op, Right = right, Location = loc };
        }

        // Member access: .
        if (token.Type == TokenType.Dot)
        {
            Advance();
            var propLoc = Loc();
            var propName = ExpectIdentifierName();
            return new MemberExpression
            {
                Object = left,
                Property = new Identifier { Name = propName, Location = propLoc },
                Location = loc
            };
        }

        // Computed member access: [
        if (token.Type == TokenType.LeftBracket)
        {
            Advance();
            var property = ParseExpression();
            Expect(TokenType.RightBracket);
            return new MemberExpression { Object = left, Property = property, Computed = true, Location = loc };
        }

        // Optional chaining: ?.
        if (token.Type == TokenType.QuestionDot)
        {
            Advance();
            SyntaxNode inner;
            if (Current.Type == TokenType.LeftBracket)
            {
                Advance();
                var prop = ParseExpression();
                Expect(TokenType.RightBracket);
                inner = new MemberExpression { Object = left, Property = prop, Computed = true, Optional = true, Location = loc };
            }
            else if (Current.Type == TokenType.LeftParen)
            {
                var args = ParseArguments();
                inner = new CallExpression { Callee = left, Arguments = args, Location = loc };
            }
            else
            {
                var propLoc2 = Loc();
                var name = ExpectIdentifierName();
                inner = new MemberExpression
                {
                    Object = left,
                    Property = new Identifier { Name = name, Location = propLoc2 },
                    Optional = true,
                    Location = loc
                };
            }
            return new ChainExpression { Expression = inner, Location = loc };
        }

        // Call: (
        if (token.Type == TokenType.LeftParen)
        {
            var args = ParseArguments();
            return new CallExpression { Callee = left, Arguments = args, Location = loc };
        }

        // Tagged template
        if (token.Type is TokenType.TemplateLiteral or TokenType.TemplateHead)
        {
            var quasi = ParseTemplateLiteralNode();
            return new TaggedTemplateExpression { Tag = left, Quasi = quasi, Location = loc };
        }

        throw new JsSyntaxError($"Unexpected infix token: {token.Type}", loc.Line, loc.Column);
    }

    // ═══════════════════════════════════════════
    //  Identifier / arrow
    // ═══════════════════════════════════════════

    private SyntaxNode ParseIdentifierOrArrow()
    {
        var loc = Loc();

        // async arrow: async (params) => ... or async x => ...
        if (Current.Type == TokenType.Async && !Current.PrecedingLineTerminator)
        {
            // async function expression
            if (Peek().Type == TokenType.Function && !Peek().PrecedingLineTerminator)
            {
                Advance(); // skip 'async'
                return ParseFunctionExpression(true);
            }

            // Check for async arrow: async x => ... or async (x) => ...
            if (Peek().Type == TokenType.LeftParen)
            {
                int saved = _pos;
                Advance(); // skip 'async'
                var result = TryParseArrowParams();
                if (result is not null && Current.Type == TokenType.Arrow)
                {
                    Advance(); // skip '=>'
                    var body = ParseArrowBody();
                    return new ArrowFunctionExpression
                    {
                        Params = result,
                        Body = body,
                        IsAsync = true,
                        IsExpression = body is not BlockStatement,
                        Location = loc
                    };
                }
                _pos = saved;
            }
            else if (IsIdentifier(Peek()) && PeekAt(2) == TokenType.Arrow)
            {
                Advance(); // skip 'async'
                var paramName = Advance().Value;
                Advance(); // skip '=>'
                var body = ParseArrowBody();
                return new ArrowFunctionExpression
                {
                    Params = [new Identifier { Name = paramName }],
                    Body = body,
                    IsAsync = true,
                    IsExpression = body is not BlockStatement,
                    Location = loc
                };
            }
        }

        // Simple identifier: check for arrow x => ...
        var name = Advance().Value;
        if (Current.Type == TokenType.Arrow && !Current.PrecedingLineTerminator)
        {
            Advance(); // skip '=>'
            var body = ParseArrowBody();
            return new ArrowFunctionExpression
            {
                Params = [new Identifier { Name = name, Location = loc }],
                Body = body,
                IsExpression = body is not BlockStatement,
                Location = loc
            };
        }

        return new Identifier { Name = name, Location = loc };
    }

    private TokenType PeekAt(int offset)
    {
        int idx = _pos + offset;
        return idx < _tokens.Count ? _tokens[idx].Type : TokenType.EndOfFile;
    }

    // ═══════════════════════════════════════════
    //  Parenthesized / arrow
    // ═══════════════════════════════════════════

    private SyntaxNode ParseParenthesizedOrArrow()
    {
        var loc = Loc();

        // Try arrow function with params
        var saved = _pos;
        var arrowParams = TryParseArrowParams();
        if (arrowParams is not null && Current.Type == TokenType.Arrow)
        {
            Advance(); // skip '=>'
            var body = ParseArrowBody();
            return new ArrowFunctionExpression
            {
                Params = arrowParams,
                Body = body,
                IsExpression = body is not BlockStatement,
                Location = loc
            };
        }

        // Restore and parse as parenthesized expression
        _pos = saved;
        Expect(TokenType.LeftParen);

        // Empty parens followed by => is arrow with no params
        if (Current.Type == TokenType.RightParen)
        {
            Advance();
            if (Current.Type == TokenType.Arrow)
            {
                Advance(); // skip '=>'
                var body = ParseArrowBody();
                return new ArrowFunctionExpression
                {
                    Params = [],
                    Body = body,
                    IsExpression = body is not BlockStatement,
                    Location = loc
                };
            }
            throw new JsSyntaxError("Unexpected ')'", loc.Line, loc.Column);
        }

        var expr = ParseExpression();
        Expect(TokenType.RightParen);

        // Check for arrow after parenthesized expression
        if (Current.Type == TokenType.Arrow && !Current.PrecedingLineTerminator)
        {
            Advance(); // skip '=>'
            var parameters = ExpressionToParams(expr);
            var body = ParseArrowBody();
            return new ArrowFunctionExpression
            {
                Params = parameters,
                Body = body,
                IsExpression = body is not BlockStatement,
                Location = loc
            };
        }

        return expr;
    }

    private List<SyntaxNode>? TryParseArrowParams()
    {
        if (Current.Type != TokenType.LeftParen) return null;

        var saved = _pos;
        try
        {
            Advance(); // skip '('
            var parameters = new List<SyntaxNode>();

            if (Current.Type == TokenType.RightParen)
            {
                Advance();
                return parameters;
            }

            while (true)
            {
                if (Current.Type == TokenType.Ellipsis)
                {
                    Advance();
                    var rest = ParseBindingPattern();
                    parameters.Add(new RestElement { Argument = rest, Location = rest.Location });
                    if (Current.Type == TokenType.RightParen) break;
                    throw new JsSyntaxError("Rest parameter must be last", Current.Line, Current.Column);
                }

                var param = ParseBindingPattern();
                if (Match(TokenType.Assign))
                {
                    var defaultVal = ParseAssignmentExpression();
                    param = new AssignmentPattern { Left = param, Right = defaultVal, Location = param.Location };
                }
                parameters.Add(param);

                if (Current.Type == TokenType.RightParen) break;
                Expect(TokenType.Comma);
                if (Current.Type == TokenType.RightParen) break; // trailing comma
            }

            Expect(TokenType.RightParen);
            return parameters;
        }
        catch (JsSyntaxError)
        {
            _pos = saved;
            return null;
        }
    }

    private SyntaxNode ParseArrowBody()
    {
        if (Current.Type == TokenType.LeftBrace)
            return ParseBlockStatement();
        return ParseAssignmentExpression();
    }

    private List<SyntaxNode> ExpressionToParams(SyntaxNode expr)
    {
        if (expr is SequenceExpression seq)
        {
            var result = new List<SyntaxNode>();
            foreach (var e in seq.Expressions)
                result.Add(ToParam(e));
            return result;
        }
        return [ToParam(expr)];
    }

    private SyntaxNode ToParam(SyntaxNode node)
    {
        return node switch
        {
            Identifier => node,
            AssignmentExpression { Operator: "=" } assign =>
                new AssignmentPattern { Left = ToParam(assign.Left), Right = assign.Right, Location = node.Location },
            SpreadElement spread => new RestElement { Argument = ToParam(spread.Argument), Location = node.Location },
            ArrayExpression arr => ToArrayPattern(arr),
            ObjectExpression obj => ToObjectPattern(obj),
            _ => node
        };
    }

    private SyntaxNode ToAssignmentTarget(SyntaxNode node)
    {
        return node switch
        {
            Identifier => node,
            MemberExpression => node,
            ArrayExpression arr => ToArrayPattern(arr),
            ObjectExpression obj => ToObjectPattern(obj),
            _ => node
        };
    }

    private ArrayPattern ToArrayPattern(ArrayExpression arr)
    {
        var elements = new List<SyntaxNode?>();
        foreach (var e in arr.Elements)
        {
            if (e is null) { elements.Add(null); continue; }
            elements.Add(ToParam(e));
        }
        return new ArrayPattern { Elements = elements, Location = arr.Location };
    }

    private ObjectPattern ToObjectPattern(ObjectExpression obj)
    {
        var properties = new List<SyntaxNode>();
        foreach (var p in obj.Properties)
        {
            if (p is SpreadElement spread)
            {
                properties.Add(new RestElement { Argument = ToParam(spread.Argument), Location = spread.Location });
            }
            else if (p is Property prop)
            {
                var value = ToParam(prop.Value);
                properties.Add(new Property
                {
                    Key = prop.Key,
                    Value = value,
                    Computed = prop.Computed,
                    Shorthand = prop.Shorthand,
                    Kind = prop.Kind,
                    IsMethod = prop.IsMethod,
                    Location = prop.Location
                });
            }
            else
            {
                properties.Add(p);
            }
        }
        return new ObjectPattern { Properties = properties, Location = obj.Location };
    }

    // ═══════════════════════════════════════════
    //  Yield / Await
    // ═══════════════════════════════════════════

    private SyntaxNode ParseYieldExpression()
    {
        var loc = Loc();
        Advance(); // skip 'yield'

        // If followed by line terminator or end, yield with no argument
        if (Current.PrecedingLineTerminator || IsAtEnd ||
            Current.Type is TokenType.Semicolon or TokenType.RightBrace or TokenType.RightParen or
            TokenType.RightBracket or TokenType.Comma or TokenType.Colon)
        {
            return new YieldExpression { Location = loc };
        }

        bool isDelegate = false;
        if (Match(TokenType.Star))
            isDelegate = true;

        var argument = ParseAssignmentExpression();
        return new YieldExpression { Argument = argument, Delegate = isDelegate, Location = loc };
    }

    private SyntaxNode ParseAwaitExpression()
    {
        var loc = Loc();
        Advance(); // skip 'await'

        // Check if this is really await or just an identifier
        if (Current.PrecedingLineTerminator || Current.Type == TokenType.Arrow)
        {
            // Treat as identifier
            if (Current.Type == TokenType.Arrow && !Current.PrecedingLineTerminator)
            {
                Advance(); // skip '=>'
                var body = ParseArrowBody();
                return new ArrowFunctionExpression
                {
                    Params = [new Identifier { Name = "await", Location = loc }],
                    Body = body,
                    IsExpression = body is not BlockStatement,
                    Location = loc
                };
            }
            return new Identifier { Name = "await", Location = loc };
        }

        var argument = ParseExpression(PrecUnary);
        return new AwaitExpression { Argument = argument, Location = loc };
    }

    // ═══════════════════════════════════════════
    //  Literals
    // ═══════════════════════════════════════════

    private SyntaxNode ParseArrayLiteral()
    {
        var loc = Loc();
        Expect(TokenType.LeftBracket);
        var elements = new List<SyntaxNode?>();

        while (!IsAtEnd && Current.Type != TokenType.RightBracket)
        {
            if (Current.Type == TokenType.Comma)
            {
                elements.Add(null); // elision
                Advance();
                continue;
            }
            if (Current.Type == TokenType.Ellipsis)
            {
                var spreadLoc = Loc();
                Advance();
                var arg = ParseAssignmentExpression();
                elements.Add(new SpreadElement { Argument = arg, Location = spreadLoc });
            }
            else
            {
                elements.Add(ParseAssignmentExpression());
            }
            if (Current.Type != TokenType.RightBracket)
                Match(TokenType.Comma);
        }

        Expect(TokenType.RightBracket);
        return new ArrayExpression { Elements = elements, Location = loc };
    }

    private SyntaxNode ParseObjectLiteral()
    {
        var loc = Loc();
        Expect(TokenType.LeftBrace);
        var properties = new List<SyntaxNode>();

        while (!IsAtEnd && Current.Type != TokenType.RightBrace)
        {
            if (Current.Type == TokenType.Ellipsis)
            {
                var spreadLoc = Loc();
                Advance();
                var arg = ParseAssignmentExpression();
                properties.Add(new SpreadElement { Argument = arg, Location = spreadLoc });
            }
            else
            {
                properties.Add(ParsePropertyDefinitionExpr());
            }
            if (Current.Type != TokenType.RightBrace)
                Match(TokenType.Comma);
        }

        Expect(TokenType.RightBrace);
        return new ObjectExpression { Properties = properties, Location = loc };
    }

    private Property ParsePropertyDefinitionExpr()
    {
        var loc = Loc();

        // Getter/setter: get name() {} / set name(v) {}
        if ((Current.Type == TokenType.Get || Current.Type == TokenType.Set) &&
            Peek().Type != TokenType.LeftParen && Peek().Type != TokenType.Colon &&
            Peek().Type != TokenType.Comma && Peek().Type != TokenType.RightBrace &&
            Peek().Type != TokenType.Assign)
        {
            var kind = Current.Type == TokenType.Get ? PropertyKind.Get : PropertyKind.Set;
            Advance();
            bool computed = Current.Type == TokenType.LeftBracket;
            var key = ParsePropertyKey();
            Expect(TokenType.LeftParen);
            var parameters = new List<SyntaxNode>();
            if (kind == PropertyKind.Set && Current.Type != TokenType.RightParen)
            {
                parameters.Add(ParseBindingPattern());
            }
            Expect(TokenType.RightParen);
            var body = ParseBlockStatement();
            var fn = new FunctionExpression { Params = parameters, Body = body, Location = loc };
            return new Property { Key = key, Value = fn, Kind = kind, Computed = computed, IsMethod = true, Location = loc };
        }

        // async method: async name() {}
        if (Current.Type == TokenType.Async && !Peek().PrecedingLineTerminator &&
            Peek().Type != TokenType.Colon && Peek().Type != TokenType.Comma &&
            Peek().Type != TokenType.RightBrace && Peek().Type != TokenType.Assign &&
            Peek().Type != TokenType.LeftParen)
        {
            Advance(); // skip 'async'
            bool isGenerator = Match(TokenType.Star);
            bool computed = Current.Type == TokenType.LeftBracket;
            var key = ParsePropertyKey();
            var parameters = ParseFormalParams();
            var body = ParseBlockStatement();
            var fn = new FunctionExpression { Params = parameters, Body = body, IsAsync = true, IsGenerator = isGenerator, Location = loc };
            return new Property { Key = key, Value = fn, Kind = PropertyKind.Init, Computed = computed, IsMethod = true, Location = loc };
        }

        // Generator method: *name() {}
        if (Current.Type == TokenType.Star)
        {
            Advance();
            bool computed = Current.Type == TokenType.LeftBracket;
            var key = ParsePropertyKey();
            var parameters = ParseFormalParams();
            var body = ParseBlockStatement();
            var fn = new FunctionExpression { Params = parameters, Body = body, IsGenerator = true, Location = loc };
            return new Property { Key = key, Value = fn, Kind = PropertyKind.Init, Computed = computed, IsMethod = true, Location = loc };
        }

        // Computed key
        bool computedKey = Current.Type == TokenType.LeftBracket;
        var propertyKey = ParsePropertyKey();

        // Method shorthand: name() {}
        if (Current.Type == TokenType.LeftParen)
        {
            var parameters = ParseFormalParams();
            var body = ParseBlockStatement();
            var fn = new FunctionExpression { Params = parameters, Body = body, Location = loc };
            return new Property { Key = propertyKey, Value = fn, Kind = PropertyKind.Init, Computed = computedKey, IsMethod = true, Location = loc };
        }

        // name: value
        if (Match(TokenType.Colon))
        {
            var value = ParseAssignmentExpression();
            return new Property { Key = propertyKey, Value = value, Kind = PropertyKind.Init, Computed = computedKey, Location = loc };
        }

        // Shorthand: { name } or { name = default }
        if (propertyKey is Identifier ident)
        {
            SyntaxNode value = ident;
            if (Match(TokenType.Assign))
            {
                var defaultVal = ParseAssignmentExpression();
                value = new AssignmentPattern { Left = ident, Right = defaultVal, Location = loc };
            }
            return new Property { Key = ident, Value = value, Kind = PropertyKind.Init, Shorthand = true, Location = loc };
        }

        throw new JsSyntaxError("Unexpected property definition", loc.Line, loc.Column);
    }

    private SyntaxNode ParsePropertyKey()
    {
        if (Current.Type == TokenType.LeftBracket)
        {
            Advance();
            var expr = ParseAssignmentExpression();
            Expect(TokenType.RightBracket);
            return expr;
        }
        if (Current.Type == TokenType.NumericLiteral)
        {
            var t = Advance();
            return new Literal { Value = t.NumericValue, Raw = t.Value, Location = new SourceLocation(t.Line, t.Column) };
        }
        if (Current.Type == TokenType.StringLiteral)
        {
            var t = Advance();
            return new Literal { Value = t.Value, Raw = t.Value, Location = new SourceLocation(t.Line, t.Column) };
        }
        var nameLoc = Loc();
        var name = ExpectIdentifierName();
        return new Identifier { Name = name, Location = nameLoc };
    }

    // ═══════════════════════════════════════════
    //  Function / Class expressions
    // ═══════════════════════════════════════════

    private SyntaxNode ParseFunctionExpression(bool isAsync)
    {
        var loc = Loc();
        Expect(TokenType.Function);
        bool isGenerator = Match(TokenType.Star);
        Identifier? id = null;
        if (IsIdentifier(Current))
            id = new Identifier { Name = Advance().Value, Location = Loc() };
        var parameters = ParseFormalParams();
        var body = ParseBlockStatement();
        return new FunctionExpression { Id = id, Params = parameters, Body = body, IsAsync = isAsync, IsGenerator = isGenerator, Location = loc };
    }

    private SyntaxNode ParseClassExpression()
    {
        var loc = Loc();
        Expect(TokenType.Class);
        Identifier? id = null;
        if (IsIdentifier(Current) && Current.Type != TokenType.Extends && Current.Type != TokenType.LeftBrace)
            id = new Identifier { Name = Advance().Value, Location = Loc() };
        SyntaxNode? superClass = null;
        if (Match(TokenType.Extends))
            superClass = ParseExpression(PrecAssignment);
        var body = ParseClassBody();
        return new ClassExpression { Id = id, SuperClass = superClass, Body = body, Location = loc };
    }

    private SyntaxNode ParseNewExpression()
    {
        var loc = Loc();
        Expect(TokenType.New);

        // new.target
        if (Match(TokenType.Dot))
        {
            var name = ExpectIdentifierName();
            return new MemberExpression
            {
                Object = new Identifier { Name = "new", Location = loc },
                Property = new Identifier { Name = name },
                Location = loc
            };
        }

        var callee = ParseExpression(PrecCall);

        // Parse arguments if present
        List<SyntaxNode> args = [];
        if (Current.Type == TokenType.LeftParen)
        {
            args = ParseArguments();
        }

        return new NewExpression { Callee = callee, Arguments = args, Location = loc };
    }

    // ═══════════════════════════════════════════
    //  Template literals
    // ═══════════════════════════════════════════

    private SyntaxNode ParseTemplateLiteralExpr()
    {
        return ParseTemplateLiteralNode();
    }

    private TemplateLiteral ParseTemplateLiteralNode()
    {
        var loc = Loc();
        var quasis = new List<TemplateElement>();
        var expressions = new List<SyntaxNode>();

        if (Current.Type == TokenType.TemplateLiteral)
        {
            // No-substitution template
            var t = Advance();
            quasis.Add(new TemplateElement { Value = t.Value, Raw = t.Value, Tail = true, Location = loc });
            return new TemplateLiteral { Quasis = quasis, Expressions = expressions, Location = loc };
        }

        // Template with substitutions
        if (Current.Type == TokenType.TemplateHead)
        {
            var head = Advance();
            quasis.Add(new TemplateElement { Value = head.Value, Raw = head.Value, Location = loc });

            while (true)
            {
                expressions.Add(ParseExpression());

                if (Current.Type == TokenType.TemplateTail)
                {
                    var tail = Advance();
                    quasis.Add(new TemplateElement { Value = tail.Value, Raw = tail.Value, Tail = true });
                    break;
                }
                if (Current.Type == TokenType.TemplateMiddle)
                {
                    var mid = Advance();
                    quasis.Add(new TemplateElement { Value = mid.Value, Raw = mid.Value });
                }
                else
                {
                    throw new JsSyntaxError("Expected template continuation", Current.Line, Current.Column);
                }
            }
        }

        return new TemplateLiteral { Quasis = quasis, Expressions = expressions, Location = loc };
    }

    // ═══════════════════════════════════════════
    //  Arguments
    // ═══════════════════════════════════════════

    private List<SyntaxNode> ParseArguments()
    {
        Expect(TokenType.LeftParen);
        var args = new List<SyntaxNode>();

        while (!IsAtEnd && Current.Type != TokenType.RightParen)
        {
            if (Current.Type == TokenType.Ellipsis)
            {
                var spreadLoc = Loc();
                Advance();
                args.Add(new SpreadElement { Argument = ParseAssignmentExpression(), Location = spreadLoc });
            }
            else
            {
                args.Add(ParseAssignmentExpression());
            }
            if (Current.Type != TokenType.RightParen)
                Expect(TokenType.Comma);
        }

        Expect(TokenType.RightParen);
        return args;
    }

    // ═══════════════════════════════════════════
    //  BigInt literal parsing
    // ═══════════════════════════════════════════

    private static BigInteger ParseBigIntLiteral(string raw)
    {
        // Strip the trailing 'n' suffix
        string text = raw.EndsWith('n') ? raw[..^1] : raw;
        text = text.Replace("_", "", StringComparison.Ordinal);

        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return BigInteger.Parse("0" + text[2..], System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture);
        }

        if (text.StartsWith("0o", StringComparison.OrdinalIgnoreCase))
        {
            BigInteger result = BigInteger.Zero;
            foreach (char c in text[2..])
            {
                result = result * 8 + (c - '0');
            }
            return result;
        }

        if (text.StartsWith("0b", StringComparison.OrdinalIgnoreCase))
        {
            BigInteger result = BigInteger.Zero;
            foreach (char c in text[2..])
            {
                result = result * 2 + (c - '0');
            }
            return result;
        }

        return BigInteger.Parse(text, System.Globalization.CultureInfo.InvariantCulture);
    }
}
