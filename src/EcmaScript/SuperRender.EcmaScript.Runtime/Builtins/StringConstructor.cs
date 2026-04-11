namespace SuperRender.EcmaScript.Runtime.Builtins;

using System.Text;
using SuperRender.EcmaScript.Runtime;

public static class StringConstructor
{
    public static void Install(Realm realm)
    {
        var proto = realm.StringPrototype;

        var ctor = new JsFunction
        {
            Name = "String",
            Length = 1,
            IsConstructor = true,
            Prototype = realm.FunctionPrototype,
            PrototypeObject = proto,
            CallTarget = (_, args) =>
            {
                if (args.Length == 0) return JsString.Empty;
                if (args[0] is JsSymbol sym) return new JsString(sym.ToString());
                return new JsString(args[0].ToJsString());
            },
            ConstructTarget = args =>
            {
                var val = args.Length == 0 ? "" : args[0].ToJsString();
                var wrapper = new JsObject { Prototype = realm.StringPrototype };
                wrapper.DefineOwnProperty("[[StringData]]",
                    PropertyDescriptor.Data(new JsString(val), writable: false, enumerable: false, configurable: false));
                return wrapper;
            }
        };

        // Static methods
        BuiltinHelper.DefineMethod(ctor, "fromCharCode", (_, args) =>
        {
            var sb = new StringBuilder(args.Length);
            foreach (var arg in args)
            {
                sb.Append((char)(ushort)arg.ToNumber());
            }

            return new JsString(sb.ToString());
        }, 1);

        BuiltinHelper.DefineMethod(ctor, "fromCodePoint", (_, args) =>
        {
            var sb = new StringBuilder(args.Length);
            foreach (var arg in args)
            {
                var cp = (int)arg.ToNumber();
                if (cp < 0 || cp > 0x10FFFF || cp != arg.ToNumber())
                {
                    throw new Errors.JsRangeError("Invalid code point " + arg.ToJsString());
                }

                sb.Append(char.ConvertFromUtf32(cp));
            }

            return new JsString(sb.ToString());
        }, 1);

        // Prototype methods
        BuiltinHelper.DefineProperty(proto, "constructor", ctor);

        BuiltinHelper.DefineMethod(proto, "charAt", (thisArg, args) =>
        {
            var str = GetStringValue(thisArg);
            var index = args.Length > 0 ? (int)args[0].ToNumber() : 0;
            if (index < 0 || index >= str.Length)
            {
                return JsString.Empty;
            }

            return new JsString(str[index].ToString());
        }, 1);

        BuiltinHelper.DefineMethod(proto, "charCodeAt", (thisArg, args) =>
        {
            var str = GetStringValue(thisArg);
            var index = args.Length > 0 ? (int)args[0].ToNumber() : 0;
            if (index < 0 || index >= str.Length)
            {
                return JsNumber.NaN;
            }

            return JsNumber.Create(str[index]);
        }, 1);

        BuiltinHelper.DefineMethod(proto, "indexOf", (thisArg, args) =>
        {
            var str = GetStringValue(thisArg);
            var searchStr = BuiltinHelper.Arg(args, 0).ToJsString();
            var position = args.Length > 1 ? Math.Max(0, (int)args[1].ToNumber()) : 0;
            var result = str.IndexOf(searchStr, position, StringComparison.Ordinal);
            return JsNumber.Create(result);
        }, 1);

        BuiltinHelper.DefineMethod(proto, "lastIndexOf", (thisArg, args) =>
        {
            var str = GetStringValue(thisArg);
            var searchStr = BuiltinHelper.Arg(args, 0).ToJsString();
            var position = args.Length > 1 && args[1] is not JsUndefined
                ? Math.Min((int)args[1].ToNumber(), str.Length)
                : str.Length;
            if (position < 0) position = 0;

            // lastIndexOf searches from position backwards
            var searchLen = searchStr.Length;
            var maxStart = Math.Min(position, str.Length - searchLen);
            for (var i = maxStart; i >= 0; i--)
            {
                if (string.Compare(str, i, searchStr, 0, searchLen, StringComparison.Ordinal) == 0)
                {
                    return JsNumber.Create(i);
                }
            }

            return JsNumber.Create(-1);
        }, 1);

        BuiltinHelper.DefineMethod(proto, "includes", (thisArg, args) =>
        {
            var str = GetStringValue(thisArg);
            var searchStr = BuiltinHelper.Arg(args, 0).ToJsString();
            var position = args.Length > 1 ? Math.Max(0, (int)args[1].ToNumber()) : 0;
            return str.IndexOf(searchStr, position, StringComparison.Ordinal) >= 0 ? JsValue.True : JsValue.False;
        }, 1);

        BuiltinHelper.DefineMethod(proto, "startsWith", (thisArg, args) =>
        {
            var str = GetStringValue(thisArg);
            var searchStr = BuiltinHelper.Arg(args, 0).ToJsString();
            var position = args.Length > 1 ? Math.Max(0, Math.Min((int)args[1].ToNumber(), str.Length)) : 0;
            if (position + searchStr.Length > str.Length) return JsValue.False;
            return str.AsSpan(position).StartsWith(searchStr, StringComparison.Ordinal) ? JsValue.True : JsValue.False;
        }, 1);

        BuiltinHelper.DefineMethod(proto, "endsWith", (thisArg, args) =>
        {
            var str = GetStringValue(thisArg);
            var searchStr = BuiltinHelper.Arg(args, 0).ToJsString();
            var endPos = args.Length > 1 && args[1] is not JsUndefined
                ? Math.Max(0, Math.Min((int)args[1].ToNumber(), str.Length))
                : str.Length;
            var start = endPos - searchStr.Length;
            if (start < 0) return JsValue.False;
            return string.Compare(str, start, searchStr, 0, searchStr.Length, StringComparison.Ordinal) == 0
                ? JsValue.True : JsValue.False;
        }, 1);

        BuiltinHelper.DefineMethod(proto, "slice", (thisArg, args) =>
        {
            var str = GetStringValue(thisArg);
            var len = str.Length;
            var start = args.Length > 0 ? NormalizeStringIndex((int)args[0].ToNumber(), len) : 0;
            var end = args.Length > 1 && args[1] is not JsUndefined
                ? NormalizeStringIndex((int)args[1].ToNumber(), len) : len;
            if (start >= end) return JsString.Empty;
            return new JsString(str[start..end]);
        }, 2);

        BuiltinHelper.DefineMethod(proto, "substring", (thisArg, args) =>
        {
            var str = GetStringValue(thisArg);
            var len = str.Length;
            var start = args.Length > 0 ? Math.Max(0, Math.Min((int)args[0].ToNumber(), len)) : 0;
            var end = args.Length > 1 && args[1] is not JsUndefined
                ? Math.Max(0, Math.Min((int)args[1].ToNumber(), len)) : len;
            if (start > end) (start, end) = (end, start);
            return new JsString(str[start..end]);
        }, 2);

        BuiltinHelper.DefineMethod(proto, "trim", (thisArg, _) =>
        {
            return new JsString(GetStringValue(thisArg).Trim());
        }, 0);

        BuiltinHelper.DefineMethod(proto, "trimStart", (thisArg, _) =>
        {
            return new JsString(GetStringValue(thisArg).TrimStart());
        }, 0);

        BuiltinHelper.DefineMethod(proto, "trimEnd", (thisArg, _) =>
        {
            return new JsString(GetStringValue(thisArg).TrimEnd());
        }, 0);

        BuiltinHelper.DefineMethod(proto, "padStart", (thisArg, args) =>
        {
            var str = GetStringValue(thisArg);
            var targetLength = args.Length > 0 ? (int)args[0].ToNumber() : 0;
            if (targetLength <= str.Length) return new JsString(str);
            var fillStr = args.Length > 1 && args[1] is not JsUndefined ? args[1].ToJsString() : " ";
            if (fillStr.Length == 0) return new JsString(str);
            var padLen = targetLength - str.Length;
            var sb = new StringBuilder(targetLength);
            while (sb.Length < padLen)
            {
                sb.Append(fillStr);
            }

            if (sb.Length > padLen) sb.Length = padLen;
            sb.Append(str);
            return new JsString(sb.ToString());
        }, 1);

        BuiltinHelper.DefineMethod(proto, "padEnd", (thisArg, args) =>
        {
            var str = GetStringValue(thisArg);
            var targetLength = args.Length > 0 ? (int)args[0].ToNumber() : 0;
            if (targetLength <= str.Length) return new JsString(str);
            var fillStr = args.Length > 1 && args[1] is not JsUndefined ? args[1].ToJsString() : " ";
            if (fillStr.Length == 0) return new JsString(str);
            var sb = new StringBuilder(targetLength);
            sb.Append(str);
            while (sb.Length < targetLength)
            {
                sb.Append(fillStr);
            }

            if (sb.Length > targetLength) sb.Length = targetLength;
            return new JsString(sb.ToString());
        }, 1);

        BuiltinHelper.DefineMethod(proto, "repeat", (thisArg, args) =>
        {
            var str = GetStringValue(thisArg);
            var count = args.Length > 0 ? (int)args[0].ToNumber() : 0;
            if (count < 0 || count == int.MaxValue)
            {
                throw new Errors.JsRangeError("Invalid count value");
            }

            if (count == 0 || str.Length == 0) return JsString.Empty;
            var sb = new StringBuilder(str.Length * count);
            for (var i = 0; i < count; i++)
            {
                sb.Append(str);
            }

            return new JsString(sb.ToString());
        }, 1);

        BuiltinHelper.DefineMethod(proto, "replace", (thisArg, args) =>
        {
            var str = GetStringValue(thisArg);
            var searchValue = BuiltinHelper.Arg(args, 0);
            var replaceValue = BuiltinHelper.Arg(args, 1);

            if (searchValue is JsRegExp regex)
            {
                return RegExpReplace(str, regex, replaceValue);
            }

            var searchStr = searchValue.ToJsString();
            var replaceStr = replaceValue is JsFunction replacerFn
                ? null
                : replaceValue.ToJsString();

            var idx = str.IndexOf(searchStr, StringComparison.Ordinal);
            if (idx < 0) return new JsString(str);

            string replacement;
            if (replaceValue is JsFunction fn)
            {
                var result = fn.Call(JsValue.Undefined, [new JsString(searchStr), JsNumber.Create(idx), new JsString(str)]);
                replacement = result.ToJsString();
            }
            else
            {
                replacement = replaceStr!;
            }

            return new JsString(string.Concat(str.AsSpan(0, idx), replacement, str.AsSpan(idx + searchStr.Length)));
        }, 2);

        BuiltinHelper.DefineMethod(proto, "replaceAll", (thisArg, args) =>
        {
            var str = GetStringValue(thisArg);
            var searchValue = BuiltinHelper.Arg(args, 0);
            var replaceValue = BuiltinHelper.Arg(args, 1);

            if (searchValue is JsRegExp regex)
            {
                if (!regex.Global)
                {
                    throw new Errors.JsTypeError("String.prototype.replaceAll called with a non-global RegExp argument");
                }

                return RegExpReplace(str, regex, replaceValue);
            }

            var searchStr = searchValue.ToJsString();
            var replaceStr = replaceValue is JsFunction ? null : replaceValue.ToJsString();

            if (searchStr.Length == 0)
            {
                // Replace between every character
                var sb2 = new StringBuilder();
                for (var i = 0; i <= str.Length; i++)
                {
                    var rep = replaceValue is JsFunction fn2
                        ? fn2.Call(JsValue.Undefined, [JsString.Empty, JsNumber.Create(i), new JsString(str)]).ToJsString()
                        : replaceStr!;
                    sb2.Append(rep);
                    if (i < str.Length) sb2.Append(str[i]);
                }

                return new JsString(sb2.ToString());
            }

            var sb = new StringBuilder();
            var lastIndex = 0;
            var pos = str.IndexOf(searchStr, StringComparison.Ordinal);
            while (pos >= 0)
            {
                sb.Append(str, lastIndex, pos - lastIndex);
                if (replaceValue is JsFunction fn)
                {
                    var result = fn.Call(JsValue.Undefined, [new JsString(searchStr), JsNumber.Create(pos), new JsString(str)]);
                    sb.Append(result.ToJsString());
                }
                else
                {
                    sb.Append(replaceStr);
                }

                lastIndex = pos + searchStr.Length;
                pos = str.IndexOf(searchStr, lastIndex, StringComparison.Ordinal);
            }

            sb.Append(str, lastIndex, str.Length - lastIndex);
            return new JsString(sb.ToString());
        }, 2);

        BuiltinHelper.DefineMethod(proto, "split", (thisArg, args) =>
        {
            var str = GetStringValue(thisArg);
            var separator = BuiltinHelper.Arg(args, 0);
            var limitArg = BuiltinHelper.Arg(args, 1);
            var limit = limitArg is JsUndefined ? int.MaxValue : (int)limitArg.ToNumber();
            var result = new JsArray { Prototype = realm.ArrayPrototype };

            if (limit == 0) return result;

            if (separator is JsUndefined)
            {
                result.Push(new JsString(str));
                return result;
            }

            var sepStr = separator.ToJsString();
            if (sepStr.Length == 0)
            {
                var count = Math.Min(str.Length, limit);
                for (var i = 0; i < count; i++)
                {
                    result.Push(new JsString(str[i].ToString()));
                }

                return result;
            }

            var parts = str.Split(sepStr, StringSplitOptions.None);
            var max = Math.Min(parts.Length, limit);
            for (var i = 0; i < max; i++)
            {
                result.Push(new JsString(parts[i]));
            }

            return result;
        }, 2);

        BuiltinHelper.DefineMethod(proto, "toLowerCase", (thisArg, _) =>
        {
            return new JsString(GetStringValue(thisArg).ToLowerInvariant());
        }, 0);

        BuiltinHelper.DefineMethod(proto, "toUpperCase", (thisArg, _) =>
        {
            return new JsString(GetStringValue(thisArg).ToUpperInvariant());
        }, 0);

        BuiltinHelper.DefineMethod(proto, "at", (thisArg, args) =>
        {
            var str = GetStringValue(thisArg);
            var idx = (int)BuiltinHelper.Arg(args, 0).ToNumber();
            if (idx < 0) idx = str.Length + idx;
            if (idx < 0 || idx >= str.Length) return JsValue.Undefined;
            return new JsString(str[idx].ToString());
        }, 1);

        BuiltinHelper.DefineMethod(proto, "toString", (thisArg, _) =>
        {
            return new JsString(GetStringValue(thisArg));
        }, 0);

        BuiltinHelper.DefineMethod(proto, "valueOf", (thisArg, _) =>
        {
            return new JsString(GetStringValue(thisArg));
        }, 0);

        // String.prototype[Symbol.iterator]
        proto.DefineSymbolProperty(JsSymbol.Iterator,
            PropertyDescriptor.Data(JsFunction.CreateNative("[Symbol.iterator]", (thisArg, _) =>
            {
                var str = GetStringValue(thisArg);
                var index = 0;
                var iterator = new JsObject { Prototype = realm.IteratorPrototype };
                BuiltinHelper.DefineMethod(iterator, "next", (_, _) =>
                {
                    if (index < str.Length)
                    {
                        // Handle surrogate pairs
                        string ch;
                        if (char.IsHighSurrogate(str[index]) && index + 1 < str.Length && char.IsLowSurrogate(str[index + 1]))
                        {
                            ch = str.Substring(index, 2);
                            index += 2;
                        }
                        else
                        {
                            ch = str[index].ToString();
                            index++;
                        }

                        return BuiltinHelper.CreateIteratorResult(new JsString(ch), false);
                    }

                    return BuiltinHelper.CreateIteratorResult(JsValue.Undefined, true);
                }, 0);
                iterator.DefineSymbolProperty(JsSymbol.Iterator, PropertyDescriptor.Data(
                    JsFunction.CreateNative("[Symbol.iterator]", (self, _) => self, 0),
                    writable: false, enumerable: false, configurable: true));
                return iterator;
            }, 0), writable: true, enumerable: false, configurable: true));

        realm.InstallGlobal("String", ctor);
    }

    private static string GetStringValue(JsValue thisArg)
    {
        if (thisArg is JsString s) return s.Value;
        if (thisArg is JsObject obj)
        {
            var data = obj.GetOwnProperty("[[StringData]]");
            if (data?.Value is JsString strData) return strData.Value;
        }

        return thisArg.ToJsString();
    }

    private static int NormalizeStringIndex(int index, int length)
    {
        if (index < 0) return Math.Max(0, length + index);
        return Math.Min(index, length);
    }

    private static JsValue RegExpReplace(string str, JsRegExp regex, JsValue replaceValue)
    {
        var sb = new StringBuilder();
        var lastIndex = 0;
        regex.LastIndex = 0;

        while (true)
        {
            var match = regex.Exec(str);
            if (match is JsNull) break;

            var matchArr = (JsArray)match;
            var matchStr = matchArr.GetIndex(0).ToJsString();
            var matchIndex = (int)((JsObject)match).Get("index").ToNumber();

            sb.Append(str, lastIndex, matchIndex - lastIndex);

            if (replaceValue is JsFunction fn)
            {
                var fnArgs = new JsValue[matchArr.DenseLength + 2];
                for (var i = 0; i < matchArr.DenseLength; i++)
                {
                    fnArgs[i] = matchArr.GetIndex(i);
                }

                fnArgs[matchArr.DenseLength] = JsNumber.Create(matchIndex);
                fnArgs[matchArr.DenseLength + 1] = new JsString(str);
                sb.Append(fn.Call(JsValue.Undefined, fnArgs).ToJsString());
            }
            else
            {
                sb.Append(replaceValue.ToJsString());
            }

            lastIndex = matchIndex + matchStr.Length;

            if (!regex.Global) break;
            if (matchStr.Length == 0)
            {
                regex.LastIndex = lastIndex + 1;
                if (lastIndex >= str.Length) break;
            }
        }

        sb.Append(str, lastIndex, str.Length - lastIndex);
        return new JsString(sb.ToString());
    }
}
