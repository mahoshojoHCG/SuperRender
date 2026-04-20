using System.Globalization;
using System.Text;
using SuperRender.EcmaScript.Runtime;

namespace SuperRender.EcmaScript.NodeSimulator.Modules;

/// <summary>
/// Minimal Node.js Buffer implementation backed by a byte array.
/// Supports utf8, ascii, latin1, hex, base64, base64url, utf16le encodings.
/// </summary>
public sealed class BufferObject : JsObject
{
    public byte[] Bytes { get; }
    public int Offset { get; }
    public int Length { get; }

    public BufferObject(byte[] bytes) : this(bytes, 0, bytes.Length) { }

    public BufferObject(byte[] bytes, int offset, int length)
    {
        Bytes = bytes;
        Offset = offset;
        Length = length;
        InstallMethods();
    }

    public Span<byte> Span => Bytes.AsSpan(Offset, Length);

    public override JsValue Get(string name)
    {
        if (name == "length") return JsNumber.Create(Length);
        if (uint.TryParse(name, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) && i < (uint)Length)
        {
            return JsNumber.Create(Bytes[Offset + (int)i]);
        }
        return base.Get(name);
    }

    public override void Set(string name, JsValue value)
    {
        if (uint.TryParse(name, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) && i < (uint)Length)
        {
            Bytes[Offset + (int)i] = (byte)((int)value.ToNumber() & 0xFF);
            return;
        }
        base.Set(name, value);
    }

    private static PropertyDescriptor MethodDesc(string name, Func<JsValue, JsValue[], JsValue> impl, int length) =>
        PropertyDescriptor.Data(JsFunction.CreateNative(name, impl, length), writable: true, enumerable: false, configurable: true);

    private void InstallMethods()
    {
        DefineOwnProperty("toString", MethodDesc("toString", (thisArg, args) =>
        {
            var enc = args.Length > 0 && args[0] is JsString es ? es.Value : "utf8";
            var start = args.Length > 1 && args[1] is not JsUndefined ? (int)args[1].ToNumber() : 0;
            var end = args.Length > 2 && args[2] is not JsUndefined ? (int)args[2].ToNumber() : Length;
            start = Math.Clamp(start, 0, Length);
            end = Math.Clamp(end, start, Length);
            return new JsString(BufferModule.Decode(Bytes, Offset + start, end - start, enc));
        }, 3));

        DefineOwnProperty("slice", MethodDesc("slice", (_, args) => Subarray(args), 2));
        DefineOwnProperty("subarray", MethodDesc("subarray", (_, args) => Subarray(args), 2));

        DefineOwnProperty("equals", MethodDesc("equals", (_, args) =>
        {
            if (args.Length == 0 || args[0] is not BufferObject other) return False;
            if (other.Length != Length) return False;
            return Span.SequenceEqual(other.Span) ? True : False;
        }, 1));

        DefineOwnProperty("write", MethodDesc("write", (_, args) =>
        {
            if (args.Length == 0 || args[0] is not JsString strJs)
                throw new Runtime.Errors.JsTypeError("The \"string\" argument must be of type string");
            var str = strJs.Value;
            var offset = args.Length > 1 && args[1] is not JsUndefined ? (int)args[1].ToNumber() : 0;
            var length = args.Length > 2 && args[2] is not JsUndefined ? (int)args[2].ToNumber() : Length - offset;
            var enc = args.Length > 3 && args[3] is JsString es2 ? es2.Value : "utf8";
            var bytes = BufferModule.Encode(str, enc);
            var toWrite = Math.Min(length, Math.Min(bytes.Length, Length - offset));
            Array.Copy(bytes, 0, Bytes, Offset + offset, toWrite);
            return JsNumber.Create(toWrite);
        }, 4));

        DefineOwnProperty("fill", MethodDesc("fill", (thisArg, args) =>
        {
            var val = args.Length > 0 ? args[0] : Undefined;
            byte b = val is JsString s
                ? (s.Value.Length > 0 ? (byte)s.Value[0] : (byte)0)
                : (byte)((int)val.ToNumber() & 0xFF);
            Array.Fill(Bytes, b, Offset, Length);
            return thisArg;
        }, 3));

        DefineOwnProperty("copy", MethodDesc("copy", (_, args) =>
        {
            if (args.Length == 0 || args[0] is not BufferObject target) return JsNumber.Create(0);
            var targetStart = args.Length > 1 && args[1] is not JsUndefined ? (int)args[1].ToNumber() : 0;
            var sourceStart = args.Length > 2 && args[2] is not JsUndefined ? (int)args[2].ToNumber() : 0;
            var sourceEnd = args.Length > 3 && args[3] is not JsUndefined ? (int)args[3].ToNumber() : Length;
            sourceStart = Math.Clamp(sourceStart, 0, Length);
            sourceEnd = Math.Clamp(sourceEnd, sourceStart, Length);
            var toCopy = Math.Min(sourceEnd - sourceStart, target.Length - targetStart);
            if (toCopy <= 0) return JsNumber.Create(0);
            Array.Copy(Bytes, Offset + sourceStart, target.Bytes, target.Offset + targetStart, toCopy);
            return JsNumber.Create(toCopy);
        }, 4));

        DefineOwnProperty("indexOf", MethodDesc("indexOf", (_, args) =>
        {
            var needle = EncodeArg(args, 0);
            var from = args.Length > 1 && args[1] is not JsUndefined ? (int)args[1].ToNumber() : 0;
            if (from < 0) from = Math.Max(0, Length + from);
            for (int i = from; i <= Length - needle.Length; i++)
            {
                bool match = true;
                for (int j = 0; j < needle.Length; j++)
                {
                    if (Bytes[Offset + i + j] != needle[j]) { match = false; break; }
                }
                if (match) return JsNumber.Create(i);
            }
            return JsNumber.Create(-1);
        }, 2));

        DefineOwnProperty("includes", MethodDesc("includes", (thisArg, args) =>
        {
            var idx = Get("indexOf");
            if (idx is JsFunction fn) return fn.Call(thisArg, args).ToNumber() >= 0 ? True : False;
            return False;
        }, 2));

        DefineOwnProperty("readUInt8", MethodDesc("readUInt8", (_, args) =>
        {
            var o = args.Length > 0 && args[0] is not JsUndefined ? (int)args[0].ToNumber() : 0;
            return JsNumber.Create(Bytes[Offset + o]);
        }, 1));
        DefineOwnProperty("writeUInt8", MethodDesc("writeUInt8", (_, args) =>
        {
            var v = args.Length > 0 ? (byte)((int)args[0].ToNumber() & 0xFF) : (byte)0;
            var o = args.Length > 1 && args[1] is not JsUndefined ? (int)args[1].ToNumber() : 0;
            Bytes[Offset + o] = v;
            return JsNumber.Create(o + 1);
        }, 2));
    }

    private static byte[] EncodeArg(JsValue[] args, int index)
    {
        var v = index < args.Length ? args[index] : Undefined;
        if (v is JsString s) return BufferModule.Encode(s.Value, "utf8");
        if (v is BufferObject b) return b.Span.ToArray();
        if (v is JsNumber n) return [(byte)((int)n.Value & 0xFF)];
        return Array.Empty<byte>();
    }

    private BufferObject Subarray(JsValue[] args)
    {
        var start = args.Length > 0 && args[0] is not JsUndefined ? (int)args[0].ToNumber() : 0;
        var end = args.Length > 1 && args[1] is not JsUndefined ? (int)args[1].ToNumber() : Length;
        start = Math.Clamp(start < 0 ? Length + start : start, 0, Length);
        end = Math.Clamp(end < 0 ? Length + end : end, start, Length);
        return new BufferObject(Bytes, Offset + start, end - start);
    }
}

/// <summary>
/// Installs the Node Buffer constructor as a global.
/// </summary>
public static class BufferModule
{
    private static PropertyDescriptor MethodDesc(string name, Func<JsValue, JsValue[], JsValue> impl, int length) =>
        PropertyDescriptor.Data(JsFunction.CreateNative(name, impl, length), writable: true, enumerable: false, configurable: true);

    public static JsFunction Install(Realm realm)
    {
        var ctor = JsFunction.CreateNative("Buffer", (_, _) =>
            throw new Runtime.Errors.JsTypeError("Calling the Buffer constructor directly is not supported; use Buffer.from/alloc"), 0);

        ctor.DefineOwnProperty("from", MethodDesc("from", (_, args) =>
        {
            var src = args.Length > 0 ? args[0] : JsValue.Undefined;
            if (src is JsString s)
            {
                var enc = args.Length > 1 && args[1] is JsString es ? es.Value : "utf8";
                return new BufferObject(Encode(s.Value, enc));
            }
            if (src is JsArray a)
            {
                var len = (int)a.Get("length").ToNumber();
                var bytes = new byte[len];
                for (int i = 0; i < len; i++)
                {
                    bytes[i] = (byte)((int)a.Get(i.ToString(CultureInfo.InvariantCulture)).ToNumber() & 0xFF);
                }
                return new BufferObject(bytes);
            }
            if (src is BufferObject b)
            {
                var copy = new byte[b.Length];
                b.Span.CopyTo(copy);
                return new BufferObject(copy);
            }
            throw new Runtime.Errors.JsTypeError("Buffer.from: unsupported argument");
        }, 3));

        ctor.DefineOwnProperty("alloc", MethodDesc("alloc", (_, args) =>
        {
            var size = args.Length > 0 ? (int)args[0].ToNumber() : 0;
            if (size < 0) throw new Runtime.Errors.JsRangeError("Buffer size must be non-negative");
            var bytes = new byte[size];
            var fill = args.Length > 1 ? args[1] : JsValue.Undefined;
            if (fill is JsNumber fn)
            {
                Array.Fill(bytes, (byte)((int)fn.Value & 0xFF));
            }
            else if (fill is JsString fs)
            {
                var enc = args.Length > 2 && args[2] is JsString es ? es.Value : "utf8";
                var patt = Encode(fs.Value, enc);
                if (patt.Length > 0)
                {
                    for (int i = 0; i < size; i++) bytes[i] = patt[i % patt.Length];
                }
            }
            return new BufferObject(bytes);
        }, 3));

        ctor.DefineOwnProperty("allocUnsafe", MethodDesc("allocUnsafe", (_, args) =>
        {
            var size = args.Length > 0 ? (int)args[0].ToNumber() : 0;
            if (size < 0) throw new Runtime.Errors.JsRangeError("Buffer size must be non-negative");
            return new BufferObject(new byte[size]);
        }, 1));

        ctor.DefineOwnProperty("byteLength", MethodDesc("byteLength", (_, args) =>
        {
            var v = args.Length > 0 ? args[0] : JsValue.Undefined;
            var enc = args.Length > 1 && args[1] is JsString es ? es.Value : "utf8";
            if (v is BufferObject b) return JsNumber.Create(b.Length);
            if (v is JsString s) return JsNumber.Create(Encode(s.Value, enc).Length);
            throw new Runtime.Errors.JsTypeError("Buffer.byteLength: unsupported argument");
        }, 2));

        ctor.DefineOwnProperty("isBuffer", MethodDesc("isBuffer", (_, args) =>
            args.Length > 0 && args[0] is BufferObject ? JsValue.True : JsValue.False, 1));

        ctor.DefineOwnProperty("isEncoding", MethodDesc("isEncoding", (_, args) =>
        {
            if (args.Length == 0 || args[0] is not JsString s) return JsValue.False;
            return IsSupportedEncoding(s.Value) ? JsValue.True : JsValue.False;
        }, 1));

        ctor.DefineOwnProperty("concat", MethodDesc("concat", (_, args) =>
        {
            if (args.Length == 0 || args[0] is not JsArray list)
                throw new Runtime.Errors.JsTypeError("Buffer.concat expects an array");
            var totalLen = list.DenseLength;
            var parts = new List<BufferObject>();
            int total = 0;
            for (int i = 0; i < totalLen; i++)
            {
                if (list.Get(i.ToString(CultureInfo.InvariantCulture)) is BufferObject b)
                {
                    parts.Add(b);
                    total += b.Length;
                }
            }
            if (args.Length > 1 && args[1] is JsNumber rn) total = Math.Min(total, (int)rn.Value);
            var result = new byte[total];
            int off = 0;
            foreach (var p in parts)
            {
                if (off >= total) break;
                var take = Math.Min(p.Length, total - off);
                Array.Copy(p.Bytes, p.Offset, result, off, take);
                off += take;
            }
            return new BufferObject(result);
        }, 2));

        realm.InstallGlobal("Buffer", ctor);
        return ctor;
    }

    internal static bool IsSupportedEncoding(string encoding) => Normalize(encoding) switch
    {
        "utf8" or "utf-8" or "ascii" or "latin1" or "binary" or "hex" or "base64" or "base64url" or "utf16le" or "ucs2" or "ucs-2" or "utf-16le" => true,
        _ => false,
    };

    internal static byte[] Encode(string input, string encoding) => Normalize(encoding) switch
    {
        "utf8" or "utf-8" => Encoding.UTF8.GetBytes(input),
        "ascii" => Encoding.ASCII.GetBytes(MaskAscii(input)),
        "latin1" or "binary" => Encoding.Latin1.GetBytes(input),
        "utf16le" or "ucs2" or "ucs-2" or "utf-16le" => Encoding.Unicode.GetBytes(input),
        "hex" => DecodeHex(input),
        "base64" => Convert.FromBase64String(PadBase64(input.Replace('-', '+').Replace('_', '/'))),
        "base64url" => Convert.FromBase64String(PadBase64(input.Replace('-', '+').Replace('_', '/'))),
        _ => throw new Runtime.Errors.JsTypeError($"Unknown encoding: {encoding}"),
    };

    internal static string Decode(byte[] bytes, int offset, int count, string encoding) => Normalize(encoding) switch
    {
        "utf8" or "utf-8" => Encoding.UTF8.GetString(bytes, offset, count),
        "ascii" => Encoding.ASCII.GetString(bytes, offset, count),
        "latin1" or "binary" => Encoding.Latin1.GetString(bytes, offset, count),
        "utf16le" or "ucs2" or "ucs-2" or "utf-16le" => Encoding.Unicode.GetString(bytes, offset, count),
        "hex" => Convert.ToHexString(bytes, offset, count).ToLowerInvariant(),
        "base64" => Convert.ToBase64String(bytes, offset, count),
        "base64url" => Convert.ToBase64String(bytes, offset, count).Replace('+', '-').Replace('/', '_').TrimEnd('='),
        _ => throw new Runtime.Errors.JsTypeError($"Unknown encoding: {encoding}"),
    };

    private static string Normalize(string e) => e.Trim().ToLowerInvariant();

    private static string MaskAscii(string s)
    {
        var chars = s.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            if (chars[i] > 127) chars[i] = (char)(chars[i] & 0x7F);
        }
        return new string(chars);
    }

    private static byte[] DecodeHex(string input)
    {
        var clean = new StringBuilder(input.Length);
        foreach (var ch in input)
        {
            if ((ch >= '0' && ch <= '9') || (ch >= 'a' && ch <= 'f') || (ch >= 'A' && ch <= 'F'))
            {
                clean.Append(ch);
            }
            else
            {
                break;
            }
        }
        if (clean.Length % 2 == 1) clean.Length--;
        return Convert.FromHexString(clean.ToString());
    }

    private static string PadBase64(string s)
    {
        var pad = s.Length % 4;
        return pad == 0 ? s : s + new string('=', 4 - pad);
    }
}
