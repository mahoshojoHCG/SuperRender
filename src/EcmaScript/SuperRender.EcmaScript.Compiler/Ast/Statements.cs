namespace SuperRender.EcmaScript.Compiler.Ast;

public sealed class BlockStatement : SyntaxNode
{
    public required IReadOnlyList<SyntaxNode> Body { get; init; }
}

public sealed class ExpressionStatement : SyntaxNode
{
    public required SyntaxNode Expression { get; init; }
}

public sealed class EmptyStatement : SyntaxNode;

public sealed class IfStatement : SyntaxNode
{
    public required SyntaxNode Test { get; init; }
    public required SyntaxNode Consequent { get; init; }
    public SyntaxNode? Alternate { get; init; }
}

public sealed class ForStatement : SyntaxNode
{
    public SyntaxNode? Init { get; init; }
    public SyntaxNode? Test { get; init; }
    public SyntaxNode? Update { get; init; }
    public required SyntaxNode Body { get; init; }
}

public sealed class ForInStatement : SyntaxNode
{
    public required SyntaxNode Left { get; init; }
    public required SyntaxNode Right { get; init; }
    public required SyntaxNode Body { get; init; }
}

public sealed class ForOfStatement : SyntaxNode
{
    public required SyntaxNode Left { get; init; }
    public required SyntaxNode Right { get; init; }
    public required SyntaxNode Body { get; init; }
    public bool IsAwait { get; init; }
}

public sealed class WhileStatement : SyntaxNode
{
    public required SyntaxNode Test { get; init; }
    public required SyntaxNode Body { get; init; }
}

public sealed class DoWhileStatement : SyntaxNode
{
    public required SyntaxNode Test { get; init; }
    public required SyntaxNode Body { get; init; }
}

public sealed class SwitchStatement : SyntaxNode
{
    public required SyntaxNode Discriminant { get; init; }
    public required IReadOnlyList<SwitchCase> Cases { get; init; }
}

public sealed class SwitchCase : SyntaxNode
{
    public SyntaxNode? Test { get; init; }
    public required IReadOnlyList<SyntaxNode> Consequent { get; init; }
}

public sealed class TryStatement : SyntaxNode
{
    public required BlockStatement Block { get; init; }
    public CatchClause? Handler { get; init; }
    public BlockStatement? Finalizer { get; init; }
}

public sealed class CatchClause : SyntaxNode
{
    public SyntaxNode? Param { get; init; }
    public required BlockStatement Body { get; init; }
}

public sealed class ReturnStatement : SyntaxNode
{
    public SyntaxNode? Argument { get; init; }
}

public sealed class ThrowStatement : SyntaxNode
{
    public required SyntaxNode Argument { get; init; }
}

public sealed class BreakStatement : SyntaxNode
{
    public string? Label { get; init; }
}

public sealed class ContinueStatement : SyntaxNode
{
    public string? Label { get; init; }
}

public sealed class LabeledStatement : SyntaxNode
{
    public required string Label { get; init; }
    public required SyntaxNode Body { get; init; }
}
