namespace SuperRender.EcmaScript.Runtime;

public sealed class ExecutionContext
{
    public required Environment LexicalEnvironment { get; init; }
    public required Environment VariableEnvironment { get; init; }
    public required JsValue ThisBinding { get; init; }
    public bool IsStrict { get; init; } = true;
}
