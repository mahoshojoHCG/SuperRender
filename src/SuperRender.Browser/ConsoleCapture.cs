using System.Text;

namespace SuperRender.Browser;

/// <summary>
/// A TextWriter that captures output into a ConsoleLog at a given severity level.
/// Used to redirect console.log / console.warn / console.error into the DevTools console.
/// </summary>
public sealed class ConsoleCapture : TextWriter
{
    private readonly ConsoleLog _log;
    private readonly ConsoleMessageLevel _level;

    public override Encoding Encoding => Encoding.UTF8;

    public ConsoleCapture(ConsoleLog log, ConsoleMessageLevel level)
    {
        _log = log;
        _level = level;
    }

    public override void WriteLine(string? value)
    {
        _log.Add(new ConsoleMessage
        {
            Level = _level,
            Text = value ?? "",
        });
    }

    public override void Write(string? value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            _log.Add(new ConsoleMessage
            {
                Level = _level,
                Text = value,
            });
        }
    }
}
