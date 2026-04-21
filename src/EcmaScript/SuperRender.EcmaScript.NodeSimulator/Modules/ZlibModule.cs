using System.IO.Compression;
using SuperRender.EcmaScript.Runtime;
using SuperRender.EcmaScript.Runtime.Builtins;

namespace SuperRender.EcmaScript.NodeSimulator.Modules;

// JSGEN005/006/007: Node zlib mirrors the Node.js API — sync/callback/promise calls accept
// Buffer|string + optional options (legacy variadic) and return Buffer wrappers (JsValue).
// A typed migration would require IBuffer/IZlibOptions IJsType interfaces.
#pragma warning disable JSGEN005, JSGEN006, JSGEN007

/// <summary>
/// Node.js `zlib` module. Sync + callback + promise variants for gzip, deflate,
/// deflate-raw, and brotli, backed by <c>System.IO.Compression</c>.
/// </summary>
[JsObject]
public sealed partial class ZlibModule : JsDynamicObject
{
    private enum Kind { Gzip, Deflate, DeflateRaw, Brotli }

    private readonly Realm _realm;

    public ZlibModule(Realm realm)
    {
        _realm = realm;
        Prototype = realm.ObjectPrototype;
        DefineOwnProperty("promises", PropertyDescriptor.Data(BuildPromises(realm)));
        DefineOwnProperty("constants", PropertyDescriptor.Data(BuildConstants()));
    }

    public static ZlibModule Create(Realm realm) => new(realm);

    [JsMethod("gzipSync")] public static JsValue GzipSync(JsValue _, JsValue[] args) => Sync(args, Kind.Gzip, compress: true);
    [JsMethod("gunzipSync")] public static JsValue GunzipSync(JsValue _, JsValue[] args) => Sync(args, Kind.Gzip, compress: false);
    [JsMethod("deflateSync")] public static JsValue DeflateSync(JsValue _, JsValue[] args) => Sync(args, Kind.Deflate, compress: true);
    [JsMethod("inflateSync")] public static JsValue InflateSync(JsValue _, JsValue[] args) => Sync(args, Kind.Deflate, compress: false);
    [JsMethod("deflateRawSync")] public static JsValue DeflateRawSync(JsValue _, JsValue[] args) => Sync(args, Kind.DeflateRaw, compress: true);
    [JsMethod("inflateRawSync")] public static JsValue InflateRawSync(JsValue _, JsValue[] args) => Sync(args, Kind.DeflateRaw, compress: false);
    [JsMethod("brotliCompressSync")] public static JsValue BrotliCompressSync(JsValue _, JsValue[] args) => Sync(args, Kind.Brotli, compress: true);
    [JsMethod("brotliDecompressSync")] public static JsValue BrotliDecompressSync(JsValue _, JsValue[] args) => Sync(args, Kind.Brotli, compress: false);

    [JsMethod("gzip")] public static JsValue GzipAsync(JsValue _, JsValue[] args) => Async(args, Kind.Gzip, compress: true);
    [JsMethod("gunzip")] public static JsValue GunzipAsync(JsValue _, JsValue[] args) => Async(args, Kind.Gzip, compress: false);
    [JsMethod("deflate")] public static JsValue DeflateAsync(JsValue _, JsValue[] args) => Async(args, Kind.Deflate, compress: true);
    [JsMethod("inflate")] public static JsValue InflateAsync(JsValue _, JsValue[] args) => Async(args, Kind.Deflate, compress: false);
    [JsMethod("deflateRaw")] public static JsValue DeflateRawAsync(JsValue _, JsValue[] args) => Async(args, Kind.DeflateRaw, compress: true);
    [JsMethod("inflateRaw")] public static JsValue InflateRawAsync(JsValue _, JsValue[] args) => Async(args, Kind.DeflateRaw, compress: false);
    [JsMethod("brotliCompress")] public static JsValue BrotliCompressAsync(JsValue _, JsValue[] args) => Async(args, Kind.Brotli, compress: true);
    [JsMethod("brotliDecompress")] public static JsValue BrotliDecompressAsync(JsValue _, JsValue[] args) => Async(args, Kind.Brotli, compress: false);

    private static BufferObject Sync(JsValue[] args, Kind kind, bool compress)
    {
        var input = ExtractBytes(args);
        return new BufferObject(Transform(input, kind, compress));
    }

    private static JsValue Async(JsValue[] args, Kind kind, bool compress)
    {
        if (args.Length == 0) throw new Runtime.Errors.JsTypeError("requires a buffer");
        JsFunction? cb = null;
        if (args[^1] is JsFunction fn) cb = fn;
        var input = ExtractBytes(args);
        try
        {
            var result = new BufferObject(Transform(input, kind, compress));
            if (cb is not null) cb.Call(JsValue.Undefined, [JsValue.Null, result]);
        }
        catch (Exception ex)
        {
            if (cb is null) throw;
            cb.Call(JsValue.Undefined, [new JsString(ex.Message), JsValue.Undefined]);
        }
        return JsValue.Undefined;
    }

    private static JsDynamicObject BuildPromises(Realm realm)
    {
        var promises = new JsDynamicObject();
        foreach (var (name, kind, compress) in new (string, Kind, bool)[]
        {
            ("gzip", Kind.Gzip, true), ("gunzip", Kind.Gzip, false),
            ("deflate", Kind.Deflate, true), ("inflate", Kind.Deflate, false),
            ("deflateRaw", Kind.DeflateRaw, true), ("inflateRaw", Kind.DeflateRaw, false),
            ("brotliCompress", Kind.Brotli, true), ("brotliDecompress", Kind.Brotli, false),
        })
        {
            var capturedKind = kind;
            var capturedCompress = compress;
            promises.DefineOwnProperty(name, PropertyDescriptor.Data(
                JsFunction.CreateNative(name, (_, args) =>
                {
                    var promise = new JsPromise { Prototype = realm.PromisePrototype };
                    try
                    {
                        var input = ExtractBytes(args);
                        PromiseConstructor.ResolvePromise(promise, new BufferObject(Transform(input, capturedKind, capturedCompress)), realm);
                    }
                    catch (Exception ex)
                    {
                        PromiseConstructor.RejectPromise(promise, new JsString(ex.Message));
                    }
                    return promise;
                }, 2),
                writable: true, enumerable: false, configurable: true));
        }
        return promises;
    }

    private static JsDynamicObject BuildConstants()
    {
        var constants = new JsDynamicObject();
        constants.DefineOwnProperty("Z_NO_COMPRESSION", PropertyDescriptor.Data(JsNumber.Create(0)));
        constants.DefineOwnProperty("Z_BEST_SPEED", PropertyDescriptor.Data(JsNumber.Create(1)));
        constants.DefineOwnProperty("Z_BEST_COMPRESSION", PropertyDescriptor.Data(JsNumber.Create(9)));
        constants.DefineOwnProperty("Z_DEFAULT_COMPRESSION", PropertyDescriptor.Data(JsNumber.Create(-1)));
        return constants;
    }

    private static byte[] ExtractBytes(JsValue[] args)
    {
        if (args.Length == 0) throw new Runtime.Errors.JsTypeError("data required");
        return args[0] switch
        {
            BufferObject b => b.Span.ToArray(),
            JsString s => System.Text.Encoding.UTF8.GetBytes(s.Value),
            _ => throw new Runtime.Errors.JsTypeError("data must be string or Buffer"),
        };
    }

    private static byte[] Transform(byte[] input, Kind kind, bool compress)
    {
        using var outStream = new MemoryStream();
        if (compress)
        {
            Stream writer = kind switch
            {
                Kind.Gzip => new GZipStream(outStream, CompressionLevel.Optimal, leaveOpen: true),
                Kind.Deflate => new ZLibStream(outStream, CompressionLevel.Optimal, leaveOpen: true),
                Kind.DeflateRaw => new DeflateStream(outStream, CompressionLevel.Optimal, leaveOpen: true),
                Kind.Brotli => new BrotliStream(outStream, CompressionLevel.Optimal, leaveOpen: true),
                _ => throw new InvalidOperationException(),
            };
            using (writer) writer.Write(input, 0, input.Length);
            return outStream.ToArray();
        }
        using var inStream = new MemoryStream(input);
        Stream reader = kind switch
        {
            Kind.Gzip => new GZipStream(inStream, CompressionMode.Decompress),
            Kind.Deflate => new ZLibStream(inStream, CompressionMode.Decompress),
            Kind.DeflateRaw => new DeflateStream(inStream, CompressionMode.Decompress),
            Kind.Brotli => new BrotliStream(inStream, CompressionMode.Decompress),
            _ => throw new InvalidOperationException(),
        };
        using (reader) reader.CopyTo(outStream);
        return outStream.ToArray();
    }
}
