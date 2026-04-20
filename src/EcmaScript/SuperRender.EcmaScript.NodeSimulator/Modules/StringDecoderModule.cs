using System.Text;
using SuperRender.EcmaScript.Runtime;

namespace SuperRender.EcmaScript.NodeSimulator.Modules;

/// <summary>
/// Node.js `string_decoder` module. Implements a byte-to-string decoder that
/// correctly handles partial multi-byte sequences across chunks.
/// Supports utf8, utf16le/ucs2, base64, hex, latin1/binary, ascii.
/// </summary>
public static class StringDecoderModule
{
    private static PropertyDescriptor MethodDesc(string name, Func<JsValue, JsValue[], JsValue> impl, int length) =>
        PropertyDescriptor.Data(JsFunction.CreateNative(name, impl, length), writable: true, enumerable: false, configurable: true);

    public static JsDynamicObject Create(Realm realm)
    {
        var proto = new JsDynamicObject { Prototype = realm.ObjectPrototype };
        InstallMethods(proto);

        var ctor = new JsFunction
        {
            Name = "StringDecoder",
            Length = 1,
            IsConstructor = true,
            PrototypeObject = proto,
            CallTarget = (thisArg, args) =>
            {
                var enc = args.Length > 0 && args[0] is JsString s ? s.Value : "utf8";
                StringDecoderInstance target = thisArg is StringDecoderInstance inst ? inst : new StringDecoderInstance(enc);
                target.Prototype = proto;
                return target;
            },
        };
        ctor.ConstructTarget = args =>
        {
            var enc = args.Length > 0 && args[0] is JsString s ? s.Value : "utf8";
            var obj = new StringDecoderInstance(enc) { Prototype = proto };
            return obj;
        };
        proto.DefineOwnProperty("constructor", PropertyDescriptor.Data(ctor, writable: true, enumerable: false, configurable: true));

        var module = new JsDynamicObject();
        module.DefineOwnProperty("StringDecoder", PropertyDescriptor.Data(ctor));
        return module;
    }

    private static void InstallMethods(JsDynamicObject proto)
    {
        proto.DefineOwnProperty("write", MethodDesc("write", (thisArg, args) =>
        {
            if (thisArg is not StringDecoderInstance s) throw new Runtime.Errors.JsTypeError("StringDecoder.write called on incompatible receiver");
            var bytes = ExtractBytes(args.Length > 0 ? args[0] : JsValue.Undefined);
            return new JsString(s.Write(bytes));
        }, 1));

        proto.DefineOwnProperty("end", MethodDesc("end", (thisArg, args) =>
        {
            if (thisArg is not StringDecoderInstance s) throw new Runtime.Errors.JsTypeError("StringDecoder.end called on incompatible receiver");
            string mid = "";
            if (args.Length > 0 && args[0] is not JsUndefined)
            {
                var bytes = ExtractBytes(args[0]);
                mid = s.Write(bytes);
            }
            return new JsString(mid + s.End());
        }, 1));
    }

    private static byte[] ExtractBytes(JsValue v)
    {
        if (v is BufferObject b) return b.Span.ToArray();
        if (v is JsString s) return Encoding.UTF8.GetBytes(s.Value);
        if (v is JsArray a)
        {
            var arr = new byte[a.DenseLength];
            for (int i = 0; i < arr.Length; i++)
                arr[i] = (byte)((int)a.Get(i.ToString(System.Globalization.CultureInfo.InvariantCulture)).ToNumber() & 0xFF);
            return arr;
        }
        return Array.Empty<byte>();
    }

    internal static string NormalizeEncoding(string e) => e.Trim().ToLowerInvariant() switch
    {
        "utf-8" => "utf8",
        "ucs-2" or "ucs2" or "utf-16le" => "utf16le",
        "binary" => "latin1",
        var x => x,
    };
}

/// <summary>Decoder instance holding per-call partial-byte state.</summary>
public sealed class StringDecoderInstance : JsDynamicObject
{
    public string Encoding { get; }
    private readonly byte[] _tail = new byte[4];
    private int _tailLen;

    public StringDecoderInstance(string encoding)
    {
        Encoding = StringDecoderModule.NormalizeEncoding(encoding);
        DefineOwnProperty("encoding", PropertyDescriptor.Data(new JsString(Encoding), writable: false, enumerable: true, configurable: true));
    }

    public string Write(byte[] input) => Encoding switch
    {
        "utf8" => WriteUtf8(input),
        "utf16le" => WriteUtf16Le(input),
        "base64" => WriteBase64(input),
        "hex" => Convert.ToHexString(input).ToLowerInvariant(),
        "latin1" => System.Text.Encoding.Latin1.GetString(input),
        "ascii" => System.Text.Encoding.ASCII.GetString(input),
        _ => throw new Runtime.Errors.JsTypeError($"Unknown encoding: {Encoding}"),
    };

    public string End()
    {
        if (_tailLen == 0) return "";
        string tail = Encoding switch
        {
            "utf8" => new string('\uFFFD', _tailLen),
            "utf16le" => _tailLen >= 2 ? System.Text.Encoding.Unicode.GetString(_tail, 0, _tailLen & ~1) : "",
            "base64" => FinishBase64(),
            _ => "",
        };
        _tailLen = 0;
        return tail;
    }

    private string WriteUtf8(byte[] input)
    {
        var combined = new byte[_tailLen + input.Length];
        Array.Copy(_tail, 0, combined, 0, _tailLen);
        Array.Copy(input, 0, combined, _tailLen, input.Length);
        _tailLen = 0;

        int validEnd = combined.Length;
        int incompleteStart = FindIncompleteUtf8Start(combined);
        if (incompleteStart >= 0)
        {
            int tailSize = combined.Length - incompleteStart;
            Array.Copy(combined, incompleteStart, _tail, 0, tailSize);
            _tailLen = tailSize;
            validEnd = incompleteStart;
        }
        return System.Text.Encoding.UTF8.GetString(combined, 0, validEnd);
    }

    private static int FindIncompleteUtf8Start(byte[] b)
    {
        int i = b.Length - 1;
        int steps = 0;
        while (i >= 0 && steps < 3)
        {
            byte x = b[i];
            if ((x & 0xC0) != 0x80)
            {
                int needed;
                if ((x & 0x80) == 0) needed = 1;
                else if ((x & 0xE0) == 0xC0) needed = 2;
                else if ((x & 0xF0) == 0xE0) needed = 3;
                else if ((x & 0xF8) == 0xF0) needed = 4;
                else return -1;
                int have = b.Length - i;
                return have < needed ? i : -1;
            }
            i--;
            steps++;
        }
        return -1;
    }

    private string WriteUtf16Le(byte[] input)
    {
        var combined = new byte[_tailLen + input.Length];
        Array.Copy(_tail, 0, combined, 0, _tailLen);
        Array.Copy(input, 0, combined, _tailLen, input.Length);
        _tailLen = 0;

        int validEnd = combined.Length - (combined.Length % 2);
        if (validEnd >= 2)
        {
            int w = (combined[validEnd - 1] << 8) | combined[validEnd - 2];
            if (w >= 0xD800 && w <= 0xDBFF) validEnd -= 2;
        }
        int tailSize = combined.Length - validEnd;
        if (tailSize > 0)
        {
            Array.Copy(combined, validEnd, _tail, 0, tailSize);
            _tailLen = tailSize;
        }
        return System.Text.Encoding.Unicode.GetString(combined, 0, validEnd);
    }

    private string WriteBase64(byte[] input)
    {
        int total = _tailLen + input.Length;
        var combined = new byte[total];
        Array.Copy(_tail, 0, combined, 0, _tailLen);
        Array.Copy(input, 0, combined, _tailLen, input.Length);
        _tailLen = 0;

        int complete = total - (total % 3);
        int tailSize = total - complete;
        if (tailSize > 0)
        {
            Array.Copy(combined, complete, _tail, 0, tailSize);
            _tailLen = tailSize;
        }
        return complete == 0 ? "" : Convert.ToBase64String(combined, 0, complete);
    }

    private string FinishBase64()
    {
        if (_tailLen == 0) return "";
        var padded = new byte[_tailLen];
        Array.Copy(_tail, 0, padded, 0, _tailLen);
        return Convert.ToBase64String(padded);
    }
}
