using System.Diagnostics;
using SuperRender.EcmaScript.Runtime;

namespace SuperRender.EcmaScript.NodeSimulator.Modules;

/// <summary>
/// Node.js `process` global. Exposes argv, env, platform/arch, versions, pid, cwd/chdir,
/// exit/exitCode, stdout/stderr write, hrtime, nextTick, and basic event-emitter-style on().
/// </summary>
public sealed class ProcessObject : JsDynamicObject
{
    private readonly Stopwatch _startTime = Stopwatch.StartNew();
    private readonly Queue<JsFunction> _nextTickQueue = new();
    private readonly Dictionary<string, List<JsFunction>> _listeners = new(StringComparer.Ordinal);
    private int _exitCode;

    public IReadOnlyCollection<JsFunction> PendingNextTicks => _nextTickQueue;
    public int ExitCode => _exitCode;
    public IReadOnlyDictionary<string, List<JsFunction>> Listeners => _listeners;

    public TextWriter StdOut { get; set; } = Console.Out;
    public TextWriter StdErr { get; set; } = Console.Error;

    public ProcessObject(string[] argv) : this(argv, null) { }

    public ProcessObject(string[] argv, Realm? realm)
    {
        Prototype = realm?.ObjectPrototype;

        var argvArr = new JsArray { Prototype = realm?.ArrayPrototype };
        foreach (var s in argv) argvArr.Push(new JsString(s));
        DefineData("argv", argvArr);
        DefineData("argv0", argv.Length > 0 ? new JsString(argv[0]) : new JsString("node"));
        DefineData("execPath", new JsString(System.Environment.ProcessPath ?? "node"));
        DefineData("platform", new JsString(OsModule.GetPlatform()));
        DefineData("arch", new JsString(OsModule.GetArch()));
        DefineData("pid", JsNumber.Create(System.Environment.ProcessId));
        DefineData("ppid", JsNumber.Create(0));
        DefineData("version", new JsString("v25.6.0"));
        DefineData("title", new JsString("node"));

        var versions = new JsDynamicObject();
        versions.DefineOwnProperty("node", PropertyDescriptor.Data(new JsString("25.6.0")));
        versions.DefineOwnProperty("ecmascript", PropertyDescriptor.Data(new JsString("2025")));
        DefineData("versions", versions);

        DefineData("release", MakeRelease());
        DefineData("env", BuildEnv());
        DefineData("stdout", MakeWriteStream("stdout", () => StdOut));
        DefineData("stderr", MakeWriteStream("stderr", () => StdErr));
        DefineData("stdin", MakeReadStream());

        DefineOwnProperty("exitCode", PropertyDescriptor.Accessor(
            JsFunction.CreateNative("get exitCode", (_, _) => JsNumber.Create(_exitCode), 0),
            JsFunction.CreateNative("set exitCode", (_, args) =>
            {
                _exitCode = args.Length > 0 ? (int)args[0].ToNumber() : 0;
                return Undefined;
            }, 1),
            enumerable: true, configurable: true));

        DefineMethod("cwd", 0, (_, _) => new JsString(System.Environment.CurrentDirectory));
        DefineMethod("chdir", 1, (_, args) =>
        {
            if (args.Length == 0 || args[0] is not JsString dir)
                throw new Runtime.Errors.JsTypeError("The \"directory\" argument must be of type string");
            System.Environment.CurrentDirectory = dir.Value;
            return Undefined;
        });
        DefineMethod("exit", 1, (_, args) =>
        {
            var code = args.Length > 0 ? (int)args[0].ToNumber() : _exitCode;
            _exitCode = code;
            Fire("exit", [JsNumber.Create(code)]);
            throw new ProcessExitException(code);
        });
        DefineMethod("uptime", 0, (_, _) => JsNumber.Create(_startTime.Elapsed.TotalSeconds));
        DefineMethod("memoryUsage", 0, (_, _) =>
        {
            var obj = new JsDynamicObject();
            var total = GC.GetTotalMemory(forceFullCollection: false);
            obj.DefineOwnProperty("rss", PropertyDescriptor.Data(JsNumber.Create(total)));
            obj.DefineOwnProperty("heapTotal", PropertyDescriptor.Data(JsNumber.Create(total)));
            obj.DefineOwnProperty("heapUsed", PropertyDescriptor.Data(JsNumber.Create(total)));
            obj.DefineOwnProperty("external", PropertyDescriptor.Data(JsNumber.Create(0)));
            obj.DefineOwnProperty("arrayBuffers", PropertyDescriptor.Data(JsNumber.Create(0)));
            return obj;
        });
        DefineMethod("hrtime", 1, (_, args) => HrtimeArray(args));
        ((JsFunction)Get("hrtime")).DefineOwnProperty("bigint", PropertyDescriptor.Data(
            JsFunction.CreateNative("bigint", (_, _) =>
            {
                var ns = (long)(_startTime.Elapsed.TotalMilliseconds * 1_000_000);
                return new JsBigInt(ns);
            }, 0),
            writable: true, enumerable: false, configurable: true));

        DefineMethod("nextTick", 1, (_, args) =>
        {
            if (args.Length == 0 || args[0] is not JsFunction fn)
                throw new Runtime.Errors.JsTypeError("process.nextTick requires a function");
            _nextTickQueue.Enqueue(fn);
            return Undefined;
        });

        DefineMethod("on", 2, (thisArg, args) =>
        {
            if (args.Length == 0 || args[0] is not JsString evtStr)
                throw new Runtime.Errors.JsTypeError("The \"event\" argument must be of type string");
            if (args.Length < 2 || args[1] is not JsFunction fn)
                throw new Runtime.Errors.JsTypeError("listener must be a function");
            if (!_listeners.TryGetValue(evtStr.Value, out var list))
            {
                list = [];
                _listeners[evtStr.Value] = list;
            }
            list.Add(fn);
            return thisArg;
        });
        DefineMethod("off", 2, (thisArg, args) =>
        {
            if (args.Length == 0 || args[0] is not JsString evtStr)
                throw new Runtime.Errors.JsTypeError("The \"event\" argument must be of type string");
            var fn = args.Length > 1 ? args[1] as JsFunction : null;
            if (fn is not null && _listeners.TryGetValue(evtStr.Value, out var list))
            {
                list.Remove(fn);
            }
            return thisArg;
        });
        DefineMethod("emitWarning", 1, (_, args) =>
        {
            var msg = (args.Length > 0 ? args[0] : Undefined).ToJsString();
            StdErr.WriteLine("(node:warning) " + msg);
            return Undefined;
        });
    }

    private void DefineData(string name, JsValue value)
    {
        DefineOwnProperty(name, PropertyDescriptor.Data(value, writable: true, enumerable: true, configurable: true));
    }

    private void DefineMethod(string name, int length, Func<JsValue, JsValue[], JsValue> impl)
    {
        DefineOwnProperty(name, PropertyDescriptor.Data(
            JsFunction.CreateNative(name, impl, length),
            writable: true, enumerable: false, configurable: true));
    }

    /// <summary>Drain the queued nextTick callbacks. Must be called by host.</summary>
    public int DrainNextTicks()
    {
        int count = 0;
        while (_nextTickQueue.Count > 0)
        {
            var fn = _nextTickQueue.Dequeue();
            fn.Call(Undefined, []);
            count++;
        }
        return count;
    }

    internal void Fire(string evt, JsValue[] args)
    {
        if (_listeners.TryGetValue(evt, out var list))
        {
            foreach (var fn in list.ToArray()) fn.Call(Undefined, args);
        }
    }

    private static JsDynamicObject MakeRelease()
    {
        var r = new JsDynamicObject();
        r.DefineOwnProperty("name", PropertyDescriptor.Data(new JsString("node")));
        r.DefineOwnProperty("sourceUrl", PropertyDescriptor.Data(new JsString("")));
        r.DefineOwnProperty("headersUrl", PropertyDescriptor.Data(new JsString("")));
        return r;
    }

    private static JsDynamicObject BuildEnv()
    {
        var env = new JsDynamicObject();
        foreach (System.Collections.DictionaryEntry entry in System.Environment.GetEnvironmentVariables())
        {
            var key = entry.Key?.ToString();
            var val = entry.Value?.ToString();
            if (key is null || val is null) continue;
            env.DefineOwnProperty(key, PropertyDescriptor.Data(new JsString(val), writable: true, enumerable: true, configurable: true));
        }
        return env;
    }

    private static JsDynamicObject MakeWriteStream(string name, Func<TextWriter> getWriter)
    {
        var s = new JsDynamicObject();
        s.DefineOwnProperty("writable", PropertyDescriptor.Data(True));
        s.DefineOwnProperty("isTTY", PropertyDescriptor.Data(False));
        s.DefineOwnProperty("fd", PropertyDescriptor.Data(JsNumber.Create(name == "stdout" ? 1 : 2)));
        s.DefineOwnProperty("write", PropertyDescriptor.Data(
            JsFunction.CreateNative("write", (_, args) =>
            {
                var v = args.Length > 0 ? args[0] : Undefined;
                string text = v switch
                {
                    BufferObject b => System.Text.Encoding.UTF8.GetString(b.Span),
                    _ => v.ToJsString(),
                };
                getWriter().Write(text);
                return True;
            }, 2),
            writable: true, enumerable: false, configurable: true));
        return s;
    }

    private static JsDynamicObject MakeReadStream()
    {
        var s = new JsDynamicObject();
        s.DefineOwnProperty("readable", PropertyDescriptor.Data(True));
        s.DefineOwnProperty("isTTY", PropertyDescriptor.Data(False));
        s.DefineOwnProperty("fd", PropertyDescriptor.Data(JsNumber.Create(0)));
        return s;
    }

    private JsArray HrtimeArray(JsValue[] args)
    {
        var ticks = _startTime.Elapsed.TotalMilliseconds * 1_000_000;
        long seconds = (long)(ticks / 1_000_000_000);
        long nanos = (long)(ticks - (double)seconds * 1_000_000_000);
        if (args.Length > 0 && args[0] is JsArray prev && prev.DenseLength >= 2)
        {
            long prevS = (long)prev.Get("0").ToNumber();
            long prevN = (long)prev.Get("1").ToNumber();
            long diffN = nanos - prevN;
            long diffS = seconds - prevS;
            if (diffN < 0) { diffN += 1_000_000_000; diffS -= 1; }
            var a = new JsArray();
            a.Push(JsNumber.Create(diffS)); a.Push(JsNumber.Create(diffN));
            return a;
        }
        var arr = new JsArray();
        arr.Push(JsNumber.Create(seconds));
        arr.Push(JsNumber.Create(nanos));
        return arr;
    }
}

/// <summary>Thrown by process.exit(). The host may catch it to terminate.</summary>
public sealed class ProcessExitException : Exception
{
    public int ExitCode { get; }
    public ProcessExitException(int code) : base($"process.exit({code})")
    {
        ExitCode = code;
    }
}
