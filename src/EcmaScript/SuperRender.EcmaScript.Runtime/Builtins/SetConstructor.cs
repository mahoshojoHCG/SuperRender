namespace SuperRender.EcmaScript.Runtime.Builtins;

using SuperRender.EcmaScript.Runtime;

[JsGlobalInstall("Set")]
public sealed partial class SetConstructor
{
    private static void __Install(Realm realm)
    {
        var proto = realm.SetPrototype;

        var ctor = new JsFunction
        {
            Name = "Set",
            Length = 0,
            IsConstructor = true,
            Prototype = realm.FunctionPrototype,
            PrototypeObject = proto,
            ConstructTarget = args =>
            {
                var set = new JsSetObject { Prototype = realm.SetPrototype };
                var iterable = BuiltinHelper.Arg(args, 0);
                if (iterable is not JsUndefined and not JsNull)
                {
                    if (iterable is JsArray arr)
                    {
                        for (var i = 0; i < arr.DenseLength; i++)
                        {
                            set.SetAdd(arr.GetIndex(i));
                        }
                    }
                }

                return set;
            },
            CallTarget = (_, _) =>
            {
                throw new Errors.JsTypeError("Constructor Set requires 'new'", ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);
            }
        };

        // Prototype methods
        BuiltinHelper.DefineProperty(proto, "constructor", ctor);

        BuiltinHelper.DefineMethod(proto, "add", (thisArg, args) =>
        {
            var set = RequireSet(thisArg);
            set.SetAdd(BuiltinHelper.Arg(args, 0));
            return thisArg;
        }, 1);

        BuiltinHelper.DefineMethod(proto, "has", (thisArg, args) =>
        {
            var set = RequireSet(thisArg);
            return set.SetHas(BuiltinHelper.Arg(args, 0)) ? JsValue.True : JsValue.False;
        }, 1);

        BuiltinHelper.DefineMethod(proto, "delete", (thisArg, args) =>
        {
            var set = RequireSet(thisArg);
            return set.SetDelete(BuiltinHelper.Arg(args, 0)) ? JsValue.True : JsValue.False;
        }, 1);

        BuiltinHelper.DefineMethod(proto, "clear", (thisArg, _) =>
        {
            var set = RequireSet(thisArg);
            set.SetClear();
            return JsValue.Undefined;
        }, 0);

        BuiltinHelper.DefineGetter(proto, "size", (thisArg, _) =>
        {
            var set = RequireSet(thisArg);
            return JsNumber.Create(set.SetSize);
        });

        BuiltinHelper.DefineMethod(proto, "forEach", (thisArg, args) =>
        {
            var set = RequireSet(thisArg);
            var callback = args.Length > 0 && args[0] is JsFunction fn
                ? fn
                : throw new Errors.JsTypeError("forEach callback must be a function", ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);
            var thisArgCb = BuiltinHelper.Arg(args, 1);

            foreach (var value in set.SetValues())
            {
                callback.Call(thisArgCb, [value, value, thisArg]);
            }

            return JsValue.Undefined;
        }, 1);

        BuiltinHelper.DefineMethod(proto, "entries", (thisArg, _) =>
        {
            var set = RequireSet(thisArg);
            var items = new List<JsValue>();
            foreach (var value in set.SetValues())
            {
                var entry = new JsArray { Prototype = realm.ArrayPrototype };
                entry.Push(value);
                entry.Push(value);
                items.Add(entry);
            }

            return BuiltinHelper.CreateListIterator(items, realm);
        }, 0);

        BuiltinHelper.DefineMethod(proto, "keys", (thisArg, _) =>
        {
            var set = RequireSet(thisArg);
            return BuiltinHelper.CreateListIterator(set.SetValues().ToList(), realm);
        }, 0);

        BuiltinHelper.DefineMethod(proto, "values", (thisArg, _) =>
        {
            var set = RequireSet(thisArg);
            return BuiltinHelper.CreateListIterator(set.SetValues().ToList(), realm);
        }, 0);

        // Symbol.iterator => values
        var valuesMethod = proto.Get("values");
        proto.DefineSymbolProperty(JsSymbol.Iterator,
            PropertyDescriptor.Data(valuesMethod, writable: true, enumerable: false, configurable: true));

        // ES2025 Set methods
        BuiltinHelper.DefineMethod(proto, "union", (thisArg, args) =>
        {
            var set = RequireSet(thisArg);
            var other = RequireSet(BuiltinHelper.Arg(args, 0));
            var result = new JsSetObject { Prototype = realm.SetPrototype };
            foreach (var v in set.SetValues()) result.SetAdd(v);
            foreach (var v in other.SetValues()) result.SetAdd(v);
            return result;
        }, 1);

        BuiltinHelper.DefineMethod(proto, "intersection", (thisArg, args) =>
        {
            var set = RequireSet(thisArg);
            var other = RequireSet(BuiltinHelper.Arg(args, 0));
            var result = new JsSetObject { Prototype = realm.SetPrototype };
            foreach (var v in set.SetValues())
            {
                if (other.SetHas(v)) result.SetAdd(v);
            }

            return result;
        }, 1);

        BuiltinHelper.DefineMethod(proto, "difference", (thisArg, args) =>
        {
            var set = RequireSet(thisArg);
            var other = RequireSet(BuiltinHelper.Arg(args, 0));
            var result = new JsSetObject { Prototype = realm.SetPrototype };
            foreach (var v in set.SetValues())
            {
                if (!other.SetHas(v)) result.SetAdd(v);
            }

            return result;
        }, 1);

        BuiltinHelper.DefineMethod(proto, "symmetricDifference", (thisArg, args) =>
        {
            var set = RequireSet(thisArg);
            var other = RequireSet(BuiltinHelper.Arg(args, 0));
            var result = new JsSetObject { Prototype = realm.SetPrototype };
            foreach (var v in set.SetValues())
            {
                if (!other.SetHas(v)) result.SetAdd(v);
            }

            foreach (var v in other.SetValues())
            {
                if (!set.SetHas(v)) result.SetAdd(v);
            }

            return result;
        }, 1);

        BuiltinHelper.DefineMethod(proto, "isSubsetOf", (thisArg, args) =>
        {
            var set = RequireSet(thisArg);
            var other = RequireSet(BuiltinHelper.Arg(args, 0));
            foreach (var v in set.SetValues())
            {
                if (!other.SetHas(v)) return JsValue.False;
            }

            return JsValue.True;
        }, 1);

        BuiltinHelper.DefineMethod(proto, "isSupersetOf", (thisArg, args) =>
        {
            var set = RequireSet(thisArg);
            var other = RequireSet(BuiltinHelper.Arg(args, 0));
            foreach (var v in other.SetValues())
            {
                if (!set.SetHas(v)) return JsValue.False;
            }

            return JsValue.True;
        }, 1);

        BuiltinHelper.DefineMethod(proto, "isDisjointFrom", (thisArg, args) =>
        {
            var set = RequireSet(thisArg);
            var other = RequireSet(BuiltinHelper.Arg(args, 0));
            foreach (var v in set.SetValues())
            {
                if (other.SetHas(v)) return JsValue.False;
            }

            return JsValue.True;
        }, 1);

        // Symbol.toStringTag
        proto.DefineSymbolProperty(JsSymbol.ToStringTag,
            PropertyDescriptor.Data(new JsString("Set"), writable: false, enumerable: false, configurable: true));

        realm.InstallGlobal("Set", ctor);
    }

    private static JsSetObject RequireSet(JsValue value)
    {
        if (value is JsSetObject set)
        {
            return set;
        }

        throw new Errors.JsTypeError("Method requires that 'this' be a Set", ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);
    }
}

/// <summary>
/// Internal Set storage that uses SameValueZero comparison and preserves insertion order.
/// </summary>
internal sealed class JsSetObject : JsObject
{
    private readonly List<JsValue> _values = [];

    public int SetSize => _values.Count;

    public void SetAdd(JsValue value)
    {
        if (!SetHas(value))
        {
            _values.Add(value);
        }
    }

    public bool SetHas(JsValue value)
    {
        for (var i = 0; i < _values.Count; i++)
        {
            if (SameValueZero(_values[i], value))
            {
                return true;
            }
        }

        return false;
    }

    public bool SetDelete(JsValue value)
    {
        for (var i = 0; i < _values.Count; i++)
        {
            if (SameValueZero(_values[i], value))
            {
                _values.RemoveAt(i);
                return true;
            }
        }

        return false;
    }

    public void SetClear() => _values.Clear();

    public IEnumerable<JsValue> SetValues()
    {
        var snapshot = _values.ToArray();
        foreach (var val in snapshot)
        {
            yield return val;
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

            // ReSharper disable once CompareOfFloatsByEqualityOperator
            return xn.Value == yn.Value;
        }

        return x.StrictEquals(y);
    }
}
