using SuperRender.EcmaScript.Runtime;
using SuperRender.EcmaScript.Runtime.Builtins;

namespace SuperRender.EcmaScript.NodeSimulator.Modules;

/// <summary>
/// Node `events` module. Exports `EventEmitter` constructor with on/once/off/emit/listeners.
/// </summary>
public static class EventsModule
{
    private static PropertyDescriptor MethodDesc(string name, Func<JsValue, JsValue[], JsValue> impl, int length) =>
        PropertyDescriptor.Data(JsFunction.CreateNative(name, impl, length), writable: true, enumerable: false, configurable: true);

    public static JsDynamicObject Create(Realm realm)
    {
        var ctor = CreateEmitterConstructor(realm);
        var module = ctor;
        module.DefineOwnProperty("EventEmitter", PropertyDescriptor.Data(ctor, writable: true, enumerable: false, configurable: true));
        module.DefineOwnProperty("default", PropertyDescriptor.Data(ctor, writable: true, enumerable: false, configurable: true));
        module.DefineOwnProperty("once", MethodDesc("once", (_, args) => StaticOnce(realm, args), 2));
        return module;
    }

    private static JsFunction CreateEmitterConstructor(Realm realm)
    {
        var proto = new JsDynamicObject { Prototype = realm.ObjectPrototype };
        InstallEmitterMethods(proto);

        var ctor = new JsFunction
        {
            Name = "EventEmitter",
            Length = 0,
            IsConstructor = true,
            PrototypeObject = proto,
            CallTarget = (thisArg, _) =>
            {
                JsDynamicObject target = thisArg is JsDynamicObject to ? to : new JsDynamicObject();
                target.Prototype = proto;
                target.DefineOwnProperty("_events", PropertyDescriptor.Data(new JsDynamicObject(), writable: true, enumerable: false, configurable: true));
                target.DefineOwnProperty("_maxListeners", PropertyDescriptor.Data(JsNumber.Create(10), writable: true, enumerable: false, configurable: true));
                return target;
            },
        };
        ctor.ConstructTarget = args =>
        {
            var obj = new JsDynamicObject { Prototype = proto };
            ctor.CallTarget!(obj, args);
            return obj;
        };
        proto.DefineOwnProperty("constructor", PropertyDescriptor.Data(ctor, writable: true, enumerable: false, configurable: true));
        return ctor;
    }

    private static void InstallEmitterMethods(JsDynamicObject proto)
    {
        proto.DefineOwnProperty("on", MethodDesc("on", (thisArg, args) => AddListener(thisArg, args, once: false, prepend: false), 2));
        proto.DefineOwnProperty("addListener", MethodDesc("addListener", (thisArg, args) => AddListener(thisArg, args, once: false, prepend: false), 2));
        proto.DefineOwnProperty("once", MethodDesc("once", (thisArg, args) => AddListener(thisArg, args, once: true, prepend: false), 2));
        proto.DefineOwnProperty("prependListener", MethodDesc("prependListener", (thisArg, args) => AddListener(thisArg, args, once: false, prepend: true), 2));
        proto.DefineOwnProperty("prependOnceListener", MethodDesc("prependOnceListener", (thisArg, args) => AddListener(thisArg, args, once: true, prepend: true), 2));

        proto.DefineOwnProperty("off", MethodDesc("off", (thisArg, args) => RemoveListener(thisArg, args), 2));
        proto.DefineOwnProperty("removeListener", MethodDesc("removeListener", (thisArg, args) => RemoveListener(thisArg, args), 2));
        proto.DefineOwnProperty("removeAllListeners", MethodDesc("removeAllListeners", (thisArg, args) =>
        {
            var events = GetEvents(thisArg);
            if (args.Length == 0 || args[0] is JsUndefined)
            {
                foreach (var k in events.OwnPropertyKeys().ToArray()) events.Delete(k);
            }
            else
            {
                events.Delete(RequireString(args, 0, "event"));
            }
            return thisArg;
        }, 1));

        proto.DefineOwnProperty("emit", MethodDesc("emit", (thisArg, args) =>
        {
            var evt = RequireString(args, 0, "event");
            var rest = args.Length > 1 ? args[1..] : [];
            var events = GetEvents(thisArg);
            if (events.Get(evt) is not JsArray list || list.DenseLength == 0)
            {
                return JsValue.False;
            }
            // Snapshot to support listeners that mutate during dispatch
            var snapshot = new List<JsValue>();
            for (int i = 0; i < list.DenseLength; i++) snapshot.Add(list.Get(i.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            foreach (var entry in snapshot)
            {
                if (entry is JsFunction fn) fn.Call(thisArg, rest);
                else if (entry is JsDynamicObject wrap && wrap.Get("listener") is JsFunction inner)
                {
                    inner.Call(thisArg, rest);
                    // Remove once-wrapper
                    for (int i = 0; i < list.DenseLength; i++)
                    {
                        if (ReferenceEquals(list.Get(i.ToString(System.Globalization.CultureInfo.InvariantCulture)), wrap))
                        {
                            RemoveAt(list, i);
                            break;
                        }
                    }
                }
            }
            return JsValue.True;
        }, 1));

        proto.DefineOwnProperty("listenerCount", MethodDesc("listenerCount", (thisArg, args) =>
        {
            var evt = RequireString(args, 0, "event");
            var events = GetEvents(thisArg);
            return events.Get(evt) is JsArray list ? JsNumber.Create(list.DenseLength) : JsNumber.Create(0);
        }, 1));
        proto.DefineOwnProperty("listeners", MethodDesc("listeners", (thisArg, args) =>
        {
            var evt = RequireString(args, 0, "event");
            var events = GetEvents(thisArg);
            if (events.Get(evt) is not JsArray list) return new JsArray();
            var clone = new JsArray();
            for (int i = 0; i < list.DenseLength; i++)
            {
                var v = list.Get(i.ToString(System.Globalization.CultureInfo.InvariantCulture));
                if (v is JsDynamicObject wrap && wrap.Get("listener") is JsFunction inner) clone.Push(inner);
                else clone.Push(v);
            }
            return clone;
        }, 1));
        proto.DefineOwnProperty("rawListeners", MethodDesc("rawListeners", (thisArg, args) =>
        {
            var evt = RequireString(args, 0, "event");
            var events = GetEvents(thisArg);
            if (events.Get(evt) is not JsArray list) return new JsArray();
            var clone = new JsArray();
            for (int i = 0; i < list.DenseLength; i++) clone.Push(list.Get(i.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            return clone;
        }, 1));
        proto.DefineOwnProperty("eventNames", MethodDesc("eventNames", (thisArg, _) =>
        {
            var events = GetEvents(thisArg);
            var arr = new JsArray();
            foreach (var k in events.OwnPropertyKeys()) arr.Push(new JsString(k));
            return arr;
        }, 0));
        proto.DefineOwnProperty("setMaxListeners", MethodDesc("setMaxListeners", (thisArg, args) =>
        {
            if (thisArg is JsDynamicObject o)
            {
                var n = args.Length > 0 && args[0] is not JsUndefined ? args[0].ToNumber() : 10;
                o.DefineOwnProperty("_maxListeners", PropertyDescriptor.Data(JsNumber.Create(n), writable: true, enumerable: false, configurable: true));
            }
            return thisArg;
        }, 1));
        proto.DefineOwnProperty("getMaxListeners", MethodDesc("getMaxListeners", (thisArg, _) =>
        {
            if (thisArg is JsDynamicObject o && o.Get("_maxListeners") is JsNumber n) return n;
            return JsNumber.Create(10);
        }, 0));
    }

    private static JsPromiseObject StaticOnce(Realm realm, JsValue[] args)
    {
        if (args.Length < 1 || args[0] is not JsDynamicObject emitter)
            throw new Runtime.Errors.JsTypeError("emitter must be an EventEmitter");
        if (args.Length < 2 || args[1] is not JsString evtStr)
            throw new Runtime.Errors.JsTypeError("The \"name\" argument must be of type string");

        var promise = new JsPromiseObject { Prototype = realm.PromisePrototype };
        var settled = false;

        JsFunction? resolveListener = null;
        JsFunction? rejectListener = null;

        resolveListener = JsFunction.CreateNative("", (_, listenerArgs) =>
        {
            if (settled) return JsValue.Undefined;
            settled = true;
            if (rejectListener is not null)
            {
                RemoveEmitterListener(emitter, "error", rejectListener);
            }
            var arr = new JsArray { Prototype = realm.ArrayPrototype };
            foreach (var v in listenerArgs) arr.Push(v);
            PromiseConstructor.ResolvePromise(promise, arr, realm);
            return JsValue.Undefined;
        }, 0);

        rejectListener = JsFunction.CreateNative("", (_, rejectArgs) =>
        {
            if (settled) return JsValue.Undefined;
            settled = true;
            if (resolveListener is not null)
            {
                RemoveEmitterListener(emitter, evtStr.Value, resolveListener);
            }
            var reason = rejectArgs.Length > 0 ? rejectArgs[0] : JsValue.Undefined;
            PromiseConstructor.RejectPromise(promise, reason);
            return JsValue.Undefined;
        }, 0);

        AddEmitterListener(emitter, evtStr.Value, resolveListener, once: true);
        if (!string.Equals(evtStr.Value, "error", StringComparison.Ordinal))
        {
            AddEmitterListener(emitter, "error", rejectListener, once: true);
        }
        return promise;
    }

    private static void AddEmitterListener(JsDynamicObject emitter, string evt, JsFunction listener, bool once)
    {
        if (emitter.Get(once ? "once" : "on") is not JsFunction addFn)
            throw new Runtime.Errors.JsTypeError("emitter is not an EventEmitter");
        addFn.Call(emitter, [new JsString(evt), listener]);
    }

    private static void RemoveEmitterListener(JsDynamicObject emitter, string evt, JsFunction listener)
    {
        if (emitter.Get("off") is JsFunction off)
            off.Call(emitter, [new JsString(evt), listener]);
    }

    private static string RequireString(JsValue[] args, int index, string param)
    {
        var v = index < args.Length ? args[index] : JsValue.Undefined;
        if (v is JsString s) return s.Value;
        throw new Runtime.Errors.JsTypeError($"The \"{param}\" argument must be of type string");
    }

    private static JsValue AddListener(JsValue thisArg, JsValue[] args, bool once, bool prepend)
    {
        var evt = RequireString(args, 0, "event");
        if (args.Length < 2 || args[1] is not JsFunction fn)
            throw new Runtime.Errors.JsTypeError("listener must be a function");
        var events = GetEvents(thisArg);
        var list = events.Get(evt) as JsArray;
        if (list is null)
        {
            list = new JsArray();
            events.DefineOwnProperty(evt, PropertyDescriptor.Data(list, writable: true, enumerable: true, configurable: true));
        }
        JsValue entry = fn;
        if (once)
        {
            var wrap = new JsDynamicObject();
            wrap.DefineOwnProperty("listener", PropertyDescriptor.Data(fn));
            entry = wrap;
        }
        if (prepend) Unshift(list, entry);
        else list.Push(entry);
        return thisArg;
    }

    private static JsValue RemoveListener(JsValue thisArg, JsValue[] args)
    {
        var evt = RequireString(args, 0, "event");
        var target = (args.Length > 1 ? args[1] : JsValue.Undefined) as JsFunction;
        if (target is null) return thisArg;
        var events = GetEvents(thisArg);
        if (events.Get(evt) is not JsArray list) return thisArg;
        for (int i = list.DenseLength - 1; i >= 0; i--)
        {
            var v = list.Get(i.ToString(System.Globalization.CultureInfo.InvariantCulture));
            bool match = ReferenceEquals(v, target) ||
                         (v is JsDynamicObject wrap && ReferenceEquals(wrap.Get("listener"), target));
            if (match) { RemoveAt(list, i); break; }
        }
        return thisArg;
    }

    private static JsDynamicObject GetEvents(JsValue self)
    {
        if (self is not JsDynamicObject o) throw new Runtime.Errors.JsTypeError("EventEmitter method called on non-object");
        if (o.Get("_events") is not JsDynamicObject events)
        {
            events = new JsDynamicObject();
            o.DefineOwnProperty("_events", PropertyDescriptor.Data(events, writable: true, enumerable: false, configurable: true));
        }
        return events;
    }

    private static void RemoveAt(JsArray arr, int index)
    {
        var len = arr.DenseLength;
        for (int i = index; i < len - 1; i++)
        {
            var k = (i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
            arr.Set(i.ToString(System.Globalization.CultureInfo.InvariantCulture), arr.Get(k));
        }
        arr.Set("length", JsNumber.Create(len - 1));
    }

    private static void Unshift(JsArray arr, JsValue value)
    {
        var len = arr.DenseLength;
        arr.Set("length", JsNumber.Create(len + 1));
        for (int i = len; i > 0; i--)
        {
            var from = (i - 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
            arr.Set(i.ToString(System.Globalization.CultureInfo.InvariantCulture), arr.Get(from));
        }
        arr.Set("0", value);
    }
}
