namespace SuperRender.EcmaScript.Runtime.Builtins;

using System.Globalization;
using SuperRender.EcmaScript.Runtime;

public static partial class DateConstructor
{
    private static readonly DateTimeOffset Epoch = new(1970, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public static void Install(Realm realm)
    {
        var proto = realm.DatePrototype;

        var ctor = new JsFunction
        {
            Name = "Date",
            Length = 7,
            IsConstructor = true,
            Prototype = realm.FunctionPrototype,
            PrototypeObject = proto,
            CallTarget = (_, _) =>
            {
                // Date() without new returns a string
                return new JsString(DateTimeOffset.UtcNow.ToString("ddd MMM dd yyyy HH:mm:ss 'GMT'zzz", CultureInfo.InvariantCulture));
            },
            ConstructTarget = args =>
            {
                var dateObj = new JsDynamicObject { Prototype = realm.DatePrototype };
                double ms;

                if (args.Length == 0)
                {
                    ms = CurrentTimeMs();
                }
                else if (args.Length == 1)
                {
                    var arg = args[0];
                    if (arg is JsString str)
                    {
                        ms = ParseDateString(str.Value);
                    }
                    else
                    {
                        ms = arg.ToNumber();
                    }
                }
                else
                {
                    var year = (int)args[0].ToNumber();
                    var month = args.Length > 1 ? (int)args[1].ToNumber() : 0;
                    var day = args.Length > 2 ? (int)args[2].ToNumber() : 1;
                    var hours = args.Length > 3 ? (int)args[3].ToNumber() : 0;
                    var minutes = args.Length > 4 ? (int)args[4].ToNumber() : 0;
                    var seconds = args.Length > 5 ? (int)args[5].ToNumber() : 0;
                    var milliseconds = args.Length > 6 ? (int)args[6].ToNumber() : 0;

                    if (year is >= 0 and <= 99) year += 1900;

                    try
                    {
                        var dt = new DateTimeOffset(year, month + 1, day, hours, minutes, seconds, milliseconds, TimeSpan.Zero);
                        ms = (dt - Epoch).TotalMilliseconds;
                    }
                    catch (ArgumentOutOfRangeException)
                    {
                        ms = double.NaN;
                    }
                }

                dateObj.DefineOwnProperty("[[DateValue]]",
                    PropertyDescriptor.Data(JsNumber.Create(ms), writable: true, enumerable: false, configurable: false));
                return dateObj;
            }
        };

        // Static methods
        BuiltinHelper.DefineMethod(ctor, "now", (_, _) =>
        {
            return JsNumber.Create(CurrentTimeMs());
        }, 0);

        BuiltinHelper.DefineMethod(ctor, "parse", (_, args) =>
        {
            var str = BuiltinHelper.Arg(args, 0).ToJsString();
            return JsNumber.Create(ParseDateString(str));
        }, 1);

        BuiltinHelper.DefineMethod(ctor, "UTC", (_, args) =>
        {
            var year = (int)BuiltinHelper.Arg(args, 0).ToNumber();
            var month = args.Length > 1 ? (int)args[1].ToNumber() : 0;
            var day = args.Length > 2 ? (int)args[2].ToNumber() : 1;
            var hours = args.Length > 3 ? (int)args[3].ToNumber() : 0;
            var minutes = args.Length > 4 ? (int)args[4].ToNumber() : 0;
            var seconds = args.Length > 5 ? (int)args[5].ToNumber() : 0;
            var milliseconds = args.Length > 6 ? (int)args[6].ToNumber() : 0;

            if (year is >= 0 and <= 99) year += 1900;

            try
            {
                var dt = new DateTimeOffset(year, month + 1, day, hours, minutes, seconds, milliseconds, TimeSpan.Zero);
                return JsNumber.Create((dt - Epoch).TotalMilliseconds);
            }
            catch (ArgumentOutOfRangeException)
            {
                return JsNumber.NaN;
            }
        }, 7);

        // Prototype methods
        BuiltinHelper.DefineProperty(proto, "constructor", ctor);

        BuiltinHelper.DefineMethod(proto, "getTime", (thisArg, _) =>
        {
            return JsNumber.Create(GetDateMs(thisArg));
        }, 0);

        BuiltinHelper.DefineMethod(proto, "getFullYear", (thisArg, _) =>
        {
            var ms = GetDateMs(thisArg);
            if (double.IsNaN(ms)) return JsNumber.NaN;
            return JsNumber.Create(ToDateTimeOffset(ms).LocalDateTime.Year);
        }, 0);

        BuiltinHelper.DefineMethod(proto, "getMonth", (thisArg, _) =>
        {
            var ms = GetDateMs(thisArg);
            if (double.IsNaN(ms)) return JsNumber.NaN;
            return JsNumber.Create(ToDateTimeOffset(ms).LocalDateTime.Month - 1);
        }, 0);

        BuiltinHelper.DefineMethod(proto, "getDate", (thisArg, _) =>
        {
            var ms = GetDateMs(thisArg);
            if (double.IsNaN(ms)) return JsNumber.NaN;
            return JsNumber.Create(ToDateTimeOffset(ms).LocalDateTime.Day);
        }, 0);

        BuiltinHelper.DefineMethod(proto, "getDay", (thisArg, _) =>
        {
            var ms = GetDateMs(thisArg);
            if (double.IsNaN(ms)) return JsNumber.NaN;
            return JsNumber.Create((int)ToDateTimeOffset(ms).LocalDateTime.DayOfWeek);
        }, 0);

        BuiltinHelper.DefineMethod(proto, "getHours", (thisArg, _) =>
        {
            var ms = GetDateMs(thisArg);
            if (double.IsNaN(ms)) return JsNumber.NaN;
            return JsNumber.Create(ToDateTimeOffset(ms).LocalDateTime.Hour);
        }, 0);

        BuiltinHelper.DefineMethod(proto, "getMinutes", (thisArg, _) =>
        {
            var ms = GetDateMs(thisArg);
            if (double.IsNaN(ms)) return JsNumber.NaN;
            return JsNumber.Create(ToDateTimeOffset(ms).LocalDateTime.Minute);
        }, 0);

        BuiltinHelper.DefineMethod(proto, "getSeconds", (thisArg, _) =>
        {
            var ms = GetDateMs(thisArg);
            if (double.IsNaN(ms)) return JsNumber.NaN;
            return JsNumber.Create(ToDateTimeOffset(ms).LocalDateTime.Second);
        }, 0);

        BuiltinHelper.DefineMethod(proto, "getMilliseconds", (thisArg, _) =>
        {
            var ms = GetDateMs(thisArg);
            if (double.IsNaN(ms)) return JsNumber.NaN;
            return JsNumber.Create(ToDateTimeOffset(ms).LocalDateTime.Millisecond);
        }, 0);

        BuiltinHelper.DefineMethod(proto, "toISOString", (thisArg, _) =>
        {
            var ms = GetDateMs(thisArg);
            if (double.IsNaN(ms))
            {
                throw new Errors.JsRangeError("Invalid time value", ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);
            }

            var dto = ToDateTimeOffset(ms);
            return new JsString(dto.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture));
        }, 0);

        BuiltinHelper.DefineMethod(proto, "toJSON", (thisArg, _) =>
        {
            var ms = GetDateMs(thisArg);
            if (double.IsNaN(ms) || double.IsInfinity(ms))
            {
                return JsValue.Null;
            }

            var dto = ToDateTimeOffset(ms);
            return new JsString(dto.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture));
        }, 0);

        BuiltinHelper.DefineMethod(proto, "toString", (thisArg, _) =>
        {
            var ms = GetDateMs(thisArg);
            if (double.IsNaN(ms))
            {
                return new JsString("Invalid Date");
            }

            var dto = ToDateTimeOffset(ms);
            return new JsString(dto.LocalDateTime.ToString("ddd MMM dd yyyy HH:mm:ss 'GMT'zzz", CultureInfo.InvariantCulture));
        }, 0);

        BuiltinHelper.DefineMethod(proto, "valueOf", (thisArg, _) =>
        {
            return JsNumber.Create(GetDateMs(thisArg));
        }, 0);

        // Symbol.toPrimitive
        proto.DefineSymbolProperty(JsSymbol.ToPrimitiveSymbol,
            PropertyDescriptor.Data(__JsFn_DateToPrimitive(), writable: false, enumerable: false, configurable: true));

        realm.InstallGlobal("Date", ctor);
    }

    [JsMethod("[Symbol.toPrimitive]")]
    internal static JsValue DateToPrimitive(JsValue thisArg, JsValue[] args)
    {
        var hint = BuiltinHelper.Arg(args, 0).ToJsString();
        if (hint == "number" || hint == "default")
        {
            return JsNumber.Create(GetDateMs(thisArg));
        }

        var ms = GetDateMs(thisArg);
        if (double.IsNaN(ms))
        {
            return new JsString("Invalid Date");
        }

        var dto = ToDateTimeOffset(ms);
        return new JsString(dto.LocalDateTime.ToString("ddd MMM dd yyyy HH:mm:ss 'GMT'zzz", CultureInfo.InvariantCulture));
    }

    private static double CurrentTimeMs()
    {
        return (DateTimeOffset.UtcNow - Epoch).TotalMilliseconds;
    }

    private static double GetDateMs(JsValue thisArg)
    {
        if (thisArg is JsDynamicObject obj)
        {
            var data = obj.GetOwnProperty("[[DateValue]]");
            if (data?.Value is JsNumber num)
            {
                return num.Value;
            }
        }

        throw new Errors.JsTypeError("this is not a Date object", ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);
    }

    private static DateTimeOffset ToDateTimeOffset(double ms)
    {
        return Epoch.AddMilliseconds(ms);
    }

    private static double ParseDateString(string str)
    {
        if (DateTimeOffset.TryParse(str, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dto))
        {
            return (dto.UtcDateTime - Epoch.UtcDateTime).TotalMilliseconds;
        }

        return double.NaN;
    }
}
