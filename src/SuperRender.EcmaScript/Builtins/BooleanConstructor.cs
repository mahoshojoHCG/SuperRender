namespace SuperRender.EcmaScript.Builtins;

using SuperRender.EcmaScript.Runtime;

public static class BooleanConstructor
{
    public static void Install(Realm realm)
    {
        var proto = realm.BooleanPrototype;

        var ctor = new JsFunction
        {
            Name = "Boolean",
            Length = 1,
            IsConstructor = true,
            Prototype = realm.FunctionPrototype,
            PrototypeObject = proto,
            CallTarget = (_, args) =>
            {
                var val = BuiltinHelper.Arg(args, 0);
                return val.ToBoolean() ? JsValue.True : JsValue.False;
            },
            ConstructTarget = args =>
            {
                var val = BuiltinHelper.Arg(args, 0);
                var wrapper = new JsObject { Prototype = realm.BooleanPrototype };
                wrapper.DefineOwnProperty("[[BooleanData]]",
                    PropertyDescriptor.Data(val.ToBoolean() ? JsValue.True : JsValue.False, writable: false, enumerable: false, configurable: false));
                return wrapper;
            }
        };

        BuiltinHelper.DefineProperty(proto, "constructor", ctor);

        BuiltinHelper.DefineMethod(proto, "toString", (thisArg, _) =>
        {
            var b = GetBooleanValue(thisArg);
            return new JsString(b ? "true" : "false");
        }, 0);

        BuiltinHelper.DefineMethod(proto, "valueOf", (thisArg, _) =>
        {
            var b = GetBooleanValue(thisArg);
            return b ? JsValue.True : JsValue.False;
        }, 0);

        realm.InstallGlobal("Boolean", ctor);
    }

    private static bool GetBooleanValue(JsValue thisArg)
    {
        if (thisArg is JsBoolean b)
        {
            return b.Value;
        }

        if (thisArg is JsObject obj)
        {
            var data = obj.GetOwnProperty("[[BooleanData]]");
            if (data?.Value is JsBoolean boolData)
            {
                return boolData.Value;
            }
        }

        throw new Errors.JsTypeError("Boolean.prototype.valueOf requires that 'this' be a Boolean");
    }
}
