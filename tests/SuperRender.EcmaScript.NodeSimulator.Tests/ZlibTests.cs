using Xunit;

namespace SuperRender.EcmaScript.NodeSimulator.Tests;

public class ZlibTests
{
    [Fact]
    public void Gzip_RoundTrip()
    {
        var (engine, _) = TestHost.Create();
        var code = @"
            const z = require('zlib');
            const compressed = z.gzipSync(Buffer.from('hello world'));
            z.gunzipSync(compressed).toString('utf8')";
        Assert.Equal("hello world", engine.RunString(code));
    }

    [Fact]
    public void Deflate_RoundTrip()
    {
        var (engine, _) = TestHost.Create();
        var code = @"
            const z = require('zlib');
            z.inflateSync(z.deflateSync(Buffer.from('abc123'))).toString('utf8')";
        Assert.Equal("abc123", engine.RunString(code));
    }

    [Fact]
    public void DeflateRaw_RoundTrip()
    {
        var (engine, _) = TestHost.Create();
        var code = @"
            const z = require('zlib');
            z.inflateRawSync(z.deflateRawSync(Buffer.from('raw'))).toString('utf8')";
        Assert.Equal("raw", engine.RunString(code));
    }

    [Fact]
    public void Brotli_RoundTrip()
    {
        var (engine, _) = TestHost.Create();
        var code = @"
            const z = require('zlib');
            z.brotliDecompressSync(z.brotliCompressSync(Buffer.from('brotli'))).toString('utf8')";
        Assert.Equal("brotli", engine.RunString(code));
    }

    [Fact]
    public void GzipCallback_RoundTrip()
    {
        var (engine, _) = TestHost.Create();
        var code = @"
            const z = require('zlib');
            let out = '';
            z.gzip(Buffer.from('cb'), (err, data) => {
                if (err) out = 'err';
                else z.gunzip(data, (e2, d2) => out = d2.toString('utf8'));
            });
            out";
        Assert.Equal("cb", engine.RunString(code));
    }

    [Fact]
    public void Gunzip_CorruptedThrows()
    {
        var (engine, _) = TestHost.Create();
        var code = @"
            try { require('zlib').gunzipSync(Buffer.from([1,2,3,4])); 'ok' }
            catch (e) { 'threw' }";
        Assert.Equal("threw", engine.RunString(code));
    }
}
