using SuperRender.EcmaScript.Engine;
using SuperRender.EcmaScript.NodeSimulator.Modules;
using SuperRender.EcmaScript.Runtime;

namespace SuperRender.EcmaScript.NodeSimulator;

/// <summary>
/// Installs the Node.js-compatible runtime surface (P0) onto a <see cref="JsEngine"/>.
/// Exposes globals: globalThis/global, process, Buffer, queueMicrotask, setTimeout family.
/// Exposes built-in modules via <c>require(id)</c>: path, os, fs, util, events, assert, buffer, timers.
/// </summary>
public sealed class NodeRuntime
{
    public JsEngine Engine { get; }
    public ProcessObject Process { get; }
    public TimerQueue Timers { get; }

    private readonly Dictionary<string, Func<JsValue>> _builtinModules;

    internal NodeRuntime(JsEngine engine, ProcessObject process, TimerQueue timers, Dictionary<string, Func<JsValue>> modules)
    {
        Engine = engine;
        Process = process;
        Timers = timers;
        _builtinModules = modules;
    }

    /// <summary>Drain queued microtasks, immediates, and due timers. Returns callbacks fired.</summary>
    public int DrainOnce()
    {
        int fired = Process.DrainNextTicks();
        fired += Timers.Poll();
        return fired;
    }

    /// <summary>Advance virtual timer clock (for tests).</summary>
    public void AdvanceTimers(long ms) => Timers.Advance(ms);

    /// <summary>Resolve a builtin module by id. Returns null if unknown.</summary>
    public JsValue? ResolveBuiltin(string id)
    {
        var normalized = id.StartsWith("node:", StringComparison.Ordinal) ? id[5..] : id;
        return _builtinModules.TryGetValue(normalized, out var factory) ? factory() : null;
    }
}

/// <summary>Entry point for installing Node.js-compatible globals and modules onto a JsEngine.</summary>
public static class NodeSimulator
{
    public static NodeRuntime Install(JsEngine engine) => Install(engine, Array.Empty<string>());

    public static NodeRuntime Install(JsEngine engine, string[] argv)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(argv);

        var realm = engine.Realm;
        var process = new ProcessObject(BuildArgv(argv), realm);
        var timers = TimersModule.Install(realm);
        BufferModule.Install(realm);
        realm.InstallGlobal("process", process);
        realm.InstallGlobal("global", realm.GlobalObject);
        realm.InstallGlobal("globalThis", realm.GlobalObject);

        // Cached module singletons
        JsDynamicObject? pathMod = null, eventsMod = null, assertMod = null;
        JsObjectBase? qsMod = null, osMod = null, utilMod = null, urlMod = null, zlibMod = null, fsMod = null, cryptoMod = null;
        JsDynamicObject? strDecMod = null, streamMod = null;
        var modules = new Dictionary<string, Func<JsValue>>(StringComparer.Ordinal)
        {
            ["path"] = () => pathMod ??= PathModule.CreateDefault(),
            ["path/posix"] = () => PathModule.Create(win32: false),
            ["path/win32"] = () => PathModule.Create(win32: true),
            ["os"] = () => osMod ??= OsModule.Create(realm),
            ["util"] = () => utilMod ??= UtilModule.Create(realm),
            ["events"] = () => eventsMod ??= EventsModule.Create(realm),
            ["assert"] = () => assertMod ??= AssertModule.Create(),
            ["assert/strict"] = () => assertMod ??= AssertModule.Create(),
            ["fs"] = () => fsMod ??= FsModule.Create(realm),
            ["fs/promises"] = () => (fsMod ??= FsModule.Create(realm)).Get("promises") is JsDynamicObject o ? o : new JsDynamicObject(),
            ["querystring"] = () => qsMod ??= QueryStringModule.Create(realm),
            ["url"] = () => urlMod ??= UrlModule.Create(realm),
            ["string_decoder"] = () => strDecMod ??= StringDecoderModule.Create(realm),
            ["crypto"] = () => cryptoMod ??= CryptoModule.Create(realm),
            ["zlib"] = () => zlibMod ??= ZlibModule.Create(realm),
            ["stream"] = () => streamMod ??= StreamModule.Create(realm),
            ["stream/promises"] = () =>
            {
                var s = streamMod ??= StreamModule.Create(realm);
                var p = new JsDynamicObject();
                if (s.Get("pipeline") is JsFunction pl) p.DefineOwnProperty("pipeline", PropertyDescriptor.Data(pl));
                if (s.Get("finished") is JsFunction fi) p.DefineOwnProperty("finished", PropertyDescriptor.Data(fi));
                return p;
            },
            ["buffer"] = () =>
            {
                var m = new JsDynamicObject();
                m.DefineOwnProperty("Buffer", PropertyDescriptor.Data(realm.GlobalObject.Get("Buffer")));
                return m;
            },
            ["timers"] = () =>
            {
                var m = new JsDynamicObject();
                m.DefineOwnProperty("setTimeout", PropertyDescriptor.Data(realm.GlobalObject.Get("setTimeout")));
                m.DefineOwnProperty("setInterval", PropertyDescriptor.Data(realm.GlobalObject.Get("setInterval")));
                m.DefineOwnProperty("setImmediate", PropertyDescriptor.Data(realm.GlobalObject.Get("setImmediate")));
                m.DefineOwnProperty("clearTimeout", PropertyDescriptor.Data(realm.GlobalObject.Get("clearTimeout")));
                m.DefineOwnProperty("clearInterval", PropertyDescriptor.Data(realm.GlobalObject.Get("clearInterval")));
                m.DefineOwnProperty("clearImmediate", PropertyDescriptor.Data(realm.GlobalObject.Get("clearImmediate")));
                return m;
            },
        };

        // require() shim: only resolves built-in modules.
        realm.InstallGlobal("require", JsFunction.CreateNative("require", (_, args) =>
        {
            if (args.Length == 0 || args[0] is not JsString idStr)
                throw new Runtime.Errors.JsTypeError("The \"id\" argument must be of type string");
            var id = idStr.Value;
            var normalized = id.StartsWith("node:", StringComparison.Ordinal) ? id[5..] : id;
            if (modules.TryGetValue(normalized, out var factory)) return factory();
            throw new Runtime.Errors.JsErrorBase($"Cannot find module '{id}'");
        }, 1));

        return new NodeRuntime(engine, process, timers, modules);
    }

    private static string[] BuildArgv(string[] argv)
    {
        var full = new string[argv.Length + 2];
        full[0] = System.Environment.ProcessPath ?? "node";
        full[1] = "script.js";
        for (int i = 0; i < argv.Length; i++) full[i + 2] = argv[i];
        return full;
    }
}
