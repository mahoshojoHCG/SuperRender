using SuperRender.EcmaScript.Runtime;

namespace SuperRender.EcmaScript.NodeSimulator.Modules;

/// <summary>
/// Node.js `path` module. Provides posix + win32 variants plus the platform-default namespace.
/// </summary>
[JsObject]
public sealed partial class PathModule : JsDynamicObject
{
    private readonly bool _win32;

    public PathModule(bool win32)
    {
        _win32 = win32;
    }

    public static PathModule Create(bool win32) => new(win32);

    public static PathModule CreateDefault()
    {
        var isWin = OperatingSystem.IsWindows();
        var obj = new PathModule(isWin);
        obj.DefineOwnProperty("posix", PropertyDescriptor.Data(new PathModule(win32: false)));
        obj.DefineOwnProperty("win32", PropertyDescriptor.Data(new PathModule(win32: true)));
        return obj;
    }

    [JsProperty("sep")]
    public string Sep => _win32 ? "\\" : "/";

    [JsProperty("delimiter")]
    public string Delimiter => _win32 ? ";" : ":";

    [JsMethod("join")]
    public JsValue JoinMethod(JsValue _, JsValue[] args) => new JsString(Join(CollectStrings(args), _win32));

    [JsMethod("resolve")]
    public JsValue ResolveMethod(JsValue _, JsValue[] args) => new JsString(Resolve(CollectStrings(args), _win32));

    [JsMethod("normalize")]
    public string NormalizeMethod(string path) => Normalize(path, _win32);

    [JsMethod("isAbsolute")]
    public bool IsAbsoluteMethod(string path) => IsAbsolute(path, _win32);

    [JsMethod("dirname")]
    public string DirnameMethod(string path) => Dirname(path, _win32);

    [JsMethod("basename")]
    public JsValue BasenameMethod(JsValue _, JsValue[] args)
    {
        var p = RequireString(args, 0, "path");
        var ext = args.Length > 1 && args[1] is JsString es ? es.Value : null;
        return new JsString(Basename(p, ext, _win32));
    }

    [JsMethod("extname")]
    public string ExtnameMethod(string path) => Extname(path, _win32);

    [JsMethod("relative")]
    public string RelativeMethod(string from, string to) => Relative(from, to, _win32);

    [JsMethod("parse")]
    public JsValue ParseMethod(string path) => Parse(path, _win32);

    [JsMethod("format")]
    public JsValue FormatMethod(JsValue _, JsValue[] args)
    {
        if (args.Length == 0 || args[0] is not JsDynamicObject o)
            throw new Runtime.Errors.JsTypeError("path.format requires an object");
        return new JsString(Format(o, _win32));
    }

    [JsMethod("toNamespacedPath")]
    public static JsValue ToNamespacedPathMethod(JsValue _, JsValue[] args) =>
        args.Length > 0 ? args[0] : JsValue.Undefined;

    private static string RequireString(JsValue[] args, int index, string param)
    {
        var v = index < args.Length ? args[index] : JsValue.Undefined;
        if (v is JsString s) return s.Value;
        throw new Runtime.Errors.JsTypeError($"The \"{param}\" argument must be of type string");
    }

    private static string[] CollectStrings(JsValue[] args)
    {
        var result = new string[args.Length];
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] is not JsString s) throw new Runtime.Errors.JsTypeError("Path argument must be a string");
            result[i] = s.Value;
        }
        return result;
    }

    public static string Join(string[] parts, bool win32)
    {
        if (parts.Length == 0) return ".";
        var sep = win32 ? '\\' : '/';
        var joined = string.Join(sep, parts.Where(p => p.Length > 0));
        return joined.Length == 0 ? "." : Normalize(joined, win32);
    }

    public static string Resolve(string[] parts, bool win32)
    {
        string resolved = "";
        bool absolute = false;
        for (int i = parts.Length - 1; i >= 0 && !absolute; i--)
        {
            var p = parts[i];
            if (string.IsNullOrEmpty(p)) continue;
            resolved = p + (win32 ? "\\" : "/") + resolved;
            absolute = IsAbsolute(p, win32);
        }
        if (!absolute)
        {
            var cwd = System.Environment.CurrentDirectory;
            resolved = cwd + (win32 ? "\\" : "/") + resolved;
        }
        var normalized = Normalize(resolved, win32);
        if (normalized.Length > 1)
        {
            var trimChar = win32 ? '\\' : '/';
            if (normalized[^1] == trimChar) normalized = normalized[..^1];
        }
        return normalized;
    }

    public static string Normalize(string path, bool win32)
    {
        if (path.Length == 0) return ".";
        var sep = win32 ? '\\' : '/';
        var other = win32 ? '/' : '\\';
        var normalized = path.Replace(other, sep);
        bool isAbs = IsAbsolute(normalized, win32);
        bool trailingSep = normalized[^1] == sep;

        string prefix = "";
        string body = normalized;
        if (win32 && body.Length >= 2 && char.IsLetter(body[0]) && body[1] == ':')
        {
            prefix = body[..2];
            body = body[2..];
        }
        if (body.StartsWith(sep))
        {
            prefix += sep;
            body = body.TrimStart(sep);
        }

        var parts = body.Split(sep, StringSplitOptions.RemoveEmptyEntries);
        var stack = new List<string>();
        foreach (var part in parts)
        {
            if (part == ".") continue;
            if (part == "..")
            {
                if (stack.Count > 0 && stack[^1] != "..") stack.RemoveAt(stack.Count - 1);
                else if (!isAbs) stack.Add("..");
            }
            else
            {
                stack.Add(part);
            }
        }

        var rejoined = string.Join(sep, stack);
        var result = prefix + rejoined;
        if (result.Length == 0) result = ".";
        if (trailingSep && !result.EndsWith(sep) && result != ".") result += sep;
        return result;
    }

    public static bool IsAbsolute(string path, bool win32)
    {
        if (path.Length == 0) return false;
        if (path[0] == '/') return true;
        if (win32)
        {
            if (path[0] == '\\') return true;
            if (path.Length >= 3 && char.IsLetter(path[0]) && path[1] == ':' && (path[2] == '\\' || path[2] == '/')) return true;
        }
        return false;
    }

    public static string Dirname(string path, bool win32)
    {
        if (string.IsNullOrEmpty(path)) return ".";
        var sep = win32 ? '\\' : '/';
        var other = win32 ? '/' : '\\';
        path = path.Replace(other, sep);
        if (path == sep.ToString()) return path;
        var trimmed = path.TrimEnd(sep);
        var idx = trimmed.LastIndexOf(sep);
        if (idx < 0) return ".";
        if (idx == 0) return sep.ToString();
        if (win32 && idx == 2 && char.IsLetter(trimmed[0]) && trimmed[1] == ':') return trimmed[..3];
        return trimmed[..idx];
    }

    public static string Basename(string path, string? ext, bool win32)
    {
        var sep = win32 ? '\\' : '/';
        var other = win32 ? '/' : '\\';
        path = path.Replace(other, sep).TrimEnd(sep);
        var idx = path.LastIndexOf(sep);
        var name = idx < 0 ? path : path[(idx + 1)..];
        if (!string.IsNullOrEmpty(ext) && name.EndsWith(ext, StringComparison.Ordinal) && name.Length > ext.Length)
        {
            name = name[..^ext.Length];
        }
        return name;
    }

    public static string Extname(string path, bool win32)
    {
        var name = Basename(path, null, win32);
        var idx = name.LastIndexOf('.');
        if (idx <= 0) return "";
        return name[idx..];
    }

    public static string Relative(string from, string to, bool win32)
    {
        var fromAbs = Resolve([from], win32);
        var toAbs = Resolve([to], win32);
        if (fromAbs == toAbs) return "";
        var sep = win32 ? '\\' : '/';
        var cmp = win32 ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var fromParts = fromAbs.Split(sep, StringSplitOptions.RemoveEmptyEntries);
        var toParts = toAbs.Split(sep, StringSplitOptions.RemoveEmptyEntries);
        int common = 0;
        while (common < fromParts.Length && common < toParts.Length &&
               string.Equals(fromParts[common], toParts[common], cmp)) common++;
        var ups = Enumerable.Repeat("..", fromParts.Length - common);
        var downs = toParts.Skip(common);
        var result = string.Join(sep, ups.Concat(downs));
        return result;
    }

    internal static JsDynamicObject Parse(string path, bool win32)
    {
        var obj = new JsDynamicObject();
        var root = "";
        var sep = win32 ? '\\' : '/';
        var normalized = path.Replace(win32 ? '/' : '\\', sep);
        if (win32 && normalized.Length >= 3 && char.IsLetter(normalized[0]) && normalized[1] == ':' && normalized[2] == sep)
        {
            root = normalized[..3];
        }
        else if (normalized.StartsWith(sep))
        {
            root = sep.ToString();
        }
        var dir = Dirname(normalized, win32);
        var @base = Basename(normalized, null, win32);
        var ext = Extname(normalized, win32);
        var name = ext.Length > 0 ? @base[..^ext.Length] : @base;
        obj.DefineOwnProperty("root", PropertyDescriptor.Data(new JsString(root)));
        obj.DefineOwnProperty("dir", PropertyDescriptor.Data(new JsString(dir == "." ? "" : dir)));
        obj.DefineOwnProperty("base", PropertyDescriptor.Data(new JsString(@base)));
        obj.DefineOwnProperty("ext", PropertyDescriptor.Data(new JsString(ext)));
        obj.DefineOwnProperty("name", PropertyDescriptor.Data(new JsString(name)));
        return obj;
    }

    internal static string Format(JsDynamicObject o, bool win32)
    {
        var sep = win32 ? '\\' : '/';
        string dir = o.Get("dir").ToJsString();
        string root = o.Get("root").ToJsString();
        string @base = o.Get("base").ToJsString();
        string name = o.Get("name").ToJsString();
        string ext = o.Get("ext").ToJsString();

        if (o.Get("base") is JsUndefined) @base = name + ext;

        var prefix = !string.IsNullOrEmpty(dir) ? dir : root;
        if (string.IsNullOrEmpty(prefix)) return @base;
        if (prefix == root) return prefix + @base;
        return prefix + sep + @base;
    }
}
