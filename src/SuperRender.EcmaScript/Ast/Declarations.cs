namespace SuperRender.EcmaScript.Ast;

public enum VariableKind
{
    Var,
    Let,
    Const
}

public sealed class VariableDeclaration : SyntaxNode
{
    public required VariableKind Kind { get; init; }
    public required IReadOnlyList<VariableDeclarator> Declarations { get; init; }
}

public sealed class VariableDeclarator : SyntaxNode
{
    public required SyntaxNode Id { get; init; }
    public SyntaxNode? Init { get; init; }
}

public sealed class FunctionDeclaration : SyntaxNode
{
    public Identifier? Id { get; init; }
    public required IReadOnlyList<SyntaxNode> Params { get; init; }
    public required BlockStatement Body { get; init; }
    public bool IsAsync { get; init; }
    public bool IsGenerator { get; init; }
}

public sealed class ClassDeclaration : SyntaxNode
{
    public Identifier? Id { get; init; }
    public SyntaxNode? SuperClass { get; init; }
    public required ClassBody Body { get; init; }
}

public sealed class ClassBody : SyntaxNode
{
    public required IReadOnlyList<SyntaxNode> Body { get; init; }
}

public enum MethodKind
{
    Constructor,
    Method,
    Get,
    Set
}

public sealed class MethodDefinition : SyntaxNode
{
    public required SyntaxNode Key { get; init; }
    public required FunctionExpression Value { get; init; }
    public MethodKind Kind { get; init; }
    public bool IsStatic { get; init; }
    public bool Computed { get; init; }
}

public sealed class PropertyDefinition : SyntaxNode
{
    public required SyntaxNode Key { get; init; }
    public SyntaxNode? Value { get; init; }
    public bool IsStatic { get; init; }
    public bool Computed { get; init; }
}

public sealed class ObjectPattern : SyntaxNode
{
    public required IReadOnlyList<SyntaxNode> Properties { get; init; }
}

public sealed class ArrayPattern : SyntaxNode
{
    public required IReadOnlyList<SyntaxNode?> Elements { get; init; }
}

public sealed class AssignmentPattern : SyntaxNode
{
    public required SyntaxNode Left { get; init; }
    public required SyntaxNode Right { get; init; }
}

public sealed class RestElement : SyntaxNode
{
    public required SyntaxNode Argument { get; init; }
}

public sealed class ImportDeclaration : SyntaxNode
{
    public required IReadOnlyList<SyntaxNode> Specifiers { get; init; }
    public required Literal Source { get; init; }
}

public sealed class ImportSpecifier : SyntaxNode
{
    public required Identifier Local { get; init; }
    public required Identifier Imported { get; init; }
}

public sealed class ImportDefaultSpecifier : SyntaxNode
{
    public required Identifier Local { get; init; }
}

public sealed class ImportNamespaceSpecifier : SyntaxNode
{
    public required Identifier Local { get; init; }
}

public sealed class ExportNamedDeclaration : SyntaxNode
{
    public SyntaxNode? Declaration { get; init; }
    public required IReadOnlyList<ExportSpecifier> Specifiers { get; init; }
    public Literal? Source { get; init; }
}

public sealed class ExportDefaultDeclaration : SyntaxNode
{
    public required SyntaxNode Declaration { get; init; }
}

public sealed class ExportAllDeclaration : SyntaxNode
{
    public required Literal Source { get; init; }
    public Identifier? Exported { get; init; }
}

public sealed class ExportSpecifier : SyntaxNode
{
    public required Identifier Local { get; init; }
    public required Identifier Exported { get; init; }
}
