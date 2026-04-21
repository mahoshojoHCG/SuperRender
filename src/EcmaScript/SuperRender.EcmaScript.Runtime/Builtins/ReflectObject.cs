namespace SuperRender.EcmaScript.Runtime.Builtins;

using SuperRender.EcmaScript.Runtime;

// JSGEN005/006/007: Reflect is a pure meta-object — every method operates on an
// untyped target `JsObject`/`JsValue` and forwards raw args. Typed signatures
// cannot express Reflect.apply/construct/defineProperty/etc. without losing the
// "pass-through any JS value" contract, so the legacy variadic shape is kept.
#pragma warning disable JSGEN005, JSGEN006, JSGEN007

[JsObject]
public sealed partial class ReflectObject : JsObject
{
    private static readonly JsString ToStringTagValue = new("Reflect");

    public ReflectObject(Realm realm)
    {
        Prototype = realm.ObjectPrototype;
        Extensible = false;
    }

    public static void Install(Realm realm) => realm.InstallGlobal("Reflect", new ReflectObject(realm));

    public override bool TryGetSymbolProperty(JsSymbol symbol, out JsValue value)
    {
        if (symbol == JsSymbol.ToStringTag)
        {
            value = ToStringTagValue;
            return true;
        }

        return base.TryGetSymbolProperty(symbol, out value);
    }

    [JsMethod("get")]
    public static JsValue Get(JsValue _, JsValue[] args)
    {
        var target = RequireObject(BuiltinHelper.Arg(args, 0));
        var propertyKey = BuiltinHelper.Arg(args, 1).ToJsString();
        return target.Get(propertyKey);
    }

    [JsMethod("set")]
    public static JsValue Set(JsValue _, JsValue[] args)
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
    }

    [JsMethod("has")]
    public static JsValue Has(JsValue _, JsValue[] args)
    {
        var target = RequireObject(BuiltinHelper.Arg(args, 0));
        var propertyKey = BuiltinHelper.Arg(args, 1).ToJsString();
        return target.HasProperty(propertyKey) ? JsValue.True : JsValue.False;
    }

    [JsMethod("deleteProperty")]
    public static JsValue DeleteProperty(JsValue _, JsValue[] args)
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
    }

    [JsMethod("ownKeys")]
    public static JsValue OwnKeys(JsValue _, JsValue[] args)
    {
        var target = RequireObject(BuiltinHelper.Arg(args, 0));
        var result = new JsArray();
        foreach (var key in target.OwnPropertyKeys())
        {
            result.Push(new JsString(key));
        }

        return result;
    }

    [JsMethod("apply")]
    public static JsValue Apply(JsValue _, JsValue[] args)
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
    }

    [JsMethod("construct")]
    public static JsValue Construct(JsValue _, JsValue[] args)
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
    }

    [JsMethod("getPrototypeOf")]
    public static JsValue GetPrototypeOf(JsValue _, JsValue[] args)
    {
        var target = RequireObject(BuiltinHelper.Arg(args, 0));
        return (JsValue?)target.Prototype ?? JsValue.Null;
    }

    [JsMethod("setPrototypeOf")]
    public static JsValue SetPrototypeOf(JsValue _, JsValue[] args)
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
    }

    [JsMethod("isExtensible")]
    public static JsValue IsExtensible(JsValue _, JsValue[] args)
    {
        var target = RequireObject(BuiltinHelper.Arg(args, 0));
        return target.Extensible ? JsValue.True : JsValue.False;
    }

    [JsMethod("preventExtensions")]
    public static JsValue PreventExtensions(JsValue _, JsValue[] args)
    {
        var target = RequireObject(BuiltinHelper.Arg(args, 0));
        target.Extensible = false;
        return JsValue.True;
    }

    [JsMethod("defineProperty")]
    public static JsValue DefineProperty(JsValue _, JsValue[] args)
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
    }

    [JsMethod("getOwnPropertyDescriptor")]
    public static JsOptional<JsValue> GetOwnPropertyDescriptor(JsValue target, JsValue propertyKey)
    {
        var obj = RequireObject(target);
        var key = propertyKey.ToJsString();
        var desc = obj.GetOwnProperty(key);
        if (desc is null)
        {
            return JsOptional<JsValue>.Undefined;
        }

        return JsOptional<JsValue>.Of(FromPropertyDescriptor(desc));
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
