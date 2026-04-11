namespace SuperRender.Browser;

/// <summary>
/// Per-tab navigation history with back/forward cursor.
/// </summary>
public sealed class NavigationHistory
{
    private readonly List<Uri> _entries = [];
    private int _currentIndex = -1;

    public Uri? CurrentEntry => _currentIndex >= 0 && _currentIndex < _entries.Count
        ? _entries[_currentIndex] : null;

    public bool CanGoBack => _currentIndex > 0;
    public bool CanGoForward => _currentIndex < _entries.Count - 1;

    /// <summary>
    /// Number of entries in the history stack.
    /// </summary>
    public int Length => _entries.Count;

    /// <summary>
    /// Push a new navigation entry. Truncates any forward history.
    /// </summary>
    public void Push(Uri uri)
    {
        if (_currentIndex < _entries.Count - 1)
            _entries.RemoveRange(_currentIndex + 1, _entries.Count - _currentIndex - 1);
        _entries.Add(uri);
        _currentIndex = _entries.Count - 1;
    }

    /// <summary>
    /// Replace the current entry without pushing.
    /// </summary>
    public void ReplaceCurrent(Uri uri)
    {
        if (_currentIndex >= 0 && _currentIndex < _entries.Count)
        {
            _entries[_currentIndex] = uri;
        }
        else
        {
            Push(uri);
        }
    }

    public Uri? GoBack()
    {
        if (!CanGoBack) return null;
        _currentIndex--;
        return _entries[_currentIndex];
    }

    public Uri? GoForward()
    {
        if (!CanGoForward) return null;
        _currentIndex++;
        return _entries[_currentIndex];
    }
}
