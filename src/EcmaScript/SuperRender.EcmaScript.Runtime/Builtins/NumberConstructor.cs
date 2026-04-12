namespace SuperRender.EcmaScript.Runtime.Builtins;

using System.Globalization;
using SuperRender.EcmaScript.Runtime;

public static class NumberConstructor
{
    public static void Install(Realm realm)
    {
        var proto = realm.NumberPrototype;

        var ctor = new JsFunction
        {
            Name = "Number",
            Length = 1,
            IsConstructor = true,
            Prototype = realm.FunctionPrototype,
            PrototypeObject = proto,
            CallTarget = (_, args) =>
            {
                if (args.Length == 0)
                {
                    return JsNumber.Zero;
                }

                return JsNumber.Create(args[0].ToNumber());
            },
            ConstructTarget = args =>
            {
                var val = args.Length == 0 ? 0.0 : args[0].ToNumber();
                var wrapper = new JsObject { Prototype = realm.NumberPrototype };
                wrapper.DefineOwnProperty("[[NumberData]]",
                    PropertyDescriptor.Data(JsNumber.Create(val), writable: false, enumerable: false, configurable: false));
                return wrapper;
            }
        };

        // Static constants
        BuiltinHelper.DefineProperty(ctor, "EPSILON", JsNumber.Create(2.2204460492503131e-16));
        BuiltinHelper.DefineProperty(ctor, "MAX_SAFE_INTEGER", JsNumber.Create(9007199254740991.0));
        BuiltinHelper.DefineProperty(ctor, "MIN_SAFE_INTEGER", JsNumber.Create(-9007199254740991.0));
        BuiltinHelper.DefineProperty(ctor, "MAX_VALUE", JsNumber.Create(double.MaxValue));
        BuiltinHelper.DefineProperty(ctor, "MIN_VALUE", JsNumber.Create(double.Epsilon));
        BuiltinHelper.DefineProperty(ctor, "POSITIVE_INFINITY", JsNumber.PositiveInfinity);
        BuiltinHelper.DefineProperty(ctor, "NEGATIVE_INFINITY", JsNumber.NegativeInfinity);
        BuiltinHelper.DefineProperty(ctor, "NaN", JsNumber.NaN);

        // Static methods
        BuiltinHelper.DefineMethod(ctor, "isFinite", (_, args) =>
        {
            var val = BuiltinHelper.Arg(args, 0);
            if (val is not JsNumber num)
            {
                return JsValue.False;
            }

            return double.IsFinite(num.Value) ? JsValue.True : JsValue.False;
        }, 1);

        BuiltinHelper.DefineMethod(ctor, "isInteger", (_, args) =>
        {
            var val = BuiltinHelper.Arg(args, 0);
            if (val is not JsNumber num)
            {
                return JsValue.False;
            }

            if (!double.IsFinite(num.Value))
            {
                return JsValue.False;
            }

            // ReSharper disable once CompareOfFloatsByEqualityOperator
            return Math.Truncate(num.Value) == num.Value ? JsValue.True : JsValue.False;
        }, 1);

        BuiltinHelper.DefineMethod(ctor, "isNaN", (_, args) =>
        {
            var val = BuiltinHelper.Arg(args, 0);
            if (val is not JsNumber num)
            {
                return JsValue.False;
            }

            return double.IsNaN(num.Value) ? JsValue.True : JsValue.False;
        }, 1);

        BuiltinHelper.DefineMethod(ctor, "isSafeInteger", (_, args) =>
        {
            var val = BuiltinHelper.Arg(args, 0);
            if (val is not JsNumber num)
            {
                return JsValue.False;
            }

            if (!double.IsFinite(num.Value))
            {
                return JsValue.False;
            }

            // ReSharper disable once CompareOfFloatsByEqualityOperator
            if (Math.Truncate(num.Value) != num.Value)
            {
                return JsValue.False;
            }

            return Math.Abs(num.Value) <= 9007199254740991.0 ? JsValue.True : JsValue.False;
        }, 1);

        BuiltinHelper.DefineMethod(ctor, "parseInt", (_, args) =>
        {
            var str = BuiltinHelper.Arg(args, 0).ToJsString().Trim();
            var radixVal = BuiltinHelper.Arg(args, 1);
            var radix = radixVal is JsUndefined ? 10 : (int)radixVal.ToNumber();

            if (radix == 0)
            {
                radix = 10;
            }

            if (radix < 2 || radix > 36)
            {
                return JsNumber.NaN;
            }

            return ParseInt(str, radix);
        }, 2);

        BuiltinHelper.DefineMethod(ctor, "parseFloat", (_, args) =>
        {
            var str = BuiltinHelper.Arg(args, 0).ToJsString().Trim();
            if (double.TryParse(str, NumberStyles.Float | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var result))
            {
                return JsNumber.Create(result);
            }

            return JsNumber.NaN;
        }, 1);

        // Prototype methods
        BuiltinHelper.DefineProperty(proto, "constructor", ctor);

        BuiltinHelper.DefineMethod(proto, "toFixed", (thisArg, args) =>
        {
            var val = GetNumberValue(thisArg);
            var digits = args.Length > 0 ? (int)args[0].ToNumber() : 0;

            if (digits < 0 || digits > 100)
            {
                throw new Errors.JsRangeError("toFixed() digits argument must be between 0 and 100", ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);
            }

            if (!double.IsFinite(val))
            {
                return new JsString(val.ToString(CultureInfo.InvariantCulture));
            }

            return new JsString(val.ToString("F" + digits.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture));
        }, 1);

        BuiltinHelper.DefineMethod(proto, "toPrecision", (thisArg, args) =>
        {
            var val = GetNumberValue(thisArg);
            if (args.Length == 0 || args[0] is JsUndefined)
            {
                return new JsString(val.ToString(CultureInfo.InvariantCulture));
            }

            var precision = (int)args[0].ToNumber();
            if (precision < 1 || precision > 100)
            {
                throw new Errors.JsRangeError("toPrecision() argument must be between 1 and 100", ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);
            }

            if (!double.IsFinite(val))
            {
                return new JsString(val.ToString(CultureInfo.InvariantCulture));
            }

            return new JsString(val.ToString("G" + precision.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture));
        }, 1);

        BuiltinHelper.DefineMethod(proto, "toString", (thisArg, args) =>
        {
            var val = GetNumberValue(thisArg);
            var radixVal = BuiltinHelper.Arg(args, 0);
            var radix = radixVal is JsUndefined ? 10 : (int)radixVal.ToNumber();

            if (radix < 2 || radix > 36)
            {
                throw new Errors.JsRangeError("toString() radix must be between 2 and 36", ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);
            }

            if (radix == 10)
            {
                return new JsString(JsNumber.Create(val).ToJsString());
            }

            return new JsString(ConvertToBase(val, radix));
        }, 1);

        BuiltinHelper.DefineMethod(proto, "valueOf", (thisArg, _) =>
        {
            return JsNumber.Create(GetNumberValue(thisArg));
        }, 0);

        // Install global parseInt / parseFloat
        realm.InstallGlobal("Number", ctor);
        realm.InstallGlobal("parseInt", ctor.Get("parseInt"));
        realm.InstallGlobal("parseFloat", ctor.Get("parseFloat"));
        realm.InstallGlobal("isFinite", JsFunction.CreateNative("isFinite", (_, args) =>
        {
            var n = BuiltinHelper.Arg(args, 0).ToNumber();
            return double.IsFinite(n) ? JsValue.True : JsValue.False;
        }, 1));
        realm.InstallGlobal("isNaN", JsFunction.CreateNative("isNaN", (_, args) =>
        {
            var n = BuiltinHelper.Arg(args, 0).ToNumber();
            return double.IsNaN(n) ? JsValue.True : JsValue.False;
        }, 1));
    }

    private static double GetNumberValue(JsValue thisArg)
    {
        if (thisArg is JsNumber n)
        {
            return n.Value;
        }

        if (thisArg is JsObject obj)
        {
            var data = obj.GetOwnProperty("[[NumberData]]");
            if (data?.Value is JsNumber numData)
            {
                return numData.Value;
            }
        }

        throw new Errors.JsTypeError("Number.prototype.valueOf requires that 'this' be a Number", ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);
    }

    private static JsValue ParseInt(string str, int radix)
    {
        if (str.Length == 0)
        {
            return JsNumber.NaN;
        }

        var sign = 1;
        var startIndex = 0;

        if (str[0] == '-')
        {
            sign = -1;
            startIndex = 1;
        }
        else if (str[0] == '+')
        {
            startIndex = 1;
        }

        // Handle 0x prefix for hex
        if (radix == 16 && startIndex + 1 < str.Length && str[startIndex] == '0'
            && (str[startIndex + 1] == 'x' || str[startIndex + 1] == 'X'))
        {
            startIndex += 2;
        }

        double result = 0;
        var parsed = false;

        for (var i = startIndex; i < str.Length; i++)
        {
            var digit = DigitValue(str[i]);
            if (digit < 0 || digit >= radix)
            {
                break;
            }

            result = result * radix + digit;
            parsed = true;
        }

        return parsed ? JsNumber.Create(sign * result) : JsNumber.NaN;
    }

    private static int DigitValue(char c)
    {
        if (c is >= '0' and <= '9')
        {
            return c - '0';
        }

        if (c is >= 'a' and <= 'z')
        {
            return c - 'a' + 10;
        }

        if (c is >= 'A' and <= 'Z')
        {
            return c - 'A' + 10;
        }

        return -1;
    }

    private static string ConvertToBase(double value, int radix)
    {
        if (double.IsNaN(value))
        {
            return "NaN";
        }

        if (double.IsPositiveInfinity(value))
        {
            return "Infinity";
        }

        if (double.IsNegativeInfinity(value))
        {
            return "-Infinity";
        }

        // ReSharper disable once CompareOfFloatsByEqualityOperator
        if (value == 0)
        {
            return "0";
        }

        var negative = value < 0;
        var longVal = (long)Math.Truncate(Math.Abs(value));

        if (longVal == 0)
        {
            return negative ? "-0" : "0";
        }

        var chars = new char[64];
        var idx = 63;

        while (longVal > 0)
        {
            var remainder = (int)(longVal % radix);
            chars[idx--] = remainder < 10 ? (char)('0' + remainder) : (char)('a' + remainder - 10);
            longVal /= radix;
        }

        if (negative)
        {
            chars[idx--] = '-';
        }

        return new string(chars, idx + 1, 63 - idx);
    }
}
