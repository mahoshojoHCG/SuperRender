using SuperRender.EcmaScript.Compiler.Ast;
using SuperRender.EcmaScript.Runtime.Errors;
using SuperRender.EcmaScript.Compiler.Lexing;

namespace SuperRender.EcmaScript.Compiler.Parsing;

public sealed partial class Parser
{
    // ═══════════════════════════════════════════
    //  Variable declarations
    // ═══════════════════════════════════════════

    private VariableDeclaration ParseVariableDeclaration()
    {
        var loc = Loc();
        var kindToken = Advance();
        var kind = kindToken.Type switch
        {
            TokenType.Var => VariableKind.Var,
            TokenType.Let => VariableKind.Let,
            _ => VariableKind.Const
        };

        var declarators = new List<VariableDeclarator>();
        do
        {
            var declLoc = Loc();
            var id = ParseBindingPattern();
            SyntaxNode? init = null;
            if (Match(TokenType.Assign))
                init = ParseAssignmentExpression();
            declarators.Add(new VariableDeclarator { Id = id, Init = init, Location = declLoc });
        } while (Match(TokenType.Comma));

        ExpectSemicolon();
        return new VariableDeclaration { Kind = kind, Declarations = declarators, Location = loc };
    }

    // ═══════════════════════════════════════════
    //  Function declarations
    // ═══════════════════════════════════════════

    private FunctionDeclaration ParseFunctionDeclaration()
    {
        var loc = Loc();
        Expect(TokenType.Function);
        bool isGenerator = Match(TokenType.Star);
        Identifier? id = null;
        if (IsIdentifier(Current))
            id = new Identifier { Name = Advance().Value, Location = Loc() };
        var parameters = ParseFormalParams();
        var body = ParseBlockStatement();
        return new FunctionDeclaration { Id = id, Params = parameters, Body = body, IsGenerator = isGenerator, Location = loc };
    }

    private FunctionDeclaration ParseAsyncFunctionDeclaration()
    {
        var loc = Loc();
        Advance(); // skip 'async'
        Expect(TokenType.Function);
        bool isGenerator = Match(TokenType.Star);
        Identifier? id = null;
        if (IsIdentifier(Current))
            id = new Identifier { Name = Advance().Value, Location = Loc() };
        var parameters = ParseFormalParams();
        var body = ParseBlockStatement();
        return new FunctionDeclaration { Id = id, Params = parameters, Body = body, IsAsync = true, IsGenerator = isGenerator, Location = loc };
    }

    internal List<SyntaxNode> ParseFormalParams()
    {
        Expect(TokenType.LeftParen);
        var parameters = new List<SyntaxNode>();

        while (!IsAtEnd && Current.Type != TokenType.RightParen)
        {
            if (Current.Type == TokenType.Ellipsis)
            {
                var restLoc = Loc();
                Advance();
                var restArg = ParseBindingPattern();
                parameters.Add(new RestElement { Argument = restArg, Location = restLoc });
                break; // rest must be last
            }

            var param = ParseBindingPattern();
            if (Match(TokenType.Assign))
            {
                var defaultVal = ParseAssignmentExpression();
                param = new AssignmentPattern { Left = param, Right = defaultVal, Location = param.Location };
            }
            parameters.Add(param);

            if (Current.Type != TokenType.RightParen)
                Expect(TokenType.Comma);
        }

        Expect(TokenType.RightParen);
        return parameters;
    }

    // ═══════════════════════════════════════════
    //  Class declarations
    // ═══════════════════════════════════════════

    private ClassDeclaration ParseClassDeclaration()
    {
        var loc = Loc();
        Expect(TokenType.Class);
        Identifier? id = null;
        if (IsIdentifier(Current))
            id = new Identifier { Name = Advance().Value, Location = Loc() };
        SyntaxNode? superClass = null;
        if (Match(TokenType.Extends))
            superClass = ParseExpression(PrecAssignment);
        var body = ParseClassBody();
        return new ClassDeclaration { Id = id, SuperClass = superClass, Body = body, Location = loc };
    }

    internal ClassBody ParseClassBody()
    {
        var loc = Loc();
        Expect(TokenType.LeftBrace);
        var members = new List<SyntaxNode>();

        while (!IsAtEnd && Current.Type != TokenType.RightBrace)
        {
            if (Match(TokenType.Semicolon)) continue;
            members.Add(ParseClassMember());
        }

        Expect(TokenType.RightBrace);
        return new ClassBody { Body = members, Location = loc };
    }

    private SyntaxNode ParseClassMember()
    {
        var loc = Loc();
        bool isStatic = false;

        // Check for 'static'
        if (Current.Type == TokenType.Static)
        {
            // static could be a field name, or the 'static' modifier
            if (Peek().Type == TokenType.LeftBrace)
            {
                // Static initialization block — parse as method
                Advance(); // skip 'static'
                var body = ParseBlockStatement();
                var fn = new FunctionExpression { Params = [], Body = body, Location = loc };
                return new MethodDefinition
                {
                    Key = new Identifier { Name = "static", Location = loc },
                    Value = fn,
                    Kind = MethodKind.Method,
                    IsStatic = true,
                    Location = loc
                };
            }

            // Check if 'static' is followed by a field/method name or end of class
            if (Peek().Type is not (TokenType.Semicolon or TokenType.Assign or TokenType.RightBrace) ||
                Peek().Type == TokenType.LeftParen)
            {
                // If next is ( then 'static' is a method name
                if (Peek().Type == TokenType.LeftParen)
                {
                    // static() {} — method named 'static'
                }
                else
                {
                    isStatic = true;
                    Advance();
                }
            }
        }

        // Async method
        if (Current.Type == TokenType.Async && !Peek().PrecedingLineTerminator &&
            Peek().Type != TokenType.LeftParen && Peek().Type != TokenType.Semicolon &&
            Peek().Type != TokenType.Assign)
        {
            Advance(); // skip 'async'
            bool isGenerator = Match(TokenType.Star);

            bool computed = Current.Type == TokenType.LeftBracket;
            var key = ParsePropertyKey();
            var methodName = key is Identifier mid ? mid.Name : "";

            var parameters = ParseFormalParams();
            var body = ParseBlockStatement();
            var fn = new FunctionExpression { Params = parameters, Body = body, IsAsync = true, IsGenerator = isGenerator, Location = loc };
            var kind = methodName == "constructor" && !isStatic ? MethodKind.Constructor : MethodKind.Method;
            return new MethodDefinition { Key = key, Value = fn, Kind = kind, IsStatic = isStatic, Computed = computed, Location = loc };
        }

        // Generator method
        if (Current.Type == TokenType.Star)
        {
            Advance();
            bool computed = Current.Type == TokenType.LeftBracket;
            var key = ParsePropertyKey();
            var parameters = ParseFormalParams();
            var body = ParseBlockStatement();
            var fn = new FunctionExpression { Params = parameters, Body = body, IsGenerator = true, Location = loc };
            return new MethodDefinition { Key = key, Value = fn, Kind = MethodKind.Method, IsStatic = isStatic, Computed = computed, Location = loc };
        }

        // Getter/setter
        if ((Current.Type == TokenType.Get || Current.Type == TokenType.Set) &&
            Peek().Type != TokenType.LeftParen && Peek().Type != TokenType.Semicolon &&
            Peek().Type != TokenType.Assign)
        {
            var kind = Current.Type == TokenType.Get ? MethodKind.Get : MethodKind.Set;
            Advance();
            bool computed = Current.Type == TokenType.LeftBracket;
            var key = ParsePropertyKey();
            var parameters = ParseFormalParams();
            var body = ParseBlockStatement();
            var fn = new FunctionExpression { Params = parameters, Body = body, Location = loc };
            return new MethodDefinition { Key = key, Value = fn, Kind = kind, IsStatic = isStatic, Computed = computed, Location = loc };
        }

        // Private field/method: #name
        bool computedKey = Current.Type == TokenType.LeftBracket;
        SyntaxNode memberKey;

        if (Current.Type == TokenType.Hash)
        {
            Advance();
            var privName = ExpectIdentifierName();
            memberKey = new Identifier { Name = "#" + privName, Location = loc };
        }
        else
        {
            memberKey = ParsePropertyKey();
        }

        // Method: name() {}
        if (Current.Type == TokenType.LeftParen)
        {
            var methodName = memberKey is Identifier ident ? ident.Name : "";
            var parameters = ParseFormalParams();
            var body = ParseBlockStatement();
            var fn = new FunctionExpression { Params = parameters, Body = body, Location = loc };
            var kind = methodName == "constructor" && !isStatic ? MethodKind.Constructor : MethodKind.Method;
            return new MethodDefinition { Key = memberKey, Value = fn, Kind = kind, IsStatic = isStatic, Computed = computedKey, Location = loc };
        }

        // Field: name = value; or name;
        SyntaxNode? fieldValue = null;
        if (Match(TokenType.Assign))
        {
            fieldValue = ParseAssignmentExpression();
        }
        ExpectSemicolon();
        return new PropertyDefinition { Key = memberKey, Value = fieldValue, IsStatic = isStatic, Computed = computedKey, Location = loc };
    }

    // ═══════════════════════════════════════════
    //  Binding patterns (destructuring)
    // ═══════════════════════════════════════════

    internal SyntaxNode ParseBindingPattern()
    {
        if (Current.Type == TokenType.LeftBracket)
            return ParseArrayBindingPattern();
        if (Current.Type == TokenType.LeftBrace)
            return ParseObjectBindingPattern();

        var loc = Loc();
        var name = ExpectIdentifierName();
        return new Identifier { Name = name, Location = loc };
    }

    private ArrayPattern ParseArrayBindingPattern()
    {
        var loc = Loc();
        Expect(TokenType.LeftBracket);
        var elements = new List<SyntaxNode?>();

        while (!IsAtEnd && Current.Type != TokenType.RightBracket)
        {
            if (Current.Type == TokenType.Comma)
            {
                elements.Add(null);
                Advance();
                continue;
            }
            if (Current.Type == TokenType.Ellipsis)
            {
                var restLoc = Loc();
                Advance();
                var arg = ParseBindingPattern();
                elements.Add(new RestElement { Argument = arg, Location = restLoc });
                break;
            }
            var element = ParseBindingPattern();
            if (Match(TokenType.Assign))
            {
                var defaultVal = ParseAssignmentExpression();
                element = new AssignmentPattern { Left = element, Right = defaultVal, Location = element.Location };
            }
            elements.Add(element);
            if (Current.Type != TokenType.RightBracket)
                Match(TokenType.Comma);
        }

        Expect(TokenType.RightBracket);
        return new ArrayPattern { Elements = elements, Location = loc };
    }

    private ObjectPattern ParseObjectBindingPattern()
    {
        var loc = Loc();
        Expect(TokenType.LeftBrace);
        var properties = new List<SyntaxNode>();

        while (!IsAtEnd && Current.Type != TokenType.RightBrace)
        {
            if (Current.Type == TokenType.Ellipsis)
            {
                var restLoc = Loc();
                Advance();
                var arg = ParseBindingPattern();
                properties.Add(new RestElement { Argument = arg, Location = restLoc });
                break;
            }

            var propLoc = Loc();
            bool computed = Current.Type == TokenType.LeftBracket;
            var key = ParsePropertyKey();

            SyntaxNode value;
            bool shorthand = false;

            if (Match(TokenType.Colon))
            {
                value = ParseBindingPattern();
            }
            else if (key is Identifier ident)
            {
                value = ident;
                shorthand = true;
            }
            else
            {
                throw new JsSyntaxError("Expected ':' in object binding pattern", propLoc.Line, propLoc.Column);
            }

            if (Match(TokenType.Assign))
            {
                var defaultVal = ParseAssignmentExpression();
                value = new AssignmentPattern { Left = value, Right = defaultVal, Location = propLoc };
            }

            properties.Add(new Property { Key = key, Value = value, Computed = computed, Shorthand = shorthand, Kind = PropertyKind.Init, Location = propLoc });

            if (Current.Type != TokenType.RightBrace)
                Match(TokenType.Comma);
        }

        Expect(TokenType.RightBrace);
        return new ObjectPattern { Properties = properties, Location = loc };
    }

    // ═══════════════════════════════════════════
    //  Import / Export declarations
    // ═══════════════════════════════════════════

    private SyntaxNode ParseImportDeclaration()
    {
        var loc = Loc();
        Expect(TokenType.Import);

        // Dynamic import() handled in expression parser
        // import.meta handled in expression parser

        // import 'source' — side-effect import
        if (Current.Type == TokenType.StringLiteral)
        {
            var source = ParseStringLiteral();
            var assertions = TryParseImportAssertions();
            ExpectSemicolon();
            return new ImportDeclaration { Specifiers = [], Source = source, Assertions = assertions, Location = loc };
        }

        var specifiers = new List<SyntaxNode>();

        // import defaultExport from 'source'
        if (IsIdentifier(Current))
        {
            var local = new Identifier { Name = Advance().Value, Location = Loc() };
            specifiers.Add(new ImportDefaultSpecifier { Local = local, Location = local.Location });

            if (Match(TokenType.Comma))
            {
                // import default, { named } from 'source'
                // import default, * as ns from 'source'
                if (Current.Type == TokenType.LeftBrace)
                    ParseNamedImports(specifiers);
                else if (Current.Type == TokenType.Star)
                    ParseNamespaceImport(specifiers);
            }
        }
        else if (Current.Type == TokenType.LeftBrace)
        {
            ParseNamedImports(specifiers);
        }
        else if (Current.Type == TokenType.Star)
        {
            ParseNamespaceImport(specifiers);
        }

        ExpectKeyword("from");
        var sourceLiteral = ParseStringLiteral();
        var importAssertions = TryParseImportAssertions();
        ExpectSemicolon();
        return new ImportDeclaration { Specifiers = specifiers, Source = sourceLiteral, Assertions = importAssertions, Location = loc };
    }

    private void ParseNamedImports(List<SyntaxNode> specifiers)
    {
        Expect(TokenType.LeftBrace);
        while (!IsAtEnd && Current.Type != TokenType.RightBrace)
        {
            var specLoc = Loc();
            var imported = new Identifier { Name = ExpectIdentifierName(), Location = specLoc };
            Identifier local;
            if (Current.Type == TokenType.As)
            {
                Advance();
                local = new Identifier { Name = ExpectIdentifierName(), Location = Loc() };
            }
            else
            {
                local = imported;
            }
            specifiers.Add(new ImportSpecifier { Local = local, Imported = imported, Location = specLoc });
            if (Current.Type != TokenType.RightBrace)
                Expect(TokenType.Comma);
        }
        Expect(TokenType.RightBrace);
    }

    private void ParseNamespaceImport(List<SyntaxNode> specifiers)
    {
        var nsLoc = Loc();
        Expect(TokenType.Star);
        ExpectKeyword("as");
        var local = new Identifier { Name = ExpectIdentifierName(), Location = Loc() };
        specifiers.Add(new ImportNamespaceSpecifier { Local = local, Location = nsLoc });
    }

    private SyntaxNode ParseExportDeclaration()
    {
        var loc = Loc();
        Expect(TokenType.Export);

        // export default ...
        if (Match(TokenType.Default))
        {
            SyntaxNode decl;
            if (Current.Type == TokenType.Function)
                decl = ParseFunctionDeclaration();
            else if (Current.Type == TokenType.Async && Peek().Type == TokenType.Function)
                decl = ParseAsyncFunctionDeclaration();
            else if (Current.Type == TokenType.Class)
                decl = ParseClassDeclaration();
            else
            {
                decl = ParseAssignmentExpression();
                ExpectSemicolon();
            }
            return new ExportDefaultDeclaration { Declaration = decl, Location = loc };
        }

        // export * from 'source'
        if (Current.Type == TokenType.Star)
        {
            Advance();
            Identifier? exported = null;
            if (Current.Type == TokenType.As)
            {
                Advance();
                exported = new Identifier { Name = ExpectIdentifierName(), Location = Loc() };
            }
            ExpectKeyword("from");
            var source = ParseStringLiteral();
            ExpectSemicolon();
            return new ExportAllDeclaration { Source = source, Exported = exported, Location = loc };
        }

        // export { name1, name2 as alias }
        if (Current.Type == TokenType.LeftBrace)
        {
            var specifiers = new List<ExportSpecifier>();
            Expect(TokenType.LeftBrace);
            while (!IsAtEnd && Current.Type != TokenType.RightBrace)
            {
                var specLoc = Loc();
                var local = new Identifier { Name = ExpectIdentifierName(), Location = specLoc };
                Identifier exported;
                if (Current.Type == TokenType.As)
                {
                    Advance();
                    exported = new Identifier { Name = ExpectIdentifierName(), Location = Loc() };
                }
                else
                {
                    exported = local;
                }
                specifiers.Add(new ExportSpecifier { Local = local, Exported = exported, Location = specLoc });
                if (Current.Type != TokenType.RightBrace)
                    Expect(TokenType.Comma);
            }
            Expect(TokenType.RightBrace);

            Literal? source = null;
            if (Current.Type == TokenType.From || (Current.Type == TokenType.Identifier && Current.Value == "from"))
            {
                Advance();
                source = ParseStringLiteral();
            }
            ExpectSemicolon();
            return new ExportNamedDeclaration { Specifiers = specifiers, Source = source, Location = loc };
        }

        // export var/let/const/function/class
        SyntaxNode declaration;
        if (Current.Type is TokenType.Var or TokenType.Let or TokenType.Const)
            declaration = ParseVariableDeclaration();
        else if (Current.Type == TokenType.Function)
            declaration = ParseFunctionDeclaration();
        else if (Current.Type == TokenType.Async && Peek().Type == TokenType.Function)
            declaration = ParseAsyncFunctionDeclaration();
        else if (Current.Type == TokenType.Class)
            declaration = ParseClassDeclaration();
        else
            throw new JsSyntaxError("Unexpected export", loc.Line, loc.Column);

        return new ExportNamedDeclaration { Declaration = declaration, Specifiers = [], Location = loc };
    }

    // ═══════════════════════════════════════════
    //  Helpers
    // ═══════════════════════════════════════════

    private Literal ParseStringLiteral()
    {
        var loc = Loc();
        var token = Expect(TokenType.StringLiteral);
        return new Literal { Value = token.Value, Raw = token.Value, Location = loc };
    }

    private void ExpectKeyword(string keyword)
    {
        if ((IsIdentifier(Current) || Current.Type == TokenType.From) && Current.Value == keyword)
        {
            Advance();
            return;
        }
        throw new JsSyntaxError($"Expected '{keyword}'", Current.Line, Current.Column);
    }

    private Dictionary<string, string>? TryParseImportAssertions()
    {
        // import ... with { key: 'value' }
        // 'with' is parsed as an identifier since TokenType.With is a keyword
        if (Current.Type != TokenType.With && !(IsIdentifier(Current) && Current.Value == "with") &&
            !(Current.Type == TokenType.Identifier && Current.Value == "assert"))
        {
            return null;
        }

        Advance(); // skip 'with' or 'assert'
        Expect(TokenType.LeftBrace);

        var assertions = new Dictionary<string, string>(StringComparer.Ordinal);

        while (!IsAtEnd && Current.Type != TokenType.RightBrace)
        {
            string key;
            if (Current.Type == TokenType.StringLiteral)
            {
                key = Advance().Value;
            }
            else
            {
                key = ExpectIdentifierName();
            }

            Expect(TokenType.Colon);

            var value = Expect(TokenType.StringLiteral).Value;
            assertions[key] = value;

            if (Current.Type != TokenType.RightBrace)
                Match(TokenType.Comma);
        }

        Expect(TokenType.RightBrace);
        return assertions;
    }
}
