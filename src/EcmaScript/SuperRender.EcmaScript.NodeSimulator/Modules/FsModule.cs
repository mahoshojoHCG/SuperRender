using System.Text;
using SuperRender.EcmaScript.Runtime;

namespace SuperRender.EcmaScript.NodeSimulator.Modules;

/// <summary>
/// Node.js `fs` module. Implements the commonly used sync operations plus a
/// `promises` namespace whose methods return JS Promises. Real disk I/O is
/// performed through <see cref="System.IO"/>.
/// </summary>
[JsObject]
public sealed partial class FsModule : JsDynamicObject
{
    private readonly Realm _realm;

    public FsModule(Realm realm)
    {
        _realm = realm;
        Prototype = realm.ObjectPrototype;
        DefineOwnProperty("constants", PropertyDescriptor.Data(BuildConstants()));
        DefineOwnProperty("promises", PropertyDescriptor.Data(CreatePromises(realm, this)));
    }

    public static FsModule Create(Realm realm) => new(realm);

    private static JsValue Arg(JsValue[] args, int index) => index < args.Length ? args[index] : JsValue.Undefined;

    private static string RequireString(JsValue[] args, int index, string param)
    {
        var v = Arg(args, index);
        if (v is JsString s) return s.Value;
        throw new Runtime.Errors.JsTypeError($"The \"{param}\" argument must be of type string");
    }

#pragma warning disable JSGEN005, JSGEN006, JSGEN007 // legacy variadic: Node.js fs API — path/data/options positional args
    [JsMethod("readFileSync")]
    public static JsValue ReadFileSync(JsValue _, JsValue[] args)
    {
        var path = RequireString(args, 0, "path");
        var bytes = File.ReadAllBytes(path);
        var encArg = Arg(args, 1);
        string? enc = encArg switch
        {
            JsString s => s.Value,
            JsDynamicObject o when o.Get("encoding") is JsString se => se.Value,
            _ => null,
        };
        return enc is null ? new BufferObject(bytes) : new JsString(BufferModule.Decode(bytes, 0, bytes.Length, enc));
    }

    [JsMethod("writeFileSync")]
    public static JsValue WriteFileSync(JsValue _, JsValue[] args)
    {
        var path = RequireString(args, 0, "path");
        var data = Arg(args, 1);
        byte[] bytes = data switch
        {
            BufferObject b => b.Span.ToArray(),
            JsString s => Encoding.UTF8.GetBytes(s.Value),
            _ => Encoding.UTF8.GetBytes(data.ToJsString()),
        };
        File.WriteAllBytes(path, bytes);
        return JsValue.Undefined;
    }

    [JsMethod("appendFileSync")]
    public static JsValue AppendFileSync(JsValue _, JsValue[] args)
    {
        var path = RequireString(args, 0, "path");
        var data = Arg(args, 1);
        byte[] bytes = data switch
        {
            BufferObject b => b.Span.ToArray(),
            JsString s => Encoding.UTF8.GetBytes(s.Value),
            _ => Encoding.UTF8.GetBytes(data.ToJsString()),
        };
        using var fs2 = new FileStream(path, FileMode.Append, FileAccess.Write);
        fs2.Write(bytes, 0, bytes.Length);
        return JsValue.Undefined;
    }

    [JsMethod("existsSync")]
    public static JsValue ExistsSync(JsValue _, JsValue[] args)
    {
        var path = RequireString(args, 0, "path");
        return (File.Exists(path) || Directory.Exists(path)) ? JsValue.True : JsValue.False;
    }

    [JsMethod("mkdirSync")]
    public static JsValue MkdirSync(JsValue _, JsValue[] args)
    {
        var path = RequireString(args, 0, "path");
        var opts = Arg(args, 1) as JsDynamicObject;
        bool recursive = opts?.Get("recursive").ToBoolean() ?? false;
        if (recursive || !Directory.Exists(path)) Directory.CreateDirectory(path);
        return recursive ? new JsString(path) : JsValue.Undefined;
    }

    [JsMethod("rmSync")]
    public static JsValue RmSync(JsValue _, JsValue[] args)
    {
        var path = RequireString(args, 0, "path");
        var opts = Arg(args, 1) as JsDynamicObject;
        bool recursive = opts?.Get("recursive").ToBoolean() ?? false;
        bool force = opts?.Get("force").ToBoolean() ?? false;
        try
        {
            if (Directory.Exists(path)) Directory.Delete(path, recursive);
            else if (File.Exists(path)) File.Delete(path);
            else if (!force) throw new FileNotFoundException(path);
        }
        catch when (force) { }
        return JsValue.Undefined;
    }

    [JsMethod("rmdirSync")]
    public static JsValue RmdirSync(JsValue _, JsValue[] args)
    {
        var path = RequireString(args, 0, "path");
        var opts = Arg(args, 1) as JsDynamicObject;
        bool recursive = opts?.Get("recursive").ToBoolean() ?? false;
        Directory.Delete(path, recursive);
        return JsValue.Undefined;
    }

    [JsMethod("unlinkSync")]
    public static JsValue UnlinkSync(JsValue _, JsValue[] args)
    {
        File.Delete(RequireString(args, 0, "path"));
        return JsValue.Undefined;
    }

    [JsMethod("renameSync")]
    public static JsValue RenameSync(JsValue _, JsValue[] args)
    {
        var from = RequireString(args, 0, "oldPath");
        var to = RequireString(args, 1, "newPath");
        if (File.Exists(from)) File.Move(from, to, overwrite: true);
        else Directory.Move(from, to);
        return JsValue.Undefined;
    }

    [JsMethod("readdirSync")]
    public JsValue ReaddirSync(JsValue _, JsValue[] args)
    {
        var path = RequireString(args, 0, "path");
        var arr = new JsArray { Prototype = _realm.ArrayPrototype };
        var entries = Directory.EnumerateFileSystemEntries(path).Select(Path.GetFileName).Where(s => s is not null).Cast<string>().OrderBy(s => s, StringComparer.Ordinal);
        foreach (var e in entries) arr.Push(new JsString(e));
        return arr;
    }

    [JsMethod("statSync")]
    public static JsValue StatSync(JsValue _, JsValue[] args) => MakeStats(RequireString(args, 0, "path"));

    [JsMethod("lstatSync")]
    public static JsValue LstatSync(JsValue _, JsValue[] args) => MakeStats(RequireString(args, 0, "path"));

    [JsMethod("realpathSync")]
    public static JsValue RealpathSync(JsValue _, JsValue[] args) =>
        new JsString(Path.GetFullPath(RequireString(args, 0, "path")));

    [JsMethod("accessSync")]
    public static JsValue AccessSync(JsValue _, JsValue[] args)
    {
        var path = RequireString(args, 0, "path");
        if (!File.Exists(path) && !Directory.Exists(path))
            throw new Runtime.Errors.JsErrorBase($"ENOENT: no such file or directory, access '{path}'");
        return JsValue.Undefined;
    }

    [JsMethod("copyFileSync")]
    public static JsValue CopyFileSync(JsValue _, JsValue[] args)
    {
        File.Copy(RequireString(args, 0, "src"), RequireString(args, 1, "dest"), overwrite: true);
        return JsValue.Undefined;
    }

    [JsMethod("watch")]
    public JsValue Watch(JsValue _, JsValue[] args)
    {
        var path = RequireString(args, 0, "path");
        JsFunction? listener = null;
        bool recursive = false;
        for (int i = 1; i < args.Length; i++)
        {
            if (args[i] is JsFunction f) { listener = f; break; }
            if (args[i] is JsDynamicObject opt && opt.Get("recursive").ToBoolean()) recursive = true;
        }
        return new FsWatcherObject(path, recursive, listener, _realm);
    }
#pragma warning restore JSGEN005, JSGEN006, JSGEN007

    private static JsDynamicObject BuildConstants()
    {
        var constants = new JsDynamicObject();
        constants.DefineOwnProperty("F_OK", PropertyDescriptor.Data(JsNumber.Create(0)));
        constants.DefineOwnProperty("R_OK", PropertyDescriptor.Data(JsNumber.Create(4)));
        constants.DefineOwnProperty("W_OK", PropertyDescriptor.Data(JsNumber.Create(2)));
        constants.DefineOwnProperty("X_OK", PropertyDescriptor.Data(JsNumber.Create(1)));
        return constants;
    }

    private static JsDynamicObject MakeStats(string path)
    {
        var info = new FileInfo(path);
        bool isFile = info.Exists;
        var dir = new DirectoryInfo(path);
        bool isDir = !isFile && dir.Exists;
        if (!isFile && !isDir) throw new Runtime.Errors.JsErrorBase($"ENOENT: {path}");
        var stats = new JsDynamicObject();
        long size = isFile ? info.Length : 0;
        DateTime mtime = isFile ? info.LastWriteTimeUtc : dir.LastWriteTimeUtc;
        DateTime ctime = isFile ? info.CreationTimeUtc : dir.CreationTimeUtc;
        DateTime atime = isFile ? info.LastAccessTimeUtc : dir.LastAccessTimeUtc;
        stats.DefineOwnProperty("size", PropertyDescriptor.Data(JsNumber.Create(size)));
        stats.DefineOwnProperty("mtimeMs", PropertyDescriptor.Data(JsNumber.Create(ToMs(mtime))));
        stats.DefineOwnProperty("ctimeMs", PropertyDescriptor.Data(JsNumber.Create(ToMs(ctime))));
        stats.DefineOwnProperty("atimeMs", PropertyDescriptor.Data(JsNumber.Create(ToMs(atime))));
        stats.DefineOwnProperty("birthtimeMs", PropertyDescriptor.Data(JsNumber.Create(ToMs(ctime))));
        DefineStatMethod(stats, "isFile", isFile);
        DefineStatMethod(stats, "isDirectory", isDir);
        DefineStatMethod(stats, "isSymbolicLink", false);
        DefineStatMethod(stats, "isBlockDevice", false);
        DefineStatMethod(stats, "isCharacterDevice", false);
        DefineStatMethod(stats, "isFIFO", false);
        DefineStatMethod(stats, "isSocket", false);
        return stats;
    }

    private static void DefineStatMethod(JsDynamicObject stats, string name, bool value) =>
        stats.DefineOwnProperty(name, PropertyDescriptor.Data(
            JsFunction.CreateNative(name, (_, _) => value ? JsValue.True : JsValue.False, 0),
            writable: true, enumerable: false, configurable: true));

    private static double ToMs(DateTime utc) => (utc - DateTime.UnixEpoch).TotalMilliseconds;

    private static JsDynamicObject CreatePromises(Realm realm, JsObject syncFs)
    {
        var p = new JsDynamicObject();
        string[] wrap = ["readFile", "writeFile", "appendFile", "mkdir", "rm", "rmdir", "unlink", "rename", "readdir", "stat", "lstat", "realpath", "access", "copyFile"];
        foreach (var name in wrap)
        {
            var syncName = name + "Sync";
            if (syncFs.Get(syncName) is not JsFunction sync) continue;
            var local = sync;
            p.DefineOwnProperty(name, PropertyDescriptor.Data(
                JsFunction.CreateNative(name, (_, args) =>
                {
                    if (realm.GlobalObject.Get("Promise") is not JsFunction promiseCtor)
                        throw new Runtime.Errors.JsTypeError("Promise is not available");
                    var executor = JsFunction.CreateNative("executor", (_, exArgs) =>
                    {
                        var resolve = (JsFunction)exArgs[0];
                        var reject = (JsFunction)exArgs[1];
                        try
                        {
                            var result = local.Call(JsValue.Undefined, args);
                            resolve.Call(JsValue.Undefined, [result]);
                        }
                        catch (Runtime.Errors.JsErrorBase ex) { reject.Call(JsValue.Undefined, [new JsString(ex.Message)]); }
                        catch (Exception ex) { reject.Call(JsValue.Undefined, [new JsString(ex.Message)]); }
                        return JsValue.Undefined;
                    }, 2);
                    return promiseCtor.Construct([executor]);
                }, local.Length),
                writable: true, enumerable: false, configurable: true));
        }
        return p;
    }
}
