namespace SuperRender.Renderer.Rendering.Painting;

/// <summary>
/// Represents a position within laid-out text: which run and character offset.
/// </summary>
public readonly record struct TextPosition(int RunIndex, int CharOffset) : IComparable<TextPosition>
{
    public int CompareTo(TextPosition other)
    {
        int cmp = RunIndex.CompareTo(other.RunIndex);
        return cmp != 0 ? cmp : CharOffset.CompareTo(other.CharOffset);
    }

    public static bool operator <(TextPosition left, TextPosition right) => left.CompareTo(right) < 0;
    public static bool operator >(TextPosition left, TextPosition right) => left.CompareTo(right) > 0;
    public static bool operator <=(TextPosition left, TextPosition right) => left.CompareTo(right) <= 0;
    public static bool operator >=(TextPosition left, TextPosition right) => left.CompareTo(right) >= 0;
}

/// <summary>
/// Tracks a text selection range across laid-out text runs.
/// </summary>
public sealed class TextSelectionState
{
    public TextPosition? Start { get; set; }
    public TextPosition? End { get; set; }

    public bool IsActive => Start.HasValue && End.HasValue;

    public bool HasSelection => IsActive && !Start!.Value.Equals(End!.Value);

    public void Clear()
    {
        Start = null;
        End = null;
    }

    /// <summary>
    /// Returns the normalized (ordered) start and end so start &lt;= end.
    /// </summary>
    public (TextPosition start, TextPosition end) GetOrdered()
    {
        if (!IsActive) return (default, default);
        var s = Start!.Value;
        var e = End!.Value;
        return s <= e ? (s, e) : (e, s);
    }
}
