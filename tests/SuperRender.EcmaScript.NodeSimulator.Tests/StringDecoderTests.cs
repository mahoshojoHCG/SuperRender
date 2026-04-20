using Xunit;

namespace SuperRender.EcmaScript.NodeSimulator.Tests;

public class StringDecoderTests
{
    [Fact]
    public void Write_Utf8Complete()
    {
        var (engine, _) = TestHost.Create();
        var code = @"
            const { StringDecoder } = require('string_decoder');
            const d = new StringDecoder('utf8');
            d.write(Buffer.from('hello'))";
        Assert.Equal("hello", engine.RunString(code));
    }

    [Fact]
    public void Write_SplitsMultiByteAcrossChunks()
    {
        var (engine, _) = TestHost.Create();
        // 'ä' UTF-8 is 0xC3 0xA4 — split across writes
        var code = @"
            const { StringDecoder } = require('string_decoder');
            const d = new StringDecoder('utf8');
            const a = d.write(Buffer.from([0xC3]));
            const b = d.write(Buffer.from([0xA4]));
            a + '|' + b";
        Assert.Equal("|ä", engine.RunString(code));
    }

    [Fact]
    public void End_ReturnsReplacementForDanglingBytes()
    {
        var (engine, _) = TestHost.Create();
        var code = @"
            const { StringDecoder } = require('string_decoder');
            const d = new StringDecoder('utf8');
            d.write(Buffer.from([0xC3]));
            d.end()";
        // One replacement for the incomplete 0xC3
        Assert.Equal("\uFFFD", engine.RunString(code));
    }

    [Fact]
    public void HexEncoding()
    {
        var (engine, _) = TestHost.Create();
        var code = @"
            const { StringDecoder } = require('string_decoder');
            const d = new StringDecoder('hex');
            d.write(Buffer.from([0xDE, 0xAD, 0xBE, 0xEF]))";
        Assert.Equal("deadbeef", engine.RunString(code));
    }

    [Fact]
    public void EncodingProperty()
    {
        var (engine, _) = TestHost.Create();
        Assert.Equal("utf8", engine.RunString(
            "const {StringDecoder}=require('string_decoder'); new StringDecoder('utf-8').encoding"));
    }
}
