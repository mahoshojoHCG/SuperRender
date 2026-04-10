namespace SuperRender.EcmaScript.Ast;

public sealed class Identifier : SyntaxNode
{
    public required string Name { get; init; }
}

public sealed class Literal : SyntaxNode
{
    public object? Value { get; init; }
    public required string Raw { get; init; }
}

public sealed class ThisExpression : SyntaxNode;

public sealed class BinaryExpression : SyntaxNode
{
    public required SyntaxNode Left { get; init; }
    public required string Operator { get; init; }
    public required SyntaxNode Right { get; init; }
}

public sealed class LogicalExpression : SyntaxNode
{
    public required SyntaxNode Left { get; init; }
    public required string Operator { get; init; }
    public required SyntaxNode Right { get; init; }
}

public sealed class UnaryExpression : SyntaxNode
{
    public required string Operator { get; init; }
    public required SyntaxNode Argument { get; init; }
    public bool Prefix { get; init; }
}

public sealed class UpdateExpression : SyntaxNode
{
    public required string Operator { get; init; }
    public required SyntaxNode Argument { get; init; }
    public bool Prefix { get; init; }
}

public sealed class AssignmentExpression : SyntaxNode
{
    public required SyntaxNode Left { get; init; }
    public required string Operator { get; init; }
    public required SyntaxNode Right { get; init; }
}

public sealed class ConditionalExpression : SyntaxNode
{
    public required SyntaxNode Test { get; init; }
    public required SyntaxNode Consequent { get; init; }
    public required SyntaxNode Alternate { get; init; }
}

public sealed class CallExpression : SyntaxNode
{
    public required SyntaxNode Callee { get; init; }
    public required IReadOnlyList<SyntaxNode> Arguments { get; init; }
}

public sealed class NewExpression : SyntaxNode
{
    public required SyntaxNode Callee { get; init; }
    public required IReadOnlyList<SyntaxNode> Arguments { get; init; }
}

public sealed class MemberExpression : SyntaxNode
{
    public required SyntaxNode Object { get; init; }
    public required SyntaxNode Property { get; init; }
    public bool Computed { get; init; }
    public bool Optional { get; init; }
}

public sealed class ArrowFunctionExpression : SyntaxNode
{
    public required IReadOnlyList<SyntaxNode> Params { get; init; }
    public required SyntaxNode Body { get; init; }
    public bool IsAsync { get; init; }
    public bool IsExpression { get; init; }
}

public sealed class FunctionExpression : SyntaxNode
{
    public Identifier? Id { get; init; }
    public required IReadOnlyList<SyntaxNode> Params { get; init; }
    public required BlockStatement Body { get; init; }
    public bool IsAsync { get; init; }
    public bool IsGenerator { get; init; }
}

public sealed class ObjectExpression : SyntaxNode
{
    public required IReadOnlyList<SyntaxNode> Properties { get; init; }
}

public enum PropertyKind
{
    Init,
    Get,
    Set
}

public sealed class Property : SyntaxNode
{
    public required SyntaxNode Key { get; init; }
    public required SyntaxNode Value { get; init; }
    public PropertyKind Kind { get; init; }
    public bool Computed { get; init; }
    public bool Shorthand { get; init; }
    public bool IsMethod { get; init; }
}

public sealed class ArrayExpression : SyntaxNode
{
    public required IReadOnlyList<SyntaxNode?> Elements { get; init; }
}

public sealed class SpreadElement : SyntaxNode
{
    public required SyntaxNode Argument { get; init; }
}

public sealed class TemplateLiteral : SyntaxNode
{
    public required IReadOnlyList<TemplateElement> Quasis { get; init; }
    public required IReadOnlyList<SyntaxNode> Expressions { get; init; }
}

public sealed class TemplateElement : SyntaxNode
{
    public required string Value { get; init; }
    public required string Raw { get; init; }
    public bool Tail { get; init; }
}

public sealed class TaggedTemplateExpression : SyntaxNode
{
    public required SyntaxNode Tag { get; init; }
    public required TemplateLiteral Quasi { get; init; }
}

public sealed class SequenceExpression : SyntaxNode
{
    public required IReadOnlyList<SyntaxNode> Expressions { get; init; }
}

public sealed class YieldExpression : SyntaxNode
{
    public SyntaxNode? Argument { get; init; }
    public bool Delegate { get; init; }
}

public sealed class AwaitExpression : SyntaxNode
{
    public required SyntaxNode Argument { get; init; }
}

public sealed class ClassExpression : SyntaxNode
{
    public Identifier? Id { get; init; }
    public SyntaxNode? SuperClass { get; init; }
    public required ClassBody Body { get; init; }
}

public sealed class ChainExpression : SyntaxNode
{
    public required SyntaxNode Expression { get; init; }
}
