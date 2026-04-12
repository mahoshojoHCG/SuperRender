namespace SuperRender.EcmaScript.Runtime;

using System.Globalization;
using System.Numerics;

public sealed class JsBigInt : JsValue
{
    public BigInteger Value { get; }

    public JsBigInt(BigInteger value) => Value = value;

    public static JsBigInt Create(BigInteger value) => new(value);

    public override string TypeOf => "bigint";
    public override bool ToBoolean() => !Value.IsZero;

    public override double ToNumber() =>
        throw new Errors.JsTypeError("Cannot convert a BigInt value to a number",
            ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);

    public override string ToJsString() => Value.ToString(CultureInfo.InvariantCulture);
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture) + "n";

    public override bool StrictEquals(JsValue other) =>
        other is JsBigInt b && Value == b.Value;

    public override bool AbstractEquals(JsValue other)
    {
        if (other is JsBigInt b) return Value == b.Value;
        if (other is JsNumber n)
        {
            // Compare as double (may lose precision for very large BigInts)
            if (double.IsNaN(n.Value) || double.IsInfinity(n.Value)) return false;
            return (double)Value == n.Value;
        }
        if (other is JsString s && BigInteger.TryParse(s.Value, out var parsed)) return Value == parsed;
        if (other is JsBoolean bo) return Value == (bo.Value ? BigInteger.One : BigInteger.Zero);
        return false;
    }
}
