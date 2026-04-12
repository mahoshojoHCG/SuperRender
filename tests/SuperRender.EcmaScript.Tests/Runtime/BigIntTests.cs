using SuperRender.EcmaScript.Engine;
using SuperRender.EcmaScript.Runtime;
using SuperRender.EcmaScript.Runtime.Errors;
using Xunit;

namespace SuperRender.EcmaScript.Tests.Runtime;

public class BigIntTests
{
    private readonly JsEngine _engine = new();

    // ═══════════════════════════════════════════
    //  Literal parsing
    // ═══════════════════════════════════════════

    [Fact]
    public void BigIntLiteral_Decimal_ParsesCorrectly()
    {
        var result = _engine.Execute("42n");
        Assert.IsType<JsBigInt>(result);
        Assert.Equal("42", result.ToJsString());
    }

    [Fact]
    public void BigIntLiteral_Hex_ParsesCorrectly()
    {
        var result = _engine.Execute("0xFFn");
        Assert.IsType<JsBigInt>(result);
        Assert.Equal("255", result.ToJsString());
    }

    [Fact]
    public void BigIntLiteral_Octal_ParsesCorrectly()
    {
        var result = _engine.Execute("0o17n");
        Assert.IsType<JsBigInt>(result);
        Assert.Equal("15", result.ToJsString());
    }

    [Fact]
    public void BigIntLiteral_Binary_ParsesCorrectly()
    {
        var result = _engine.Execute("0b1010n");
        Assert.IsType<JsBigInt>(result);
        Assert.Equal("10", result.ToJsString());
    }

    [Fact]
    public void BigIntLiteral_Zero_ParsesCorrectly()
    {
        var result = _engine.Execute("0n");
        Assert.IsType<JsBigInt>(result);
        Assert.Equal("0", result.ToJsString());
    }

    [Fact]
    public void BigIntLiteral_Large_ParsesCorrectly()
    {
        var result = _engine.Execute("9007199254740993n");
        Assert.IsType<JsBigInt>(result);
        Assert.Equal("9007199254740993", result.ToJsString());
    }

    // ═══════════════════════════════════════════
    //  Typeof
    // ═══════════════════════════════════════════

    [Fact]
    public void Typeof_BigInt_ReturnsBigint()
    {
        var result = _engine.Execute("typeof 42n");
        Assert.Equal("bigint", ((JsString)result).Value);
    }

    // ═══════════════════════════════════════════
    //  Arithmetic
    // ═══════════════════════════════════════════

    [Fact]
    public void Add_TwoBigInts_ReturnsBigInt()
    {
        var result = _engine.Execute("10n + 20n");
        Assert.IsType<JsBigInt>(result);
        Assert.Equal("30", result.ToJsString());
    }

    [Fact]
    public void Subtract_BigInts_Correct()
    {
        var result = _engine.Execute("50n - 30n");
        Assert.IsType<JsBigInt>(result);
        Assert.Equal("20", result.ToJsString());
    }

    [Fact]
    public void Multiply_BigInts_Correct()
    {
        var result = _engine.Execute("6n * 7n");
        Assert.IsType<JsBigInt>(result);
        Assert.Equal("42", result.ToJsString());
    }

    [Fact]
    public void Divide_BigInts_Truncates()
    {
        var result = _engine.Execute("7n / 2n");
        Assert.IsType<JsBigInt>(result);
        Assert.Equal("3", result.ToJsString());
    }

    [Fact]
    public void Modulo_BigInts_Correct()
    {
        var result = _engine.Execute("7n % 3n");
        Assert.IsType<JsBigInt>(result);
        Assert.Equal("1", result.ToJsString());
    }

    [Fact]
    public void Power_BigInts_Correct()
    {
        var result = _engine.Execute("2n ** 10n");
        Assert.IsType<JsBigInt>(result);
        Assert.Equal("1024", result.ToJsString());
    }

    // ═══════════════════════════════════════════
    //  Mixed type errors
    // ═══════════════════════════════════════════

    [Fact]
    public void Add_BigIntAndNumber_ThrowsTypeError()
    {
        Assert.Throws<JsTypeError>(() => _engine.Execute("1n + 1"));
    }

    [Fact]
    public void Add_BigIntAndString_Concatenates()
    {
        var result = _engine.Execute("1n + 'hello'");
        Assert.IsType<JsString>(result);
        Assert.Equal("1hello", ((JsString)result).Value);
    }

    [Fact]
    public void Subtract_BigIntAndNumber_ThrowsTypeError()
    {
        Assert.Throws<JsTypeError>(() => _engine.Execute("1n - 1"));
    }

    [Fact]
    public void Multiply_BigIntAndNumber_ThrowsTypeError()
    {
        Assert.Throws<JsTypeError>(() => _engine.Execute("1n * 1"));
    }

    // ═══════════════════════════════════════════
    //  Comparison
    // ═══════════════════════════════════════════

    [Fact]
    public void StrictEquals_SameBigInts_True()
    {
        var result = _engine.Execute("42n === 42n");
        Assert.Equal(JsValue.True, result);
    }

    [Fact]
    public void StrictEquals_DifferentBigInts_False()
    {
        var result = _engine.Execute("42n === 43n");
        Assert.Equal(JsValue.False, result);
    }

    [Fact]
    public void LessThan_BigInts_Correct()
    {
        var result = _engine.Execute("1n < 2n");
        Assert.Equal(JsValue.True, result);
    }

    [Fact]
    public void GreaterThan_BigInts_Correct()
    {
        var result = _engine.Execute("2n > 1n");
        Assert.Equal(JsValue.True, result);
    }

    [Fact]
    public void AbstractEquals_BigIntAndNumber_Coerces()
    {
        var result = _engine.Execute("42n == 42");
        Assert.Equal(JsValue.True, result);
    }

    [Fact]
    public void AbstractEquals_BigIntAndString_Coerces()
    {
        var result = _engine.Execute("42n == '42'");
        Assert.Equal(JsValue.True, result);
    }

    [Fact]
    public void StrictEquals_BigIntAndNumber_False()
    {
        var result = _engine.Execute("42n === 42");
        Assert.Equal(JsValue.False, result);
    }

    // ═══════════════════════════════════════════
    //  Negation
    // ═══════════════════════════════════════════

    [Fact]
    public void UnaryMinus_BigInt_Negates()
    {
        var result = _engine.Execute("-42n");
        Assert.IsType<JsBigInt>(result);
        Assert.Equal("-42", result.ToJsString());
    }

    [Fact]
    public void UnaryPlus_BigInt_ThrowsTypeError()
    {
        Assert.Throws<JsTypeError>(() => _engine.Execute("+42n"));
    }

    // ═══════════════════════════════════════════
    //  Constructor function
    // ═══════════════════════════════════════════

    [Fact]
    public void BigInt_FromNumber_Converts()
    {
        var result = _engine.Execute("BigInt(42)");
        Assert.IsType<JsBigInt>(result);
        Assert.Equal("42", result.ToJsString());
    }

    [Fact]
    public void BigInt_FromString_Parses()
    {
        var result = _engine.Execute("BigInt('123')");
        Assert.IsType<JsBigInt>(result);
        Assert.Equal("123", result.ToJsString());
    }

    [Fact]
    public void BigInt_FromBoolean_Converts()
    {
        var result = _engine.Execute("BigInt(true)");
        Assert.IsType<JsBigInt>(result);
        Assert.Equal("1", result.ToJsString());
    }

    [Fact]
    public void BigInt_NewThrowsTypeError()
    {
        Assert.Throws<JsTypeError>(() => _engine.Execute("new BigInt(42)"));
    }

    [Fact]
    public void BigInt_FromFloat_ThrowsRangeError()
    {
        Assert.Throws<JsRangeError>(() => _engine.Execute("BigInt(1.5)"));
    }

    // ═══════════════════════════════════════════
    //  Boolean coercion
    // ═══════════════════════════════════════════

    [Fact]
    public void ToBoolean_ZeroBigInt_False()
    {
        var result = _engine.Execute("!0n");
        Assert.Equal(JsValue.True, result);
    }

    [Fact]
    public void ToBoolean_NonZeroBigInt_True()
    {
        var result = _engine.Execute("!1n");
        Assert.Equal(JsValue.False, result);
    }

    // ═══════════════════════════════════════════
    //  Bitwise operations
    // ═══════════════════════════════════════════

    [Fact]
    public void BitwiseAnd_BigInts_Correct()
    {
        var result = _engine.Execute("0xFFn & 0x0Fn");
        Assert.IsType<JsBigInt>(result);
        Assert.Equal("15", result.ToJsString());
    }

    [Fact]
    public void BitwiseOr_BigInts_Correct()
    {
        var result = _engine.Execute("0xF0n | 0x0Fn");
        Assert.IsType<JsBigInt>(result);
        Assert.Equal("255", result.ToJsString());
    }

    [Fact]
    public void LeftShift_BigInts_Correct()
    {
        var result = _engine.Execute("1n << 10n");
        Assert.IsType<JsBigInt>(result);
        Assert.Equal("1024", result.ToJsString());
    }

    [Fact]
    public void RightShift_BigInts_Correct()
    {
        var result = _engine.Execute("1024n >> 3n");
        Assert.IsType<JsBigInt>(result);
        Assert.Equal("128", result.ToJsString());
    }

    // ═══════════════════════════════════════════
    //  Prototype methods
    // ═══════════════════════════════════════════

    [Fact]
    public void ToString_DefaultRadix_ReturnsDecimal()
    {
        var result = _engine.Execute("(42n).toString()");
        Assert.Equal("42", ((JsString)result).Value);
    }

    [Fact]
    public void ToString_Radix16_ReturnsHex()
    {
        var result = _engine.Execute("(255n).toString(16)");
        Assert.Equal("ff", ((JsString)result).Value);
    }

    [Fact]
    public void ToString_Radix2_ReturnsBinary()
    {
        var result = _engine.Execute("(10n).toString(2)");
        Assert.Equal("1010", ((JsString)result).Value);
    }

    // ═══════════════════════════════════════════
    //  Variable assignment and usage
    // ═══════════════════════════════════════════

    [Fact]
    public void BigInt_VariableAssignment_Works()
    {
        var result = _engine.Execute("let x = 100n; x + 200n");
        Assert.IsType<JsBigInt>(result);
        Assert.Equal("300", result.ToJsString());
    }

    [Fact]
    public void BigInt_InConditional_Works()
    {
        var result = _engine.Execute("0n ? 'yes' : 'no'");
        Assert.Equal("no", ((JsString)result).Value);
    }

    [Fact]
    public void BigInt_NonZeroInConditional_Works()
    {
        var result = _engine.Execute("1n ? 'yes' : 'no'");
        Assert.Equal("yes", ((JsString)result).Value);
    }
}
