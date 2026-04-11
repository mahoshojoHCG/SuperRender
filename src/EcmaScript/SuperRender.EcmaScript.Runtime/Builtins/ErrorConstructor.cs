namespace SuperRender.EcmaScript.Runtime.Builtins;

using SuperRender.EcmaScript.Runtime;

public static class ErrorConstructor
{
    public static void Install(Realm realm)
    {
        // Base Error
        var errorProto = realm.ErrorPrototype;
        var errorCtor = CreateErrorConstructor(realm, "Error", errorProto, null);
        errorProto.DefineOwnProperty("name", PropertyDescriptor.Data(new JsString("Error"), writable: true, enumerable: false, configurable: true));
        errorProto.DefineOwnProperty("message", PropertyDescriptor.Data(JsString.Empty, writable: true, enumerable: false, configurable: true));

        BuiltinHelper.DefineMethod(errorProto, "toString", (thisArg, _) =>
        {
            if (thisArg is not JsObject obj)
            {
                throw new Errors.JsTypeError("Error.prototype.toString called on non-object");
            }

            var name = obj.Get("name");
            var nameStr = name is JsUndefined ? "Error" : name.ToJsString();
            var message = obj.Get("message");
            var messageStr = message is JsUndefined ? "" : message.ToJsString();

            if (nameStr.Length == 0)
            {
                return new JsString(messageStr);
            }

            if (messageStr.Length == 0)
            {
                return new JsString(nameStr);
            }

            return new JsString(nameStr + ": " + messageStr);
        }, 0);

        realm.InstallGlobal("Error", errorCtor);

        // TypeError
        var typeErrorProto = new JsObject { Prototype = errorProto };
        typeErrorProto.DefineOwnProperty("name", PropertyDescriptor.Data(new JsString("TypeError"), writable: true, enumerable: false, configurable: true));
        typeErrorProto.DefineOwnProperty("message", PropertyDescriptor.Data(JsString.Empty, writable: true, enumerable: false, configurable: true));
        var typeErrorCtor = CreateErrorConstructor(realm, "TypeError", typeErrorProto, errorCtor);
        realm.InstallGlobal("TypeError", typeErrorCtor);

        // RangeError
        var rangeErrorProto = new JsObject { Prototype = errorProto };
        rangeErrorProto.DefineOwnProperty("name", PropertyDescriptor.Data(new JsString("RangeError"), writable: true, enumerable: false, configurable: true));
        rangeErrorProto.DefineOwnProperty("message", PropertyDescriptor.Data(JsString.Empty, writable: true, enumerable: false, configurable: true));
        var rangeErrorCtor = CreateErrorConstructor(realm, "RangeError", rangeErrorProto, errorCtor);
        realm.InstallGlobal("RangeError", rangeErrorCtor);

        // ReferenceError
        var refErrorProto = new JsObject { Prototype = errorProto };
        refErrorProto.DefineOwnProperty("name", PropertyDescriptor.Data(new JsString("ReferenceError"), writable: true, enumerable: false, configurable: true));
        refErrorProto.DefineOwnProperty("message", PropertyDescriptor.Data(JsString.Empty, writable: true, enumerable: false, configurable: true));
        var refErrorCtor = CreateErrorConstructor(realm, "ReferenceError", refErrorProto, errorCtor);
        realm.InstallGlobal("ReferenceError", refErrorCtor);

        // SyntaxError
        var syntaxErrorProto = new JsObject { Prototype = errorProto };
        syntaxErrorProto.DefineOwnProperty("name", PropertyDescriptor.Data(new JsString("SyntaxError"), writable: true, enumerable: false, configurable: true));
        syntaxErrorProto.DefineOwnProperty("message", PropertyDescriptor.Data(JsString.Empty, writable: true, enumerable: false, configurable: true));
        var syntaxErrorCtor = CreateErrorConstructor(realm, "SyntaxError", syntaxErrorProto, errorCtor);
        realm.InstallGlobal("SyntaxError", syntaxErrorCtor);

        // URIError
        var uriErrorProto = new JsObject { Prototype = errorProto };
        uriErrorProto.DefineOwnProperty("name", PropertyDescriptor.Data(new JsString("URIError"), writable: true, enumerable: false, configurable: true));
        uriErrorProto.DefineOwnProperty("message", PropertyDescriptor.Data(JsString.Empty, writable: true, enumerable: false, configurable: true));
        var uriErrorCtor = CreateErrorConstructor(realm, "URIError", uriErrorProto, errorCtor);
        realm.InstallGlobal("URIError", uriErrorCtor);
    }

    private static JsFunction CreateErrorConstructor(Realm realm, string name, JsObject proto, JsFunction? parent)
    {
        var ctor = new JsFunction
        {
            Name = name,
            Length = 1,
            IsConstructor = true,
            Prototype = parent ?? realm.FunctionPrototype,
            PrototypeObject = proto,
            ConstructTarget = args =>
            {
                var obj = new JsObject { Prototype = proto };
                var message = BuiltinHelper.Arg(args, 0);
                if (message is not JsUndefined)
                {
                    obj.DefineOwnProperty("message", PropertyDescriptor.Data(
                        new JsString(message.ToJsString()), writable: true, enumerable: false, configurable: true));
                }

                obj.DefineOwnProperty("stack", PropertyDescriptor.Data(
                    new JsString(name + ": " + (message is JsUndefined ? "" : message.ToJsString())),
                    writable: true, enumerable: false, configurable: true));

                return obj;
            },
            CallTarget = (_, args) =>
            {
                // Calling Error() without new should still create an error object
                var obj = new JsObject { Prototype = proto };
                var message = BuiltinHelper.Arg(args, 0);
                if (message is not JsUndefined)
                {
                    obj.DefineOwnProperty("message", PropertyDescriptor.Data(
                        new JsString(message.ToJsString()), writable: true, enumerable: false, configurable: true));
                }

                obj.DefineOwnProperty("stack", PropertyDescriptor.Data(
                    new JsString(name + ": " + (message is JsUndefined ? "" : message.ToJsString())),
                    writable: true, enumerable: false, configurable: true));

                return obj;
            }
        };

        BuiltinHelper.DefineProperty(proto, "constructor", ctor);
        return ctor;
    }
}
