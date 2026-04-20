namespace SuperRender.EcmaScript.Runtime.Builtins;

using SuperRender.EcmaScript.Runtime;

public static class AtomicsObject
{
    public static void Install(Realm realm)
    {
        var atomics = new JsDynamicObject { Prototype = realm.ObjectPrototype };

        // Symbol.toStringTag
        atomics.DefineSymbolProperty(JsSymbol.ToStringTag,
            PropertyDescriptor.Data(new JsString("Atomics"), writable: false, enumerable: false, configurable: true));

        BuiltinHelper.DefineMethod(atomics, "load", (_, args) =>
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
        }, 2);

        BuiltinHelper.DefineMethod(atomics, "store", (_, args) =>
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
        }, 3);

        BuiltinHelper.DefineMethod(atomics, "add", (_, args) =>
        {
            return AtomicOp(args, (old, val) => old + val);
        }, 3);

        BuiltinHelper.DefineMethod(atomics, "sub", (_, args) =>
        {
            return AtomicOp(args, (old, val) => old - val);
        }, 3);

        BuiltinHelper.DefineMethod(atomics, "and", (_, args) =>
        {
            return AtomicOp(args, (old, val) => (int)old & (int)val);
        }, 3);

        BuiltinHelper.DefineMethod(atomics, "or", (_, args) =>
        {
            return AtomicOp(args, (old, val) => (int)old | (int)val);
        }, 3);

        BuiltinHelper.DefineMethod(atomics, "xor", (_, args) =>
        {
            return AtomicOp(args, (old, val) => (int)old ^ (int)val);
        }, 3);

        BuiltinHelper.DefineMethod(atomics, "exchange", (_, args) =>
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
        }, 3);

        BuiltinHelper.DefineMethod(atomics, "compareExchange", (_, args) =>
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
        }, 4);

        BuiltinHelper.DefineMethod(atomics, "isLockFree", (_, args) =>
        {
            var size = (int)BuiltinHelper.Arg(args, 0).ToNumber();
            // 1, 2, 4, 8 bytes are typically lock-free on modern hardware
            return size is 1 or 2 or 4 or 8 ? JsValue.True : JsValue.False;
        }, 1);

        BuiltinHelper.DefineMethod(atomics, "wait", (_, _) =>
        {
            // Simplified: always returns "not-equal" since we don't have real shared memory
            return new JsString("not-equal");
        }, 4);

        BuiltinHelper.DefineMethod(atomics, "notify", (_, _) =>
        {
            // Simplified: returns 0 since no waiters
            return JsNumber.Zero;
        }, 3);

        BuiltinHelper.DefineMethod(atomics, "waitAsync", (_, _) =>
        {
            // Return a promise that resolves to "not-equal"
            var promise = new JsPromiseObject { Prototype = realm.PromisePrototype };
            var result = new JsDynamicObject { Prototype = realm.ObjectPrototype };
            result.Set("value", new JsString("not-equal"));
            PromiseConstructor.ResolvePromise(promise, result, realm);

            var wrapper = new JsDynamicObject { Prototype = realm.ObjectPrototype };
            wrapper.Set("async", JsValue.True);
            wrapper.Set("value", promise);
            return wrapper;
        }, 4);

        realm.InstallGlobal("Atomics", atomics);
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

        if (ta is JsTypedArrayObject typed)
        {
            // Access through reflection or stored accessors
            var bpe = (int)ta.Get("BYTES_PER_ELEMENT").ToNumber();
            if (bpe == 0) bpe = 4; // default to Int32

            return bpe switch
            {
                1 => ((b, i) => (sbyte)b[i], (b, i, v) => b[i] = (byte)(sbyte)v, 1, byteOffset),
                2 => ((b, i) => BitConverter.ToInt16(b, i), (b, i, v) => BitConverter.TryWriteBytes(b.AsSpan(i), (short)v), 2, byteOffset),
                8 => ((b, i) => BitConverter.ToDouble(b, i), (b, i, v) => BitConverter.TryWriteBytes(b.AsSpan(i), v), 8, byteOffset),
                _ => ((b, i) => BitConverter.ToInt32(b, i), (b, i, v) => BitConverter.TryWriteBytes(b.AsSpan(i), (int)v), 4, byteOffset)
            };
        }

        // Fallback for non-JsTypedArrayObject
        return ((b, i) => BitConverter.ToInt32(b, i), (b, i, v) => BitConverter.TryWriteBytes(b.AsSpan(i), (int)v), 4, byteOffset);
    }
}
