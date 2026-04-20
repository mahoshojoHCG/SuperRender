namespace SuperRender.EcmaScript.Runtime.Builtins;

using SuperRender.EcmaScript.Runtime;

public static class ReflectObject
{
    public static void Install(Realm realm)
    {
        var reflect = new JsDynamicObject { Prototype = realm.ObjectPrototype };

        reflect.DefineSymbolProperty(JsSymbol.ToStringTag,
            PropertyDescriptor.Data(new JsString("Reflect"), writable: false, enumerable: false, configurable: true));

        BuiltinHelper.DefineMethod(reflect, "get", (_, args) =>
        {
            var target = RequireObject(BuiltinHelper.Arg(args, 0));
            var propertyKey = BuiltinHelper.Arg(args, 1).ToJsString();
            return target.Get(propertyKey);
        }, 2);

        BuiltinHelper.DefineMethod(reflect, "set", (_, args) =>
        {
            var target = RequireObject(BuiltinHelper.Arg(args, 0));
            var propertyKey = BuiltinHelper.Arg(args, 1).ToJsString();
            var value = BuiltinHelper.Arg(args, 2);

            try
            {
                target.Set(propertyKey, value);
                return JsValue.True;
            }
            catch (Errors.JsTypeError)
            {
                return JsValue.False;
            }
        }, 3);

        BuiltinHelper.DefineMethod(reflect, "has", (_, args) =>
        {
            var target = RequireObject(BuiltinHelper.Arg(args, 0));
            var propertyKey = BuiltinHelper.Arg(args, 1).ToJsString();
            return target.HasProperty(propertyKey) ? JsValue.True : JsValue.False;
        }, 2);

        BuiltinHelper.DefineMethod(reflect, "deleteProperty", (_, args) =>
        {
            var target = RequireObject(BuiltinHelper.Arg(args, 0));
            var propertyKey = BuiltinHelper.Arg(args, 1).ToJsString();

            try
            {
                return target.Delete(propertyKey) ? JsValue.True : JsValue.False;
            }
            catch (Errors.JsTypeError)
            {
                return JsValue.False;
            }
        }, 2);

        BuiltinHelper.DefineMethod(reflect, "ownKeys", (_, args) =>
        {
            var target = RequireObject(BuiltinHelper.Arg(args, 0));
            var result = new JsArray();
            foreach (var key in target.OwnPropertyKeys())
            {
                result.Push(new JsString(key));
            }

            return result;
        }, 1);

        BuiltinHelper.DefineMethod(reflect, "apply", (_, args) =>
        {
            var target = BuiltinHelper.Arg(args, 0);
            if (target is not JsFunction fn)
            {
                throw new Errors.JsTypeError("Reflect.apply requires a function", ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);
            }

            var thisArg = BuiltinHelper.Arg(args, 1);
            var argsList = BuiltinHelper.Arg(args, 2);

            JsValue[] callArgs;
            if (argsList is JsArray arr)
            {
                callArgs = new JsValue[arr.DenseLength];
                for (var i = 0; i < arr.DenseLength; i++)
                {
                    callArgs[i] = arr.GetIndex(i);
                }
            }
            else if (argsList is JsUndefined or JsNull)
            {
                callArgs = [];
            }
            else
            {
                throw new Errors.JsTypeError("CreateListFromArrayLike called on non-object", ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);
            }

            return fn.Call(thisArg, callArgs);
        }, 3);

        BuiltinHelper.DefineMethod(reflect, "construct", (_, args) =>
        {
            var target = BuiltinHelper.Arg(args, 0);
            if (target is not JsFunction fn)
            {
                throw new Errors.JsTypeError("Reflect.construct requires a constructor", ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);
            }

            var argsList = BuiltinHelper.Arg(args, 1);

            JsValue[] constructArgs;
            if (argsList is JsArray arr)
            {
                constructArgs = new JsValue[arr.DenseLength];
                for (var i = 0; i < arr.DenseLength; i++)
                {
                    constructArgs[i] = arr.GetIndex(i);
                }
            }
            else if (argsList is JsUndefined or JsNull)
            {
                constructArgs = [];
            }
            else
            {
                throw new Errors.JsTypeError("CreateListFromArrayLike called on non-object", ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);
            }

            return fn.Construct(constructArgs);
        }, 2);

        BuiltinHelper.DefineMethod(reflect, "getPrototypeOf", (_, args) =>
        {
            var target = RequireObject(BuiltinHelper.Arg(args, 0));
            return (JsValue?)target.Prototype ?? JsValue.Null;
        }, 1);

        BuiltinHelper.DefineMethod(reflect, "setPrototypeOf", (_, args) =>
        {
            var target = RequireObject(BuiltinHelper.Arg(args, 0));
            var proto = BuiltinHelper.Arg(args, 1);

            if (proto is JsNull)
            {
                target.Prototype = null;
            }
            else if (proto is JsDynamicObject protoObj)
            {
                target.Prototype = protoObj;
            }
            else
            {
                throw new Errors.JsTypeError("Object prototype may only be an Object or null", ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);
            }

            return JsValue.True;
        }, 2);

        BuiltinHelper.DefineMethod(reflect, "isExtensible", (_, args) =>
        {
            var target = RequireObject(BuiltinHelper.Arg(args, 0));
            return target.Extensible ? JsValue.True : JsValue.False;
        }, 1);

        BuiltinHelper.DefineMethod(reflect, "preventExtensions", (_, args) =>
        {
            var target = RequireObject(BuiltinHelper.Arg(args, 0));
            target.Extensible = false;
            return JsValue.True;
        }, 1);

        BuiltinHelper.DefineMethod(reflect, "defineProperty", (_, args) =>
        {
            var target = RequireObject(BuiltinHelper.Arg(args, 0));
            var propertyKey = BuiltinHelper.Arg(args, 1).ToJsString();
            var descObj = RequireObject(BuiltinHelper.Arg(args, 2));

            var desc = ToPropertyDescriptor(descObj);
            try
            {
                target.DefineOwnProperty(propertyKey, desc);
                return JsValue.True;
            }
            catch (Errors.JsTypeError)
            {
                return JsValue.False;
            }
        }, 3);

        BuiltinHelper.DefineMethod(reflect, "getOwnPropertyDescriptor", (_, args) =>
        {
            var target = RequireObject(BuiltinHelper.Arg(args, 0));
            var propertyKey = BuiltinHelper.Arg(args, 1).ToJsString();
            var desc = target.GetOwnProperty(propertyKey);
            if (desc is null)
            {
                return JsValue.Undefined;
            }

            return FromPropertyDescriptor(desc);
        }, 2);

        realm.InstallGlobal("Reflect", reflect);
    }

    private static JsDynamicObject RequireObject(JsValue value)
    {
        if (value is JsDynamicObject obj)
        {
            return obj;
        }

        throw new Errors.JsTypeError("Reflect method requires an object as the first argument", ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);
    }

    private static PropertyDescriptor ToPropertyDescriptor(JsDynamicObject desc)
    {
        var hasGet = desc.HasProperty("get");
        var hasSet = desc.HasProperty("set");
        var hasValue = desc.HasProperty("value");
        var hasWritable = desc.HasProperty("writable");

        if ((hasGet || hasSet) && (hasValue || hasWritable))
        {
            throw new Errors.JsTypeError("Invalid property descriptor", ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);
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
            if (desc.Get is not null) obj.Set("get", desc.Get);
            if (desc.Set is not null) obj.Set("set", desc.Set);
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
}
