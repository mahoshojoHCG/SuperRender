namespace SuperRender.EcmaScript.Runtime.Builtins;

using System.Text.RegularExpressions;
using SuperRender.EcmaScript.Runtime;

public static class RegExpConstructor
{
    public static void Install(Realm realm)
    {
        var proto = realm.RegExpPrototype;

        var ctor = new JsFunction
        {
            Name = "RegExp",
            Length = 2,
            IsConstructor = true,
            Prototype = realm.FunctionPrototype,
            PrototypeObject = proto,
            CallTarget = (_, args) => ConstructRegExp(args, realm),
            ConstructTarget = args => ConstructRegExp(args, realm)
        };

        // Prototype methods
        BuiltinHelper.DefineProperty(proto, "constructor", ctor);

        BuiltinHelper.DefineMethod(proto, "exec", (thisArg, args) =>
        {
            var regex = RequireRegExp(thisArg);
            var str = BuiltinHelper.Arg(args, 0).ToJsString();
            return regex.Exec(str);
        }, 1);

        BuiltinHelper.DefineMethod(proto, "test", (thisArg, args) =>
        {
            var regex = RequireRegExp(thisArg);
            var str = BuiltinHelper.Arg(args, 0).ToJsString();
            return regex.Test(str);
        }, 1);

        BuiltinHelper.DefineMethod(proto, "toString", (thisArg, _) =>
        {
            var regex = RequireRegExp(thisArg);
            return new JsString("/" + regex.Pattern + "/" + regex.Flags);
        }, 0);

        // Prototype property getters
        BuiltinHelper.DefineGetter(proto, "source", (thisArg, _) =>
        {
            if (thisArg is JsRegExp regex)
            {
                return new JsString(regex.Pattern);
            }

            return new JsString("(?:)");
        });

        BuiltinHelper.DefineGetter(proto, "flags", (thisArg, _) =>
        {
            if (thisArg is JsRegExp regex)
            {
                return new JsString(regex.Flags);
            }

            return JsString.Empty;
        });

        BuiltinHelper.DefineGetter(proto, "global", (thisArg, _) =>
        {
            if (thisArg is JsRegExp regex)
            {
                return regex.Global ? JsValue.True : JsValue.False;
            }

            return JsValue.False;
        });

        BuiltinHelper.DefineGetter(proto, "ignoreCase", (thisArg, _) =>
        {
            if (thisArg is JsRegExp regex)
            {
                return regex.IgnoreCase ? JsValue.True : JsValue.False;
            }

            return JsValue.False;
        });

        BuiltinHelper.DefineGetter(proto, "multiline", (thisArg, _) =>
        {
            if (thisArg is JsRegExp regex)
            {
                return regex.Multiline ? JsValue.True : JsValue.False;
            }

            return JsValue.False;
        });

        realm.InstallGlobal("RegExp", ctor);
    }

    private static JsRegExp ConstructRegExp(JsValue[] args, Realm realm)
    {
        string pattern;
        string flags;

        var patternArg = BuiltinHelper.Arg(args, 0);
        var flagsArg = BuiltinHelper.Arg(args, 1);

        if (patternArg is JsRegExp existingRegex)
        {
            pattern = existingRegex.Pattern;
            flags = flagsArg is JsUndefined ? existingRegex.Flags : flagsArg.ToJsString();
        }
        else
        {
            pattern = patternArg is JsUndefined ? "" : patternArg.ToJsString();
            flags = flagsArg is JsUndefined ? "" : flagsArg.ToJsString();
        }

        // Validate flags
        foreach (var c in flags)
        {
            if (c is not ('g' or 'i' or 'm' or 's' or 'u' or 'y' or 'd'))
            {
                throw new Errors.JsSyntaxError("Invalid regular expression flags: " + flags, ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);
            }
        }

        try
        {
            var regex = new JsRegExp(pattern, flags) { Prototype = realm.RegExpPrototype };
            return regex;
        }
        catch (RegexParseException ex)
        {
            throw new Errors.JsSyntaxError("Invalid regular expression: " + ex.Message, ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);
        }
    }

    private static JsRegExp RequireRegExp(JsValue value)
    {
        if (value is JsRegExp regex)
        {
            return regex;
        }

        throw new Errors.JsTypeError("Method requires that 'this' be a RegExp", ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);
    }
}
