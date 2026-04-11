using System.Diagnostics;

namespace SuperRender.EcmaScript.Dom;

/// <summary>
/// A scheduled timer entry (setTimeout, setInterval, or requestAnimationFrame).
/// </summary>
internal sealed class TimerEntry
{
    public int Id { get; init; }
    public double FireAtMs { get; set; }
    public required Action Callback { get; init; }
    public double IntervalMs { get; init; }
    public bool IsRaf { get; init; }
    public bool Cancelled { get; set; }
}

/// <summary>
/// Manages scheduled timers (setTimeout, setInterval, requestAnimationFrame).
/// Drained once per frame from the browser render loop.
/// </summary>
public sealed class TimerScheduler
{
    private readonly List<TimerEntry> _timers = [];
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private int _nextId = 1;

    /// <summary>
    /// Current monotonic time in milliseconds.
    /// </summary>
    public double NowMs => _clock.Elapsed.TotalMilliseconds;

    public int SetTimeout(Action callback, double delayMs)
    {
        var id = _nextId++;
        _timers.Add(new TimerEntry
        {
            Id = id,
            FireAtMs = NowMs + Math.Max(delayMs, 0),
            Callback = callback,
            IntervalMs = 0,
        });
        return id;
    }

    public int SetInterval(Action callback, double intervalMs)
    {
        var id = _nextId++;
        intervalMs = Math.Max(intervalMs, 4); // minimum 4ms per spec
        _timers.Add(new TimerEntry
        {
            Id = id,
            FireAtMs = NowMs + intervalMs,
            Callback = callback,
            IntervalMs = intervalMs,
        });
        return id;
    }

    public int RequestAnimationFrame(Action callback)
    {
        var id = _nextId++;
        _timers.Add(new TimerEntry
        {
            Id = id,
            FireAtMs = 0, // fire next DrainReady call
            Callback = callback,
            IsRaf = true,
        });
        return id;
    }

    public void Cancel(int id)
    {
        for (int i = 0; i < _timers.Count; i++)
        {
            if (_timers[i].Id == id)
            {
                _timers[i].Cancelled = true;
                break;
            }
        }
    }

    /// <summary>
    /// Fires all ready timers. Call once per frame from the render loop.
    /// </summary>
    public void DrainReady()
    {
        if (_timers.Count == 0) return;

        double now = NowMs;
        // Snapshot to allow modification during callbacks
        var count = _timers.Count;
        for (int i = 0; i < count; i++)
        {
            var timer = _timers[i];
            if (timer.Cancelled) continue;
            if (!timer.IsRaf && timer.FireAtMs > now) continue;

            try { timer.Callback(); }
            catch (Exception ex) { Console.WriteLine($"[Timer] Error in timer {timer.Id}: {ex.Message}"); }

            if (timer.IntervalMs > 0 && !timer.Cancelled)
            {
                // Reschedule interval timer
                timer.FireAtMs = now + timer.IntervalMs;
            }
            else
            {
                timer.Cancelled = true;
            }
        }

        _timers.RemoveAll(t => t.Cancelled);
    }
}
