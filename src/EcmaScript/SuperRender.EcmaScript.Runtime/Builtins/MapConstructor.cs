namespace SuperRender.EcmaScript.Runtime.Builtins;

using SuperRender.EcmaScript.Runtime;

public sealed class MapConstructor : IJsInstallable
{
    public static void Install(Realm realm)
    {
        var proto = realm.MapPrototype;

        var ctor = new JsFunction
        {
            Name = "Map",
            Length = 0,
            IsConstructor = true,
            Prototype = realm.FunctionPrototype,
            PrototypeObject = proto,
            ConstructTarget = args =>
            {
                var map = new JsMapObject { Prototype = realm.MapPrototype };
                var iterable = BuiltinHelper.Arg(args, 0);
                if (iterable is not JsUndefined and not JsNull)
                {
                    if (iterable is JsArray arr)
                    {
                        for (var i = 0; i < arr.DenseLength; i++)
                        {
                            var entry = arr.GetIndex(i);
                            if (entry is not JsArray pair)
                            {
                                throw new Errors.JsTypeError("Iterator value is not an entry object", ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);
                            }

                            map.MapSet(pair.GetIndex(0), pair.GetIndex(1));
                        }
                    }
                }

                return map;
            },
            CallTarget = (_, _) =>
            {
                throw new Errors.JsTypeError("Constructor Map requires 'new'", ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);
            }
        };

        // Prototype methods
        BuiltinHelper.DefineProperty(proto, "constructor", ctor);

        BuiltinHelper.DefineMethod(proto, "get", (thisArg, args) =>
        {
            var map = RequireMap(thisArg);
            var key = BuiltinHelper.Arg(args, 0);
            return map.MapGet(key);
        }, 1);

        BuiltinHelper.DefineMethod(proto, "set", (thisArg, args) =>
        {
            var map = RequireMap(thisArg);
            var key = BuiltinHelper.Arg(args, 0);
            var value = BuiltinHelper.Arg(args, 1);
            map.MapSet(key, value);
            return thisArg;
        }, 2);

        BuiltinHelper.DefineMethod(proto, "has", (thisArg, args) =>
        {
            var map = RequireMap(thisArg);
            var key = BuiltinHelper.Arg(args, 0);
            return map.MapHas(key) ? JsValue.True : JsValue.False;
        }, 1);

        BuiltinHelper.DefineMethod(proto, "delete", (thisArg, args) =>
        {
            var map = RequireMap(thisArg);
            var key = BuiltinHelper.Arg(args, 0);
            return map.MapDelete(key) ? JsValue.True : JsValue.False;
        }, 1);

        BuiltinHelper.DefineMethod(proto, "clear", (thisArg, _) =>
        {
            var map = RequireMap(thisArg);
            map.MapClear();
            return JsValue.Undefined;
        }, 0);

        BuiltinHelper.DefineGetter(proto, "size", (thisArg, _) =>
        {
            var map = RequireMap(thisArg);
            return JsNumber.Create(map.MapSize);
        });

        BuiltinHelper.DefineMethod(proto, "forEach", (thisArg, args) =>
        {
            var map = RequireMap(thisArg);
            var callback = args.Length > 0 && args[0] is JsFunction fn
                ? fn
                : throw new Errors.JsTypeError("forEach callback must be a function", ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);
            var thisArgCb = BuiltinHelper.Arg(args, 1);

            foreach (var (key, value) in map.MapEntries())
            {
                callback.Call(thisArgCb, [value, key, thisArg]);
            }

            return JsValue.Undefined;
        }, 1);

        BuiltinHelper.DefineMethod(proto, "entries", (thisArg, _) =>
        {
            var map = RequireMap(thisArg);
            var items = new List<JsValue>();
            foreach (var (key, value) in map.MapEntries())
            {
                var entry = new JsArray { Prototype = realm.ArrayPrototype };
                entry.Push(key);
                entry.Push(value);
                items.Add(entry);
            }

            return BuiltinHelper.CreateListIterator(items, realm);
        }, 0);

        BuiltinHelper.DefineMethod(proto, "keys", (thisArg, _) =>
        {
            var map = RequireMap(thisArg);
            var items = new List<JsValue>();
            foreach (var (key, _) in map.MapEntries())
            {
                items.Add(key);
            }

            return BuiltinHelper.CreateListIterator(items, realm);
        }, 0);

        BuiltinHelper.DefineMethod(proto, "values", (thisArg, _) =>
        {
            var map = RequireMap(thisArg);
            var items = new List<JsValue>();
            foreach (var (_, value) in map.MapEntries())
            {
                items.Add(value);
            }

            return BuiltinHelper.CreateListIterator(items, realm);
        }, 0);

        // Static methods
        BuiltinHelper.DefineMethod(ctor, "groupBy", (_, args) =>
        {
            var iterable = BuiltinHelper.Arg(args, 0);
            var callback = BuiltinHelper.Arg(args, 1);
            if (callback is not JsFunction callbackFn)
            {
                throw new Errors.JsTypeError("callback must be a function", ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);
            }

            var map = new JsMapObject { Prototype = realm.MapPrototype };
            if (iterable is JsArray arr)
            {
                for (var i = 0; i < arr.DenseLength; i++)
                {
                    var value = arr.GetIndex(i);
                    var key = callbackFn.Call(JsValue.Undefined, [value, JsNumber.Create(i)]);
                    var group = map.MapGet(key);
                    if (group is JsUndefined)
                    {
                        group = new JsArray { Prototype = realm.ArrayPrototype };
                        map.MapSet(key, group);
                    }

                    ((JsArray)group).Push(value);
                }
            }

            return map;
        }, 2);

        // Symbol.iterator => entries
        var entriesMethod = proto.Get("entries");
        proto.DefineSymbolProperty(JsSymbol.Iterator,
            PropertyDescriptor.Data(entriesMethod, writable: true, enumerable: false, configurable: true));

        // Symbol.toStringTag
        proto.DefineSymbolProperty(JsSymbol.ToStringTag,
            PropertyDescriptor.Data(new JsString("Map"), writable: false, enumerable: false, configurable: true));

        realm.InstallGlobal("Map", ctor);
    }

    private static JsMapObject RequireMap(JsValue value)
    {
        if (value is JsMapObject map)
        {
            return map;
        }

        throw new Errors.JsTypeError("Method requires that 'this' be a Map", ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);
    }
}

/// <summary>
/// Internal Map storage that uses SameValueZero comparison.
/// </summary>
internal sealed class JsMapObject : JsObject
{
    // Use a list-of-entries to preserve insertion order
    private readonly List<KeyValuePair<JsValue, JsValue>> _entries = [];

    public int MapSize => _entries.Count;

    public JsValue MapGet(JsValue key)
    {
        for (var i = 0; i < _entries.Count; i++)
        {
            if (SameValueZero(_entries[i].Key, key))
            {
                return _entries[i].Value;
            }
        }

        return Undefined;
    }

    public void MapSet(JsValue key, JsValue value)
    {
        for (var i = 0; i < _entries.Count; i++)
        {
            if (SameValueZero(_entries[i].Key, key))
            {
                _entries[i] = new KeyValuePair<JsValue, JsValue>(key, value);
                return;
            }
        }

        _entries.Add(new KeyValuePair<JsValue, JsValue>(key, value));
    }

    public bool MapHas(JsValue key)
    {
        for (var i = 0; i < _entries.Count; i++)
        {
            if (SameValueZero(_entries[i].Key, key))
            {
                return true;
            }
        }

        return false;
    }

    public bool MapDelete(JsValue key)
    {
        for (var i = 0; i < _entries.Count; i++)
        {
            if (SameValueZero(_entries[i].Key, key))
            {
                _entries.RemoveAt(i);
                return true;
            }
        }

        return false;
    }

    public void MapClear() => _entries.Clear();

    public IEnumerable<(JsValue Key, JsValue Value)> MapEntries()
    {
        // Snapshot to allow modification during iteration
        var snapshot = _entries.ToArray();
        foreach (var kvp in snapshot)
        {
            yield return (kvp.Key, kvp.Value);
        }
    }

    private static bool SameValueZero(JsValue x, JsValue y)
    {
        if (x is JsNumber xn && y is JsNumber yn)
        {
            if (double.IsNaN(xn.Value) && double.IsNaN(yn.Value))
            {
                return true;
            }

            // +0 and -0 are equal in SameValueZero
            // ReSharper disable once CompareOfFloatsByEqualityOperator
            return xn.Value == yn.Value;
        }

        return x.StrictEquals(y);
    }
}
