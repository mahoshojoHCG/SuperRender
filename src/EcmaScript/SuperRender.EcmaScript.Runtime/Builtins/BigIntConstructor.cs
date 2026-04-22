namespace SuperRender.EcmaScript.Runtime.Builtins;

using System.Globalization;
using System.Numerics;
using SuperRender.EcmaScript.Runtime;

[JsGlobalInstall("BigInt")]
public sealed partial class BigIntConstructor
{
    private static void __Install(Realm realm)
    {
        var proto = realm.BigIntPrototype;

        // BigInt is callable (not constructible). BigInt(value) converts to BigInt.
        var ctor = JsFunction.CreateNative("BigInt", (_, args) =>
        {
            var val = BuiltinHelper.Arg(args, 0);
            return ToBigInt(val);
        }, 1);
        ctor.Prototype = realm.FunctionPrototype;
        ctor.PrototypeObject = proto;

        // Static methods
        BuiltinHelper.DefineMethod(ctor, "asIntN", (_, args) =>
        {
            var bits = (int)BuiltinHelper.Arg(args, 0).ToNumber();
            var bigint = ToBigInt(BuiltinHelper.Arg(args, 1));
            if (bigint is not JsBigInt bi)
                throw new Errors.JsTypeError("Cannot convert to BigInt", ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);

            if (bits <= 0) return JsBigInt.Create(BigInteger.Zero);

            var mod = BigInteger.One << bits;
            var result = BigInteger.Remainder(bi.Value, mod);
            if (result < 0) result += mod;

            // Sign-extend: if high bit set, subtract mod
            if (result >= (BigInteger.One << (bits - 1)))
                result -= mod;

            return JsBigInt.Create(result);
        }, 2);

        BuiltinHelper.DefineMethod(ctor, "asUintN", (_, args) =>
        {
            var bits = (int)BuiltinHelper.Arg(args, 0).ToNumber();
            var bigint = ToBigInt(BuiltinHelper.Arg(args, 1));
            if (bigint is not JsBigInt bi)
                throw new Errors.JsTypeError("Cannot convert to BigInt", ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);

            if (bits <= 0) return JsBigInt.Create(BigInteger.Zero);

            var mod = BigInteger.One << bits;
            var result = BigInteger.Remainder(bi.Value, mod);
            if (result < 0) result += mod;

            return JsBigInt.Create(result);
        }, 2);

        // Prototype methods
        BuiltinHelper.DefineProperty(proto, "constructor", ctor);

        BuiltinHelper.DefineMethod(proto, "toString", (thisArg, args) =>
        {
            var bi = GetBigIntValue(thisArg);
            var radixVal = BuiltinHelper.Arg(args, 0);
            var radix = radixVal is JsUndefined ? 10 : (int)radixVal.ToNumber();

            if (radix < 2 || radix > 36)
            {
                throw new Errors.JsRangeError("toString() radix must be between 2 and 36",
                    ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);
            }

            if (radix == 10)
            {
                return new JsString(bi.Value.ToString(CultureInfo.InvariantCulture));
            }

            return new JsString(ConvertToBase(bi.Value, radix));
        }, 1);

        BuiltinHelper.DefineMethod(proto, "valueOf", (thisArg, _) =>
        {
            return GetBigIntValue(thisArg);
        }, 0);

        BuiltinHelper.DefineMethod(proto, "toLocaleString", (thisArg, _) =>
        {
            var bi = GetBigIntValue(thisArg);
            return new JsString(bi.Value.ToString(CultureInfo.InvariantCulture));
        }, 0);

        realm.InstallGlobal("BigInt", ctor);
    }

    private static JsValue ToBigInt(JsValue value)
    {
        if (value is JsBigInt bi) return bi;

        if (value is JsNumber num)
        {
            if (!double.IsFinite(num.Value) || num.Value != Math.Truncate(num.Value))
            {
                throw new Errors.JsRangeError("The number " + num.ToJsString() + " cannot be converted to a BigInt because it is not an integer",
                    ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);
            }

            return JsBigInt.Create(new BigInteger(num.Value));
        }

        if (value is JsString str)
        {
            var trimmed = str.Value.Trim();
            if (trimmed.Length == 0)
            {
                throw new Errors.JsSyntaxError("Cannot convert empty string to a BigInt",
                    ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);
            }

            // Handle hex, octal, binary prefixes
            if (trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                if (BigInteger.TryParse("0" + trimmed[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var hexVal))
                    return JsBigInt.Create(hexVal);
            }
            else if (trimmed.StartsWith("0o", StringComparison.OrdinalIgnoreCase))
            {
                BigInteger result = BigInteger.Zero;
                foreach (char c in trimmed[2..])
                {
                    if (c is < '0' or > '7')
                        throw new Errors.JsSyntaxError("Cannot convert " + str.Value + " to a BigInt",
                            ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);
                    result = result * 8 + (c - '0');
                }
                return JsBigInt.Create(result);
            }
            else if (trimmed.StartsWith("0b", StringComparison.OrdinalIgnoreCase))
            {
                BigInteger result = BigInteger.Zero;
                foreach (char c in trimmed[2..])
                {
                    if (c is not '0' and not '1')
                        throw new Errors.JsSyntaxError("Cannot convert " + str.Value + " to a BigInt",
                            ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);
                    result = result * 2 + (c - '0');
                }
                return JsBigInt.Create(result);
            }
            else if (BigInteger.TryParse(trimmed, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var parsed))
            {
                return JsBigInt.Create(parsed);
            }

            throw new Errors.JsSyntaxError("Cannot convert " + str.Value + " to a BigInt",
                ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);
        }

        if (value is JsBoolean b)
        {
            return JsBigInt.Create(b.Value ? BigInteger.One : BigInteger.Zero);
        }

        throw new Errors.JsTypeError("Cannot convert " + value.TypeOf + " to a BigInt",
            ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);
    }

    private static JsBigInt GetBigIntValue(JsValue thisArg)
    {
        if (thisArg is JsBigInt bi)
        {
            return bi;
        }

        if (thisArg is JsObject obj)
        {
            var data = obj.GetOwnProperty("[[BigIntData]]");
            if (data?.Value is JsBigInt biData)
            {
                return biData;
            }
        }

        throw new Errors.JsTypeError("BigInt.prototype.valueOf requires that 'this' be a BigInt",
            ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);
    }

    private static string ConvertToBase(BigInteger value, int radix)
    {
        if (value.IsZero) return "0";

        bool negative = value < 0;
        var abs = BigInteger.Abs(value);
        var chars = new List<char>();

        while (abs > 0)
        {
            var remainder = (int)(abs % radix);
            chars.Add(remainder < 10 ? (char)('0' + remainder) : (char)('a' + remainder - 10));
            abs /= radix;
        }

        if (negative) chars.Add('-');
        chars.Reverse();
        return new string(chars.ToArray());
    }
}
