namespace SuperRender.EcmaScript.Runtime.Builtins;

using SuperRender.EcmaScript.Runtime;

[JsObject]
public sealed partial class MathObject : JsObject
{
    private static readonly JsString ToStringTagValue = new("Math");

    public MathObject(Realm realm)
    {
        Prototype = realm.ObjectPrototype;
        Extensible = false;
    }

    public static void Install(Realm realm) => realm.InstallGlobal("Math", new MathObject(realm));

    public override bool TryGetSymbolProperty(JsSymbol symbol, out JsValue value)
    {
        if (symbol == JsSymbol.ToStringTag)
        {
            value = ToStringTagValue;
            return true;
        }

        return base.TryGetSymbolProperty(symbol, out value);
    }

    [JsProperty("PI")] public static double PI => Math.PI;
    [JsProperty("E")] public static double E => Math.E;
    [JsProperty("LN2")] public static double LN2 => Math.Log(2);
    [JsProperty("LN10")] public static double LN10 => Math.Log(10);
    [JsProperty("LOG2E")] public static double LOG2E => Math.Log2(Math.E);
    [JsProperty("LOG10E")] public static double LOG10E => Math.Log10(Math.E);
    [JsProperty("SQRT2")] public static double SQRT2 => Math.Sqrt(2);
    [JsProperty("SQRT1_2")] public static double Sqrt1Over2 => Math.Sqrt(0.5);

    [JsMethod("abs")] public static double Abs(double x) => Math.Abs(x);
    [JsMethod("ceil")] public static double Ceil(double x) => Math.Ceiling(x);
    [JsMethod("floor")] public static double Floor(double x) => Math.Floor(x);
    [JsMethod("round")] public static double Round(double x) => RoundImpl(x);
    [JsMethod("trunc")] public static double Trunc(double x) => Math.Truncate(x);
    [JsMethod("sqrt")] public static double Sqrt(double x) => Math.Sqrt(x);
    [JsMethod("cbrt")] public static double Cbrt(double x) => Math.Cbrt(x);
    [JsMethod("log")] public static double Log(double x) => Math.Log(x);
    [JsMethod("log2")] public static double Log2(double x) => Math.Log2(x);
    [JsMethod("log10")] public static double Log10(double x) => Math.Log10(x);
    [JsMethod("exp")] public static double Exp(double x) => Math.Exp(x);
    [JsMethod("sign")] public static int Sign(double x) => Math.Sign(x);
    [JsMethod("sin")] public static double Sin(double x) => Math.Sin(x);
    [JsMethod("cos")] public static double Cos(double x) => Math.Cos(x);
    [JsMethod("tan")] public static double Tan(double x) => Math.Tan(x);
    [JsMethod("asin")] public static double Asin(double x) => Math.Asin(x);
    [JsMethod("acos")] public static double Acos(double x) => Math.Acos(x);
    [JsMethod("atan")] public static double Atan(double x) => Math.Atan(x);
    [JsMethod("fround")] public static double Fround(double x) => (double)(float)x;

    [JsMethod("clz32")]
    public static int Clz32(double x)
    {
        var n = (uint)(int)x;
        if (n == 0)
        {
            return 32;
        }

        var count = 0;
        while ((n & 0x80000000) == 0)
        {
            count++;
            n <<= 1;
        }

        return count;
    }

    [JsMethod("imul")]
    public static int Imul(int a, int b) => a * b;

    [JsMethod("atan2")]
    public static double Atan2(double y, double x) => Math.Atan2(y, x);

    [JsMethod("pow")]
    public static double Pow(double b, double e) => Math.Pow(b, e);

#pragma warning disable JSGEN005, JSGEN006, JSGEN007 // Math.max/min/hypot are variadic — typed signatures cannot express arbitrary-arity numeric folds
    [JsMethod("max")]
    public static JsValue Max(JsValue _, JsValue[] args)
    {
        if (args.Length == 0)
        {
            return JsNumber.NegativeInfinity;
        }

        var result = double.NegativeInfinity;
        foreach (var arg in args)
        {
            var n = arg.ToNumber();
            if (double.IsNaN(n))
            {
                return JsNumber.NaN;
            }

            if (n > result || (n == 0 && result == 0 && !double.IsNegative(n)))
            {
                result = n;
            }
        }

        return JsNumber.Create(result);
    }

    [JsMethod("min")]
    public static JsValue Min(JsValue _, JsValue[] args)
    {
        if (args.Length == 0)
        {
            return JsNumber.PositiveInfinity;
        }

        var result = double.PositiveInfinity;
        foreach (var arg in args)
        {
            var n = arg.ToNumber();
            if (double.IsNaN(n))
            {
                return JsNumber.NaN;
            }

            if (n < result || (n == 0 && result == 0 && double.IsNegative(n)))
            {
                result = n;
            }
        }

        return JsNumber.Create(result);
    }

    [JsMethod("hypot")]
    public static JsValue Hypot(JsValue _, JsValue[] args)
    {
        if (args.Length == 0)
        {
            return JsNumber.Zero;
        }

        var sum = 0.0;
        foreach (var arg in args)
        {
            var n = arg.ToNumber();
            if (double.IsInfinity(n))
            {
                return JsNumber.PositiveInfinity;
            }

            sum += n * n;
        }

        return JsNumber.Create(Math.Sqrt(sum));
    }
#pragma warning restore JSGEN005, JSGEN006, JSGEN007

    [JsMethod("random")]
    public static double Random() => System.Random.Shared.NextDouble();

    private static double RoundImpl(double v)
    {
        if (v >= 0)
        {
            return Math.Floor(v + 0.5);
        }

        var floored = Math.Floor(v);
#pragma warning disable CA1508 // float equality is intentional here
        return (v - floored == 0.5) ? floored : Math.Floor(v + 0.5);
#pragma warning restore CA1508
    }
}
