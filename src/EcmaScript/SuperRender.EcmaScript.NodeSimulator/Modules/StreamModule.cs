using System.Globalization;
using SuperRender.EcmaScript.Runtime;

namespace SuperRender.EcmaScript.NodeSimulator.Modules;

/// <summary>
/// Minimal Node.js `stream` module. Provides synchronous-friendly Readable, Writable,
/// Transform, PassThrough built on top of EventEmitter, plus `pipeline` and `finished`.
/// Flow is driven by the host draining events — no async scheduler needed.
/// </summary>
public static class StreamModule
{
    private static PropertyDescriptor MethodDesc(string name, Func<JsValue, JsValue[], JsValue> impl, int length) =>
        PropertyDescriptor.Data(JsFunction.CreateNative(name, impl, length), writable: true, enumerable: false, configurable: true);

    public static JsObject Create(Realm realm)
    {
        var module = new JsObject();

        var readableCtor = BuildReadableCtor(realm);
        var writableCtor = BuildWritableCtor(realm);
        var transformCtor = BuildTransformCtor(realm, readableCtor, writableCtor);
        var passThroughCtor = BuildPassThroughCtor(realm, transformCtor);

        module.DefineOwnProperty("Readable", PropertyDescriptor.Data(readableCtor));
        module.DefineOwnProperty("Writable", PropertyDescriptor.Data(writableCtor));
        module.DefineOwnProperty("Duplex", PropertyDescriptor.Data(transformCtor)); // treat as Transform
        module.DefineOwnProperty("Transform", PropertyDescriptor.Data(transformCtor));
        module.DefineOwnProperty("PassThrough", PropertyDescriptor.Data(passThroughCtor));

        module.DefineOwnProperty("pipeline", MethodDesc("pipeline", (_, args) => Pipeline(args, realm), 0));
        module.DefineOwnProperty("finished", MethodDesc("finished", (_, args) => Finished(args, realm), 2));

        // default export
        module.DefineOwnProperty("default", PropertyDescriptor.Data(module));
        return module;
    }

    // ─── Readable ──────────────────────────────────────────────────────────────

    private static JsFunction BuildReadableCtor(Realm realm)
    {
        var proto = new JsObject { Prototype = realm.ObjectPrototype };
        InstallEmitterMethods(proto);

        proto.DefineOwnProperty("push", MethodDesc("push", (thisArg, args) =>
        {
            if (thisArg is not ReadableStream r) return JsValue.False;
            var chunk = args.Length > 0 ? args[0] : JsValue.Undefined;
            if (chunk is JsNull || chunk is JsUndefined)
            {
                r.Ended = true;
                EmitReadable(r);
                return JsValue.False;
            }
            r.Enqueue(chunk);
            EmitReadable(r);
            return JsValue.True;
        }, 2));

        proto.DefineOwnProperty("read", MethodDesc("read", (thisArg, _) =>
        {
            if (thisArg is not ReadableStream r) return JsValue.Null;
            if (r.Buffer.Count == 0) return JsValue.Null;
            return r.Buffer.Dequeue();
        }, 1));

        proto.DefineOwnProperty("pipe", MethodDesc("pipe", (thisArg, args) =>
        {
            if (thisArg is not ReadableStream r || args.Length == 0 || args[0] is not JsObject dest)
                return args.Length > 0 ? args[0] : JsValue.Undefined;
            r.Pipes.Add(dest);
            // Flush whatever is already buffered
            FlushToDest(r, dest);
            return dest;
        }, 2));

        proto.DefineOwnProperty("on", MethodDesc("on", (thisArg, args) =>
        {
            AddListener(thisArg, args);
            // If adding a 'data' listener, flow mode: drain buffer immediately
            if (thisArg is ReadableStream r && args.Length > 0 && args[0] is JsString s && s.Value == "data")
            {
                r.Flowing = true;
                FlushFlowing(r);
            }
            return thisArg;
        }, 2));

        var ctor = new JsFunction
        {
            Name = "Readable",
            Length = 1,
            IsConstructor = true,
            PrototypeObject = proto,
            CallTarget = (thisArg, args) =>
            {
                var target = thisArg as ReadableStream ?? new ReadableStream();
                target.Prototype = proto;
                ApplyReadOpts(target, args);
                return target;
            },
        };
        ctor.ConstructTarget = args =>
        {
            var obj = new ReadableStream { Prototype = proto };
            ApplyReadOpts(obj, args);
            return obj;
        };
        proto.DefineOwnProperty("constructor", PropertyDescriptor.Data(ctor, writable: true, enumerable: false, configurable: true));

        // Static Readable.from(iterable | array)
        ctor.DefineOwnProperty("from", MethodDesc("from", (_, args) =>
        {
            var stream = new ReadableStream { Prototype = proto };
            if (args.Length > 0 && args[0] is JsArray arr)
            {
                for (int i = 0; i < arr.DenseLength; i++)
                    stream.Enqueue(arr.Get(i.ToString(CultureInfo.InvariantCulture)));
            }
            stream.Ended = true;
            return stream;
        }, 1));

        return ctor;
    }

    private static void ApplyReadOpts(ReadableStream r, JsValue[] args)
    {
        if (args.Length > 0 && args[0] is JsObject opts)
        {
            r.ReadFn = opts.Get("read") as JsFunction;
        }
    }

    private static void EmitReadable(ReadableStream r)
    {
        if (r.Flowing) FlushFlowing(r);
        // Also push to pipes
        foreach (var dest in r.Pipes.ToArray()) FlushToDest(r, dest);
        if (r.Ended && r.Buffer.Count == 0 && !r.EndEmitted)
        {
            r.EndEmitted = true;
            Emit(r, "end", Array.Empty<JsValue>());
            foreach (var dest in r.Pipes.ToArray())
            {
                if (dest.Get("end") is JsFunction endFn) endFn.Call(dest, Array.Empty<JsValue>());
            }
        }
    }

    private static void FlushFlowing(ReadableStream r)
    {
        while (r.Buffer.Count > 0)
        {
            var chunk = r.Buffer.Dequeue();
            Emit(r, "data", new[] { chunk });
        }
    }

    private static void FlushToDest(ReadableStream r, JsObject dest)
    {
        while (r.Buffer.Count > 0 && dest.Get("write") is JsFunction write)
        {
            var chunk = r.Buffer.Dequeue();
            write.Call(dest, new[] { chunk });
        }
    }

    // ─── Writable ──────────────────────────────────────────────────────────────

    private static JsFunction BuildWritableCtor(Realm realm)
    {
        var proto = new JsObject { Prototype = realm.ObjectPrototype };
        InstallEmitterMethods(proto);

        proto.DefineOwnProperty("write", MethodDesc("write", (thisArg, args) =>
        {
            if (thisArg is not WritableStream w) return JsValue.False;
            if (w.Ended) { Emit(w, "error", new JsValue[] { new JsString("write after end") }); return JsValue.False; }
            var chunk = args.Length > 0 ? args[0] : JsValue.Undefined;
            w.WriteFn?.Call(thisArg, new[] { chunk, JsValue.Undefined, JsFunction.CreateNative("", (_, _) => JsValue.Undefined, 0) });
            w.Chunks.Add(chunk);
            Emit(w, "drain", Array.Empty<JsValue>());
            return JsValue.True;
        }, 3));

        proto.DefineOwnProperty("end", MethodDesc("end", (thisArg, args) =>
        {
            if (thisArg is not WritableStream w) return thisArg;
            if (args.Length > 0 && args[0] is not JsUndefined && args[0] is not JsFunction)
            {
                var writeFn = w.Get("write") as JsFunction;
                writeFn?.Call(w, new[] { args[0] });
            }
            if (!w.Ended)
            {
                w.Ended = true;
                Emit(w, "finish", Array.Empty<JsValue>());
                Emit(w, "close", Array.Empty<JsValue>());
            }
            return thisArg;
        }, 2));

        var ctor = new JsFunction
        {
            Name = "Writable",
            Length = 1,
            IsConstructor = true,
            PrototypeObject = proto,
            CallTarget = (thisArg, args) =>
            {
                var target = thisArg as WritableStream ?? new WritableStream();
                target.Prototype = proto;
                ApplyWriteOpts(target, args);
                return target;
            },
        };
        ctor.ConstructTarget = args =>
        {
            var obj = new WritableStream { Prototype = proto };
            ApplyWriteOpts(obj, args);
            return obj;
        };
        proto.DefineOwnProperty("constructor", PropertyDescriptor.Data(ctor, writable: true, enumerable: false, configurable: true));
        return ctor;
    }

    private static void ApplyWriteOpts(WritableStream w, JsValue[] args)
    {
        if (args.Length > 0 && args[0] is JsObject opts)
        {
            w.WriteFn = opts.Get("write") as JsFunction;
        }
    }

    // ─── Transform ─────────────────────────────────────────────────────────────

    private static JsFunction BuildTransformCtor(Realm realm, JsFunction readableCtor, JsFunction writableCtor)
    {
        var proto = new JsObject { Prototype = realm.ObjectPrototype };
        InstallEmitterMethods(proto);

        proto.DefineOwnProperty("push", MethodDesc("push", (thisArg, args) =>
        {
            if (thisArg is not TransformStream t) return JsValue.False;
            var chunk = args.Length > 0 ? args[0] : JsValue.Undefined;
            if (chunk is JsNull || chunk is JsUndefined)
            {
                t.Ended = true;
                Emit(t, "end", Array.Empty<JsValue>());
                return JsValue.False;
            }
            Emit(t, "data", new[] { chunk });
            return JsValue.True;
        }, 2));

        proto.DefineOwnProperty("write", MethodDesc("write", (thisArg, args) =>
        {
            if (thisArg is not TransformStream t) return JsValue.False;
            var chunk = args.Length > 0 ? args[0] : JsValue.Undefined;
            if (t.TransformFn is not null)
            {
                var pushFn = t.Get("push") as JsFunction;
                var cb = JsFunction.CreateNative("", (_, _) => JsValue.Undefined, 0);
                t.TransformFn.Call(thisArg, new[] { chunk, JsValue.Undefined, (JsValue)cb });
            }
            else
            {
                Emit(t, "data", new[] { chunk });
            }
            return JsValue.True;
        }, 3));

        proto.DefineOwnProperty("end", MethodDesc("end", (thisArg, args) =>
        {
            if (thisArg is not TransformStream t) return thisArg;
            if (args.Length > 0 && args[0] is not JsUndefined)
            {
                var writeFn = t.Get("write") as JsFunction;
                writeFn?.Call(thisArg, new[] { args[0] });
            }
            if (t.FlushFn is not null)
            {
                var cb = JsFunction.CreateNative("", (_, _) => JsValue.Undefined, 0);
                t.FlushFn.Call(thisArg, new JsValue[] { cb });
            }
            if (!t.Ended)
            {
                t.Ended = true;
                Emit(t, "end", Array.Empty<JsValue>());
                Emit(t, "finish", Array.Empty<JsValue>());
            }
            return thisArg;
        }, 2));

        var ctor = new JsFunction
        {
            Name = "Transform",
            Length = 1,
            IsConstructor = true,
            PrototypeObject = proto,
            CallTarget = (thisArg, args) =>
            {
                var target = thisArg as TransformStream ?? new TransformStream();
                target.Prototype = proto;
                ApplyTransformOpts(target, args);
                return target;
            },
        };
        ctor.ConstructTarget = args =>
        {
            var obj = new TransformStream { Prototype = proto };
            ApplyTransformOpts(obj, args);
            return obj;
        };
        proto.DefineOwnProperty("constructor", PropertyDescriptor.Data(ctor, writable: true, enumerable: false, configurable: true));
        return ctor;
    }

    private static void ApplyTransformOpts(TransformStream t, JsValue[] args)
    {
        if (args.Length > 0 && args[0] is JsObject opts)
        {
            t.TransformFn = opts.Get("transform") as JsFunction;
            t.FlushFn = opts.Get("flush") as JsFunction;
        }
    }

    // ─── PassThrough ───────────────────────────────────────────────────────────

    private static JsFunction BuildPassThroughCtor(Realm realm, JsFunction transformCtor)
    {
        var proto = new JsObject { Prototype = transformCtor.PrototypeObject };
        var ctor = new JsFunction
        {
            Name = "PassThrough",
            Length = 1,
            IsConstructor = true,
            PrototypeObject = proto,
            CallTarget = (thisArg, _) =>
            {
                var target = thisArg as TransformStream ?? new TransformStream();
                target.Prototype = proto;
                return target;
            },
        };
        ctor.ConstructTarget = _ => new TransformStream { Prototype = proto };
        proto.DefineOwnProperty("constructor", PropertyDescriptor.Data(ctor, writable: true, enumerable: false, configurable: true));
        return ctor;
    }

    // ─── pipeline / finished ───────────────────────────────────────────────────

    private static JsValue Pipeline(JsValue[] args, Realm realm)
    {
        if (args.Length < 2) throw new Runtime.Errors.JsTypeError("pipeline requires source and destination");
        JsFunction? cb = null;
        int end = args.Length;
        if (args[^1] is JsFunction fn) { cb = fn; end--; }

        var steps = new JsValue[end];
        for (int i = 0; i < end; i++) steps[i] = args[i];

        try
        {
            for (int i = 0; i < steps.Length - 1; i++)
            {
                if (steps[i] is JsObject src && steps[i + 1] is JsObject dst && src.Get("pipe") is JsFunction pipe)
                {
                    pipe.Call(src, new JsValue[] { dst });
                }
            }
            if (steps.Length > 0 && steps[^1] is JsObject last)
            {
                if (last.Get("on") is JsFunction on)
                {
                    on.Call(last, new JsValue[]
                    {
                        new JsString("finish"),
                        JsFunction.CreateNative("", (_, _) =>
                        {
                            cb?.Call(JsValue.Undefined, new JsValue[] { JsValue.Null });
                            return JsValue.Undefined;
                        }, 0),
                    });
                }
            }
        }
        catch (Exception ex)
        {
            cb?.Call(JsValue.Undefined, new JsValue[] { new JsString(ex.Message) });
        }
        return JsValue.Undefined;
    }

    private static JsValue Finished(JsValue[] args, Realm realm)
    {
        if (args.Length < 2 || args[0] is not JsObject stream || args[^1] is not JsFunction cb)
            throw new Runtime.Errors.JsTypeError("finished(stream, callback)");
        if (stream.Get("on") is JsFunction on)
        {
            var handler = JsFunction.CreateNative("", (_, _) =>
            {
                cb.Call(JsValue.Undefined, new JsValue[] { JsValue.Null });
                return JsValue.Undefined;
            }, 0);
            on.Call(stream, new JsValue[] { new JsString("end"), handler });
            on.Call(stream, new JsValue[] { new JsString("finish"), handler });
            on.Call(stream, new JsValue[] { new JsString("close"), handler });
        }
        return JsValue.Undefined;
    }

    // ─── EventEmitter core (trimmed) ───────────────────────────────────────────

    private static void InstallEmitterMethods(JsObject proto)
    {
        proto.DefineOwnProperty("on", MethodDesc("on", (thisArg, args) => AddListener(thisArg, args), 2));
        proto.DefineOwnProperty("once", MethodDesc("once", (thisArg, args) =>
        {
            if (args.Length < 2 || args[1] is not JsFunction orig) return thisArg;
            JsFunction? wrap = null;
            wrap = JsFunction.CreateNative("", (self, a) =>
            {
                if (thisArg is JsObject em && em.Get("off") is JsFunction off && wrap is not null)
                    off.Call(em, new JsValue[] { args[0], wrap });
                return orig.Call(self, a);
            }, orig.Length);
            return AddListener(thisArg, new JsValue[] { args[0], wrap });
        }, 2));
        proto.DefineOwnProperty("off", MethodDesc("off", (thisArg, args) => RemoveListener(thisArg, args), 2));
        proto.DefineOwnProperty("removeListener", MethodDesc("removeListener", (thisArg, args) => RemoveListener(thisArg, args), 2));
        proto.DefineOwnProperty("emit", MethodDesc("emit", (thisArg, args) =>
        {
            var evt = args.Length > 0 && args[0] is JsString s ? s.Value : "";
            var rest = args.Length > 1 ? args[1..] : Array.Empty<JsValue>();
            return EmitInternal(thisArg, evt, rest) ? JsValue.True : JsValue.False;
        }, 1));
        proto.DefineOwnProperty("listenerCount", MethodDesc("listenerCount", (thisArg, args) =>
        {
            var evt = args.Length > 0 && args[0] is JsString s ? s.Value : "";
            return JsNumber.Create(GetListeners(thisArg, evt)?.Count ?? 0);
        }, 1));
    }

    internal static JsValue AddListener(JsValue thisArg, JsValue[] args)
    {
        if (args.Length < 2 || args[0] is not JsString evt || args[1] is not JsFunction fn) return thisArg;
        if (thisArg is not StreamBase sb) return thisArg;
        if (!sb.Listeners.TryGetValue(evt.Value, out var list))
        {
            list = new List<JsFunction>();
            sb.Listeners[evt.Value] = list;
        }
        list.Add(fn);
        return thisArg;
    }

    internal static JsValue RemoveListener(JsValue thisArg, JsValue[] args)
    {
        if (args.Length < 2 || args[0] is not JsString evt || args[1] is not JsFunction fn) return thisArg;
        if (thisArg is not StreamBase sb) return thisArg;
        if (sb.Listeners.TryGetValue(evt.Value, out var list)) list.Remove(fn);
        return thisArg;
    }

    internal static void Emit(StreamBase stream, string evt, JsValue[] args) => EmitInternal(stream, evt, args);

    private static bool EmitInternal(JsValue thisArg, string evt, JsValue[] args)
    {
        if (thisArg is not StreamBase sb) return false;
        if (!sb.Listeners.TryGetValue(evt, out var list) || list.Count == 0) return false;
        foreach (var fn in list.ToArray()) fn.Call(thisArg, args);
        return true;
    }

    private static List<JsFunction>? GetListeners(JsValue thisArg, string evt)
    {
        if (thisArg is StreamBase sb && sb.Listeners.TryGetValue(evt, out var list)) return list;
        return null;
    }
}

/// <summary>Base class carrying event-listener state for stream subclasses.</summary>
public abstract class StreamBase : JsObject
{
    public Dictionary<string, List<JsFunction>> Listeners { get; } = new(StringComparer.Ordinal);
}

public sealed class ReadableStream : StreamBase
{
    public Queue<JsValue> Buffer { get; } = new();
    public bool Ended { get; set; }
    public bool EndEmitted { get; set; }
    public bool Flowing { get; set; }
    public List<JsObject> Pipes { get; } = new();
    public JsFunction? ReadFn { get; set; }

    public void Enqueue(JsValue value) => Buffer.Enqueue(value);
}

public sealed class WritableStream : StreamBase
{
    public List<JsValue> Chunks { get; } = new();
    public bool Ended { get; set; }
    public JsFunction? WriteFn { get; set; }
}

public sealed class TransformStream : StreamBase
{
    public bool Ended { get; set; }
    public JsFunction? TransformFn { get; set; }
    public JsFunction? FlushFn { get; set; }
}
