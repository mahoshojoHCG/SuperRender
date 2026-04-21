using System.Security.Cryptography;
using System.Text;
using SuperRender.EcmaScript.Runtime;

namespace SuperRender.EcmaScript.NodeSimulator.Modules;

/// <summary>
/// Node.js `crypto` module (hash/hmac/random subset).
/// Cross-platform via System.Security.Cryptography.
/// </summary>
[JsObject]
public sealed partial class CryptoModule : JsObjectBase
{
    private readonly Realm _realm;

    public CryptoModule(Realm realm)
    {
        _realm = realm;
        Prototype = realm.ObjectPrototype;
    }

    public static CryptoModule Create(Realm realm) => new(realm);

    [JsMethod("randomUUID")]
    public static string RandomUUID() => Guid.NewGuid().ToString("D");

    [JsMethod("randomBytes")]
    public static JsValue RandomBytes(JsValue _, JsValue[] args)
    {
        int size = args.Length > 0 ? (int)args[0].ToNumber() : 0;
        if (size < 0) throw new Runtime.Errors.JsRangeError("size must be non-negative");
        var bytes = new byte[size];
        RandomNumberGenerator.Fill(bytes);
        return new BufferObject(bytes);
    }

    [JsMethod("randomFillSync")]
    public static JsValue RandomFillSync(JsValue _, JsValue[] args)
    {
        if (args.Length == 0 || args[0] is not BufferObject buf)
            throw new Runtime.Errors.JsTypeError("randomFillSync requires a Buffer");
        int offset = args.Length > 1 && args[1] is not JsUndefined ? (int)args[1].ToNumber() : 0;
        int size = args.Length > 2 && args[2] is not JsUndefined ? (int)args[2].ToNumber() : buf.Length - offset;
        offset = Math.Clamp(offset, 0, buf.Length);
        size = Math.Clamp(size, 0, buf.Length - offset);
        RandomNumberGenerator.Fill(buf.Bytes.AsSpan(buf.Offset + offset, size));
        return buf;
    }

    [JsMethod("randomInt")]
    public static JsValue RandomInt(JsValue _, JsValue[] args)
    {
        int min = 0, max;
        if (args.Length >= 2) { min = (int)args[0].ToNumber(); max = (int)args[1].ToNumber(); }
        else if (args.Length == 1) { max = (int)args[0].ToNumber(); }
        else throw new Runtime.Errors.JsTypeError("randomInt requires max");
        if (max <= min) throw new Runtime.Errors.JsRangeError("max must be greater than min");
        return JsNumber.Create(RandomNumberGenerator.GetInt32(min, max));
    }

    [JsMethod("createHash")]
    public static JsValue CreateHashMethod(JsValue _, JsValue[] args)
    {
        var algo = args.Length > 0 && args[0] is JsString s ? s.Value : throw new Runtime.Errors.JsTypeError("algorithm must be a string");
        return new HashObject(algo);
    }

    [JsMethod("createHmac")]
    public static JsValue CreateHmacMethod(JsValue _, JsValue[] args)
    {
        var algo = args.Length > 0 && args[0] is JsString s ? s.Value : throw new Runtime.Errors.JsTypeError("algorithm must be a string");
        if (args.Length < 2) throw new Runtime.Errors.JsTypeError("createHmac requires a key");
        var key = ArgToBytes(args[1]);
        return new HmacObject(algo, key);
    }

    [JsMethod("timingSafeEqual")]
    public static JsValue TimingSafeEqual(JsValue _, JsValue[] args)
    {
        if (args.Length < 2 || args[0] is not BufferObject a || args[1] is not BufferObject b)
            throw new Runtime.Errors.JsTypeError("timingSafeEqual requires two Buffers");
        if (a.Length != b.Length) throw new Runtime.Errors.JsRangeError("Input buffers must have the same byte length");
        return CryptographicOperations.FixedTimeEquals(a.Span, b.Span) ? JsValue.True : JsValue.False;
    }

    [JsMethod("pbkdf2Sync")]
    public static JsValue Pbkdf2Sync(JsValue _, JsValue[] args)
    {
        if (args.Length < 5) throw new Runtime.Errors.JsTypeError("pbkdf2Sync requires 5 arguments");
        var password = ArgToBytes(args[0]);
        var salt = ArgToBytes(args[1]);
        int iterations = (int)args[2].ToNumber();
        int keylen = (int)args[3].ToNumber();
        var digest = args[4] is JsString ds ? ds.Value : "sha1";
        var derived = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, ToHashName(digest), keylen);
        return new BufferObject(derived);
    }

    [JsMethod("pbkdf2")]
    public static JsValue Pbkdf2(JsValue _, JsValue[] args)
    {
        if (args.Length < 6 || args[5] is not JsFunction cb)
            throw new Runtime.Errors.JsTypeError("pbkdf2 requires a callback");
        try
        {
            var password = ArgToBytes(args[0]);
            var salt = ArgToBytes(args[1]);
            int iterations = (int)args[2].ToNumber();
            int keylen = (int)args[3].ToNumber();
            var digest = args[4] is JsString ds ? ds.Value : "sha1";
            var derived = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, ToHashName(digest), keylen);
            cb.Call(JsValue.Undefined, [JsValue.Null, new BufferObject(derived)]);
        }
        catch (Exception ex)
        {
            cb.Call(JsValue.Undefined, [new JsString(ex.Message), JsValue.Undefined]);
        }
        return JsValue.Undefined;
    }

    [JsMethod("getHashes")]
    public JsValue GetHashes()
    {
        var arr = new JsArray { Prototype = _realm.ArrayPrototype };
        foreach (var h in HashNames) arr.Push(new JsString(h));
        return arr;
    }

    private static readonly string[] HashNames = ["md5", "sha1", "sha256", "sha384", "sha512"];

    internal static byte[] ArgToBytes(JsValue v) => v switch
    {
        BufferObject b => b.Span.ToArray(),
        JsString s => Encoding.UTF8.GetBytes(s.Value),
        _ => throw new Runtime.Errors.JsTypeError("expected string or Buffer"),
    };

    internal static HashAlgorithmName ToHashName(string algo) => algo.ToLowerInvariant() switch
    {
        "md5" => HashAlgorithmName.MD5,
        "sha1" => HashAlgorithmName.SHA1,
        "sha256" => HashAlgorithmName.SHA256,
        "sha384" => HashAlgorithmName.SHA384,
        "sha512" => HashAlgorithmName.SHA512,
        _ => throw new Runtime.Errors.JsTypeError($"Unsupported digest: {algo}"),
    };

    internal static HashAlgorithm CreateHashAlgorithm(string algo) => algo.ToLowerInvariant() switch
    {
        "md5" => MD5.Create(),
        "sha1" => SHA1.Create(),
        "sha256" => SHA256.Create(),
        "sha384" => SHA384.Create(),
        "sha512" => SHA512.Create(),
        _ => throw new Runtime.Errors.JsTypeError($"Unsupported hash algorithm: {algo}"),
    };
}

/// <summary>Incremental hash object (`crypto.createHash().update().digest()`).</summary>
public sealed class HashObject : JsDynamicObject
{
    private readonly IncrementalHash _hash;
    private bool _finalized;

    public HashObject(string algo)
    {
        _hash = IncrementalHash.CreateHash(CryptoModule.ToHashName(algo));
        InstallMethods();
    }

    private void InstallMethods()
    {
        DefineOwnProperty("update", PropertyDescriptor.Data(JsFunction.CreateNative("update", (thisArg, args) =>
        {
            if (_finalized) throw new Runtime.Errors.JsErrorBase("Digest already called");
            if (args.Length == 0) throw new Runtime.Errors.JsTypeError("update requires data");
            var data = args[0] switch
            {
                BufferObject b => b.Span.ToArray(),
                JsString s => Encoding.UTF8.GetBytes(s.Value),
                _ => throw new Runtime.Errors.JsTypeError("data must be string or Buffer"),
            };
            _hash.AppendData(data);
            return thisArg;
        }, 2), writable: true, enumerable: false, configurable: true));

        DefineOwnProperty("digest", PropertyDescriptor.Data(JsFunction.CreateNative("digest", (_, args) =>
        {
            if (_finalized) throw new Runtime.Errors.JsErrorBase("Digest already called");
            _finalized = true;
            var bytes = _hash.GetHashAndReset();
            if (args.Length == 0 || args[0] is JsUndefined) return new BufferObject(bytes);
            var enc = ((JsString)args[0]).Value;
            return new JsString(BufferModule.Decode(bytes, 0, bytes.Length, enc));
        }, 1), writable: true, enumerable: false, configurable: true));
    }
}

/// <summary>Incremental HMAC object.</summary>
public sealed class HmacObject : JsDynamicObject
{
    private readonly HMAC _hmac;
    private bool _finalized;

    public HmacObject(string algo, byte[] key)
    {
        _hmac = algo.ToLowerInvariant() switch
        {
            "md5" => new HMACMD5(key),
            "sha1" => new HMACSHA1(key),
            "sha256" => new HMACSHA256(key),
            "sha384" => new HMACSHA384(key),
            "sha512" => new HMACSHA512(key),
            _ => throw new Runtime.Errors.JsTypeError($"Unsupported hmac algorithm: {algo}"),
        };
        InstallMethods();
    }

    private void InstallMethods()
    {
        DefineOwnProperty("update", PropertyDescriptor.Data(JsFunction.CreateNative("update", (thisArg, args) =>
        {
            if (_finalized) throw new Runtime.Errors.JsErrorBase("Digest already called");
            if (args.Length == 0) throw new Runtime.Errors.JsTypeError("update requires data");
            var data = args[0] switch
            {
                BufferObject b => b.Span.ToArray(),
                JsString s => Encoding.UTF8.GetBytes(s.Value),
                _ => throw new Runtime.Errors.JsTypeError("data must be string or Buffer"),
            };
            _hmac.TransformBlock(data, 0, data.Length, null, 0);
            return thisArg;
        }, 2), writable: true, enumerable: false, configurable: true));

        DefineOwnProperty("digest", PropertyDescriptor.Data(JsFunction.CreateNative("digest", (_, args) =>
        {
            if (_finalized) throw new Runtime.Errors.JsErrorBase("Digest already called");
            _finalized = true;
            _hmac.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            var bytes = _hmac.Hash ?? Array.Empty<byte>();
            if (args.Length == 0 || args[0] is JsUndefined) return new BufferObject(bytes);
            var enc = ((JsString)args[0]).Value;
            return new JsString(BufferModule.Decode(bytes, 0, bytes.Length, enc));
        }, 1), writable: true, enumerable: false, configurable: true));
    }
}
