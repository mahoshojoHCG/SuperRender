using SuperRender.EcmaScript.Compiler.Ast;
using SuperRender.EcmaScript.Compiler.Parsing;
using Xunit;

namespace SuperRender.EcmaScript.Tests.Parsing;

public class ParserTests
{
    private static Program Parse(string source) => new Parser(source).Parse();

    // ═══════════════════════════════════════════
    //  Variable declarations
    // ═══════════════════════════════════════════

    [Theory]
    [InlineData("let x = 1;", VariableKind.Let)]
    [InlineData("const y = 2;", VariableKind.Const)]
    [InlineData("var z = 3;", VariableKind.Var)]
    public void Parse_VariableDeclaration_ReturnsCorrectKind(string source, VariableKind expectedKind)
    {
        var program = Parse(source);
        var decl = Assert.IsType<VariableDeclaration>(program.Body[0]);
        Assert.Equal(expectedKind, decl.Kind);
        Assert.Single(decl.Declarations);
    }

    [Fact]
    public void Parse_LetWithoutInit_ParsesSuccessfully()
    {
        var program = Parse("let x;");
        var decl = Assert.IsType<VariableDeclaration>(program.Body[0]);
        Assert.Null(decl.Declarations[0].Init);
    }

    [Fact]
    public void Parse_MultipleDeclarators_ParsesAll()
    {
        var program = Parse("let a = 1, b = 2, c = 3;");
        var decl = Assert.IsType<VariableDeclaration>(program.Body[0]);
        Assert.Equal(3, decl.Declarations.Count);
    }

    // ═══════════════════════════════════════════
    //  Function declarations
    // ═══════════════════════════════════════════

    [Fact]
    public void Parse_FunctionDeclaration_ReturnsCorrectAst()
    {
        var program = Parse("function add(a, b) { return a + b; }");
        var func = Assert.IsType<FunctionDeclaration>(program.Body[0]);
        Assert.NotNull(func.Id);
        Assert.Equal("add", func.Id!.Name);
        Assert.Equal(2, func.Params.Count);
        Assert.False(func.IsAsync);
        Assert.False(func.IsGenerator);
    }

    [Fact]
    public void Parse_FunctionDeclaration_NoParams_ParsesSuccessfully()
    {
        var program = Parse("function greet() { return 'hello'; }");
        var func = Assert.IsType<FunctionDeclaration>(program.Body[0]);
        Assert.Empty(func.Params);
    }

    [Fact]
    public void Parse_GeneratorFunction_SetsIsGenerator()
    {
        var program = Parse("function* gen() { yield 1; }");
        var func = Assert.IsType<FunctionDeclaration>(program.Body[0]);
        Assert.True(func.IsGenerator);
    }

    [Fact]
    public void Parse_AsyncFunction_SetsIsAsync()
    {
        var program = Parse("async function fetchData() { return 1; }");
        var func = Assert.IsType<FunctionDeclaration>(program.Body[0]);
        Assert.True(func.IsAsync);
    }

    // ═══════════════════════════════════════════
    //  Arrow functions
    // ═══════════════════════════════════════════

    [Fact]
    public void Parse_ArrowFunction_SingleParam_ExpressionBody()
    {
        var program = Parse("x => x * 2;");
        var stmt = Assert.IsType<ExpressionStatement>(program.Body[0]);
        var arrow = Assert.IsType<ArrowFunctionExpression>(stmt.Expression);
        Assert.Single(arrow.Params);
        Assert.True(arrow.IsExpression);
    }

    [Fact]
    public void Parse_ArrowFunction_MultiParam_ExpressionBody()
    {
        var program = Parse("(a, b) => a + b;");
        var stmt = Assert.IsType<ExpressionStatement>(program.Body[0]);
        var arrow = Assert.IsType<ArrowFunctionExpression>(stmt.Expression);
        Assert.Equal(2, arrow.Params.Count);
        Assert.True(arrow.IsExpression);
    }

    [Fact]
    public void Parse_ArrowFunction_NoParam_ExpressionBody()
    {
        var program = Parse("() => 42;");
        var stmt = Assert.IsType<ExpressionStatement>(program.Body[0]);
        var arrow = Assert.IsType<ArrowFunctionExpression>(stmt.Expression);
        Assert.Empty(arrow.Params);
        Assert.True(arrow.IsExpression);
    }

    [Fact]
    public void Parse_ArrowFunction_BlockBody()
    {
        var program = Parse("(x) => { return x; };");
        var stmt = Assert.IsType<ExpressionStatement>(program.Body[0]);
        var arrow = Assert.IsType<ArrowFunctionExpression>(stmt.Expression);
        Assert.False(arrow.IsExpression);
        Assert.IsType<BlockStatement>(arrow.Body);
    }

    // ═══════════════════════════════════════════
    //  Class declarations
    // ═══════════════════════════════════════════

    [Fact]
    public void Parse_ClassDeclaration_Simple()
    {
        var program = Parse("class Animal { constructor(name) { this.name = name; } }");
        var cls = Assert.IsType<ClassDeclaration>(program.Body[0]);
        Assert.NotNull(cls.Id);
        Assert.Equal("Animal", cls.Id!.Name);
        Assert.Null(cls.SuperClass);
        Assert.Single(cls.Body.Body); // constructor
    }

    [Fact]
    public void Parse_ClassDeclaration_WithExtends()
    {
        var program = Parse("class Dog extends Animal { bark() { return 'woof'; } }");
        var cls = Assert.IsType<ClassDeclaration>(program.Body[0]);
        Assert.NotNull(cls.SuperClass);
        var superClass = Assert.IsType<Identifier>(cls.SuperClass);
        Assert.Equal("Animal", superClass.Name);
    }

    [Fact]
    public void Parse_ClassDeclaration_WithStaticMethod()
    {
        var program = Parse("class Util { static helper() { return 1; } }");
        var cls = Assert.IsType<ClassDeclaration>(program.Body[0]);
        var method = Assert.IsType<MethodDefinition>(cls.Body.Body[0]);
        Assert.True(method.IsStatic);
    }

    [Fact]
    public void Parse_ClassDeclaration_WithMultipleMembers()
    {
        var source = @"
class Person {
    constructor(name) { this.name = name; }
    greet() { return 'hello'; }
    static create(name) { return new Person(name); }
}";
        var program = Parse(source);
        var cls = Assert.IsType<ClassDeclaration>(program.Body[0]);
        Assert.Equal(3, cls.Body.Body.Count);
    }

    // ═══════════════════════════════════════════
    //  If/else
    // ═══════════════════════════════════════════

    [Fact]
    public void Parse_IfStatement_NoElse()
    {
        var program = Parse("if (x > 0) { y = 1; }");
        var ifStmt = Assert.IsType<IfStatement>(program.Body[0]);
        Assert.NotNull(ifStmt.Test);
        Assert.NotNull(ifStmt.Consequent);
        Assert.Null(ifStmt.Alternate);
    }

    [Fact]
    public void Parse_IfElseStatement_HasAlternate()
    {
        var program = Parse("if (x > 0) { y = 1; } else { y = -1; }");
        var ifStmt = Assert.IsType<IfStatement>(program.Body[0]);
        Assert.NotNull(ifStmt.Alternate);
    }

    [Fact]
    public void Parse_IfElseIfChain_ParsesCorrectly()
    {
        var program = Parse("if (x > 0) { a; } else if (x < 0) { b; } else { c; }");
        var ifStmt = Assert.IsType<IfStatement>(program.Body[0]);
        var elseIf = Assert.IsType<IfStatement>(ifStmt.Alternate);
        Assert.NotNull(elseIf.Alternate);
    }

    // ═══════════════════════════════════════════
    //  For loops
    // ═══════════════════════════════════════════

    [Fact]
    public void Parse_ForStatement_Classic()
    {
        var program = Parse("for (let i = 0; i < 10; i++) { x; }");
        var forStmt = Assert.IsType<ForStatement>(program.Body[0]);
        Assert.NotNull(forStmt.Init);
        Assert.NotNull(forStmt.Test);
        Assert.NotNull(forStmt.Update);
    }

    [Fact]
    public void Parse_ForInStatement_ParsesCorrectly()
    {
        var program = Parse("for (let key in obj) { x; }");
        var forIn = Assert.IsType<ForInStatement>(program.Body[0]);
        Assert.NotNull(forIn.Left);
        Assert.NotNull(forIn.Right);
    }

    [Fact]
    public void Parse_ForOfStatement_ParsesCorrectly()
    {
        var program = Parse("for (const item of arr) { x; }");
        var forOf = Assert.IsType<ForOfStatement>(program.Body[0]);
        Assert.NotNull(forOf.Left);
        Assert.NotNull(forOf.Right);
    }

    // ═══════════════════════════════════════════
    //  While / do-while
    // ═══════════════════════════════════════════

    [Fact]
    public void Parse_WhileStatement_ParsesCorrectly()
    {
        var program = Parse("while (x > 0) { x--; }");
        var whileStmt = Assert.IsType<WhileStatement>(program.Body[0]);
        Assert.NotNull(whileStmt.Test);
        Assert.NotNull(whileStmt.Body);
    }

    [Fact]
    public void Parse_DoWhileStatement_ParsesCorrectly()
    {
        var program = Parse("do { x++; } while (x < 10);");
        var doWhile = Assert.IsType<DoWhileStatement>(program.Body[0]);
        Assert.NotNull(doWhile.Test);
        Assert.NotNull(doWhile.Body);
    }

    // ═══════════════════════════════════════════
    //  Switch
    // ═══════════════════════════════════════════

    [Fact]
    public void Parse_SwitchStatement_WithCasesAndDefault()
    {
        var source = @"
switch (x) {
    case 1: y = 'one'; break;
    case 2: y = 'two'; break;
    default: y = 'other';
}";
        var program = Parse(source);
        var switchStmt = Assert.IsType<SwitchStatement>(program.Body[0]);
        Assert.Equal(3, switchStmt.Cases.Count);
        Assert.Null(switchStmt.Cases[2].Test); // default case
    }

    // ═══════════════════════════════════════════
    //  Try/catch/finally
    // ═══════════════════════════════════════════

    [Fact]
    public void Parse_TryCatch_ParsesCorrectly()
    {
        var program = Parse("try { x; } catch (e) { y; }");
        var tryStmt = Assert.IsType<TryStatement>(program.Body[0]);
        Assert.NotNull(tryStmt.Handler);
        Assert.Null(tryStmt.Finalizer);
    }

    [Fact]
    public void Parse_TryCatchFinally_ParsesCorrectly()
    {
        var program = Parse("try { x; } catch (e) { y; } finally { z; }");
        var tryStmt = Assert.IsType<TryStatement>(program.Body[0]);
        Assert.NotNull(tryStmt.Handler);
        Assert.NotNull(tryStmt.Finalizer);
    }

    [Fact]
    public void Parse_TryFinally_NoCatch_ParsesCorrectly()
    {
        var program = Parse("try { x; } finally { z; }");
        var tryStmt = Assert.IsType<TryStatement>(program.Body[0]);
        Assert.Null(tryStmt.Handler);
        Assert.NotNull(tryStmt.Finalizer);
    }

    // ═══════════════════════════════════════════
    //  Binary expressions with precedence
    // ═══════════════════════════════════════════

    [Fact]
    public void Parse_BinaryExpression_Addition()
    {
        var program = Parse("1 + 2;");
        var stmt = Assert.IsType<ExpressionStatement>(program.Body[0]);
        var bin = Assert.IsType<BinaryExpression>(stmt.Expression);
        Assert.Equal("+", bin.Operator);
    }

    [Fact]
    public void Parse_BinaryExpression_MultiplicationHasHigherPrecedenceThanAddition()
    {
        var program = Parse("1 + 2 * 3;");
        var stmt = Assert.IsType<ExpressionStatement>(program.Body[0]);
        var add = Assert.IsType<BinaryExpression>(stmt.Expression);
        Assert.Equal("+", add.Operator);
        // Right side should be 2 * 3
        var mul = Assert.IsType<BinaryExpression>(add.Right);
        Assert.Equal("*", mul.Operator);
    }

    [Fact]
    public void Parse_BinaryExpression_ParenthesesOverridePrecedence()
    {
        var program = Parse("(1 + 2) * 3;");
        var stmt = Assert.IsType<ExpressionStatement>(program.Body[0]);
        var mul = Assert.IsType<BinaryExpression>(stmt.Expression);
        Assert.Equal("*", mul.Operator);
        // Left side should be 1 + 2
        var add = Assert.IsType<BinaryExpression>(mul.Left);
        Assert.Equal("+", add.Operator);
    }

    [Fact]
    public void Parse_LogicalExpression_AndOr()
    {
        var program = Parse("a && b || c;");
        var stmt = Assert.IsType<ExpressionStatement>(program.Body[0]);
        var orExpr = Assert.IsType<LogicalExpression>(stmt.Expression);
        Assert.Equal("||", orExpr.Operator);
        var andExpr = Assert.IsType<LogicalExpression>(orExpr.Left);
        Assert.Equal("&&", andExpr.Operator);
    }

    // ═══════════════════════════════════════════
    //  Object / Array literals
    // ═══════════════════════════════════════════

    [Fact]
    public void Parse_ObjectLiteral_Empty()
    {
        var program = Parse("({});");
        var stmt = Assert.IsType<ExpressionStatement>(program.Body[0]);
        var obj = Assert.IsType<ObjectExpression>(stmt.Expression);
        Assert.Empty(obj.Properties);
    }

    [Fact]
    public void Parse_ObjectLiteral_WithProperties()
    {
        var program = Parse("({ a: 1, b: 2 });");
        var stmt = Assert.IsType<ExpressionStatement>(program.Body[0]);
        var obj = Assert.IsType<ObjectExpression>(stmt.Expression);
        Assert.Equal(2, obj.Properties.Count);
    }

    [Fact]
    public void Parse_ObjectLiteral_ShorthandProperty()
    {
        var program = Parse("({ x });");
        var stmt = Assert.IsType<ExpressionStatement>(program.Body[0]);
        var obj = Assert.IsType<ObjectExpression>(stmt.Expression);
        var prop = Assert.IsType<Property>(obj.Properties[0]);
        Assert.True(prop.Shorthand);
    }

    [Fact]
    public void Parse_ArrayLiteral_Empty()
    {
        var program = Parse("[];");
        var stmt = Assert.IsType<ExpressionStatement>(program.Body[0]);
        var arr = Assert.IsType<ArrayExpression>(stmt.Expression);
        Assert.Empty(arr.Elements);
    }

    [Fact]
    public void Parse_ArrayLiteral_WithElements()
    {
        var program = Parse("[1, 2, 3];");
        var stmt = Assert.IsType<ExpressionStatement>(program.Body[0]);
        var arr = Assert.IsType<ArrayExpression>(stmt.Expression);
        Assert.Equal(3, arr.Elements.Count);
    }

    // ═══════════════════════════════════════════
    //  Destructuring
    // ═══════════════════════════════════════════

    [Fact]
    public void Parse_ArrayDestructuring_Basic()
    {
        var program = Parse("const [a, b] = [1, 2];");
        var decl = Assert.IsType<VariableDeclaration>(program.Body[0]);
        var pattern = Assert.IsType<ArrayPattern>(decl.Declarations[0].Id);
        Assert.Equal(2, pattern.Elements.Count);
    }

    [Fact]
    public void Parse_ObjectDestructuring_Basic()
    {
        var program = Parse("const { x, y } = obj;");
        var decl = Assert.IsType<VariableDeclaration>(program.Body[0]);
        var pattern = Assert.IsType<ObjectPattern>(decl.Declarations[0].Id);
        Assert.Equal(2, pattern.Properties.Count);
    }

    // ═══════════════════════════════════════════
    //  Template literals
    // ═══════════════════════════════════════════

    [Fact]
    public void Parse_TemplateLiteral_NoSubstitution()
    {
        var program = Parse("`hello`;");
        var stmt = Assert.IsType<ExpressionStatement>(program.Body[0]);
        var tmpl = Assert.IsType<TemplateLiteral>(stmt.Expression);
        Assert.Single(tmpl.Quasis);
        Assert.Empty(tmpl.Expressions);
    }

    [Fact]
    public void Parse_TemplateLiteral_WithSubstitution()
    {
        var program = Parse("`hello ${name}`;");
        var stmt = Assert.IsType<ExpressionStatement>(program.Body[0]);
        var tmpl = Assert.IsType<TemplateLiteral>(stmt.Expression);
        Assert.Equal(2, tmpl.Quasis.Count);
        Assert.Single(tmpl.Expressions);
    }

    // ═══════════════════════════════════════════
    //  Import / Export
    // ═══════════════════════════════════════════

    [Fact]
    public void Parse_ImportDeclaration_Default()
    {
        var program = Parse("import foo from 'module';");
        var imp = Assert.IsType<ImportDeclaration>(program.Body[0]);
        Assert.Single(imp.Specifiers);
        Assert.IsType<ImportDefaultSpecifier>(imp.Specifiers[0]);
        Assert.True(program.IsModule);
    }

    [Fact]
    public void Parse_ImportDeclaration_Named()
    {
        var program = Parse("import { a, b } from 'module';");
        var imp = Assert.IsType<ImportDeclaration>(program.Body[0]);
        Assert.Equal(2, imp.Specifiers.Count);
    }

    [Fact]
    public void Parse_ImportDeclaration_SideEffect()
    {
        var program = Parse("import 'module';");
        var imp = Assert.IsType<ImportDeclaration>(program.Body[0]);
        Assert.Empty(imp.Specifiers);
    }

    [Fact]
    public void Parse_ExportDeclaration_Named()
    {
        var program = Parse("export const x = 1;");
        var exp = Assert.IsType<ExportNamedDeclaration>(program.Body[0]);
        Assert.NotNull(exp.Declaration);
    }

    [Fact]
    public void Parse_ExportDeclaration_Default()
    {
        var program = Parse("export default 42;");
        var exp = Assert.IsType<ExportDefaultDeclaration>(program.Body[0]);
        Assert.NotNull(exp.Declaration);
    }

    // ═══════════════════════════════════════════
    //  Misc expressions
    // ═══════════════════════════════════════════

    [Fact]
    public void Parse_ConditionalExpression_Ternary()
    {
        var program = Parse("x ? 1 : 2;");
        var stmt = Assert.IsType<ExpressionStatement>(program.Body[0]);
        var cond = Assert.IsType<ConditionalExpression>(stmt.Expression);
        Assert.NotNull(cond.Test);
        Assert.NotNull(cond.Consequent);
        Assert.NotNull(cond.Alternate);
    }

    [Fact]
    public void Parse_AssignmentExpression_Simple()
    {
        var program = Parse("x = 5;");
        var stmt = Assert.IsType<ExpressionStatement>(program.Body[0]);
        var assign = Assert.IsType<AssignmentExpression>(stmt.Expression);
        Assert.Equal("=", assign.Operator);
    }

    [Fact]
    public void Parse_CallExpression_NoArgs()
    {
        var program = Parse("foo();");
        var stmt = Assert.IsType<ExpressionStatement>(program.Body[0]);
        var call = Assert.IsType<CallExpression>(stmt.Expression);
        Assert.Empty(call.Arguments);
    }

    [Fact]
    public void Parse_CallExpression_WithArgs()
    {
        var program = Parse("foo(1, 2, 3);");
        var stmt = Assert.IsType<ExpressionStatement>(program.Body[0]);
        var call = Assert.IsType<CallExpression>(stmt.Expression);
        Assert.Equal(3, call.Arguments.Count);
    }

    [Fact]
    public void Parse_MemberExpression_DotAccess()
    {
        var program = Parse("obj.prop;");
        var stmt = Assert.IsType<ExpressionStatement>(program.Body[0]);
        var member = Assert.IsType<MemberExpression>(stmt.Expression);
        Assert.False(member.Computed);
    }

    [Fact]
    public void Parse_MemberExpression_BracketAccess()
    {
        var program = Parse("obj[0];");
        var stmt = Assert.IsType<ExpressionStatement>(program.Body[0]);
        var member = Assert.IsType<MemberExpression>(stmt.Expression);
        Assert.True(member.Computed);
    }

    [Fact]
    public void Parse_NewExpression_WithArgs()
    {
        var program = Parse("new Foo(1, 2);");
        var stmt = Assert.IsType<ExpressionStatement>(program.Body[0]);
        var newExpr = Assert.IsType<NewExpression>(stmt.Expression);
        Assert.Equal(2, newExpr.Arguments.Count);
    }

    [Fact]
    public void Parse_SpreadElement_InArray()
    {
        var program = Parse("[...arr];");
        var stmt = Assert.IsType<ExpressionStatement>(program.Body[0]);
        var arr = Assert.IsType<ArrayExpression>(stmt.Expression);
        Assert.IsType<SpreadElement>(arr.Elements[0]);
    }

    [Fact]
    public void Parse_UnaryExpression_Typeof()
    {
        var program = Parse("typeof x;");
        var stmt = Assert.IsType<ExpressionStatement>(program.Body[0]);
        var unary = Assert.IsType<UnaryExpression>(stmt.Expression);
        Assert.Equal("typeof", unary.Operator);
        Assert.True(unary.Prefix);
    }

    [Fact]
    public void Parse_UpdateExpression_PostfixIncrement()
    {
        var program = Parse("x++;");
        var stmt = Assert.IsType<ExpressionStatement>(program.Body[0]);
        var update = Assert.IsType<UpdateExpression>(stmt.Expression);
        Assert.Equal("++", update.Operator);
        Assert.False(update.Prefix);
    }

    [Fact]
    public void Parse_UpdateExpression_PrefixDecrement()
    {
        var program = Parse("--x;");
        var stmt = Assert.IsType<ExpressionStatement>(program.Body[0]);
        var update = Assert.IsType<UpdateExpression>(stmt.Expression);
        Assert.Equal("--", update.Operator);
        Assert.True(update.Prefix);
    }

    [Fact]
    public void Parse_ReturnStatement_WithValue()
    {
        var program = Parse("function f() { return 42; }");
        var func = Assert.IsType<FunctionDeclaration>(program.Body[0]);
        var ret = Assert.IsType<ReturnStatement>(func.Body.Body[0]);
        Assert.NotNull(ret.Argument);
    }

    [Fact]
    public void Parse_ThrowStatement_ParsesCorrectly()
    {
        var program = Parse("function f() { throw new Error('msg'); }");
        var func = Assert.IsType<FunctionDeclaration>(program.Body[0]);
        var throwStmt = Assert.IsType<ThrowStatement>(func.Body.Body[0]);
        Assert.NotNull(throwStmt.Argument);
    }

    [Fact]
    public void Parse_EmptyProgram_ReturnsEmptyBody()
    {
        var program = Parse("");
        Assert.Empty(program.Body);
    }

    [Fact]
    public void Parse_ChainExpression_OptionalChaining()
    {
        var program = Parse("a?.b;");
        var stmt = Assert.IsType<ExpressionStatement>(program.Body[0]);
        var chain = Assert.IsType<ChainExpression>(stmt.Expression);
        var member = Assert.IsType<MemberExpression>(chain.Expression);
        Assert.True(member.Optional);
    }
}
