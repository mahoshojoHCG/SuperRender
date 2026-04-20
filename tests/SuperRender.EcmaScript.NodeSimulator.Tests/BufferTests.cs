using SuperRender.EcmaScript.NodeSimulator.Modules;
using Xunit;

namespace SuperRender.EcmaScript.NodeSimulator.Tests;

public class BufferTests
{
    [Fact]
    public void From_Utf8String_EncodesBytes()
    {
        var (engine, _) = TestHost.Create();
        var result = engine.Execute("Buffer.from('abc').length");
        Assert.Equal(3, (int)result.ToNumber());
    }

    [Fact]
    public void From_MultibyteUtf8_CountsBytesNotChars()
    {
        var (engine, _) = TestHost.Create();
        Assert.Equal(3, (int)engine.Execute("Buffer.from('€').length").ToNumber());
    }

    [Fact]
    public void ToString_Hex_ReturnsHexEncoding()
    {
        var (engine, _) = TestHost.Create();
        var hex = engine.RunString("Buffer.from('ABC').toString('hex')");
        Assert.Equal("414243", hex);
    }

    [Fact]
    public void ToString_Base64_RoundTrips()
    {
        var (engine, _) = TestHost.Create();
        var b64 = engine.RunString("Buffer.from('hello world').toString('base64')");
        Assert.Equal("aGVsbG8gd29ybGQ=", b64);
        var roundTrip = engine.RunString("Buffer.from('aGVsbG8gd29ybGQ=', 'base64').toString('utf8')");
        Assert.Equal("hello world", roundTrip);
    }

    [Fact]
    public void Alloc_FillsZeros()
    {
        var (engine, _) = TestHost.Create();
        var b = engine.Execute("Buffer.alloc(4)");
        var buf = Assert.IsType<BufferObject>(b);
        Assert.Equal(4, buf.Length);
        Assert.All(buf.Span.ToArray(), x => Assert.Equal(0, x));
    }

    [Fact]
    public void Alloc_WithFillByte_FillsBuffer()
    {
        var (engine, _) = TestHost.Create();
        var b = (BufferObject)engine.Execute("Buffer.alloc(3, 0x7F)");
        Assert.Equal([0x7F, 0x7F, 0x7F], b.Span.ToArray());
    }

    [Fact]
    public void ByteLength_Utf8_ReturnsBytes()
    {
        var (engine, _) = TestHost.Create();
        Assert.Equal(3, (int)engine.Execute("Buffer.byteLength('abc')").ToNumber());
        Assert.Equal(3, (int)engine.Execute("Buffer.byteLength('€')").ToNumber());
    }

    [Fact]
    public void IsBuffer_Discriminates()
    {
        var (engine, _) = TestHost.Create();
        Assert.True(engine.Execute("Buffer.isBuffer(Buffer.from('x'))").ToBoolean());
        Assert.False(engine.Execute("Buffer.isBuffer('x')").ToBoolean());
    }

    [Fact]
    public void Concat_CombinesBuffers()
    {
        var (engine, _) = TestHost.Create();
        var s = engine.RunString("Buffer.concat([Buffer.from('foo'), Buffer.from('bar')]).toString()");
        Assert.Equal("foobar", s);
    }

    [Fact]
    public void Concat_WithTotalLength_Truncates()
    {
        var (engine, _) = TestHost.Create();
        var s = engine.RunString("Buffer.concat([Buffer.from('foo'), Buffer.from('bar')], 4).toString()");
        Assert.Equal("foob", s);
    }

    [Fact]
    public void Slice_ReturnsSharedBacking()
    {
        var (engine, _) = TestHost.Create();
        engine.Execute("const b = Buffer.from([1,2,3,4]); const s = b.slice(1, 3); s[0] = 9;");
        Assert.Equal(9, (int)engine.Execute("b[1]").ToNumber());
    }

    [Fact]
    public void IndexOf_FindsPattern()
    {
        var (engine, _) = TestHost.Create();
        Assert.Equal(3, (int)engine.Execute("Buffer.from('abcdef').indexOf('de')").ToNumber());
        Assert.Equal(-1, (int)engine.Execute("Buffer.from('abc').indexOf('z')").ToNumber());
    }

    [Fact]
    public void IsEncoding_ValidatesEncodings()
    {
        var (engine, _) = TestHost.Create();
        Assert.True(engine.Execute("Buffer.isEncoding('utf8')").ToBoolean());
        Assert.True(engine.Execute("Buffer.isEncoding('hex')").ToBoolean());
        Assert.False(engine.Execute("Buffer.isEncoding('klingon')").ToBoolean());
    }
}
