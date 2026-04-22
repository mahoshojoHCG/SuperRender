namespace SuperRender.EcmaScript.Runtime.Builtins;

using System.Globalization;
using SuperRender.EcmaScript.Runtime;

[JsGlobalInstall("TypedArray")]
public sealed partial class TypedArrayConstructor
{
    private static readonly (string Name, int BytesPerElement, Func<byte[], int, double> Getter, Action<byte[], int, double> Setter)[] TypedArrayTypes =
    [
        ("Int8Array", 1, (b, i) => (sbyte)b[i], (b, i, v) => b[i] = (byte)(sbyte)v),
        ("Uint8Array", 1, (b, i) => b[i], (b, i, v) => b[i] = (byte)v),
        ("Uint8ClampedArray", 1, (b, i) => b[i], (b, i, v) => b[i] = (byte)Math.Max(0, Math.Min(255, v))),
        ("Int16Array", 2, (b, i) => BitConverter.ToInt16(b, i), (b, i, v) => BitConverter.TryWriteBytes(b.AsSpan(i), (short)v)),
        ("Uint16Array", 2, (b, i) => BitConverter.ToUInt16(b, i), (b, i, v) => BitConverter.TryWriteBytes(b.AsSpan(i), (ushort)v)),
        ("Int32Array", 4, (b, i) => BitConverter.ToInt32(b, i), (b, i, v) => BitConverter.TryWriteBytes(b.AsSpan(i), (int)v)),
        ("Uint32Array", 4, (b, i) => BitConverter.ToUInt32(b, i), (b, i, v) => BitConverter.TryWriteBytes(b.AsSpan(i), (uint)v)),
        ("Float32Array", 4, (b, i) => BitConverter.ToSingle(b, i), (b, i, v) => BitConverter.TryWriteBytes(b.AsSpan(i), (float)v)),
        ("Float64Array", 8, (b, i) => BitConverter.ToDouble(b, i), (b, i, v) => BitConverter.TryWriteBytes(b.AsSpan(i), v)),
        ("BigInt64Array", 8, (b, i) => BitConverter.ToInt64(b, i), (b, i, v) => BitConverter.TryWriteBytes(b.AsSpan(i), (long)v)),
        ("BigUint64Array", 8, (b, i) => (double)BitConverter.ToUInt64(b, i), (b, i, v) => BitConverter.TryWriteBytes(b.AsSpan(i), (ulong)v)),
    ];

    private static void __Install(Realm realm)
    {
        foreach (var (name, bytesPerElement, getter, setter) in TypedArrayTypes)
        {
            InstallTypedArray(realm, name, bytesPerElement, getter, setter);
        }
    }

    private static void InstallTypedArray(Realm realm, string name, int bytesPerElement,
        Func<byte[], int, double> getter, Action<byte[], int, double> setter)
    {
        var proto = new JsDynamicObject { Prototype = realm.ObjectPrototype };

        var ctor = new JsFunction
        {
            Name = name,
            Length = 1,
            IsConstructor = true,
            Prototype = realm.FunctionPrototype,
            PrototypeObject = proto,
            ConstructTarget = args =>
            {
                var arg = BuiltinHelper.Arg(args, 0);

                JsDynamicObject buffer;
                int byteOffset = 0;
                int length;

                if (arg is JsNumber num)
                {
                    // new TypedArray(length)
                    length = (int)num.Value;
                    if (length < 0)
                        throw new Errors.JsRangeError("Invalid typed array length", ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);
                    buffer = ArrayBufferConstructor.CreateArrayBuffer(length * bytesPerElement, realm.ArrayBufferPrototype, isShared: false);
                }
                else if (arg is JsDynamicObject argObj && argObj.Get("[[BackingStore]]") is JsByteArrayWrapper)
                {
                    // new TypedArray(buffer[, byteOffset[, length]])
                    buffer = argObj;
                    if (args.Length > 1) byteOffset = (int)args[1].ToNumber();
                    var bufLen = (int)buffer.Get("byteLength").ToNumber();
                    if (args.Length > 2 && args[2] is not JsUndefined)
                        length = (int)args[2].ToNumber();
                    else
                        length = (bufLen - byteOffset) / bytesPerElement;
                }
                else if (arg is JsArray srcArr)
                {
                    // new TypedArray(array)
                    length = srcArr.DenseLength;
                    buffer = ArrayBufferConstructor.CreateArrayBuffer(length * bytesPerElement, realm.ArrayBufferPrototype, isShared: false);
                    var bytes = ArrayBufferConstructor.GetByteArray(buffer)!;
                    for (var i = 0; i < length; i++)
                    {
                        setter(bytes, i * bytesPerElement, srcArr.GetIndex(i).ToNumber());
                    }
                }
                else
                {
                    length = 0;
                    buffer = ArrayBufferConstructor.CreateArrayBuffer(0, realm.ArrayBufferPrototype, isShared: false);
                }

                return CreateTypedArray(buffer, byteOffset, length, bytesPerElement, proto, name, getter, setter, realm);
            },
            CallTarget = (_, _) =>
            {
                throw new Errors.JsTypeError($"Constructor {name} requires 'new'", ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);
            }
        };

        BuiltinHelper.DefineProperty(ctor, "BYTES_PER_ELEMENT", JsNumber.Create(bytesPerElement));
        BuiltinHelper.DefineProperty(proto, "BYTES_PER_ELEMENT", JsNumber.Create(bytesPerElement));

        // Prototype methods
        BuiltinHelper.DefineMethod(proto, "set", (self, args) =>
        {
            if (self is not JsDynamicObject selfObj)
                throw new Errors.JsTypeError("TypedArray.prototype.set called on non-typed-array", ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);
            var source = BuiltinHelper.Arg(args, 0);
            var offset = args.Length > 1 ? (int)args[1].ToNumber() : 0;

            var bytes = GetTypedArrayBytes(selfObj);
            if (bytes is null) return JsValue.Undefined;

            var selfOffset = (int)selfObj.Get("byteOffset").ToNumber();

            if (source is JsArray arr)
            {
                for (var i = 0; i < arr.DenseLength; i++)
                {
                    setter(bytes, selfOffset + (offset + i) * bytesPerElement, arr.GetIndex(i).ToNumber());
                }
            }

            return JsValue.Undefined;
        }, 1);

        BuiltinHelper.DefineMethod(proto, "slice", (self, args) =>
        {
            if (self is not JsDynamicObject selfObj) return JsValue.Undefined;
            var len = (int)selfObj.Get("length").ToNumber();
            var begin = args.Length > 0 ? NormalizeIndex((int)args[0].ToNumber(), len) : 0;
            var end = args.Length > 1 && args[1] is not JsUndefined ? NormalizeIndex((int)args[1].ToNumber(), len) : len;
            var newLen = Math.Max(end - begin, 0);

            var newBuffer = ArrayBufferConstructor.CreateArrayBuffer(newLen * bytesPerElement, realm.ArrayBufferPrototype, isShared: false);
            var srcBytes = GetTypedArrayBytes(selfObj);
            var dstBytes = ArrayBufferConstructor.GetByteArray(newBuffer)!;
            var srcOffset = (int)selfObj.Get("byteOffset").ToNumber();

            if (srcBytes is not null)
            {
                Array.Copy(srcBytes, srcOffset + begin * bytesPerElement, dstBytes, 0, newLen * bytesPerElement);
            }

            return CreateTypedArray(newBuffer, 0, newLen, bytesPerElement, proto, name, getter, setter, realm);
        }, 2);

        // Symbol.iterator
        proto.DefineSymbolProperty(JsSymbol.Iterator,
            PropertyDescriptor.Data(JsFunction.CreateNative("[Symbol.iterator]", (self, _) =>
            {
                if (self is not JsDynamicObject selfObj) return JsValue.Undefined;
                var len = (int)selfObj.Get("length").ToNumber();
                var items = new List<JsValue>();
                var bytes = GetTypedArrayBytes(selfObj);
                var selfOffset = (int)selfObj.Get("byteOffset").ToNumber();
                if (bytes is not null)
                {
                    for (var i = 0; i < len; i++)
                    {
                        items.Add(JsNumber.Create(getter(bytes, selfOffset + i * bytesPerElement)));
                    }
                }
                return BuiltinHelper.CreateListIterator(items, realm);
            }, 0), writable: true, enumerable: false, configurable: true));

        realm.InstallGlobal(name, ctor);
    }

    private static JsDynamicObject CreateTypedArray(JsDynamicObject buffer, int byteOffset, int length,
        int bytesPerElement, JsDynamicObject proto, string typeName,
        Func<byte[], int, double> getter, Action<byte[], int, double> setter, Realm realm)
    {
        var ta = new JsTypedArrayObject(buffer, byteOffset, length, bytesPerElement, getter, setter)
        {
            Prototype = proto
        };
        ta.DefineOwnProperty("buffer", PropertyDescriptor.Data(buffer, writable: false, enumerable: false, configurable: false));
        ta.DefineOwnProperty("byteOffset", PropertyDescriptor.Data(JsNumber.Create(byteOffset), writable: false, enumerable: false, configurable: false));
        ta.DefineOwnProperty("byteLength", PropertyDescriptor.Data(JsNumber.Create(length * bytesPerElement), writable: false, enumerable: false, configurable: false));
        ta.DefineOwnProperty("length", PropertyDescriptor.Data(JsNumber.Create(length), writable: false, enumerable: false, configurable: false));
        return ta;
    }

    private static byte[]? GetTypedArrayBytes(JsDynamicObject ta)
    {
        var buffer = ta.Get("buffer");
        if (buffer is JsDynamicObject bufObj)
        {
            return ArrayBufferConstructor.GetByteArray(bufObj);
        }
        return null;
    }

    private static int NormalizeIndex(int index, int length)
    {
        if (index < 0) return Math.Max(0, length + index);
        return Math.Min(index, length);
    }
}

/// <summary>
/// Typed array object with indexed property access for element read/write.
/// </summary>
internal sealed class JsTypedArrayObject : JsDynamicObject
{
    private readonly JsDynamicObject _buffer;
    private readonly int _byteOffset;
    private readonly int _length;
    private readonly int _bytesPerElement;
    private readonly Func<byte[], int, double> _getter;
    private readonly Action<byte[], int, double> _setter;

    public JsTypedArrayObject(JsDynamicObject buffer, int byteOffset, int length, int bytesPerElement,
        Func<byte[], int, double> getter, Action<byte[], int, double> setter)
    {
        _buffer = buffer;
        _byteOffset = byteOffset;
        _length = length;
        _bytesPerElement = bytesPerElement;
        _getter = getter;
        _setter = setter;
    }

    public override JsValue Get(string name)
    {
        if (int.TryParse(name, NumberStyles.Integer, CultureInfo.InvariantCulture, out var idx))
        {
            if (idx >= 0 && idx < _length)
            {
                var bytes = ArrayBufferConstructor.GetByteArray(_buffer);
                if (bytes is not null)
                {
                    return JsNumber.Create(_getter(bytes, _byteOffset + idx * _bytesPerElement));
                }
            }
            return JsValue.Undefined;
        }
        return base.Get(name);
    }

    public override void Set(string name, JsValue value)
    {
        if (int.TryParse(name, NumberStyles.Integer, CultureInfo.InvariantCulture, out var idx))
        {
            if (idx >= 0 && idx < _length)
            {
                var bytes = ArrayBufferConstructor.GetByteArray(_buffer);
                if (bytes is not null)
                {
                    _setter(bytes, _byteOffset + idx * _bytesPerElement, value.ToNumber());
                    return;
                }
            }
            return;
        }
        base.Set(name, value);
    }
}
