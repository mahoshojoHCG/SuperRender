namespace SuperRender.EcmaScript.Runtime.Builtins;

using System.Globalization;
using System.Text.RegularExpressions;
using SuperRender.EcmaScript.Runtime;

[JsObject]
public sealed partial class TemporalObject : JsDynamicObject
{
    private static readonly JsString ToStringTagValue = new("Temporal");

    public TemporalObject(Realm realm)
    {
        Prototype = realm.ObjectPrototype;
    }

    public override bool TryGetSymbolProperty(JsSymbol symbol, out JsValue value)
    {
        if (symbol == JsSymbol.ToStringTag)
        {
            value = ToStringTagValue;
            return true;
        }

        return base.TryGetSymbolProperty(symbol, out value);
    }

    public static void Install(Realm realm)
    {
        var temporal = new TemporalObject(realm);

        InstallPlainDate(temporal, realm);
        InstallPlainTime(temporal, realm);
        InstallPlainDateTime(temporal, realm);
        InstallInstant(temporal, realm);
        InstallDuration(temporal, realm);
        InstallNow(temporal, realm);

        realm.InstallGlobal("Temporal", temporal);
    }

    private static JsDynamicObject CreatePlainDateObject(DateOnly date, JsDynamicObject proto)
    {
        var obj = new JsDynamicObject { Prototype = proto };
        obj.Set("[[Date]]", new JsString(date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));
        BuiltinHelper.DefineGetter(obj, "year", (self, _) =>
        {
            var d = ParseDateFromSelf(self);
            return JsNumber.Create(d.Year);
        });
        BuiltinHelper.DefineGetter(obj, "month", (self, _) =>
        {
            var d = ParseDateFromSelf(self);
            return JsNumber.Create(d.Month);
        });
        BuiltinHelper.DefineGetter(obj, "day", (self, _) =>
        {
            var d = ParseDateFromSelf(self);
            return JsNumber.Create(d.Day);
        });
        BuiltinHelper.DefineMethod(obj, "toString", (self, _) =>
        {
            if (self is JsDynamicObject o)
            {
                var val = o.Get("[[Date]]");
                if (val is JsString s) return s;
            }
            return new JsString(date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        }, 0);
        BuiltinHelper.DefineMethod(obj, "equals", (self, args) =>
        {
            var d1 = ParseDateFromSelf(self);
            var other = BuiltinHelper.Arg(args, 0);
            if (other is JsDynamicObject otherObj)
            {
                var d2 = ParseDateFromSelf(otherObj);
                return d1 == d2 ? JsValue.True : JsValue.False;
            }
            return JsValue.False;
        }, 1);
        BuiltinHelper.DefineMethod(obj, "add", (self, args) =>
        {
            var d = ParseDateFromSelf(self);
            var dur = BuiltinHelper.Arg(args, 0);
            if (dur is JsDynamicObject durObj)
            {
                var years = (int)GetNumericProp(durObj, "years");
                var months = (int)GetNumericProp(durObj, "months");
                var days = (int)GetNumericProp(durObj, "days");
                d = d.AddYears(years).AddMonths(months).AddDays(days);
            }
            return CreatePlainDateObject(d, proto);
        }, 1);
        BuiltinHelper.DefineMethod(obj, "subtract", (self, args) =>
        {
            var d = ParseDateFromSelf(self);
            var dur = BuiltinHelper.Arg(args, 0);
            if (dur is JsDynamicObject durObj)
            {
                var years = (int)GetNumericProp(durObj, "years");
                var months = (int)GetNumericProp(durObj, "months");
                var days = (int)GetNumericProp(durObj, "days");
                d = d.AddYears(-years).AddMonths(-months).AddDays(-days);
            }
            return CreatePlainDateObject(d, proto);
        }, 1);
        return obj;
    }

    private static void InstallPlainDate(JsDynamicObject temporal, Realm realm)
    {
        var proto = new JsDynamicObject { Prototype = realm.ObjectPrototype };

        var ctor = new JsFunction
        {
            Name = "PlainDate",
            Length = 3,
            IsConstructor = true,
            Prototype = realm.FunctionPrototype,
            PrototypeObject = proto,
            ConstructTarget = args =>
            {
                var year = (int)BuiltinHelper.Arg(args, 0).ToNumber();
                var month = (int)BuiltinHelper.Arg(args, 1).ToNumber();
                var day = (int)BuiltinHelper.Arg(args, 2).ToNumber();
                var date = new DateOnly(year, month, day);
                return CreatePlainDateObject(date, proto);
            },
            CallTarget = (_, _) =>
            {
                throw new Errors.JsTypeError("Temporal.PlainDate must be called with new", ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);
            }
        };

        BuiltinHelper.DefineMethod(ctor, "from", (_, args) =>
        {
            var arg = BuiltinHelper.Arg(args, 0);
            if (arg is JsString str)
            {
                var date = DateOnly.ParseExact(str.Value, "yyyy-MM-dd", CultureInfo.InvariantCulture);
                return CreatePlainDateObject(date, proto);
            }
            throw new Errors.JsTypeError("Invalid argument to Temporal.PlainDate.from()", ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);
        }, 1);

        temporal.Set("PlainDate", ctor);
    }

    private static JsDynamicObject CreatePlainTimeObject(TimeOnly time, JsDynamicObject proto)
    {
        var obj = new JsDynamicObject { Prototype = proto };
        obj.Set("[[Time]]", new JsString(time.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture)));
        BuiltinHelper.DefineGetter(obj, "hour", (self, _) =>
        {
            var t = ParseTimeFromSelf(self);
            return JsNumber.Create(t.Hour);
        });
        BuiltinHelper.DefineGetter(obj, "minute", (self, _) =>
        {
            var t = ParseTimeFromSelf(self);
            return JsNumber.Create(t.Minute);
        });
        BuiltinHelper.DefineGetter(obj, "second", (self, _) =>
        {
            var t = ParseTimeFromSelf(self);
            return JsNumber.Create(t.Second);
        });
        BuiltinHelper.DefineGetter(obj, "millisecond", (self, _) =>
        {
            var t = ParseTimeFromSelf(self);
            return JsNumber.Create(t.Millisecond);
        });
        BuiltinHelper.DefineMethod(obj, "toString", (self, _) =>
        {
            var t = ParseTimeFromSelf(self);
            return new JsString(t.ToString("HH:mm:ss", CultureInfo.InvariantCulture));
        }, 0);
        return obj;
    }

    private static void InstallPlainTime(JsDynamicObject temporal, Realm realm)
    {
        var proto = new JsDynamicObject { Prototype = realm.ObjectPrototype };

        var ctor = new JsFunction
        {
            Name = "PlainTime",
            Length = 0,
            IsConstructor = true,
            Prototype = realm.FunctionPrototype,
            PrototypeObject = proto,
            ConstructTarget = args =>
            {
                var hour = args.Length > 0 ? (int)args[0].ToNumber() : 0;
                var minute = args.Length > 1 ? (int)args[1].ToNumber() : 0;
                var second = args.Length > 2 ? (int)args[2].ToNumber() : 0;
                var ms = args.Length > 3 ? (int)args[3].ToNumber() : 0;
                var time = new TimeOnly(hour, minute, second, ms);
                return CreatePlainTimeObject(time, proto);
            },
            CallTarget = (_, _) =>
            {
                throw new Errors.JsTypeError("Temporal.PlainTime must be called with new", ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);
            }
        };

        BuiltinHelper.DefineMethod(ctor, "from", (_, args) =>
        {
            var arg = BuiltinHelper.Arg(args, 0);
            if (arg is JsString str)
            {
                var time = TimeOnly.ParseExact(str.Value, ["HH:mm:ss", "HH:mm"], CultureInfo.InvariantCulture);
                return CreatePlainTimeObject(time, proto);
            }
            throw new Errors.JsTypeError("Invalid argument to Temporal.PlainTime.from()", ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);
        }, 1);

        temporal.Set("PlainTime", ctor);
    }

    private static JsDynamicObject CreatePlainDateTimeObject(DateTime dt, JsDynamicObject proto)
    {
        var obj = new JsDynamicObject { Prototype = proto };
        obj.Set("[[DateTime]]", new JsString(dt.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture)));
        BuiltinHelper.DefineGetter(obj, "year", (self, _) => JsNumber.Create(ParseDateTimeFromSelf(self).Year));
        BuiltinHelper.DefineGetter(obj, "month", (self, _) => JsNumber.Create(ParseDateTimeFromSelf(self).Month));
        BuiltinHelper.DefineGetter(obj, "day", (self, _) => JsNumber.Create(ParseDateTimeFromSelf(self).Day));
        BuiltinHelper.DefineGetter(obj, "hour", (self, _) => JsNumber.Create(ParseDateTimeFromSelf(self).Hour));
        BuiltinHelper.DefineGetter(obj, "minute", (self, _) => JsNumber.Create(ParseDateTimeFromSelf(self).Minute));
        BuiltinHelper.DefineGetter(obj, "second", (self, _) => JsNumber.Create(ParseDateTimeFromSelf(self).Second));
        BuiltinHelper.DefineMethod(obj, "toString", (self, _) =>
        {
            var d = ParseDateTimeFromSelf(self);
            return new JsString(d.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture));
        }, 0);
        return obj;
    }

    private static void InstallPlainDateTime(JsDynamicObject temporal, Realm realm)
    {
        var proto = new JsDynamicObject { Prototype = realm.ObjectPrototype };

        var ctor = new JsFunction
        {
            Name = "PlainDateTime",
            Length = 3,
            IsConstructor = true,
            Prototype = realm.FunctionPrototype,
            PrototypeObject = proto,
            ConstructTarget = args =>
            {
                var year = (int)BuiltinHelper.Arg(args, 0).ToNumber();
                var month = (int)BuiltinHelper.Arg(args, 1).ToNumber();
                var day = (int)BuiltinHelper.Arg(args, 2).ToNumber();
                var hour = args.Length > 3 ? (int)args[3].ToNumber() : 0;
                var minute = args.Length > 4 ? (int)args[4].ToNumber() : 0;
                var second = args.Length > 5 ? (int)args[5].ToNumber() : 0;
                var dt = new DateTime(year, month, day, hour, minute, second, DateTimeKind.Unspecified);
                return CreatePlainDateTimeObject(dt, proto);
            },
            CallTarget = (_, _) =>
            {
                throw new Errors.JsTypeError("Temporal.PlainDateTime must be called with new", ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);
            }
        };

        BuiltinHelper.DefineMethod(ctor, "from", (_, args) =>
        {
            var arg = BuiltinHelper.Arg(args, 0);
            if (arg is JsString str)
            {
                var dt = DateTime.ParseExact(str.Value, ["yyyy-MM-ddTHH:mm:ss", "yyyy-MM-ddTHH:mm"], CultureInfo.InvariantCulture);
                return CreatePlainDateTimeObject(dt, proto);
            }
            throw new Errors.JsTypeError("Invalid argument to Temporal.PlainDateTime.from()", ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);
        }, 1);

        temporal.Set("PlainDateTime", ctor);
    }

    private static void InstallInstant(JsDynamicObject temporal, Realm realm)
    {
        var proto = new JsDynamicObject { Prototype = realm.ObjectPrototype };
        var instant = new JsDynamicObject { Prototype = realm.ObjectPrototype };

        BuiltinHelper.DefineMethod(instant, "fromEpochMilliseconds", (_, args) =>
        {
            var ms = BuiltinHelper.Arg(args, 0).ToNumber();
            var obj = new JsDynamicObject { Prototype = proto };
            obj.Set("epochMilliseconds", JsNumber.Create(ms));
            obj.Set("epochNanoseconds", JsNumber.Create(ms * 1_000_000));
            BuiltinHelper.DefineMethod(obj, "toString", (self, _) =>
            {
                if (self is JsDynamicObject o)
                {
                    var epoch = o.Get("epochMilliseconds").ToNumber();
                    var dto = DateTimeOffset.FromUnixTimeMilliseconds((long)epoch);
                    return new JsString(dto.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture));
                }
                return new JsString("");
            }, 0);
            return obj;
        }, 1);

        BuiltinHelper.DefineMethod(instant, "fromEpochSeconds", (_, args) =>
        {
            var s = BuiltinHelper.Arg(args, 0).ToNumber();
            var obj = new JsDynamicObject { Prototype = proto };
            obj.Set("epochMilliseconds", JsNumber.Create(s * 1000));
            obj.Set("epochNanoseconds", JsNumber.Create(s * 1_000_000_000));
            BuiltinHelper.DefineMethod(obj, "toString", (self, _) =>
            {
                if (self is JsDynamicObject o)
                {
                    var epoch = o.Get("epochMilliseconds").ToNumber();
                    var dto = DateTimeOffset.FromUnixTimeMilliseconds((long)epoch);
                    return new JsString(dto.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture));
                }
                return new JsString("");
            }, 0);
            return obj;
        }, 1);

        temporal.Set("Instant", instant);
    }

    private static void InstallDuration(JsDynamicObject temporal, Realm realm)
    {
        var proto = new JsDynamicObject { Prototype = realm.ObjectPrototype };
        var durationNs = new JsDynamicObject { Prototype = realm.ObjectPrototype };

        BuiltinHelper.DefineMethod(durationNs, "from", (_, args) =>
        {
            var arg = BuiltinHelper.Arg(args, 0);
            if (arg is JsString str)
            {
                return ParseIsoDuration(str.Value, proto);
            }
            if (arg is JsDynamicObject obj)
            {
                var dur = new JsDynamicObject { Prototype = proto };
                dur.Set("years", JsNumber.Create(GetNumericProp(obj, "years")));
                dur.Set("months", JsNumber.Create(GetNumericProp(obj, "months")));
                dur.Set("days", JsNumber.Create(GetNumericProp(obj, "days")));
                dur.Set("hours", JsNumber.Create(GetNumericProp(obj, "hours")));
                dur.Set("minutes", JsNumber.Create(GetNumericProp(obj, "minutes")));
                dur.Set("seconds", JsNumber.Create(GetNumericProp(obj, "seconds")));
                return dur;
            }
            throw new Errors.JsTypeError("Invalid argument to Temporal.Duration.from()", ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);
        }, 1);

        temporal.Set("Duration", durationNs);
    }

    private static void InstallNow(JsDynamicObject temporal, Realm realm)
    {
        var now = new JsDynamicObject { Prototype = realm.ObjectPrototype };

        BuiltinHelper.DefineMethod(now, "instant", (_, _) =>
        {
            var ms = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var obj = new JsDynamicObject { Prototype = realm.ObjectPrototype };
            obj.Set("epochMilliseconds", JsNumber.Create(ms));
            obj.Set("epochNanoseconds", JsNumber.Create((double)ms * 1_000_000));
            return obj;
        }, 0);

        BuiltinHelper.DefineMethod(now, "plainDateISO", (_, _) =>
        {
            var today = DateTime.UtcNow;
            var obj = new JsDynamicObject { Prototype = realm.ObjectPrototype };
            obj.Set("year", JsNumber.Create(today.Year));
            obj.Set("month", JsNumber.Create(today.Month));
            obj.Set("day", JsNumber.Create(today.Day));
            BuiltinHelper.DefineMethod(obj, "toString", (_, _) =>
                new JsString(DateOnly.FromDateTime(today).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)), 0);
            return obj;
        }, 0);

        BuiltinHelper.DefineMethod(now, "plainTimeISO", (_, _) =>
        {
            var n = DateTime.UtcNow;
            var obj = new JsDynamicObject { Prototype = realm.ObjectPrototype };
            obj.Set("hour", JsNumber.Create(n.Hour));
            obj.Set("minute", JsNumber.Create(n.Minute));
            obj.Set("second", JsNumber.Create(n.Second));
            obj.Set("millisecond", JsNumber.Create(n.Millisecond));
            BuiltinHelper.DefineMethod(obj, "toString", (_, _) =>
                new JsString(TimeOnly.FromDateTime(n).ToString("HH:mm:ss", CultureInfo.InvariantCulture)), 0);
            return obj;
        }, 0);

        BuiltinHelper.DefineMethod(now, "plainDateTimeISO", (_, _) =>
        {
            var n = DateTime.UtcNow;
            var obj = new JsDynamicObject { Prototype = realm.ObjectPrototype };
            obj.Set("year", JsNumber.Create(n.Year));
            obj.Set("month", JsNumber.Create(n.Month));
            obj.Set("day", JsNumber.Create(n.Day));
            obj.Set("hour", JsNumber.Create(n.Hour));
            obj.Set("minute", JsNumber.Create(n.Minute));
            obj.Set("second", JsNumber.Create(n.Second));
            BuiltinHelper.DefineMethod(obj, "toString", (_, _) =>
                new JsString(n.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture)), 0);
            return obj;
        }, 0);

        temporal.Set("Now", now);
    }

    // ═══════════════════════════════════════════
    //  Helpers
    // ═══════════════════════════════════════════

    private static DateOnly ParseDateFromSelf(JsValue self)
    {
        if (self is JsDynamicObject obj)
        {
            var val = obj.Get("[[Date]]");
            if (val is JsString s)
            {
                return DateOnly.ParseExact(s.Value, "yyyy-MM-dd", CultureInfo.InvariantCulture);
            }
        }
        return DateOnly.FromDateTime(DateTime.UtcNow);
    }

    private static TimeOnly ParseTimeFromSelf(JsValue self)
    {
        if (self is JsDynamicObject obj)
        {
            var val = obj.Get("[[Time]]");
            if (val is JsString s)
            {
                return TimeOnly.ParseExact(s.Value, ["HH:mm:ss.fff", "HH:mm:ss", "HH:mm"], CultureInfo.InvariantCulture);
            }
        }
        return TimeOnly.FromDateTime(DateTime.UtcNow);
    }

    private static DateTime ParseDateTimeFromSelf(JsValue self)
    {
        if (self is JsDynamicObject obj)
        {
            var val = obj.Get("[[DateTime]]");
            if (val is JsString s)
            {
                return DateTime.ParseExact(s.Value, ["yyyy-MM-ddTHH:mm:ss", "yyyy-MM-ddTHH:mm"], CultureInfo.InvariantCulture);
            }
        }
        return DateTime.UtcNow;
    }

    private static double GetNumericProp(JsDynamicObject obj, string name)
    {
        var val = obj.Get(name);
        if (val is JsNumber n) return n.Value;
        return 0;
    }

    private static JsDynamicObject ParseIsoDuration(string iso, JsDynamicObject proto)
    {
        // Parse ISO 8601 duration: P[nY][nM][nD][T[nH][nM][nS]]
        var dur = new JsDynamicObject { Prototype = proto };
        int years = 0, months = 0, days = 0, hours = 0, minutes = 0, seconds = 0;

        var match = Regex.Match(iso, @"^P(?:(\d+)Y)?(?:(\d+)M)?(?:(\d+)D)?(?:T(?:(\d+)H)?(?:(\d+)M)?(?:(\d+)S)?)?$",
            RegexOptions.None, TimeSpan.FromSeconds(1));

        if (match.Success)
        {
            if (match.Groups[1].Success) years = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            if (match.Groups[2].Success) months = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
            if (match.Groups[3].Success) days = int.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture);
            if (match.Groups[4].Success) hours = int.Parse(match.Groups[4].Value, CultureInfo.InvariantCulture);
            if (match.Groups[5].Success) minutes = int.Parse(match.Groups[5].Value, CultureInfo.InvariantCulture);
            if (match.Groups[6].Success) seconds = int.Parse(match.Groups[6].Value, CultureInfo.InvariantCulture);
        }

        dur.Set("years", JsNumber.Create(years));
        dur.Set("months", JsNumber.Create(months));
        dur.Set("days", JsNumber.Create(days));
        dur.Set("hours", JsNumber.Create(hours));
        dur.Set("minutes", JsNumber.Create(minutes));
        dur.Set("seconds", JsNumber.Create(seconds));

        BuiltinHelper.DefineMethod(dur, "toString", (self, _) =>
        {
            if (self is not JsDynamicObject o) return new JsString("PT0S");
            var y = (int)GetNumericProp(o, "years");
            var mo = (int)GetNumericProp(o, "months");
            var d = (int)GetNumericProp(o, "days");
            var h = (int)GetNumericProp(o, "hours");
            var mi = (int)GetNumericProp(o, "minutes");
            var s = (int)GetNumericProp(o, "seconds");
            var result = "P";
            if (y > 0) result += $"{y}Y";
            if (mo > 0) result += $"{mo}M";
            if (d > 0) result += $"{d}D";
            if (h > 0 || mi > 0 || s > 0)
            {
                result += "T";
                if (h > 0) result += $"{h}H";
                if (mi > 0) result += $"{mi}M";
                if (s > 0) result += $"{s}S";
            }
            if (result == "P") result = "PT0S";
            return new JsString(result);
        }, 0);

        return dur;
    }
}
