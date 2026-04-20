namespace SuperRender.EcmaScript.Runtime.Builtins;

using System.Globalization;
using SuperRender.EcmaScript.Runtime;

[JsObject]
public sealed partial class IntlObject : JsDynamicObject
{
    private static readonly JsString ToStringTagValue = new("Intl");

    public IntlObject(Realm realm)
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
        var intl = new IntlObject(realm);

        InstallCollator(intl, realm);
        InstallNumberFormat(intl, realm);
        InstallDateTimeFormat(intl, realm);
        InstallPluralRules(intl, realm);

        realm.InstallGlobal("Intl", intl);
    }

    private static void InstallCollator(JsDynamicObject intl, Realm realm)
    {
        var collatorProto = new JsDynamicObject { Prototype = realm.ObjectPrototype };

        var ctor = new JsFunction
        {
            Name = "Collator",
            Length = 0,
            IsConstructor = true,
            Prototype = realm.FunctionPrototype,
            PrototypeObject = collatorProto,
            ConstructTarget = args =>
            {
                var locale = args.Length > 0 && args[0] is JsString loc ? loc.Value : "en";
                var options = args.Length > 1 && args[1] is JsDynamicObject opts ? opts : null;

                var sensitivity = "variant";
                if (options is not null)
                {
                    var sensVal = options.Get("sensitivity");
                    if (sensVal is JsString sensStr)
                    {
                        sensitivity = sensStr.Value;
                    }
                }

                CultureInfo culture;
                try
                {
                    culture = CultureInfo.GetCultureInfo(locale);
                }
                catch (CultureNotFoundException)
                {
                    culture = CultureInfo.InvariantCulture;
                }

                var compareOptions = sensitivity switch
                {
                    "base" => CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace,
                    "accent" => CompareOptions.IgnoreCase,
                    "case" => CompareOptions.IgnoreNonSpace,
                    _ => CompareOptions.None // "variant"
                };

                var collatorObj = new JsDynamicObject { Prototype = collatorProto };

                var compareFn = JsFunction.CreateNative("compare", (_, compareArgs) =>
                {
                    var a = BuiltinHelper.Arg(compareArgs, 0).ToJsString();
                    var b = BuiltinHelper.Arg(compareArgs, 1).ToJsString();
                    var result = culture.CompareInfo.Compare(a, b, compareOptions);
                    return JsNumber.Create(result);
                }, 2);

                collatorObj.Set("compare", compareFn);
                return collatorObj;
            },
            CallTarget = (_, args) =>
            {
                // Calling Collator() without new also creates a new instance
                var locale = args.Length > 0 && args[0] is JsString loc ? loc.Value : "en";
                var collatorObj = new JsDynamicObject { Prototype = collatorProto };
                CultureInfo culture;
                try
                {
                    culture = CultureInfo.GetCultureInfo(locale);
                }
                catch (CultureNotFoundException)
                {
                    culture = CultureInfo.InvariantCulture;
                }

                var compareFn = JsFunction.CreateNative("compare", (_, compareArgs) =>
                {
                    var a = BuiltinHelper.Arg(compareArgs, 0).ToJsString();
                    var b = BuiltinHelper.Arg(compareArgs, 1).ToJsString();
                    return JsNumber.Create(culture.CompareInfo.Compare(a, b, CompareOptions.None));
                }, 2);

                collatorObj.Set("compare", compareFn);
                return collatorObj;
            }
        };

        intl.Set("Collator", ctor);
    }

    private static void InstallNumberFormat(JsDynamicObject intl, Realm realm)
    {
        var nfProto = new JsDynamicObject { Prototype = realm.ObjectPrototype };

        var ctor = new JsFunction
        {
            Name = "NumberFormat",
            Length = 0,
            IsConstructor = true,
            Prototype = realm.FunctionPrototype,
            PrototypeObject = nfProto,
            ConstructTarget = args =>
            {
                var locale = args.Length > 0 && args[0] is JsString loc ? loc.Value : "en";
                var options = args.Length > 1 && args[1] is JsDynamicObject opts ? opts : null;

                CultureInfo culture;
                try
                {
                    culture = CultureInfo.GetCultureInfo(locale);
                }
                catch (CultureNotFoundException)
                {
                    culture = CultureInfo.InvariantCulture;
                }

                var style = "decimal";
                string? currency = null;
                int? minFrac = null;
                int? maxFrac = null;

                if (options is not null)
                {
                    var styleVal = options.Get("style");
                    if (styleVal is JsString styleStr) style = styleStr.Value;

                    var currVal = options.Get("currency");
                    if (currVal is JsString currStr) currency = currStr.Value;

                    var minVal = options.Get("minimumFractionDigits");
                    if (minVal is JsNumber minNum) minFrac = (int)minNum.Value;

                    var maxVal = options.Get("maximumFractionDigits");
                    if (maxVal is JsNumber maxNum) maxFrac = (int)maxNum.Value;
                }

                var nfObj = new JsDynamicObject { Prototype = nfProto };

                var formatFn = JsFunction.CreateNative("format", (_, formatArgs) =>
                {
                    var num = BuiltinHelper.Arg(formatArgs, 0).ToNumber();
                    string formatted;

                    switch (style)
                    {
                        case "currency":
                        {
                            var nfi = (NumberFormatInfo)culture.NumberFormat.Clone();
                            if (minFrac.HasValue) nfi.CurrencyDecimalDigits = minFrac.Value;
                            if (currency is not null)
                            {
                                // Use the currency symbol directly
                                nfi.CurrencySymbol = currency;
                            }
                            formatted = num.ToString("C", nfi);
                            break;
                        }
                        case "percent":
                        {
                            var nfi = (NumberFormatInfo)culture.NumberFormat.Clone();
                            if (minFrac.HasValue) nfi.PercentDecimalDigits = minFrac.Value;
                            formatted = num.ToString("P", nfi);
                            break;
                        }
                        default: // "decimal"
                        {
                            if (minFrac.HasValue || maxFrac.HasValue)
                            {
                                var min = minFrac ?? 0;
                                var max = maxFrac ?? min;
                                formatted = num.ToString($"N{max}", culture);
                            }
                            else
                            {
                                formatted = num.ToString("N", culture);
                            }
                            break;
                        }
                    }

                    return new JsString(formatted);
                }, 1);

                nfObj.Set("format", formatFn);
                return nfObj;
            },
            CallTarget = (_, args) =>
            {
                // Calling without new creates instance too
                var nfObj = new JsDynamicObject { Prototype = nfProto };
                var formatFn = JsFunction.CreateNative("format", (_, formatArgs) =>
                {
                    var num = BuiltinHelper.Arg(formatArgs, 0).ToNumber();
                    return new JsString(num.ToString("N", CultureInfo.InvariantCulture));
                }, 1);
                nfObj.Set("format", formatFn);
                return nfObj;
            }
        };

        intl.Set("NumberFormat", ctor);
    }

    private static void InstallDateTimeFormat(JsDynamicObject intl, Realm realm)
    {
        var dtfProto = new JsDynamicObject { Prototype = realm.ObjectPrototype };

        var ctor = new JsFunction
        {
            Name = "DateTimeFormat",
            Length = 0,
            IsConstructor = true,
            Prototype = realm.FunctionPrototype,
            PrototypeObject = dtfProto,
            ConstructTarget = args =>
            {
                var locale = args.Length > 0 && args[0] is JsString loc ? loc.Value : "en";
                CultureInfo culture;
                try
                {
                    culture = CultureInfo.GetCultureInfo(locale);
                }
                catch (CultureNotFoundException)
                {
                    culture = CultureInfo.InvariantCulture;
                }

                var dtfObj = new JsDynamicObject { Prototype = dtfProto };

                var formatFn = JsFunction.CreateNative("format", (_, formatArgs) =>
                {
                    var dateArg = BuiltinHelper.Arg(formatArgs, 0);
                    DateTime dt;

                    if (dateArg is JsDynamicObject dateObj)
                    {
                        // Extract [[DateValue]] (epoch ms) from JsDate-like object
                        var dateValue = dateObj.Get("[[DateValue]]");
                        if (dateValue is JsNumber ms)
                        {
                            var epoch = new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero);
                            dt = epoch.AddMilliseconds(ms.Value).UtcDateTime;
                        }
                        else
                        {
                            dt = DateTime.UtcNow;
                        }
                    }
                    else
                    {
                        dt = DateTime.UtcNow;
                    }

                    return new JsString(dt.ToString("d", culture));
                }, 1);

                dtfObj.Set("format", formatFn);
                return dtfObj;
            },
            CallTarget = (_, _) =>
            {
                var dtfObj = new JsDynamicObject { Prototype = dtfProto };
                var formatFn = JsFunction.CreateNative("format", (_, _) =>
                {
                    return new JsString(DateTime.UtcNow.ToString("d", CultureInfo.InvariantCulture));
                }, 1);
                dtfObj.Set("format", formatFn);
                return dtfObj;
            }
        };

        intl.Set("DateTimeFormat", ctor);
    }

    private static void InstallPluralRules(JsDynamicObject intl, Realm realm)
    {
        var prProto = new JsDynamicObject { Prototype = realm.ObjectPrototype };

        var ctor = new JsFunction
        {
            Name = "PluralRules",
            Length = 0,
            IsConstructor = true,
            Prototype = realm.FunctionPrototype,
            PrototypeObject = prProto,
            ConstructTarget = _ =>
            {
                var prObj = new JsDynamicObject { Prototype = prProto };

                var selectFn = JsFunction.CreateNative("select", (_, selectArgs) =>
                {
                    var n = BuiltinHelper.Arg(selectArgs, 0).ToNumber();
                    // Simplified English plural rules
                    // ReSharper disable once CompareOfFloatsByEqualityOperator
                    return new JsString(n == 1 ? "one" : "other");
                }, 1);

                prObj.Set("select", selectFn);
                return prObj;
            },
            CallTarget = (_, _) =>
            {
                var prObj = new JsDynamicObject { Prototype = prProto };
                var selectFn = JsFunction.CreateNative("select", (_, selectArgs) =>
                {
                    var n = BuiltinHelper.Arg(selectArgs, 0).ToNumber();
                    // ReSharper disable once CompareOfFloatsByEqualityOperator
                    return new JsString(n == 1 ? "one" : "other");
                }, 1);
                prObj.Set("select", selectFn);
                return prObj;
            }
        };

        intl.Set("PluralRules", ctor);
    }
}
