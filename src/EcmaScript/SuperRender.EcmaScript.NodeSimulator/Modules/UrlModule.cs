using SuperRender.EcmaScript.Runtime;

namespace SuperRender.EcmaScript.NodeSimulator.Modules;

/// <summary>
/// Node.js `url` module. The WHATWG `URL`/`URLSearchParams` are provided by the engine; this
/// module adds the legacy parse/format/resolve helpers and the fileURL/httpOptions bridges.
/// </summary>
[JsObject]
public sealed partial class UrlModule : JsObjectBase
{
    private readonly Realm _realm;

    public UrlModule(Realm realm)
    {
        _realm = realm;
        Prototype = realm.ObjectPrototype;
    }

    public static UrlModule Create(Realm realm) => new(realm);

    [JsProperty("URL")]
    public JsValue URL => _realm.GlobalObject.Get("URL");

    [JsProperty("URLSearchParams")]
    public JsValue URLSearchParams => _realm.GlobalObject.Get("URLSearchParams");

    [JsMethod("parse")]
    public static JsValue ParseMethod(JsValue _, JsValue[] args)
    {
        var input = RequireString(args, 0, "urlString");
        var parseQuery = args.Length > 1 && args[1].ToBoolean();
        return LegacyParse(input, parseQuery);
    }

    [JsMethod("format")]
    public static JsValue FormatMethod(JsValue _, JsValue[] args)
    {
        if (args.Length == 0) return new JsString("");
        if (args[0] is JsString s) return new JsString(s.Value);
        if (args[0] is JsDynamicObject o) return new JsString(LegacyFormat(o));
        throw new Runtime.Errors.JsTypeError("url.format requires a string or object");
    }

    [JsMethod("resolve")]
    public static string ResolveMethod(string from, string to) => Resolve(from, to);

    [JsMethod("fileURLToPath")]
    public static JsValue FileUrlToPathMethod(JsValue _, JsValue[] args)
    {
        var arg = args.Length > 0 ? args[0] : JsValue.Undefined;
        string url = arg is JsString js ? js.Value
            : arg is JsDynamicObject jo && jo.Get("href") is JsString hs ? hs.Value
            : throw new Runtime.Errors.JsTypeError("fileURLToPath requires a URL");
        return new JsString(FileUrlToPath(url));
    }

    [JsMethod("pathToFileURL")]
    public JsValue PathToFileUrlMethod(string path)
    {
        var href = PathToFileUrl(path);
        if (_realm.GlobalObject.Get("URL") is JsFunction ctor)
            return ctor.Construct([new JsString(href)]);
        var stub = new JsDynamicObject();
        stub.DefineOwnProperty("href", PropertyDescriptor.Data(new JsString(href)));
        return stub;
    }

    [JsMethod("urlToHttpOptions")]
    public static JsValue UrlToHttpOptionsMethod(JsValue _, JsValue[] args)
    {
        if (args.Length == 0 || args[0] is not JsDynamicObject u)
            throw new Runtime.Errors.JsTypeError("urlToHttpOptions requires a URL object");
        return UrlToHttpOptions(u);
    }

    [JsMethod("domainToASCII")]
    public static string DomainToASCII(JsValue v)
    {
        var s = v.ToJsString();
        try { return new System.Globalization.IdnMapping().GetAscii(s); }
        catch (ArgumentException) { return ""; }
    }

    [JsMethod("domainToUnicode")]
    public static string DomainToUnicode(JsValue v)
    {
        var s = v.ToJsString();
        try { return new System.Globalization.IdnMapping().GetUnicode(s); }
        catch (ArgumentException) { return ""; }
    }

    private static string RequireString(JsValue[] args, int index, string param)
    {
        var v = index < args.Length ? args[index] : JsValue.Undefined;
        if (v is JsString s) return s.Value;
        throw new Runtime.Errors.JsTypeError($"The \"{param}\" argument must be of type string");
    }

    internal static JsDynamicObject LegacyParse(string input, bool parseQuery)
    {
        var obj = new JsDynamicObject();
        string? protocol = null, host = null, hostname = null, port = null, pathname = null, search = null, hash = null, auth = null;

        int hashIdx = input.IndexOf('#', StringComparison.Ordinal);
        if (hashIdx >= 0) { hash = input[hashIdx..]; input = input[..hashIdx]; }

        int queryIdx = input.IndexOf('?', StringComparison.Ordinal);
        if (queryIdx >= 0) { search = input[queryIdx..]; input = input[..queryIdx]; }

        int schemeIdx = input.IndexOf("://", StringComparison.Ordinal);
        string rest = input;
        if (schemeIdx >= 0)
        {
            protocol = input[..schemeIdx] + ":";
            var after = input[(schemeIdx + 3)..];
            int slash = after.IndexOf('/', StringComparison.Ordinal);
            string authority = slash < 0 ? after : after[..slash];
            rest = slash < 0 ? "" : after[slash..];
            int atIdx = authority.IndexOf('@', StringComparison.Ordinal);
            if (atIdx >= 0) { auth = authority[..atIdx]; authority = authority[(atIdx + 1)..]; }
            int colonIdx = authority.LastIndexOf(':');
            if (colonIdx >= 0 && authority.IndexOf(']', StringComparison.Ordinal) < colonIdx)
            {
                hostname = authority[..colonIdx];
                port = authority[(colonIdx + 1)..];
                host = authority;
            }
            else
            {
                hostname = authority;
                host = authority;
            }
        }
        pathname = rest.Length > 0 ? rest : (schemeIdx >= 0 ? "/" : rest);
        if (pathname.Length == 0) pathname = null;

        obj.DefineOwnProperty("protocol", PropertyDescriptor.Data(protocol is null ? JsValue.Null : new JsString(protocol)));
        obj.DefineOwnProperty("slashes", PropertyDescriptor.Data(schemeIdx >= 0 ? JsValue.True : JsValue.False));
        obj.DefineOwnProperty("auth", PropertyDescriptor.Data(auth is null ? JsValue.Null : new JsString(auth)));
        obj.DefineOwnProperty("host", PropertyDescriptor.Data(host is null ? JsValue.Null : new JsString(host)));
        obj.DefineOwnProperty("port", PropertyDescriptor.Data(port is null ? JsValue.Null : new JsString(port)));
        obj.DefineOwnProperty("hostname", PropertyDescriptor.Data(hostname is null ? JsValue.Null : new JsString(hostname)));
        obj.DefineOwnProperty("hash", PropertyDescriptor.Data(hash is null ? JsValue.Null : new JsString(hash)));
        obj.DefineOwnProperty("search", PropertyDescriptor.Data(search is null ? JsValue.Null : new JsString(search)));
        obj.DefineOwnProperty("pathname", PropertyDescriptor.Data(pathname is null ? JsValue.Null : new JsString(pathname)));

        JsValue query = JsValue.Null;
        if (search is not null)
        {
            var q = search.Length > 0 && search[0] == '?' ? search[1..] : search;
            query = parseQuery ? QueryStringModule.Parse(q, "&", "=") : (q.Length == 0 ? JsValue.Null : new JsString(q));
        }
        else if (parseQuery)
        {
            query = new JsDynamicObject();
        }
        obj.DefineOwnProperty("query", PropertyDescriptor.Data(query));

        string path = (pathname ?? "") + (search ?? "");
        obj.DefineOwnProperty("path", PropertyDescriptor.Data(path.Length == 0 ? JsValue.Null : new JsString(path)));
        obj.DefineOwnProperty("href", PropertyDescriptor.Data(new JsString(BuildHref(protocol, schemeIdx >= 0, auth, host, pathname, search, hash))));
        return obj;
    }

    private static string BuildHref(string? protocol, bool slashes, string? auth, string? host, string? pathname, string? search, string? hash)
    {
        var sb = new System.Text.StringBuilder();
        if (protocol is not null) sb.Append(protocol);
        if (slashes) sb.Append("//");
        if (auth is not null) sb.Append(auth).Append('@');
        if (host is not null) sb.Append(host);
        if (pathname is not null) sb.Append(pathname);
        if (search is not null) sb.Append(search);
        if (hash is not null) sb.Append(hash);
        return sb.ToString();
    }

    internal static string LegacyFormat(JsDynamicObject o)
    {
        string? Get(string k) => o.Get(k) is JsString s ? s.Value : null;
        var protocol = Get("protocol");
        if (protocol is not null && !protocol.EndsWith(':')) protocol += ":";
        var slashes = o.Get("slashes").ToBoolean() || (protocol is "http:" or "https:" or "ftp:" or "file:" or "ws:" or "wss:");
        var auth = Get("auth");
        var host = Get("host");
        if (host is null)
        {
            var hostname = Get("hostname");
            var port = Get("port");
            if (hostname is not null) host = port is null ? hostname : hostname + ":" + port;
        }
        var pathname = Get("pathname");
        string? search = Get("search");
        if (search is null && o.Get("query") is JsDynamicObject qo) search = "?" + QueryStringModule.Stringify(qo, "&", "=");
        else if (search is null && o.Get("query") is JsString qs) search = "?" + qs.Value;
        if (search is not null && search.Length > 0 && search[0] != '?') search = "?" + search;
        var hash = Get("hash");
        if (hash is not null && hash.Length > 0 && hash[0] != '#') hash = "#" + hash;
        return BuildHref(protocol, slashes, auth, host, pathname, search, hash);
    }

    internal static string Resolve(string from, string to)
    {
        if (string.IsNullOrEmpty(to)) return from;
        if (to.Contains("://", StringComparison.Ordinal)) return to;
        if (to.StartsWith("//", StringComparison.Ordinal))
        {
            int sIdx = from.IndexOf("://", StringComparison.Ordinal);
            return sIdx >= 0 ? from[..(sIdx + 1)] + to : to;
        }
        try
        {
            var baseUri = new Uri(from, UriKind.Absolute);
            var resolved = new Uri(baseUri, to);
            return resolved.ToString();
        }
        catch (UriFormatException)
        {
            if (to.StartsWith('/'))
            {
                int sIdx = from.IndexOf("://", StringComparison.Ordinal);
                if (sIdx >= 0)
                {
                    int pathStart = from.IndexOf('/', sIdx + 3);
                    return (pathStart < 0 ? from : from[..pathStart]) + to;
                }
            }
            int last = from.LastIndexOf('/');
            return last < 0 ? to : from[..(last + 1)] + to;
        }
    }

    internal static string FileUrlToPath(string url)
    {
        if (!url.StartsWith("file:", StringComparison.Ordinal))
            throw new Runtime.Errors.JsTypeError("The URL must be of scheme file");
        var uri = new Uri(url);
        if (OperatingSystem.IsWindows())
        {
            if (!string.IsNullOrEmpty(uri.Host))
                return @"\\" + uri.Host + uri.LocalPath.Replace('/', '\\');
            return uri.LocalPath.Replace('/', '\\');
        }
        return uri.LocalPath;
    }

    internal static string PathToFileUrl(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            if (path.StartsWith(@"\\", StringComparison.Ordinal))
            {
                var body = path[2..].Replace('\\', '/');
                return "file://" + EscapeFilePath(body);
            }
            var full = System.IO.Path.GetFullPath(path);
            return "file:///" + EscapeFilePath(full.Replace('\\', '/'));
        }
        var abs = System.IO.Path.IsPathRooted(path) ? path : System.IO.Path.GetFullPath(path);
        return "file://" + EscapeFilePath(abs);
    }

    private static string EscapeFilePath(string path)
    {
        var sb = new System.Text.StringBuilder(path.Length);
        foreach (var ch in path)
        {
            if (ch == '/' || ch == ':' || (char.IsAsciiLetterOrDigit(ch)) || ch == '-' || ch == '_' || ch == '.' || ch == '~')
            {
                sb.Append(ch);
            }
            else if (ch <= 0x7F)
            {
                sb.Append('%').Append(((int)ch).ToString("X2", System.Globalization.CultureInfo.InvariantCulture));
            }
            else
            {
                foreach (var b in System.Text.Encoding.UTF8.GetBytes(ch.ToString()))
                    sb.Append('%').Append(b.ToString("X2", System.Globalization.CultureInfo.InvariantCulture));
            }
        }
        return sb.ToString();
    }

    internal static JsDynamicObject UrlToHttpOptions(JsDynamicObject url)
    {
        var opts = new JsDynamicObject();
        void Copy(string src, string dst)
        {
            var v = url.Get(src);
            if (v is JsString s && s.Value.Length > 0)
                opts.DefineOwnProperty(dst, PropertyDescriptor.Data(new JsString(s.Value)));
        }
        Copy("protocol", "protocol");
        Copy("hostname", "hostname");
        Copy("hash", "hash");
        Copy("search", "search");
        Copy("pathname", "pathname");

        var host = url.Get("host");
        if (host is JsString hs && hs.Value.Length > 0)
            opts.DefineOwnProperty("host", PropertyDescriptor.Data(hs));
        var port = url.Get("port");
        if (port is JsString ps && ps.Value.Length > 0 && int.TryParse(ps.Value, out var pn))
            opts.DefineOwnProperty("port", PropertyDescriptor.Data(JsNumber.Create(pn)));

        var pathname = url.Get("pathname") is JsString pns ? pns.Value : "";
        var search = url.Get("search") is JsString ss ? ss.Value : "";
        opts.DefineOwnProperty("path", PropertyDescriptor.Data(new JsString(pathname + search)));

        var username = url.Get("username");
        var password = url.Get("password");
        if ((username is JsString us && us.Value.Length > 0) || (password is JsString ps2 && ps2.Value.Length > 0))
        {
            var u = username is JsString us2 ? us2.Value : "";
            var p = password is JsString pp ? pp.Value : "";
            opts.DefineOwnProperty("auth", PropertyDescriptor.Data(new JsString($"{u}:{p}")));
        }
        var href = url.Get("href");
        if (href is JsString hrefS) opts.DefineOwnProperty("href", PropertyDescriptor.Data(hrefS));
        return opts;
    }
}
