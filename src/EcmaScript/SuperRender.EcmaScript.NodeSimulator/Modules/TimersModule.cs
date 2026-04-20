using System.Diagnostics;
using SuperRender.EcmaScript.Runtime;

namespace SuperRender.EcmaScript.NodeSimulator.Modules;

/// <summary>
/// Central queue for setTimeout / setInterval / setImmediate callbacks.
/// The host must call <see cref="Poll"/> to fire due callbacks, and
/// <see cref="Advance"/> in tests to simulate the passage of time.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "Queue is the correct semantic suffix for a FIFO timer queue")]
public sealed class TimerQueue
{
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly List<TimerEntry> _entries = [];
    private readonly LinkedList<ImmediateEntry> _immediates = new();
    private long _nextId = 1;
    private long _virtualOffset;

    public int PendingTimers => _entries.Count;
    public int PendingImmediates => _immediates.Count;

    /// <summary>Current logical time in milliseconds (real + virtual advance).</summary>
    public long NowMs => (long)_clock.Elapsed.TotalMilliseconds + _virtualOffset;

    public long SetTimeout(JsFunction fn, JsValue[] boundArgs, double delayMs, bool repeat)
    {
        var id = _nextId++;
        _entries.Add(new TimerEntry
        {
            Id = id,
            Callback = fn,
            Args = boundArgs,
            DueMs = NowMs + Math.Max(1, (long)delayMs),
            PeriodMs = repeat ? Math.Max(1, (long)delayMs) : 0,
            Active = true,
        });
        return id;
    }

    public void ClearTimer(long id)
    {
        for (int i = 0; i < _entries.Count; i++)
        {
            if (_entries[i].Id == id)
            {
                _entries.RemoveAt(i);
                return;
            }
        }
    }

    public long SetImmediate(JsFunction fn)
    {
        var id = _nextId++;
        _immediates.AddLast(new ImmediateEntry { Id = id, Callback = fn });
        return id;
    }

    public void ClearImmediate(long id)
    {
        for (var node = _immediates.First; node is not null; node = node.Next)
        {
            if (node.Value.Id == id)
            {
                _immediates.Remove(node);
                return;
            }
        }
    }

    /// <summary>Advance virtual time by the given milliseconds (for tests).</summary>
    public void Advance(long ms) => _virtualOffset += ms;

    /// <summary>
    /// Fire all due callbacks. Returns the number of callbacks fired.
    /// Immediates fire first, then all timers whose due time has passed.
    /// </summary>
    public int Poll()
    {
        int fired = 0;
        while (_immediates.Count > 0)
        {
            var entry = _immediates.First!.Value;
            _immediates.RemoveFirst();
            entry.Callback.Call(JsValue.Undefined, []);
            fired++;
        }

        // Fire in order of due time
        while (true)
        {
            TimerEntry? due = null;
            foreach (var e in _entries)
            {
                if (e.Active && e.DueMs <= NowMs)
                {
                    if (due is null || e.DueMs < due.DueMs) due = e;
                }
            }
            if (due is null) break;
            if (due.PeriodMs > 0)
            {
                due.DueMs += due.PeriodMs;
            }
            else
            {
                _entries.Remove(due);
            }
            due.Callback.Call(JsValue.Undefined, due.Args);
            fired++;
        }

        return fired;
    }

    private sealed class TimerEntry
    {
        public long Id;
        public JsFunction Callback = null!;
        public JsValue[] Args = [];
        public long DueMs;
        public long PeriodMs;
        public bool Active;
    }

    private sealed class ImmediateEntry
    {
        public long Id;
        public JsFunction Callback = null!;
    }
}

/// <summary>
/// Installs setTimeout/setInterval/setImmediate + their clear counterparts as globals.
/// Backed by a <see cref="TimerQueue"/> that the host polls each loop iteration.
/// </summary>
public static class TimersModule
{
    public static TimerQueue Install(Realm realm)
    {
        var queue = new TimerQueue();

        realm.InstallGlobal("setTimeout", JsFunction.CreateNative("setTimeout", (_, args) =>
        {
            if (args.Length == 0 || args[0] is not JsFunction fn)
                throw new Runtime.Errors.JsTypeError("callback must be a function");
            var delay = args.Length > 1 && args[1] is not JsUndefined ? args[1].ToNumber() : 1;
            var rest = args.Length > 2 ? args[2..] : [];
            var id = queue.SetTimeout(fn, rest, delay, repeat: false);
            return MakeTimeout(id, queue);
        }, 2));

        realm.InstallGlobal("setInterval", JsFunction.CreateNative("setInterval", (_, args) =>
        {
            if (args.Length == 0 || args[0] is not JsFunction fn)
                throw new Runtime.Errors.JsTypeError("callback must be a function");
            var delay = args.Length > 1 && args[1] is not JsUndefined ? args[1].ToNumber() : 1;
            var rest = args.Length > 2 ? args[2..] : [];
            var id = queue.SetTimeout(fn, rest, delay, repeat: true);
            return MakeTimeout(id, queue);
        }, 2));

        realm.InstallGlobal("setImmediate", JsFunction.CreateNative("setImmediate", (_, args) =>
        {
            if (args.Length == 0 || args[0] is not JsFunction fn)
                throw new Runtime.Errors.JsTypeError("callback must be a function");
            var id = queue.SetImmediate(fn);
            var obj = new JsObject();
            obj.DefineOwnProperty("_id", PropertyDescriptor.Data(JsNumber.Create(id)));
            obj.DefineOwnProperty("_immediate", PropertyDescriptor.Data(JsValue.True));
            obj.DefineOwnProperty("ref", PropertyDescriptor.Data(JsFunction.CreateNative("ref", (t, _) => t, 0), writable: true, enumerable: false, configurable: true));
            obj.DefineOwnProperty("unref", PropertyDescriptor.Data(JsFunction.CreateNative("unref", (t, _) => t, 0), writable: true, enumerable: false, configurable: true));
            return obj;
        }, 1));

        realm.InstallGlobal("clearTimeout", JsFunction.CreateNative("clearTimeout", (_, args) =>
        {
            var id = ExtractId(args.Length > 0 ? args[0] : JsValue.Undefined);
            if (id.HasValue) queue.ClearTimer(id.Value);
            return JsValue.Undefined;
        }, 1));

        realm.InstallGlobal("clearInterval", JsFunction.CreateNative("clearInterval", (_, args) =>
        {
            var id = ExtractId(args.Length > 0 ? args[0] : JsValue.Undefined);
            if (id.HasValue) queue.ClearTimer(id.Value);
            return JsValue.Undefined;
        }, 1));

        realm.InstallGlobal("clearImmediate", JsFunction.CreateNative("clearImmediate", (_, args) =>
        {
            var id = ExtractId(args.Length > 0 ? args[0] : JsValue.Undefined);
            if (id.HasValue) queue.ClearImmediate(id.Value);
            return JsValue.Undefined;
        }, 1));

        realm.InstallGlobal("queueMicrotask", JsFunction.CreateNative("queueMicrotask", (_, args) =>
        {
            if (args.Length == 0 || args[0] is not JsFunction fn)
                throw new Runtime.Errors.JsTypeError("callback must be a function");
            queue.SetImmediate(fn);
            return JsValue.Undefined;
        }, 1));

        return queue;
    }

    private static JsObject MakeTimeout(long id, TimerQueue queue)
    {
        var obj = new JsObject();
        obj.DefineOwnProperty("_id", PropertyDescriptor.Data(JsNumber.Create(id)));
        obj.DefineOwnProperty("ref", PropertyDescriptor.Data(JsFunction.CreateNative("ref", (t, _) => t, 0), writable: true, enumerable: false, configurable: true));
        obj.DefineOwnProperty("unref", PropertyDescriptor.Data(JsFunction.CreateNative("unref", (t, _) => t, 0), writable: true, enumerable: false, configurable: true));
        obj.DefineOwnProperty("hasRef", PropertyDescriptor.Data(JsFunction.CreateNative("hasRef", (_, _) => JsValue.True, 0), writable: true, enumerable: false, configurable: true));
        obj.DefineOwnProperty("refresh", PropertyDescriptor.Data(JsFunction.CreateNative("refresh", (t, _) => t, 0), writable: true, enumerable: false, configurable: true));
        obj.DefineOwnProperty("close", PropertyDescriptor.Data(JsFunction.CreateNative("close", (_, _) => { queue.ClearTimer(id); return JsValue.Undefined; }, 0), writable: true, enumerable: false, configurable: true));
        return obj;
    }

    private static long? ExtractId(JsValue v)
    {
        if (v is JsNumber n) return (long)n.Value;
        if (v is JsObject o && o.Get("_id") is JsNumber on) return (long)on.Value;
        return null;
    }
}
