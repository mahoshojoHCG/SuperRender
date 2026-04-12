namespace SuperRender.Browser;

/// <summary>
/// Severity level for a developer console message.
/// </summary>
public enum ConsoleMessageLevel
{
    Log,
    Info,
    Debug,
    Warn,
    Error,
}

/// <summary>
/// A single message displayed in the developer tools console.
/// </summary>
public sealed class ConsoleMessage
{
    public required ConsoleMessageLevel Level { get; init; }
    public required string Text { get; init; }
    public int Line { get; init; }
    public int Column { get; init; }
    public string? Source { get; init; }
}
