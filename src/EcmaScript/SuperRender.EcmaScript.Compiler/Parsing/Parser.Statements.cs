using SuperRender.EcmaScript.Compiler.Ast;
using SuperRender.EcmaScript.Runtime.Errors;
using SuperRender.EcmaScript.Compiler.Lexing;

namespace SuperRender.EcmaScript.Compiler.Parsing;

public sealed partial class Parser
{
    // ═══════════════════════════════════════════
    //  Statement parsing
    // ═══════════════════════════════════════════

    private SyntaxNode ParseStatement()
    {
        var loc = Loc();
        return Current.Type switch
        {
            TokenType.LeftBrace => ParseBlockStatement(),
            TokenType.If => ParseIfStatement(),
            TokenType.For => ParseForStatement(),
            TokenType.While => ParseWhileStatement(),
            TokenType.Do => ParseDoWhileStatement(),
            TokenType.Switch => ParseSwitchStatement(),
            TokenType.Try => ParseTryStatement(),
            TokenType.Return => ParseReturnStatement(),
            TokenType.Throw => ParseThrowStatement(),
            TokenType.Break => ParseBreakStatement(),
            TokenType.Continue => ParseContinueStatement(),
            TokenType.Semicolon => ParseEmptyStatement(),
            TokenType.Debugger => ParseDebuggerStatement(),
            TokenType.With => ParseWithStatement(),
            _ => ParseExpressionOrLabeledStatement()
        };
    }

    private BlockStatement ParseBlockStatement()
    {
        var loc = Loc();
        Expect(TokenType.LeftBrace);
        var body = new List<SyntaxNode>();
        while (!IsAtEnd && Current.Type != TokenType.RightBrace)
        {
            body.Add(ParseStatementOrDeclaration());
        }
        Expect(TokenType.RightBrace);
        return new BlockStatement { Body = body, Location = loc };
    }

    private EmptyStatement ParseEmptyStatement()
    {
        var loc = Loc();
        Expect(TokenType.Semicolon);
        return new EmptyStatement { Location = loc };
    }

    private SyntaxNode ParseDebuggerStatement()
    {
        var loc = Loc();
        Advance(); // skip 'debugger'
        ExpectSemicolon();
        return new ExpressionStatement
        {
            Expression = new Identifier { Name = "debugger", Location = loc },
            Location = loc
        };
    }

    private WithStatement ParseWithStatement()
    {
        var loc = Loc();
        Advance(); // skip 'with'
        Expect(TokenType.LeftParen);
        var obj = ParseExpression();
        Expect(TokenType.RightParen);
        var body = ParseStatement();
        return new WithStatement { Object = obj, Body = body, Location = loc };
    }

    private IfStatement ParseIfStatement()
    {
        var loc = Loc();
        Expect(TokenType.If);
        Expect(TokenType.LeftParen);
        var test = ParseExpression();
        Expect(TokenType.RightParen);
        var consequent = ParseStatement();
        SyntaxNode? alternate = null;
        if (Match(TokenType.Else))
            alternate = ParseStatement();
        return new IfStatement { Test = test, Consequent = consequent, Alternate = alternate, Location = loc };
    }

    private SyntaxNode ParseForStatement()
    {
        var loc = Loc();
        Expect(TokenType.For);

        bool isAwait = false;
        if (Current.Type == TokenType.Await)
        {
            isAwait = true;
            Advance();
        }

        Expect(TokenType.LeftParen);

        // for ( ; ... )
        if (Current.Type == TokenType.Semicolon)
        {
            Advance();
            return ParseForStatementContinuation(null, loc);
        }

        // for (var/let/const ...
        if (Current.Type is TokenType.Var or TokenType.Let or TokenType.Const)
        {
            var declLoc = Loc();
            var kind = Advance().Type switch
            {
                TokenType.Var => VariableKind.Var,
                TokenType.Let => VariableKind.Let,
                _ => VariableKind.Const
            };

            var id = ParseBindingPattern();
            // for-in / for-of
            if (Current.Type == TokenType.In)
            {
                Advance();
                var right = ParseExpression();
                Expect(TokenType.RightParen);
                var body = ParseStatement();
                var decl = new VariableDeclaration
                {
                    Kind = kind,
                    Declarations = [new VariableDeclarator { Id = id, Location = declLoc }],
                    Location = declLoc
                };
                return new ForInStatement { Left = decl, Right = right, Body = body, Location = loc };
            }
            if (Current.Type == TokenType.Of || (Current.Type == TokenType.Identifier && Current.Value == "of"))
            {
                Advance();
                var right = ParseAssignmentExpression();
                Expect(TokenType.RightParen);
                var body = ParseStatement();
                var decl = new VariableDeclaration
                {
                    Kind = kind,
                    Declarations = [new VariableDeclarator { Id = id, Location = declLoc }],
                    Location = declLoc
                };
                return new ForOfStatement { Left = decl, Right = right, Body = body, IsAwait = isAwait, Location = loc };
            }

            // regular for with variable declaration
            SyntaxNode? init2 = null;
            if (Match(TokenType.Assign))
            {
                var initVal = ParseAssignmentExpression();
                var declarators = new List<VariableDeclarator>
                {
                    new() { Id = id, Init = initVal, Location = declLoc }
                };
                while (Match(TokenType.Comma))
                {
                    var dLoc = Loc();
                    var dId = ParseBindingPattern();
                    SyntaxNode? dInit = null;
                    if (Match(TokenType.Assign))
                        dInit = ParseAssignmentExpression();
                    declarators.Add(new VariableDeclarator { Id = dId, Init = dInit, Location = dLoc });
                }
                init2 = new VariableDeclaration { Kind = kind, Declarations = declarators, Location = declLoc };
            }
            else
            {
                var declarators = new List<VariableDeclarator>
                {
                    new() { Id = id, Location = declLoc }
                };
                while (Match(TokenType.Comma))
                {
                    var dLoc = Loc();
                    var dId = ParseBindingPattern();
                    SyntaxNode? dInit = null;
                    if (Match(TokenType.Assign))
                        dInit = ParseAssignmentExpression();
                    declarators.Add(new VariableDeclarator { Id = dId, Init = dInit, Location = dLoc });
                }
                init2 = new VariableDeclaration { Kind = kind, Declarations = declarators, Location = declLoc };
            }

            Expect(TokenType.Semicolon);
            return ParseForStatementContinuation(init2, loc);
        }

        // for (expr ...
        var savedInForInit = _inForInit;
        _inForInit = true;
        var initExpr = ParseExpression();
        _inForInit = savedInForInit;

        // for-in / for-of with expression left
        if (Current.Type == TokenType.In)
        {
            Advance();
            var right = ParseExpression();
            Expect(TokenType.RightParen);
            var body = ParseStatement();
            return new ForInStatement { Left = initExpr, Right = right, Body = body, Location = loc };
        }
        if (Current.Type == TokenType.Of || (Current.Type == TokenType.Identifier && Current.Value == "of"))
        {
            Advance();
            var right = ParseAssignmentExpression();
            Expect(TokenType.RightParen);
            var body = ParseStatement();
            return new ForOfStatement { Left = initExpr, Right = right, Body = body, IsAwait = isAwait, Location = loc };
        }

        Expect(TokenType.Semicolon);
        return ParseForStatementContinuation(initExpr, loc);
    }

    private ForStatement ParseForStatementContinuation(SyntaxNode? init, SourceLocation loc)
    {
        SyntaxNode? test = Current.Type != TokenType.Semicolon ? ParseExpression() : null;
        Expect(TokenType.Semicolon);
        SyntaxNode? update = Current.Type != TokenType.RightParen ? ParseExpression() : null;
        Expect(TokenType.RightParen);
        var body = ParseStatement();
        return new ForStatement { Init = init, Test = test, Update = update, Body = body, Location = loc };
    }

    private WhileStatement ParseWhileStatement()
    {
        var loc = Loc();
        Expect(TokenType.While);
        Expect(TokenType.LeftParen);
        var test = ParseExpression();
        Expect(TokenType.RightParen);
        var body = ParseStatement();
        return new WhileStatement { Test = test, Body = body, Location = loc };
    }

    private DoWhileStatement ParseDoWhileStatement()
    {
        var loc = Loc();
        Expect(TokenType.Do);
        var body = ParseStatement();
        Expect(TokenType.While);
        Expect(TokenType.LeftParen);
        var test = ParseExpression();
        Expect(TokenType.RightParen);
        ExpectSemicolon();
        return new DoWhileStatement { Test = test, Body = body, Location = loc };
    }

    private SwitchStatement ParseSwitchStatement()
    {
        var loc = Loc();
        Expect(TokenType.Switch);
        Expect(TokenType.LeftParen);
        var discriminant = ParseExpression();
        Expect(TokenType.RightParen);
        Expect(TokenType.LeftBrace);
        var cases = new List<SwitchCase>();
        while (!IsAtEnd && Current.Type != TokenType.RightBrace)
        {
            var caseLoc = Loc();
            SyntaxNode? test = null;
            if (Match(TokenType.Case))
            {
                test = ParseExpression();
            }
            else
            {
                Expect(TokenType.Default);
            }
            Expect(TokenType.Colon);
            var consequent = new List<SyntaxNode>();
            while (!IsAtEnd && Current.Type is not TokenType.Case and not TokenType.Default and not TokenType.RightBrace)
            {
                consequent.Add(ParseStatementOrDeclaration());
            }
            cases.Add(new SwitchCase { Test = test, Consequent = consequent, Location = caseLoc });
        }
        Expect(TokenType.RightBrace);
        return new SwitchStatement { Discriminant = discriminant, Cases = cases, Location = loc };
    }

    private TryStatement ParseTryStatement()
    {
        var loc = Loc();
        Expect(TokenType.Try);
        var block = ParseBlockStatement();
        CatchClause? handler = null;
        BlockStatement? finalizer = null;

        if (Current.Type == TokenType.Catch)
        {
            var catchLoc = Loc();
            Advance();
            SyntaxNode? param = null;
            if (Match(TokenType.LeftParen))
            {
                param = ParseBindingPattern();
                Expect(TokenType.RightParen);
            }
            var catchBody = ParseBlockStatement();
            handler = new CatchClause { Param = param, Body = catchBody, Location = catchLoc };
        }

        if (Match(TokenType.Finally))
        {
            finalizer = ParseBlockStatement();
        }

        if (handler is null && finalizer is null)
            throw new JsSyntaxError("Missing catch or finally after try", loc.Line, loc.Column);

        return new TryStatement { Block = block, Handler = handler, Finalizer = finalizer, Location = loc };
    }

    private ReturnStatement ParseReturnStatement()
    {
        var loc = Loc();
        Expect(TokenType.Return);
        SyntaxNode? argument = null;
        if (!IsAtEnd && Current.Type != TokenType.Semicolon && Current.Type != TokenType.RightBrace &&
            !Current.PrecedingLineTerminator)
        {
            argument = ParseExpression();
        }
        ExpectSemicolon();
        return new ReturnStatement { Argument = argument, Location = loc };
    }

    private ThrowStatement ParseThrowStatement()
    {
        var loc = Loc();
        Expect(TokenType.Throw);
        if (Current.PrecedingLineTerminator)
            throw new JsSyntaxError("No line break allowed after 'throw'", loc.Line, loc.Column);
        var argument = ParseExpression();
        ExpectSemicolon();
        return new ThrowStatement { Argument = argument, Location = loc };
    }

    private BreakStatement ParseBreakStatement()
    {
        var loc = Loc();
        Expect(TokenType.Break);
        string? label = null;
        if (!Current.PrecedingLineTerminator && IsIdentifier(Current))
            label = Advance().Value;
        ExpectSemicolon();
        return new BreakStatement { Label = label, Location = loc };
    }

    private ContinueStatement ParseContinueStatement()
    {
        var loc = Loc();
        Expect(TokenType.Continue);
        string? label = null;
        if (!Current.PrecedingLineTerminator && IsIdentifier(Current))
            label = Advance().Value;
        ExpectSemicolon();
        return new ContinueStatement { Label = label, Location = loc };
    }

    private SyntaxNode ParseExpressionOrLabeledStatement()
    {
        var loc = Loc();

        // Check for labeled statement: Identifier ':'
        if (IsIdentifier(Current) && Peek().Type == TokenType.Colon)
        {
            var label = Advance().Value;
            Advance(); // skip ':'
            var body = ParseStatement();
            return new LabeledStatement { Label = label, Body = body, Location = loc };
        }

        var expr = ParseExpression();
        ExpectSemicolon();
        return new ExpressionStatement { Expression = expr, Location = loc };
    }
}
