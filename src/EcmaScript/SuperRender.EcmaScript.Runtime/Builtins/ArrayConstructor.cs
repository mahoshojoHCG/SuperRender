namespace SuperRender.EcmaScript.Runtime.Builtins;

using System.Globalization;
using SuperRender.EcmaScript.Runtime;

public static class ArrayConstructor
{
    public static void Install(Realm realm)
    {
        var proto = realm.ArrayPrototype;

        var ctor = new JsFunction
        {
            Name = "Array",
            Length = 1,
            IsConstructor = true,
            Prototype = realm.FunctionPrototype,
            PrototypeObject = proto,
            CallTarget = (_, args) => ConstructArray(args, realm),
            ConstructTarget = args => ConstructArray(args, realm)
        };

        // --- Static methods ---

        BuiltinHelper.DefineMethod(ctor, "isArray", (_, args) =>
        {
            return BuiltinHelper.Arg(args, 0) is JsArray ? JsValue.True : JsValue.False;
        }, 1);

        BuiltinHelper.DefineMethod(ctor, "from", (_, args) =>
        {
            var arrayLike = BuiltinHelper.Arg(args, 0);
            var mapFn = BuiltinHelper.Arg(args, 1) as JsFunction;
            var result = new JsArray { Prototype = realm.ArrayPrototype };

            if (arrayLike is JsArray srcArr)
            {
                for (var i = 0; i < srcArr.DenseLength; i++)
                {
                    var val = srcArr.GetIndex(i);
                    result.Push(mapFn is not null ? mapFn.Call(JsValue.Undefined, [val, JsNumber.Create(i)]) : val);
                }
            }
            else if (arrayLike is JsString str)
            {
                for (var i = 0; i < str.Length; i++)
                {
                    JsValue val = new JsString(str.Value[i].ToString());
                    result.Push(mapFn is not null ? mapFn.Call(JsValue.Undefined, [val, JsNumber.Create(i)]) : val);
                }
            }
            else if (arrayLike is JsObject obj)
            {
                var len = BuiltinHelper.GetLength(obj);
                for (var i = 0; i < len; i++)
                {
                    var val = obj.Get(i.ToString(CultureInfo.InvariantCulture));
                    result.Push(mapFn is not null ? mapFn.Call(JsValue.Undefined, [val, JsNumber.Create(i)]) : val);
                }
            }

            return result;
        }, 1);

        BuiltinHelper.DefineMethod(ctor, "of", (_, args) =>
        {
            var result = new JsArray { Prototype = realm.ArrayPrototype };
            foreach (var arg in args)
            {
                result.Push(arg);
            }

            return result;
        }, 0);

        // --- Prototype methods ---

        BuiltinHelper.DefineProperty(proto, "constructor", ctor);

        BuiltinHelper.DefineMethod(proto, "push", (thisArg, args) =>
        {
            var arr = AsArray(thisArg);
            foreach (var arg in args)
            {
                arr.Push(arg);
            }

            return arr.Get("length");
        }, 1);

        BuiltinHelper.DefineMethod(proto, "pop", (thisArg, _) =>
        {
            var arr = AsArray(thisArg);
            return arr.Pop();
        }, 0);

        BuiltinHelper.DefineMethod(proto, "shift", (thisArg, _) =>
        {
            var arr = AsArray(thisArg);
            var len = arr.DenseLength;
            if (len == 0)
            {
                return JsValue.Undefined;
            }

            var first = arr.GetIndex(0);
            // Shift all elements down
            for (var i = 1; i < len; i++)
            {
                arr.Set(I(i - 1), arr.GetIndex(i));
            }

            arr.Set("length", JsNumber.Create(len - 1));
            return first;
        }, 0);

        BuiltinHelper.DefineMethod(proto, "unshift", (thisArg, args) =>
        {
            var arr = AsArray(thisArg);
            var len = arr.DenseLength;
            // Shift existing elements up
            for (var i = len - 1; i >= 0; i--)
            {
                arr.Set(I(i + args.Length), arr.GetIndex(i));
            }

            for (var i = 0; i < args.Length; i++)
            {
                arr.Set(I(i), args[i]);
            }

            return arr.Get("length");
        }, 1);

        BuiltinHelper.DefineMethod(proto, "splice", (thisArg, args) =>
        {
            var arr = AsArray(thisArg);
            var len = arr.DenseLength;
            var start = args.Length > 0 ? ToSpliceIndex((int)args[0].ToNumber(), len) : 0;
            var deleteCount = args.Length > 1 ? Math.Max(0, Math.Min((int)args[1].ToNumber(), len - start)) : len - start;
            var insertItems = args.Length > 2 ? args[2..] : Array.Empty<JsValue>();

            var removed = new JsArray { Prototype = realm.ArrayPrototype };
            for (var i = 0; i < deleteCount; i++)
            {
                removed.Push(arr.GetIndex(start + i));
            }

            var diff = insertItems.Length - deleteCount;
            if (diff > 0)
            {
                for (var i = len - 1; i >= start + deleteCount; i--)
                {
                    arr.Set(I(i + diff), arr.GetIndex(i));
                }
            }
            else if (diff < 0)
            {
                for (var i = start + deleteCount; i < len; i++)
                {
                    arr.Set(I(i + diff), arr.GetIndex(i));
                }

                arr.Set("length", JsNumber.Create(len + diff));
            }

            for (var i = 0; i < insertItems.Length; i++)
            {
                arr.Set(I(start + i), insertItems[i]);
            }

            return removed;
        }, 2);

        BuiltinHelper.DefineMethod(proto, "slice", (thisArg, args) =>
        {
            var arr = AsArray(thisArg);
            var len = arr.DenseLength;
            var start = args.Length > 0 ? NormalizeIndex((int)args[0].ToNumber(), len) : 0;
            var end = args.Length > 1 && args[1] is not JsUndefined ? NormalizeIndex((int)args[1].ToNumber(), len) : len;
            var result = new JsArray { Prototype = realm.ArrayPrototype };
            for (var i = start; i < end; i++)
            {
                result.Push(arr.GetIndex(i));
            }

            return result;
        }, 2);

        BuiltinHelper.DefineMethod(proto, "concat", (thisArg, args) =>
        {
            var result = new JsArray { Prototype = realm.ArrayPrototype };
            SpreadInto(result, thisArg);
            foreach (var arg in args)
            {
                SpreadInto(result, arg);
            }

            return result;
        }, 1);

        BuiltinHelper.DefineMethod(proto, "join", (thisArg, args) =>
        {
            var arr = AsArray(thisArg);
            var sep = args.Length > 0 && args[0] is not JsUndefined ? args[0].ToJsString() : ",";
            var parts = new string[arr.DenseLength];
            for (var i = 0; i < arr.DenseLength; i++)
            {
                var val = arr.GetIndex(i);
                parts[i] = val is JsUndefined or JsNull ? "" : val.ToJsString();
            }

            return new JsString(string.Join(sep, parts));
        }, 1);

        BuiltinHelper.DefineMethod(proto, "reverse", (thisArg, _) =>
        {
            var arr = AsArray(thisArg);
            var len = arr.DenseLength;
            for (var i = 0; i < len / 2; i++)
            {
                var j = len - 1 - i;
                var tmp = arr.GetIndex(i);
                arr.Set(I(i), arr.GetIndex(j));
                arr.Set(I(j), tmp);
            }

            return arr;
        }, 0);

        BuiltinHelper.DefineMethod(proto, "sort", (thisArg, args) =>
        {
            var arr = AsArray(thisArg);
            var len = arr.DenseLength;
            var compareFn = args.Length > 0 ? args[0] as JsFunction : null;

            // Extract values
            var values = new JsValue[len];
            for (var i = 0; i < len; i++)
            {
                values[i] = arr.GetIndex(i);
            }

            Array.Sort(values, (a, b) =>
            {
                if (a is JsUndefined && b is JsUndefined) return 0;
                if (a is JsUndefined) return 1;
                if (b is JsUndefined) return -1;

                if (compareFn is not null)
                {
                    var result = compareFn.Call(JsValue.Undefined, [a, b]).ToNumber();
                    if (double.IsNaN(result)) return 0;
                    return result < 0 ? -1 : result > 0 ? 1 : 0;
                }

                return string.Compare(a.ToJsString(), b.ToJsString(), StringComparison.Ordinal);
            });

            for (var i = 0; i < len; i++)
            {
                arr.Set(I(i), values[i]);
            }

            return arr;
        }, 1);

        BuiltinHelper.DefineMethod(proto, "indexOf", (thisArg, args) =>
        {
            var arr = AsArray(thisArg);
            var searchElement = BuiltinHelper.Arg(args, 0);
            var fromIndex = args.Length > 1 ? (int)args[1].ToNumber() : 0;
            if (fromIndex < 0) fromIndex = Math.Max(0, arr.DenseLength + fromIndex);
            for (var i = fromIndex; i < arr.DenseLength; i++)
            {
                if (searchElement.StrictEquals(arr.GetIndex(i)))
                {
                    return JsNumber.Create(i);
                }
            }

            return JsNumber.Create(-1);
        }, 1);

        BuiltinHelper.DefineMethod(proto, "lastIndexOf", (thisArg, args) =>
        {
            var arr = AsArray(thisArg);
            var searchElement = BuiltinHelper.Arg(args, 0);
            var fromIndex = args.Length > 1 ? (int)args[1].ToNumber() : arr.DenseLength - 1;
            if (fromIndex < 0) fromIndex = arr.DenseLength + fromIndex;
            for (var i = Math.Min(fromIndex, arr.DenseLength - 1); i >= 0; i--)
            {
                if (searchElement.StrictEquals(arr.GetIndex(i)))
                {
                    return JsNumber.Create(i);
                }
            }

            return JsNumber.Create(-1);
        }, 1);

        BuiltinHelper.DefineMethod(proto, "includes", (thisArg, args) =>
        {
            var arr = AsArray(thisArg);
            var searchElement = BuiltinHelper.Arg(args, 0);
            var fromIndex = args.Length > 1 ? (int)args[1].ToNumber() : 0;
            if (fromIndex < 0) fromIndex = Math.Max(0, arr.DenseLength + fromIndex);
            for (var i = fromIndex; i < arr.DenseLength; i++)
            {
                var elem = arr.GetIndex(i);
                // includes uses SameValueZero (NaN === NaN, +0 === -0)
                if (searchElement is JsNumber sn && elem is JsNumber en)
                {
                    if (double.IsNaN(sn.Value) && double.IsNaN(en.Value))
                    {
                        return JsValue.True;
                    }

                    // ReSharper disable once CompareOfFloatsByEqualityOperator
                    if (sn.Value == en.Value)
                    {
                        return JsValue.True;
                    }
                }
                else if (searchElement.StrictEquals(elem))
                {
                    return JsValue.True;
                }
            }

            return JsValue.False;
        }, 1);

        BuiltinHelper.DefineMethod(proto, "find", (thisArg, args) =>
        {
            var arr = AsArray(thisArg);
            var callback = RequireFunction(BuiltinHelper.Arg(args, 0));
            var thisArgCb = BuiltinHelper.Arg(args, 1);
            for (var i = 0; i < arr.DenseLength; i++)
            {
                var val = arr.GetIndex(i);
                if (callback.Call(thisArgCb, [val, JsNumber.Create(i), arr]).ToBoolean())
                {
                    return val;
                }
            }

            return JsValue.Undefined;
        }, 1);

        BuiltinHelper.DefineMethod(proto, "findIndex", (thisArg, args) =>
        {
            var arr = AsArray(thisArg);
            var callback = RequireFunction(BuiltinHelper.Arg(args, 0));
            var thisArgCb = BuiltinHelper.Arg(args, 1);
            for (var i = 0; i < arr.DenseLength; i++)
            {
                var val = arr.GetIndex(i);
                if (callback.Call(thisArgCb, [val, JsNumber.Create(i), arr]).ToBoolean())
                {
                    return JsNumber.Create(i);
                }
            }

            return JsNumber.Create(-1);
        }, 1);

        BuiltinHelper.DefineMethod(proto, "filter", (thisArg, args) =>
        {
            var arr = AsArray(thisArg);
            var callback = RequireFunction(BuiltinHelper.Arg(args, 0));
            var thisArgCb = BuiltinHelper.Arg(args, 1);
            var result = new JsArray { Prototype = realm.ArrayPrototype };
            for (var i = 0; i < arr.DenseLength; i++)
            {
                var val = arr.GetIndex(i);
                if (callback.Call(thisArgCb, [val, JsNumber.Create(i), arr]).ToBoolean())
                {
                    result.Push(val);
                }
            }

            return result;
        }, 1);

        BuiltinHelper.DefineMethod(proto, "map", (thisArg, args) =>
        {
            var arr = AsArray(thisArg);
            var callback = RequireFunction(BuiltinHelper.Arg(args, 0));
            var thisArgCb = BuiltinHelper.Arg(args, 1);
            var result = new JsArray { Prototype = realm.ArrayPrototype };
            for (var i = 0; i < arr.DenseLength; i++)
            {
                var val = arr.GetIndex(i);
                result.Push(callback.Call(thisArgCb, [val, JsNumber.Create(i), arr]));
            }

            return result;
        }, 1);

        BuiltinHelper.DefineMethod(proto, "reduce", (thisArg, args) =>
        {
            var arr = AsArray(thisArg);
            var callback = RequireFunction(BuiltinHelper.Arg(args, 0));
            var len = arr.DenseLength;
            int startIndex;
            JsValue accumulator;

            if (args.Length > 1)
            {
                accumulator = args[1];
                startIndex = 0;
            }
            else
            {
                if (len == 0) throw new Errors.JsTypeError("Reduce of empty array with no initial value");
                accumulator = arr.GetIndex(0);
                startIndex = 1;
            }

            for (var i = startIndex; i < len; i++)
            {
                accumulator = callback.Call(JsValue.Undefined, [accumulator, arr.GetIndex(i), JsNumber.Create(i), arr]);
            }

            return accumulator;
        }, 1);

        BuiltinHelper.DefineMethod(proto, "reduceRight", (thisArg, args) =>
        {
            var arr = AsArray(thisArg);
            var callback = RequireFunction(BuiltinHelper.Arg(args, 0));
            var len = arr.DenseLength;
            int startIndex;
            JsValue accumulator;

            if (args.Length > 1)
            {
                accumulator = args[1];
                startIndex = len - 1;
            }
            else
            {
                if (len == 0) throw new Errors.JsTypeError("Reduce of empty array with no initial value");
                accumulator = arr.GetIndex(len - 1);
                startIndex = len - 2;
            }

            for (var i = startIndex; i >= 0; i--)
            {
                accumulator = callback.Call(JsValue.Undefined, [accumulator, arr.GetIndex(i), JsNumber.Create(i), arr]);
            }

            return accumulator;
        }, 1);

        BuiltinHelper.DefineMethod(proto, "forEach", (thisArg, args) =>
        {
            var arr = AsArray(thisArg);
            var callback = RequireFunction(BuiltinHelper.Arg(args, 0));
            var thisArgCb = BuiltinHelper.Arg(args, 1);
            for (var i = 0; i < arr.DenseLength; i++)
            {
                callback.Call(thisArgCb, [arr.GetIndex(i), JsNumber.Create(i), arr]);
            }

            return JsValue.Undefined;
        }, 1);

        BuiltinHelper.DefineMethod(proto, "some", (thisArg, args) =>
        {
            var arr = AsArray(thisArg);
            var callback = RequireFunction(BuiltinHelper.Arg(args, 0));
            var thisArgCb = BuiltinHelper.Arg(args, 1);
            for (var i = 0; i < arr.DenseLength; i++)
            {
                if (callback.Call(thisArgCb, [arr.GetIndex(i), JsNumber.Create(i), arr]).ToBoolean())
                {
                    return JsValue.True;
                }
            }

            return JsValue.False;
        }, 1);

        BuiltinHelper.DefineMethod(proto, "every", (thisArg, args) =>
        {
            var arr = AsArray(thisArg);
            var callback = RequireFunction(BuiltinHelper.Arg(args, 0));
            var thisArgCb = BuiltinHelper.Arg(args, 1);
            for (var i = 0; i < arr.DenseLength; i++)
            {
                if (!callback.Call(thisArgCb, [arr.GetIndex(i), JsNumber.Create(i), arr]).ToBoolean())
                {
                    return JsValue.False;
                }
            }

            return JsValue.True;
        }, 1);

        BuiltinHelper.DefineMethod(proto, "flat", (thisArg, args) =>
        {
            var arr = AsArray(thisArg);
            var depth = args.Length > 0 && args[0] is not JsUndefined ? (int)args[0].ToNumber() : 1;
            var result = new JsArray { Prototype = realm.ArrayPrototype };
            FlattenInto(result, arr, depth);
            return result;
        }, 0);

        BuiltinHelper.DefineMethod(proto, "flatMap", (thisArg, args) =>
        {
            var arr = AsArray(thisArg);
            var callback = RequireFunction(BuiltinHelper.Arg(args, 0));
            var thisArgCb = BuiltinHelper.Arg(args, 1);
            var result = new JsArray { Prototype = realm.ArrayPrototype };

            for (var i = 0; i < arr.DenseLength; i++)
            {
                var mapped = callback.Call(thisArgCb, [arr.GetIndex(i), JsNumber.Create(i), arr]);
                if (mapped is JsArray mappedArr)
                {
                    for (var j = 0; j < mappedArr.DenseLength; j++)
                    {
                        result.Push(mappedArr.GetIndex(j));
                    }
                }
                else
                {
                    result.Push(mapped);
                }
            }

            return result;
        }, 1);

        BuiltinHelper.DefineMethod(proto, "fill", (thisArg, args) =>
        {
            var arr = AsArray(thisArg);
            var value = BuiltinHelper.Arg(args, 0);
            var len = arr.DenseLength;
            var start = args.Length > 1 ? NormalizeIndex((int)args[1].ToNumber(), len) : 0;
            var end = args.Length > 2 && args[2] is not JsUndefined ? NormalizeIndex((int)args[2].ToNumber(), len) : len;

            for (var i = start; i < end; i++)
            {
                arr.Set(I(i), value);
            }

            return arr;
        }, 1);

        BuiltinHelper.DefineMethod(proto, "at", (thisArg, args) =>
        {
            var arr = AsArray(thisArg);
            var idx = (int)BuiltinHelper.Arg(args, 0).ToNumber();
            if (idx < 0) idx = arr.DenseLength + idx;
            if (idx < 0 || idx >= arr.DenseLength)
            {
                return JsValue.Undefined;
            }

            return arr.GetIndex(idx);
        }, 1);

        BuiltinHelper.DefineMethod(proto, "keys", (thisArg, _) =>
        {
            var arr = AsArray(thisArg);
            var items = new List<JsValue>();
            for (var i = 0; i < arr.DenseLength; i++)
            {
                items.Add(JsNumber.Create(i));
            }

            return BuiltinHelper.CreateListIterator(items, realm);
        }, 0);

        BuiltinHelper.DefineMethod(proto, "values", (thisArg, _) =>
        {
            var arr = AsArray(thisArg);
            var items = new List<JsValue>();
            for (var i = 0; i < arr.DenseLength; i++)
            {
                items.Add(arr.GetIndex(i));
            }

            return BuiltinHelper.CreateListIterator(items, realm);
        }, 0);

        BuiltinHelper.DefineMethod(proto, "entries", (thisArg, _) =>
        {
            var arr = AsArray(thisArg);
            var items = new List<JsValue>();
            for (var i = 0; i < arr.DenseLength; i++)
            {
                var entry = new JsArray { Prototype = realm.ArrayPrototype };
                entry.Push(JsNumber.Create(i));
                entry.Push(arr.GetIndex(i));
                items.Add(entry);
            }

            return BuiltinHelper.CreateListIterator(items, realm);
        }, 0);

        BuiltinHelper.DefineMethod(proto, "toString", (thisArg, _) =>
        {
            if (thisArg is not JsObject obj)
            {
                return new JsString("[object Undefined]");
            }

            var joinFn = obj.Get("join");
            if (joinFn is JsFunction fn)
            {
                return fn.Call(obj, []);
            }

            return new JsString("[object Array]");
        }, 0);

        // Symbol.iterator points to the values method
        var valuesMethod = proto.Get("values");
        proto.DefineSymbolProperty(JsSymbol.Iterator,
            PropertyDescriptor.Data(valuesMethod, writable: true, enumerable: false, configurable: true));

        realm.InstallGlobal("Array", ctor);
    }

    private static JsArray ConstructArray(JsValue[] args, Realm realm)
    {
        var arr = new JsArray { Prototype = realm.ArrayPrototype };

        if (args.Length == 1 && args[0] is JsNumber num)
        {
            var len = (int)num.Value;
            if (len < 0 || len != num.Value)
            {
                throw new Errors.JsRangeError("Invalid array length");
            }

            arr.Set("length", JsNumber.Create(len));
        }
        else
        {
            foreach (var arg in args)
            {
                arr.Push(arg);
            }
        }

        return arr;
    }

    private static JsArray AsArray(JsValue value)
    {
        if (value is JsArray arr)
        {
            return arr;
        }

        throw new Errors.JsTypeError("Array method called on non-array");
    }

    private static JsFunction RequireFunction(JsValue value)
    {
        if (value is JsFunction fn)
        {
            return fn;
        }

        throw new Errors.JsTypeError(value.ToJsString() + " is not a function");
    }

    private static string I(int index) => index.ToString(CultureInfo.InvariantCulture);

    private static int NormalizeIndex(int index, int length)
    {
        if (index < 0) return Math.Max(0, length + index);
        return Math.Min(index, length);
    }

    private static int ToSpliceIndex(int index, int length)
    {
        if (index < 0) return Math.Max(0, length + index);
        return Math.Min(index, length);
    }

    private static void SpreadInto(JsArray target, JsValue value)
    {
        if (value is JsArray arr)
        {
            for (var i = 0; i < arr.DenseLength; i++)
            {
                target.Push(arr.GetIndex(i));
            }
        }
        else
        {
            target.Push(value);
        }
    }

    private static void FlattenInto(JsArray target, JsArray source, int depth)
    {
        for (var i = 0; i < source.DenseLength; i++)
        {
            var element = source.GetIndex(i);
            if (depth > 0 && element is JsArray innerArr)
            {
                FlattenInto(target, innerArr, depth - 1);
            }
            else
            {
                target.Push(element);
            }
        }
    }
}
