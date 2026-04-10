namespace SuperRender.EcmaScript.Console;

/// <summary>
/// Minimal readline-style line editor with arrow key navigation and command history.
/// </summary>
internal sealed class LineEditor
{
    private readonly List<string> _history = [];
    private int _historyIndex;
    private string _savedInput = "";

    public IReadOnlyList<string> History => _history;

    /// <summary>
    /// Add an entry to the history. Skips duplicates of the most recent entry and blank lines.
    /// </summary>
    public void AddHistory(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return;
        }

        if (_history.Count > 0 && _history[^1] == line)
        {
            return;
        }

        _history.Add(line);
    }

    /// <summary>
    /// Read a line of input with full editing support.
    /// Returns null on EOF (Ctrl+D on empty line).
    /// </summary>
    public string? ReadLine(string prompt)
    {
        // Fall back to basic ReadLine when stdin is redirected (piped input).
        if (System.Console.IsInputRedirected)
        {
            System.Console.Write(prompt);
            return System.Console.ReadLine();
        }

        System.Console.Write(prompt);

        var buffer = new List<char>();
        int cursor = 0;
        _historyIndex = _history.Count;
        _savedInput = "";

        while (true)
        {
            var key = System.Console.ReadKey(intercept: true);

            // Ctrl modifier shortcuts.
            if (key.Modifiers.HasFlag(ConsoleModifiers.Control))
            {
                switch (key.Key)
                {
                    case ConsoleKey.A: // Home
                        MoveCursor(ref cursor, 0, buffer.Count, prompt.Length);
                        continue;

                    case ConsoleKey.E: // End
                        MoveCursor(ref cursor, buffer.Count, buffer.Count, prompt.Length);
                        continue;

                    case ConsoleKey.D: // EOF
                        if (buffer.Count == 0)
                        {
                            return null;
                        }
                        continue;

                    case ConsoleKey.C: // Cancel
                        System.Console.WriteLine();
                        return "";

                    case ConsoleKey.U: // Clear line
                        ClearDisplayedLine(prompt.Length, buffer.Count);
                        buffer.Clear();
                        cursor = 0;
                        RedrawLine(prompt, buffer, cursor);
                        continue;

                    case ConsoleKey.K: // Kill to end of line
                        EraseToEnd(cursor, buffer.Count, prompt.Length);
                        buffer.RemoveRange(cursor, buffer.Count - cursor);
                        continue;

                    case ConsoleKey.W: // Delete word before cursor
                        if (cursor > 0)
                        {
                            int wordStart = FindWordBoundaryLeft(buffer, cursor);
                            int count = cursor - wordStart;
                            buffer.RemoveRange(wordStart, count);
                            cursor = wordStart;
                            RedrawLine(prompt, buffer, cursor);
                        }
                        continue;

                    case ConsoleKey.LeftArrow: // Word left
                        MoveCursor(ref cursor, FindWordBoundaryLeft(buffer, cursor), buffer.Count, prompt.Length);
                        continue;

                    case ConsoleKey.RightArrow: // Word right
                        MoveCursor(ref cursor, FindWordBoundaryRight(buffer, cursor), buffer.Count, prompt.Length);
                        continue;
                }
            }

            switch (key.Key)
            {
                case ConsoleKey.Enter:
                    System.Console.WriteLine();
                    return new string(buffer.ToArray());

                case ConsoleKey.Backspace:
                    if (cursor > 0)
                    {
                        buffer.RemoveAt(cursor - 1);
                        cursor--;
                        RedrawLine(prompt, buffer, cursor);
                    }
                    continue;

                case ConsoleKey.Delete:
                    if (cursor < buffer.Count)
                    {
                        buffer.RemoveAt(cursor);
                        RedrawLine(prompt, buffer, cursor);
                    }
                    continue;

                case ConsoleKey.LeftArrow:
                    if (cursor > 0)
                    {
                        MoveCursor(ref cursor, cursor - 1, buffer.Count, prompt.Length);
                    }
                    continue;

                case ConsoleKey.RightArrow:
                    if (cursor < buffer.Count)
                    {
                        MoveCursor(ref cursor, cursor + 1, buffer.Count, prompt.Length);
                    }
                    continue;

                case ConsoleKey.Home:
                    MoveCursor(ref cursor, 0, buffer.Count, prompt.Length);
                    continue;

                case ConsoleKey.End:
                    MoveCursor(ref cursor, buffer.Count, buffer.Count, prompt.Length);
                    continue;

                case ConsoleKey.UpArrow:
                    NavigateHistory(-1, buffer, ref cursor, prompt);
                    continue;

                case ConsoleKey.DownArrow:
                    NavigateHistory(1, buffer, ref cursor, prompt);
                    continue;

                case ConsoleKey.Tab:
                    // No-op for now.
                    continue;

                case ConsoleKey.Escape:
                    // Clear line like many terminals.
                    ClearDisplayedLine(prompt.Length, buffer.Count);
                    buffer.Clear();
                    cursor = 0;
                    RedrawLine(prompt, buffer, cursor);
                    continue;

                default:
                    if (key.KeyChar >= ' ')
                    {
                        buffer.Insert(cursor, key.KeyChar);
                        cursor++;
                        RedrawLine(prompt, buffer, cursor);
                    }
                    continue;
            }
        }
    }

    // ═══════════════════════════════════════════
    //  Display helpers
    // ═══════════════════════════════════════════

    private static void RedrawLine(string prompt, List<char> buffer, int cursor)
    {
        // Move to start of line, rewrite prompt + buffer, clear trailing chars.
        System.Console.CursorLeft = 0;
        System.Console.Write(prompt);
        System.Console.Write(buffer.ToArray());
        // Clear any leftover characters from a previous longer line.
        int totalLen = prompt.Length + buffer.Count;
        int consoleWidth = System.Console.BufferWidth;
        int clearCount = consoleWidth - (totalLen % consoleWidth);
        if (clearCount < consoleWidth)
        {
            System.Console.Write(new string(' ', clearCount));
        }

        System.Console.CursorLeft = prompt.Length + cursor;
    }

    private static void MoveCursor(ref int cursor, int newPos, int bufferLen, int promptLen)
    {
        cursor = Math.Clamp(newPos, 0, bufferLen);
        System.Console.CursorLeft = promptLen + cursor;
    }

    private static void ClearDisplayedLine(int promptLen, int bufferLen)
    {
        System.Console.CursorLeft = 0;
        System.Console.Write(new string(' ', promptLen + bufferLen));
        System.Console.CursorLeft = 0;
    }

    private static void EraseToEnd(int cursor, int bufferLen, int promptLen)
    {
        int eraseCount = bufferLen - cursor;
        if (eraseCount > 0)
        {
            int pos = promptLen + cursor;
            System.Console.CursorLeft = pos;
            System.Console.Write(new string(' ', eraseCount));
            System.Console.CursorLeft = pos;
        }
    }

    // ═══════════════════════════════════════════
    //  History navigation
    // ═══════════════════════════════════════════

    private void NavigateHistory(int direction, List<char> buffer, ref int cursor, string prompt)
    {
        if (_history.Count == 0)
        {
            return;
        }

        int newIndex = _historyIndex + direction;

        // Clamp to valid range: [0 .. _history.Count] where Count means "current unsaved input".
        if (newIndex < 0 || newIndex > _history.Count)
        {
            return;
        }

        // Save current input when leaving the live editing position.
        if (_historyIndex == _history.Count)
        {
            _savedInput = new string(buffer.ToArray());
        }

        _historyIndex = newIndex;

        string replacement = _historyIndex < _history.Count
            ? _history[_historyIndex]
            : _savedInput;

        ClearDisplayedLine(prompt.Length, buffer.Count);
        buffer.Clear();
        buffer.AddRange(replacement);
        cursor = buffer.Count;
        RedrawLine(prompt, buffer, cursor);
    }

    // ═══════════════════════════════════════════
    //  Word boundary helpers
    // ═══════════════════════════════════════════

    private static int FindWordBoundaryLeft(List<char> buffer, int cursor)
    {
        if (cursor <= 0)
        {
            return 0;
        }

        int pos = cursor - 1;

        // Skip whitespace.
        while (pos > 0 && char.IsWhiteSpace(buffer[pos]))
        {
            pos--;
        }

        // Skip word chars.
        while (pos > 0 && !char.IsWhiteSpace(buffer[pos - 1]))
        {
            pos--;
        }

        return pos;
    }

    private static int FindWordBoundaryRight(List<char> buffer, int cursor)
    {
        int len = buffer.Count;
        if (cursor >= len)
        {
            return len;
        }

        int pos = cursor;

        // Skip current word chars.
        while (pos < len && !char.IsWhiteSpace(buffer[pos]))
        {
            pos++;
        }

        // Skip whitespace.
        while (pos < len && char.IsWhiteSpace(buffer[pos]))
        {
            pos++;
        }

        return pos;
    }
}
