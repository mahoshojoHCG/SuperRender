using System.IO.Compression;
using SuperRender.EcmaScript.Runtime;
using SuperRender.EcmaScript.Runtime.Builtins;

namespace SuperRender.EcmaScript.NodeSimulator.Modules;

/// <summary>
/// Node.js `zlib` module. Sync + callback + promise variants for gzip, deflate,
/// deflate-raw, and brotli, backed by <c>System.IO.Compression</c>.
/// </summary>
public static class ZlibModule
{
    private enum Kind { Gzip, Deflate, DeflateRaw, Brotli }

    private static PropertyDescriptor MethodDesc(string name, Func<JsValue, JsValue[], JsValue> impl, int length) =>
        PropertyDescriptor.Data(JsFunction.CreateNative(name, impl, length), writable: true, enumerable: false, configurable: true);

    public static JsDynamicObject Create(Realm realm)
    {
        var obj = new JsDynamicObject();

        void DefineSync(string name, Kind kind, bool compress)
        {
            obj.DefineOwnProperty(name, MethodDesc(name, (_, args) =>
            {
                var input = ExtractBytes(args);
                return new BufferObject(Transform(input, kind, compress));
            }, 2));
        }

        void DefineAsync(string name, Kind kind, bool compress)
        {
            obj.DefineOwnProperty(name, MethodDesc(name, (_, args) =>
            {
                if (args.Length == 0) throw new Runtime.Errors.JsTypeError($"{name} requires a buffer");
                JsFunction? cb = null;
                JsValue last = args[^1];
                if (last is JsFunction fn) cb = fn;
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
            }, 3));
        }

        DefineSync("gzipSync", Kind.Gzip, compress: true);
        DefineSync("gunzipSync", Kind.Gzip, compress: false);
        DefineSync("deflateSync", Kind.Deflate, compress: true);
        DefineSync("inflateSync", Kind.Deflate, compress: false);
        DefineSync("deflateRawSync", Kind.DeflateRaw, compress: true);
        DefineSync("inflateRawSync", Kind.DeflateRaw, compress: false);
        DefineSync("brotliCompressSync", Kind.Brotli, compress: true);
        DefineSync("brotliDecompressSync", Kind.Brotli, compress: false);

        DefineAsync("gzip", Kind.Gzip, compress: true);
        DefineAsync("gunzip", Kind.Gzip, compress: false);
        DefineAsync("deflate", Kind.Deflate, compress: true);
        DefineAsync("inflate", Kind.Deflate, compress: false);
        DefineAsync("deflateRaw", Kind.DeflateRaw, compress: true);
        DefineAsync("inflateRaw", Kind.DeflateRaw, compress: false);
        DefineAsync("brotliCompress", Kind.Brotli, compress: true);
        DefineAsync("brotliDecompress", Kind.Brotli, compress: false);

        // promises namespace
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
            promises.DefineOwnProperty(name, MethodDesc(name, (_, args) =>
            {
                var promise = new JsPromiseObject { Prototype = realm.PromisePrototype };
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
            }, 2));
        }
        obj.DefineOwnProperty("promises", PropertyDescriptor.Data(promises));

        var constants = new JsDynamicObject();
        constants.DefineOwnProperty("Z_NO_COMPRESSION", PropertyDescriptor.Data(JsNumber.Create(0)));
        constants.DefineOwnProperty("Z_BEST_SPEED", PropertyDescriptor.Data(JsNumber.Create(1)));
        constants.DefineOwnProperty("Z_BEST_COMPRESSION", PropertyDescriptor.Data(JsNumber.Create(9)));
        constants.DefineOwnProperty("Z_DEFAULT_COMPRESSION", PropertyDescriptor.Data(JsNumber.Create(-1)));
        obj.DefineOwnProperty("constants", PropertyDescriptor.Data(constants));
        return obj;
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
