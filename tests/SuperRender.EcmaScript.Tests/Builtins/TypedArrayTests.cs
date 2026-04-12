using SuperRender.EcmaScript.Engine;
using Xunit;

namespace SuperRender.EcmaScript.Tests.Builtins;

public class TypedArrayTests
{
    private static JsEngine CreateEngine() => new();

    [Fact]
    public void Int32Array_Constructor_CreatesWithLength()
    {
        var engine = CreateEngine();
        var result = engine.Execute<double>("new Int32Array(4).length");
        Assert.Equal(4, result);
    }

    [Fact]
    public void Uint8Array_IndexedAccess_ReadWrite()
    {
        var engine = CreateEngine();
        var result = engine.Execute<double>(@"
            const arr = new Uint8Array(3);
            arr[0] = 10;
            arr[1] = 20;
            arr[2] = 30;
            arr[1];
        ");
        Assert.Equal(20, result);
    }

    [Fact]
    public void Float64Array_StoresFloats()
    {
        var engine = CreateEngine();
        var result = engine.Execute<double>(@"
            const arr = new Float64Array(2);
            arr[0] = 3.14;
            arr[0];
        ");
        Assert.True(Math.Abs(result - 3.14) < 0.001);
    }

    [Fact]
    public void ArrayBuffer_ByteLength_ReturnsCorrectSize()
    {
        var engine = CreateEngine();
        var result = engine.Execute<double>("new ArrayBuffer(16).byteLength");
        Assert.Equal(16, result);
    }

    [Fact]
    public void Int32Array_BytesPerElement_Returns4()
    {
        var engine = CreateEngine();
        var result = engine.Execute<double>("Int32Array.BYTES_PER_ELEMENT");
        Assert.Equal(4, result);
    }

    [Fact]
    public void Uint8Array_FromArray_CopiesValues()
    {
        var engine = CreateEngine();
        var result = engine.Execute<double>(@"
            const arr = new Uint8Array([10, 20, 30]);
            arr[2];
        ");
        Assert.Equal(30, result);
    }

    [Fact]
    public void SharedArrayBuffer_Constructor_Creates()
    {
        var engine = CreateEngine();
        var result = engine.Execute<double>("new SharedArrayBuffer(8).byteLength");
        Assert.Equal(8, result);
    }

    [Fact]
    public void ArrayBuffer_Slice_CopiesRange()
    {
        var engine = CreateEngine();
        var result = engine.Execute<double>(@"
            const buf = new ArrayBuffer(8);
            const view = new Uint8Array(buf);
            view[0] = 1; view[1] = 2; view[2] = 3; view[3] = 4;
            const sliced = buf.slice(1, 3);
            const view2 = new Uint8Array(sliced);
            view2[0];
        ");
        Assert.Equal(2, result);
    }

    [Fact]
    public void ArrayBuffer_IsView_DetectsTypedArray()
    {
        var engine = CreateEngine();
        var result = engine.Execute<bool>(@"
            const arr = new Uint8Array(4);
            ArrayBuffer.isView(arr);
        ");
        Assert.True(result);
    }

    [Fact]
    public void Int32Array_Buffer_ReferencesOriginal()
    {
        var engine = CreateEngine();
        var result = engine.Execute<double>(@"
            const buf = new ArrayBuffer(16);
            const view = new Int32Array(buf);
            view.byteLength;
        ");
        Assert.Equal(16, result);
    }
}
