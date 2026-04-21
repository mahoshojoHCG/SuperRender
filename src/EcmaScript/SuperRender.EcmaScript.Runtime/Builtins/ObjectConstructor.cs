namespace SuperRender.EcmaScript.Runtime.Builtins;

using SuperRender.EcmaScript.Runtime;

public sealed class ObjectConstructor : IJsInstallable
{
    public static void Install(Realm realm)
    {
        var proto = realm.ObjectPrototype;
        var ctor = realm.ObjectConstructorFn;

        // --- Static methods on Object ---

        BuiltinHelper.DefineMethod(ctor, "keys", (_, args) =>
        {
            var obj = RequireObject(BuiltinHelper.Arg(args, 0));
            var result = new JsArray();
            foreach (var key in obj.OwnPropertyKeys())
            {
                var desc = obj.GetOwnProperty(key);
                if (desc?.Enumerable == true)
                {
                    result.Push(new JsString(key));
                }
            }

            return result;
        }, 1);

        BuiltinHelper.DefineMethod(ctor, "values", (_, args) =>
        {
            var obj = RequireObject(BuiltinHelper.Arg(args, 0));
            var result = new JsArray();
            foreach (var key in obj.OwnPropertyKeys())
            {
                var desc = obj.GetOwnProperty(key);
                if (desc?.Enumerable == true)
                {
                    result.Push(obj.Get(key));
                }
            }

            return result;
        }, 1);

        BuiltinHelper.DefineMethod(ctor, "entries", (_, args) =>
        {
            var obj = RequireObject(BuiltinHelper.Arg(args, 0));
            var result = new JsArray();
            foreach (var key in obj.OwnPropertyKeys())
            {
                var desc = obj.GetOwnProperty(key);
                if (desc?.Enumerable == true)
                {
                    var entry = new JsArray();
                    entry.Push(new JsString(key));
                    entry.Push(obj.Get(key));
                    result.Push(entry);
                }
            }

            return result;
        }, 1);

        BuiltinHelper.DefineMethod(ctor, "assign", (_, args) =>
        {
            var target = RequireObject(BuiltinHelper.Arg(args, 0));
            for (var i = 1; i < args.Length; i++)
            {
                if (args[i] is JsNull or JsUndefined)
                {
                    continue;
                }

                var source = RequireObject(args[i]);
                foreach (var key in source.OwnPropertyKeys())
                {
                    var desc = source.GetOwnProperty(key);
                    if (desc?.Enumerable == true)
                    {
                        target.Set(key, source.Get(key));
                    }
                }
            }

            return target;
        }, 2);

        BuiltinHelper.DefineMethod(ctor, "freeze", (_, args) =>
        {
            var val = BuiltinHelper.Arg(args, 0);
            if (val is not JsDynamicObject obj)
            {
                return val;
            }

            obj.Extensible = false;
            foreach (var key in obj.OwnPropertyKeys())
            {
                var desc = obj.GetOwnProperty(key);
                if (desc is null)
                {
                    continue;
                }

                if (desc.IsDataDescriptor)
                {
                    obj.DefineOwnProperty(key, PropertyDescriptor.Data(
                        desc.Value ?? JsValue.Undefined, writable: false, enumerable: desc.Enumerable ?? true, configurable: false));
                }
                else
                {
                    obj.DefineOwnProperty(key, PropertyDescriptor.Accessor(
                        desc.Get, desc.Set, enumerable: desc.Enumerable ?? true, configurable: false));
                }
            }

            return obj;
        }, 1);

        BuiltinHelper.DefineMethod(ctor, "seal", (_, args) =>
        {
            var val = BuiltinHelper.Arg(args, 0);
            if (val is not JsDynamicObject obj)
            {
                return val;
            }

            obj.Extensible = false;
            foreach (var key in obj.OwnPropertyKeys())
            {
                var desc = obj.GetOwnProperty(key);
                if (desc is null)
                {
                    continue;
                }

                if (desc.IsDataDescriptor)
                {
                    obj.DefineOwnProperty(key, PropertyDescriptor.Data(
                        desc.Value ?? JsValue.Undefined, writable: desc.Writable ?? true, enumerable: desc.Enumerable ?? true, configurable: false));
                }
                else
                {
                    obj.DefineOwnProperty(key, PropertyDescriptor.Accessor(
                        desc.Get, desc.Set, enumerable: desc.Enumerable ?? true, configurable: false));
                }
            }

            return obj;
        }, 1);

        BuiltinHelper.DefineMethod(ctor, "create", (_, args) =>
        {
            var protoArg = BuiltinHelper.Arg(args, 0);
            JsObject? protoObj = null;
            if (protoArg is JsObject p)
            {
                protoObj = p;
            }
            else if (protoArg is not JsNull)
            {
                throw new Errors.JsTypeError("Object prototype may only be an Object or null", ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);
            }

            var obj = new JsDynamicObject { Prototype = protoObj };

            if (args.Length > 1 && args[1] is JsObject props)
            {
                DefinePropertiesFromDescriptors(obj, props);
            }

            return obj;
        }, 2);

        BuiltinHelper.DefineMethod(ctor, "defineProperty", (_, args) =>
        {
            var target = RequireObject(BuiltinHelper.Arg(args, 0));
            if (target is not JsDynamicObject obj)
            {
                throw new Errors.JsTypeError("Object.defineProperty called on non-object", ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);
            }

            var prop = BuiltinHelper.Arg(args, 1).ToJsString();
            var descObj = RequireObject(BuiltinHelper.Arg(args, 2));
            var desc = ToPropertyDescriptor(descObj);
            obj.DefineOwnProperty(prop, desc);
            return obj;
        }, 3);

        BuiltinHelper.DefineMethod(ctor, "getOwnPropertyDescriptor", (_, args) =>
        {
            var obj = RequireObject(BuiltinHelper.Arg(args, 0));
            var prop = BuiltinHelper.Arg(args, 1).ToJsString();
            var desc = obj.GetOwnProperty(prop);
            if (desc is null)
            {
                return JsValue.Undefined;
            }

            return FromPropertyDescriptor(desc);
        }, 2);

        BuiltinHelper.DefineMethod(ctor, "getOwnPropertyNames", (_, args) =>
        {
            var obj = RequireObject(BuiltinHelper.Arg(args, 0));
            var result = new JsArray();
            foreach (var key in obj.OwnPropertyKeys())
            {
                result.Push(new JsString(key));
            }

            return result;
        }, 1);

        BuiltinHelper.DefineMethod(ctor, "getPrototypeOf", (_, args) =>
        {
            var obj = RequireObject(BuiltinHelper.Arg(args, 0));
            return (JsValue?)obj.Prototype ?? JsValue.Null;
        }, 1);

        BuiltinHelper.DefineMethod(ctor, "setPrototypeOf", (_, args) =>
        {
            var obj = RequireObject(BuiltinHelper.Arg(args, 0));
            var protoArg = BuiltinHelper.Arg(args, 1);

            if (protoArg is JsNull)
            {
                obj.Prototype = null;
            }
            else if (protoArg is JsObject p)
            {
                obj.Prototype = p;
            }
            else
            {
                throw new Errors.JsTypeError("Object prototype may only be an Object or null", ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);
            }

            return obj;
        }, 2);

        BuiltinHelper.DefineMethod(ctor, "is", (_, args) =>
        {
            var x = BuiltinHelper.Arg(args, 0);
            var y = BuiltinHelper.Arg(args, 1);
            return SameValue(x, y) ? JsValue.True : JsValue.False;
        }, 2);

        BuiltinHelper.DefineMethod(ctor, "groupBy", (_, args) =>
        {
            var iterable = BuiltinHelper.Arg(args, 0);
            var callback = BuiltinHelper.Arg(args, 1);
            if (callback is not JsFunction callbackFn)
            {
                throw new Errors.JsTypeError("callback must be a function", ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);
            }

            var result = new JsDynamicObject { Prototype = null };
            if (iterable is JsArray arr)
            {
                for (var i = 0; i < arr.DenseLength; i++)
                {
                    var value = arr.GetIndex(i);
                    var key = callbackFn.Call(JsValue.Undefined, [value, JsNumber.Create(i)]).ToJsString();
                    var group = result.Get(key);
                    if (group is JsUndefined)
                    {
                        group = new JsArray();
                        result.Set(key, group);
                    }

                    ((JsArray)group).Push(value);
                }
            }

            return result;
        }, 2);

        // --- Prototype methods ---

        BuiltinHelper.DefineMethod(proto, "hasOwnProperty", (thisArg, args) =>
        {
            if (thisArg is not JsObject obj)
            {
                return JsValue.False;
            }

            var prop = BuiltinHelper.Arg(args, 0).ToJsString();
            return obj.GetOwnProperty(prop) is not null ? JsValue.True : JsValue.False;
        }, 1);

        BuiltinHelper.DefineMethod(proto, "toString", (thisArg, _) =>
        {
            if (thisArg is JsUndefined)
            {
                return new JsString("[object Undefined]");
            }

            if (thisArg is JsNull)
            {
                return new JsString("[object Null]");
            }

            if (thisArg is JsObject obj && obj.TryGetSymbolProperty(JsSymbol.ToStringTag, out var tag) && tag is JsString tagStr)
            {
                return new JsString("[object " + tagStr.Value + "]");
            }

            var typeName = thisArg switch
            {
                JsArray => "Array",
                JsFunction => "Function",
                JsRegExp => "RegExp",
                JsObject => "Object",
                JsBoolean => "Boolean",
                JsNumber => "Number",
                JsString => "String",
                JsSymbol => "Symbol",
                _ => "Object"
            };

            return new JsString("[object " + typeName + "]");
        }, 0);

        BuiltinHelper.DefineMethod(proto, "valueOf", (thisArg, _) =>
        {
            return thisArg;
        }, 0);

        BuiltinHelper.DefineMethod(proto, "isPrototypeOf", (thisArg, args) =>
        {
            if (thisArg is not JsObject protoObj)
            {
                return JsValue.False;
            }

            var target = BuiltinHelper.Arg(args, 0);
            if (target is not JsObject targetObj)
            {
                return JsValue.False;
            }

            var current = targetObj.Prototype;
            while (current is not null)
            {
                if (ReferenceEquals(current, protoObj))
                {
                    return JsValue.True;
                }

                current = current.Prototype;
            }

            return JsValue.False;
        }, 1);

        BuiltinHelper.DefineMethod(proto, "propertyIsEnumerable", (thisArg, args) =>
        {
            if (thisArg is not JsObject obj)
            {
                return JsValue.False;
            }

            var prop = BuiltinHelper.Arg(args, 0).ToJsString();
            var desc = obj.GetOwnProperty(prop);
            return desc?.Enumerable == true ? JsValue.True : JsValue.False;
        }, 1);

        BuiltinHelper.DefineProperty(proto, "constructor", ctor);

        realm.InstallGlobal("Object", ctor);
    }

    private static JsObject RequireObject(JsValue value)
    {
        if (value is JsObject obj)
        {
            return obj;
        }

        throw new Errors.JsTypeError("Cannot convert " + value.TypeOf + " to object", ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);
    }

    private static bool SameValue(JsValue x, JsValue y)
    {
        if (x is JsNumber xn && y is JsNumber yn)
        {
            if (double.IsNaN(xn.Value) && double.IsNaN(yn.Value))
            {
                return true;
            }

            // Distinguish +0 and -0
            if (xn.Value == 0 && yn.Value == 0)
            {
                return double.IsNegative(xn.Value) == double.IsNegative(yn.Value);
            }

            // ReSharper disable once CompareOfFloatsByEqualityOperator
            return xn.Value == yn.Value;
        }

        return x.StrictEquals(y);
    }

    private static PropertyDescriptor ToPropertyDescriptor(JsObject desc)
    {
        var hasValue = desc.HasProperty("value");
        var hasWritable = desc.HasProperty("writable");
        var hasGet = desc.HasProperty("get");
        var hasSet = desc.HasProperty("set");

        if ((hasGet || hasSet) && (hasValue || hasWritable))
        {
            throw new Errors.JsTypeError("Invalid property descriptor. Cannot both specify accessors and a value or writable attribute", ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);
        }

        if (hasGet || hasSet)
        {
            var getter = hasGet ? desc.Get("get") : null;
            var setter = hasSet ? desc.Get("set") : null;
            var enumerable = desc.HasProperty("enumerable") && desc.Get("enumerable").ToBoolean();
            var configurable = desc.HasProperty("configurable") && desc.Get("configurable").ToBoolean();
            return PropertyDescriptor.Accessor(getter, setter, enumerable, configurable);
        }

        var value = hasValue ? desc.Get("value") : JsValue.Undefined;
        var writable = desc.HasProperty("writable") && desc.Get("writable").ToBoolean();
        var isEnumerable = desc.HasProperty("enumerable") && desc.Get("enumerable").ToBoolean();
        var isConfigurable = desc.HasProperty("configurable") && desc.Get("configurable").ToBoolean();
        return PropertyDescriptor.Data(value, writable, isEnumerable, isConfigurable);
    }

    private static JsDynamicObject FromPropertyDescriptor(PropertyDescriptor desc)
    {
        var obj = new JsDynamicObject();
        if (desc.IsAccessorDescriptor)
        {
            if (desc.Get is not null)
            {
                obj.Set("get", desc.Get);
            }

            if (desc.Set is not null)
            {
                obj.Set("set", desc.Set);
            }
        }
        else
        {
            obj.Set("value", desc.Value ?? JsValue.Undefined);
            obj.Set("writable", desc.Writable == true ? JsValue.True : JsValue.False);
        }

        obj.Set("enumerable", desc.Enumerable == true ? JsValue.True : JsValue.False);
        obj.Set("configurable", desc.Configurable == true ? JsValue.True : JsValue.False);
        return obj;
    }

    private static void DefinePropertiesFromDescriptors(JsDynamicObject target, JsObject props)
    {
        foreach (var key in props.OwnPropertyKeys())
        {
            var descObj = props.Get(key);
            if (descObj is JsObject descObjTyped)
            {
                var desc = ToPropertyDescriptor(descObjTyped);
                target.DefineOwnProperty(key, desc);
            }
        }
    }
}
