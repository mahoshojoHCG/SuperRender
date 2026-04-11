namespace SuperRender.Browser;

/// <summary>
/// Tracks vertical scroll position for a viewport.
/// </summary>
public sealed class ScrollState
{
    public const float ScrollStep = 40f;
    private const float PageScrollFraction = 0.9f;
    private const float MinThumbHeight = 20f;
    private const float ScrollBarWidth = 8f;

    public float ScrollY { get; private set; }
    public float MaxScrollY { get; private set; }
    public float ViewportHeight { get; private set; }
    public float ContentHeight { get; private set; }
    public bool CanScroll => MaxScrollY > 0;

    /// <summary>
    /// Updates the scroll bounds after layout. Call each frame when layout is recomputed.
    /// </summary>
    public void Update(float contentHeight, float viewportHeight)
    {
        ContentHeight = contentHeight;
        ViewportHeight = viewportHeight;
        MaxScrollY = Math.Max(0, contentHeight - viewportHeight);
        ScrollY = Math.Clamp(ScrollY, 0, MaxScrollY);
    }

    public void ScrollBy(float delta)
    {
        ScrollY = Math.Clamp(ScrollY + delta, 0, MaxScrollY);
    }

    public void ScrollToTop()
    {
        ScrollY = 0;
    }

    public void ScrollToBottom()
    {
        ScrollY = MaxScrollY;
    }

    public void PageUp()
    {
        ScrollBy(-ViewportHeight * PageScrollFraction);
    }

    public void PageDown()
    {
        ScrollBy(ViewportHeight * PageScrollFraction);
    }

    /// <summary>
    /// Computes scrollbar geometry for rendering. Returns null if content fits in viewport.
    /// </summary>
    public (float trackY, float trackHeight, float thumbY, float thumbHeight)? GetScrollBarGeometry(float chromeHeight)
    {
        if (!CanScroll) return null;

        float trackY = chromeHeight;
        float trackHeight = ViewportHeight;
        float thumbHeight = Math.Max(MinThumbHeight, ViewportHeight / ContentHeight * trackHeight);
        float scrollFraction = MaxScrollY > 0 ? ScrollY / MaxScrollY : 0;
        float thumbY = trackY + scrollFraction * (trackHeight - thumbHeight);

        return (trackY, trackHeight, thumbY, thumbHeight);
    }

    public static float BarWidth => ScrollBarWidth;
}
