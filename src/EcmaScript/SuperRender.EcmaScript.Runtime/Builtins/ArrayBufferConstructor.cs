namespace SuperRender.EcmaScript.Runtime.Builtins;

using SuperRender.EcmaScript.Runtime;

public static class ArrayBufferConstructor
{
    public static void Install(Realm realm)
    {
        var abProto = realm.ArrayBufferPrototype;
        var sabProto = realm.SharedArrayBufferPrototype;

        // ─── ArrayBuffer ───

        var abCtor = new JsFunction
        {
            Name = "ArrayBuffer",
            Length = 1,
            IsConstructor = true,
            Prototype = realm.FunctionPrototype,
            PrototypeObject = abProto,
            ConstructTarget = args =>
            {
                var byteLength = (int)BuiltinHelper.Arg(args, 0).ToNumber();
                if (byteLength < 0)
                    throw new Errors.JsRangeError("Invalid array buffer length", ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);
                return CreateArrayBuffer(byteLength, abProto, isShared: false);
            },
            CallTarget = (_, _) =>
            {
                throw new Errors.JsTypeError("Constructor ArrayBuffer requires 'new'", ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);
            }
        };

        BuiltinHelper.DefineMethod(abCtor, "isView", (_, args) =>
        {
            var arg = BuiltinHelper.Arg(args, 0);
            if (arg is JsDynamicObject obj)
            {
                var bufProp = obj.Get("buffer");
                return bufProp is JsDynamicObject ? JsValue.True : JsValue.False;
            }
            return JsValue.False;
        }, 1);

        // ArrayBuffer.prototype.byteLength (getter)
        BuiltinHelper.DefineGetter(abProto, "byteLength", (self, _) =>
        {
            if (self is JsDynamicObject obj)
            {
                var data = obj.Get("[[ArrayBufferData]]");
                if (data is JsDynamicObject wrapper)
                {
                    var bytes = wrapper.Get("bytes");
                    if (bytes is JsNumber len) return len;
                }
            }
            return JsNumber.Zero;
        });

        BuiltinHelper.DefineMethod(abProto, "slice", (self, args) =>
        {
            if (self is not JsDynamicObject selfObj)
                throw new Errors.JsTypeError("ArrayBuffer.prototype.slice called on non-ArrayBuffer", ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);

            var byteArray = GetByteArray(selfObj);
            if (byteArray is null)
                throw new Errors.JsTypeError("ArrayBuffer.prototype.slice called on non-ArrayBuffer", ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);

            var len = byteArray.Length;
            var begin = args.Length > 0 ? NormalizeIndex((int)args[0].ToNumber(), len) : 0;
            var end = args.Length > 1 && args[1] is not JsUndefined ? NormalizeIndex((int)args[1].ToNumber(), len) : len;

            var newLen = Math.Max(end - begin, 0);
            var newBuffer = CreateArrayBuffer(newLen, abProto, isShared: false);
            var newBytes = GetByteArray(newBuffer)!;
            Array.Copy(byteArray, begin, newBytes, 0, newLen);
            return newBuffer;
        }, 2);

        realm.InstallGlobal("ArrayBuffer", abCtor);

        // ─── SharedArrayBuffer ───

        var sabCtor = new JsFunction
        {
            Name = "SharedArrayBuffer",
            Length = 1,
            IsConstructor = true,
            Prototype = realm.FunctionPrototype,
            PrototypeObject = sabProto,
            ConstructTarget = args =>
            {
                var byteLength = (int)BuiltinHelper.Arg(args, 0).ToNumber();
                if (byteLength < 0)
                    throw new Errors.JsRangeError("Invalid shared array buffer length", ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);
                return CreateArrayBuffer(byteLength, sabProto, isShared: true);
            },
            CallTarget = (_, _) =>
            {
                throw new Errors.JsTypeError("Constructor SharedArrayBuffer requires 'new'", ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);
            }
        };

        BuiltinHelper.DefineGetter(sabProto, "byteLength", (self, _) =>
        {
            if (self is JsDynamicObject obj)
            {
                var data = obj.Get("[[ArrayBufferData]]");
                if (data is JsDynamicObject wrapper)
                {
                    var bytes = wrapper.Get("bytes");
                    if (bytes is JsNumber len) return len;
                }
            }
            return JsNumber.Zero;
        });

        realm.InstallGlobal("SharedArrayBuffer", sabCtor);
    }

    internal static JsDynamicObject CreateArrayBuffer(int byteLength, JsDynamicObject proto, bool isShared)
    {
        var buffer = new JsDynamicObject { Prototype = proto };
        var data = new byte[byteLength];

        // Store the byte array as an internal wrapper
        var wrapper = new JsDynamicObject();
        wrapper.Set("bytes", JsNumber.Create(byteLength));
        wrapper.Set("[[IsShared]]", isShared ? JsValue.True : JsValue.False);
        buffer.Set("[[ArrayBufferData]]", wrapper);

        // Keep the actual byte[] accessible via a tagged reference
        buffer.Set("[[BackingStore]]", new JsByteArrayWrapper(data));

        // Use DefineOwnProperty to override the prototype getter
        buffer.DefineOwnProperty("byteLength", PropertyDescriptor.Data(
            JsNumber.Create(byteLength), writable: false, enumerable: false, configurable: false));

        return buffer;
    }

    internal static byte[]? GetByteArray(JsDynamicObject buffer)
    {
        var store = buffer.Get("[[BackingStore]]");
        if (store is JsByteArrayWrapper wrapper)
            return wrapper.Data;
        return null;
    }

    private static int NormalizeIndex(int index, int length)
    {
        if (index < 0) return Math.Max(0, length + index);
        return Math.Min(index, length);
    }
}

/// <summary>
/// Internal wrapper to hold a byte[] inside a JsValue for ArrayBuffer backing stores.
/// </summary>
internal sealed class JsByteArrayWrapper : JsValue
{
    public byte[] Data { get; }

    public JsByteArrayWrapper(byte[] data) => Data = data;

    public override string TypeOf => "object";
    public override bool ToBoolean() => true;
    public override double ToNumber() => double.NaN;
    public override string ToJsString() => "[object ArrayBuffer]";
}
