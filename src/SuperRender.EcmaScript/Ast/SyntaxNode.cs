namespace SuperRender.EcmaScript.Ast;

public sealed record SourceLocation(int Line, int Column);

public abstract class SyntaxNode
{
    public SourceLocation? Location { get; init; }
}

public sealed class Program : SyntaxNode
{
    public required IReadOnlyList<SyntaxNode> Body { get; init; }
    public bool IsModule { get; init; }
}
