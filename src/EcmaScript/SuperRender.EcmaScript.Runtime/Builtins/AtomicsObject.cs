namespace SuperRender.EcmaScript.Runtime.Builtins;

using SuperRender.EcmaScript.Runtime;

// JSGEN005/006/007: every Atomics method takes variadic typed-array args, uses
// dynamic runtime typing on the receiver, and returns JsValue (JsNumber/JsBoolean).
// The legacy (JsValue, JsValue[]) shape is the only way to express these without
// losing the ECMAScript semantics; migration would require a dedicated IJsType
// for typed-array receivers plus per-op overloads.
#pragma warning disable JSGEN005, JSGEN006, JSGEN007

[JsObject]
public sealed partial class AtomicsObject : JsObject
{
    private static readonly JsString ToStringTagValue = new("Atomics");

    private readonly Realm _realm;

    public AtomicsObject(Realm realm)
    {
        _realm = realm;
        Prototype = realm.ObjectPrototype;
        Extensible = false;
    }

    public static void Install(Realm realm) => realm.InstallGlobal("Atomics", new AtomicsObject(realm));

    public override bool TryGetSymbolProperty(JsSymbol symbol, out JsValue value)
    {
        if (symbol == JsSymbol.ToStringTag)
        {
            value = ToStringTagValue;
            return true;
        }

        return base.TryGetSymbolProperty(symbol, out value);
    }

    [JsMethod("load")]
    public static JsValue Load(JsValue _, JsValue[] args)
    {
        var ta = RequireTypedArray(args, 0);
        var index = (int)BuiltinHelper.Arg(args, 1).ToNumber();
        ValidateIndex(ta, index);
        var bytes = GetBytes(ta);
        var (getter, _, bytesPerElement, byteOffset) = GetAccessors(ta);
        lock (bytes)
        {
            return JsNumber.Create(getter(bytes, byteOffset + index * bytesPerElement));
        }
    }

    [JsMethod("store")]
    public static JsValue Store(JsValue _, JsValue[] args)
    {
        var ta = RequireTypedArray(args, 0);
        var index = (int)BuiltinHelper.Arg(args, 1).ToNumber();
        var value = BuiltinHelper.Arg(args, 2).ToNumber();
        ValidateIndex(ta, index);
        var bytes = GetBytes(ta);
        var (_, setter, bytesPerElement, byteOffset) = GetAccessors(ta);
        lock (bytes)
        {
            setter(bytes, byteOffset + index * bytesPerElement, value);
        }
        return JsNumber.Create(value);
    }

    [JsMethod("add")]
    public static JsValue Add(JsValue _, JsValue[] args) => AtomicOp(args, (old, val) => old + val);

    [JsMethod("sub")]
    public static JsValue Sub(JsValue _, JsValue[] args) => AtomicOp(args, (old, val) => old - val);

    [JsMethod("and")]
    public static JsValue And(JsValue _, JsValue[] args) => AtomicOp(args, (old, val) => (int)old & (int)val);

    [JsMethod("or")]
    public static JsValue Or(JsValue _, JsValue[] args) => AtomicOp(args, (old, val) => (int)old | (int)val);

    [JsMethod("xor")]
    public static JsValue Xor(JsValue _, JsValue[] args) => AtomicOp(args, (old, val) => (int)old ^ (int)val);

    [JsMethod("exchange")]
    public static JsValue Exchange(JsValue _, JsValue[] args)
    {
        var ta = RequireTypedArray(args, 0);
        var index = (int)BuiltinHelper.Arg(args, 1).ToNumber();
        var value = BuiltinHelper.Arg(args, 2).ToNumber();
        ValidateIndex(ta, index);
        var bytes = GetBytes(ta);
        var (getter, setter, bytesPerElement, byteOffset) = GetAccessors(ta);
        lock (bytes)
        {
            var offset = byteOffset + index * bytesPerElement;
            var old = getter(bytes, offset);
            setter(bytes, offset, value);
            return JsNumber.Create(old);
        }
    }

    [JsMethod("compareExchange")]
    public static JsValue CompareExchange(JsValue _, JsValue[] args)
    {
        var ta = RequireTypedArray(args, 0);
        var index = (int)BuiltinHelper.Arg(args, 1).ToNumber();
        var expected = BuiltinHelper.Arg(args, 2).ToNumber();
        var replacement = BuiltinHelper.Arg(args, 3).ToNumber();
        ValidateIndex(ta, index);
        var bytes = GetBytes(ta);
        var (getter, setter, bytesPerElement, byteOffset) = GetAccessors(ta);
        lock (bytes)
        {
            var offset = byteOffset + index * bytesPerElement;
            var old = getter(bytes, offset);
            // ReSharper disable once CompareOfFloatsByEqualityOperator
            if (old == expected)
            {
                setter(bytes, offset, replacement);
            }
            return JsNumber.Create(old);
        }
    }

    [JsMethod("isLockFree")]
    public static JsValue IsLockFree(JsValue _, JsValue[] args)
    {
        var size = (int)BuiltinHelper.Arg(args, 0).ToNumber();
        return size is 1 or 2 or 4 or 8 ? JsValue.True : JsValue.False;
    }

    [JsMethod("wait")]
    public static JsValue Wait(JsValue _, JsValue[] args) => new JsString("not-equal");

    [JsMethod("notify")]
    public static JsValue Notify(JsValue _, JsValue[] args) => JsNumber.Zero;

    [JsMethod("waitAsync")]
    public JsValue WaitAsync(JsValue[] args)
    {
        var promise = new JsPromiseObject { Prototype = _realm.PromisePrototype };
        var result = new JsDynamicObject { Prototype = _realm.ObjectPrototype };
        result.Set("value", new JsString("not-equal"));
        PromiseConstructor.ResolvePromise(promise, result, _realm);

        var wrapper = new JsDynamicObject { Prototype = _realm.ObjectPrototype };
        wrapper.Set("async", JsValue.True);
        wrapper.Set("value", promise);
        return wrapper;
    }

    private static JsValue AtomicOp(JsValue[] args, Func<double, double, double> op)
    {
        var ta = RequireTypedArray(args, 0);
        var index = (int)BuiltinHelper.Arg(args, 1).ToNumber();
        var value = BuiltinHelper.Arg(args, 2).ToNumber();
        ValidateIndex(ta, index);
        var bytes = GetBytes(ta);
        var (getter, setter, bytesPerElement, byteOffset) = GetAccessors(ta);
        lock (bytes)
        {
            var offset = byteOffset + index * bytesPerElement;
            var old = getter(bytes, offset);
            setter(bytes, offset, op(old, value));
            return JsNumber.Create(old);
        }
    }

    private static JsDynamicObject RequireTypedArray(JsValue[] args, int index)
    {
        var arg = BuiltinHelper.Arg(args, index);
        if (arg is JsTypedArrayObject ta)
            return ta;
        if (arg is JsDynamicObject obj && obj.Get("buffer") is JsDynamicObject)
            return obj;
        throw new Errors.JsTypeError("Atomics operation requires a typed array", ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);
    }

    private static void ValidateIndex(JsDynamicObject ta, int index)
    {
        var length = (int)ta.Get("length").ToNumber();
        if (index < 0 || index >= length)
            throw new Errors.JsRangeError("Invalid atomic access index", ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);
    }

    private static byte[] GetBytes(JsDynamicObject ta)
    {
        var buffer = ta.Get("buffer");
        if (buffer is JsDynamicObject bufObj)
        {
            var bytes = ArrayBufferConstructor.GetByteArray(bufObj);
            if (bytes is not null) return bytes;
        }
        throw new Errors.JsTypeError("Detached buffer", ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);
    }

    private static (Func<byte[], int, double> getter, Action<byte[], int, double> setter, int bytesPerElement, int byteOffset) GetAccessors(JsDynamicObject ta)
    {
        var byteOffset = (int)ta.Get("byteOffset").ToNumber();

        if (ta is JsTypedArrayObject)
        {
            var bpe = (int)ta.Get("BYTES_PER_ELEMENT").ToNumber();
            if (bpe == 0) bpe = 4;

            return bpe switch
            {
                1 => ((b, i) => (sbyte)b[i], (b, i, v) => b[i] = (byte)(sbyte)v, 1, byteOffset),
                2 => ((b, i) => BitConverter.ToInt16(b, i), (b, i, v) => BitConverter.TryWriteBytes(b.AsSpan(i), (short)v), 2, byteOffset),
                8 => ((b, i) => BitConverter.ToDouble(b, i), (b, i, v) => BitConverter.TryWriteBytes(b.AsSpan(i), v), 8, byteOffset),
                _ => ((b, i) => BitConverter.ToInt32(b, i), (b, i, v) => BitConverter.TryWriteBytes(b.AsSpan(i), (int)v), 4, byteOffset)
            };
        }

        return ((b, i) => BitConverter.ToInt32(b, i), (b, i, v) => BitConverter.TryWriteBytes(b.AsSpan(i), (int)v), 4, byteOffset);
    }
}
