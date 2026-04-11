namespace SuperRender.Browser;

/// <summary>
/// Thread-safe buffer of developer console messages for a single tab.
/// </summary>
public sealed class ConsoleLog
{
    private const int MaxMessages = 1000;
    private readonly object _lock = new();
    private readonly List<ConsoleMessage> _messages = new();

    public void Add(ConsoleMessage message)
    {
        lock (_lock)
        {
            _messages.Add(message);
            if (_messages.Count > MaxMessages)
                _messages.RemoveAt(0);
        }
    }

    public List<ConsoleMessage> GetSnapshot()
    {
        lock (_lock)
        {
            return new List<ConsoleMessage>(_messages);
        }
    }

    public int Count
    {
        get { lock (_lock) { return _messages.Count; } }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _messages.Clear();
        }
    }
}
