namespace SuperRender.EcmaScript.Runtime;

public sealed class ExecutionContext
{
    public required Environment LexicalEnvironment { get; init; }
    public required Environment VariableEnvironment { get; init; }
    public required JsValue ThisBinding { get; init; }
    public bool IsStrict { get; init; } = true;

    /// <summary>Current JS source line, updated by compiled code at statement boundaries.</summary>
    [ThreadStatic]
#pragma warning disable CA2211 // Set by compiled JS code via RuntimeHelpers.SetLocation
    public static int CurrentLine;

    /// <summary>Current JS source column, updated by compiled code at statement boundaries.</summary>
    [ThreadStatic]
    public static int CurrentColumn;
#pragma warning restore CA2211
}
