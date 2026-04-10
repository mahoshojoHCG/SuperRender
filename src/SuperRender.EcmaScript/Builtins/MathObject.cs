namespace SuperRender.EcmaScript.Builtins;

using SuperRender.EcmaScript.Runtime;

public static class MathObject
{
    public static void Install(Realm realm)
    {
        var math = new JsObject { Prototype = realm.ObjectPrototype };

        // Constants
        BuiltinHelper.DefineProperty(math, "PI", JsNumber.Create(Math.PI));
        BuiltinHelper.DefineProperty(math, "E", JsNumber.Create(Math.E));
        BuiltinHelper.DefineProperty(math, "LN2", JsNumber.Create(Math.Log(2)));
        BuiltinHelper.DefineProperty(math, "LN10", JsNumber.Create(Math.Log(10)));
        BuiltinHelper.DefineProperty(math, "LOG2E", JsNumber.Create(Math.Log2(Math.E)));
        BuiltinHelper.DefineProperty(math, "LOG10E", JsNumber.Create(Math.Log10(Math.E)));
        BuiltinHelper.DefineProperty(math, "SQRT2", JsNumber.Create(Math.Sqrt(2)));
        BuiltinHelper.DefineProperty(math, "SQRT1_2", JsNumber.Create(Math.Sqrt(0.5)));

        // Symbol.toStringTag
        math.DefineSymbolProperty(JsSymbol.ToStringTag,
            PropertyDescriptor.Data(new JsString("Math"), writable: false, enumerable: false, configurable: true));

        // Single-argument math functions
        DefineUnary(math, "abs", Math.Abs);
        DefineUnary(math, "ceil", Math.Ceiling);
        DefineUnary(math, "floor", Math.Floor);
        DefineUnary(math, "round", MathRound);
        DefineUnary(math, "trunc", Math.Truncate);
        DefineUnary(math, "sqrt", Math.Sqrt);
        DefineUnary(math, "cbrt", Math.Cbrt);
        DefineUnary(math, "log", Math.Log);
        DefineUnary(math, "log2", Math.Log2);
        DefineUnary(math, "log10", Math.Log10);
        DefineUnary(math, "exp", Math.Exp);
        DefineUnary(math, "sign", v => Math.Sign(v));
        DefineUnary(math, "sin", Math.Sin);
        DefineUnary(math, "cos", Math.Cos);
        DefineUnary(math, "tan", Math.Tan);
        DefineUnary(math, "asin", Math.Asin);
        DefineUnary(math, "acos", Math.Acos);
        DefineUnary(math, "atan", Math.Atan);

        // fround: round to nearest float32
        DefineUnary(math, "fround", v => (double)(float)v);

        // clz32: count leading zeros in 32-bit integer
        BuiltinHelper.DefineMethod(math, "clz32", (_, args) =>
        {
            var n = (uint)(int)BuiltinHelper.Arg(args, 0).ToNumber();
            if (n == 0)
            {
                return JsNumber.Create(32);
            }

            var count = 0;
            while ((n & 0x80000000) == 0)
            {
                count++;
                n <<= 1;
            }

            return JsNumber.Create(count);
        }, 1);

        // imul: C-like 32-bit integer multiplication
        BuiltinHelper.DefineMethod(math, "imul", (_, args) =>
        {
            var a = (int)BuiltinHelper.Arg(args, 0).ToNumber();
            var b = (int)BuiltinHelper.Arg(args, 1).ToNumber();
            return JsNumber.Create(a * b);
        }, 2);

        // Two-argument math functions
        BuiltinHelper.DefineMethod(math, "atan2", (_, args) =>
        {
            var y = BuiltinHelper.Arg(args, 0).ToNumber();
            var x = BuiltinHelper.Arg(args, 1).ToNumber();
            return JsNumber.Create(Math.Atan2(y, x));
        }, 2);

        BuiltinHelper.DefineMethod(math, "pow", (_, args) =>
        {
            var b = BuiltinHelper.Arg(args, 0).ToNumber();
            var e = BuiltinHelper.Arg(args, 1).ToNumber();
            return JsNumber.Create(Math.Pow(b, e));
        }, 2);

        // Variadic functions
        BuiltinHelper.DefineMethod(math, "max", (_, args) =>
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
        }, 2);

        BuiltinHelper.DefineMethod(math, "min", (_, args) =>
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
        }, 2);

        BuiltinHelper.DefineMethod(math, "hypot", (_, args) =>
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
        }, 2);

        BuiltinHelper.DefineMethod(math, "random", (_, _) =>
        {
            return JsNumber.Create(Random.Shared.NextDouble());
        }, 0);

        realm.InstallGlobal("Math", math);
    }

    private static double MathRound(double v)
    {
        // JavaScript Math.round rounds half toward +Infinity
        if (v >= 0)
        {
            return Math.Floor(v + 0.5);
        }

        // ReSharper disable once CompareOfFloatsByEqualityOperator
        var floored = Math.Floor(v);
        return (v - floored == 0.5) ? floored : Math.Floor(v + 0.5);
    }

    private static void DefineUnary(JsObject math, string name, Func<double, double> fn)
    {
        BuiltinHelper.DefineMethod(math, name, (_, args) =>
        {
            var n = BuiltinHelper.Arg(args, 0).ToNumber();
            return JsNumber.Create(fn(n));
        }, 1);
    }
}
