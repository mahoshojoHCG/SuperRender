using System.Text;
using SuperRender.EcmaScript.Runtime;

namespace SuperRender.EcmaScript.NodeSimulator.Modules;

/// <summary>
/// Node.js `fs` module. Implements the commonly used sync operations plus a
/// `promises` namespace whose methods return JS Promises. Real disk I/O is
/// performed through <see cref="System.IO"/>.
/// </summary>
public static class FsModule
{
    private static PropertyDescriptor MethodDesc(string name, Func<JsValue, JsValue[], JsValue> impl, int length) =>
        PropertyDescriptor.Data(JsFunction.CreateNative(name, impl, length), writable: true, enumerable: false, configurable: true);

    private static void Define(JsDynamicObject obj, string name, int length, Func<JsValue, JsValue[], JsValue> impl) =>
        obj.DefineOwnProperty(name, MethodDesc(name, impl, length));

    private static JsValue Arg(JsValue[] args, int index) => index < args.Length ? args[index] : JsValue.Undefined;

    private static string RequireString(JsValue[] args, int index, string param)
    {
        var v = Arg(args, index);
        if (v is JsString s) return s.Value;
        throw new Runtime.Errors.JsTypeError($"The \"{param}\" argument must be of type string");
    }

    public static JsDynamicObject Create(Realm realm)
    {
        var fs = new JsDynamicObject();

        Define(fs, "readFileSync", 2, (_, args) =>
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
        });

        Define(fs, "writeFileSync", 3, (_, args) =>
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
        });

        Define(fs, "appendFileSync", 3, (_, args) =>
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
        });

        Define(fs, "existsSync", 1, (_, args) =>
        {
            var path = RequireString(args, 0, "path");
            return (File.Exists(path) || Directory.Exists(path)) ? JsValue.True : JsValue.False;
        });

        Define(fs, "mkdirSync", 2, (_, args) =>
        {
            var path = RequireString(args, 0, "path");
            var opts = Arg(args, 1) as JsDynamicObject;
            bool recursive = opts?.Get("recursive").ToBoolean() ?? false;
            if (recursive || !Directory.Exists(path)) Directory.CreateDirectory(path);
            return recursive ? (JsValue)new JsString(path) : JsValue.Undefined;
        });

        Define(fs, "rmSync", 2, (_, args) =>
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
        });

        Define(fs, "rmdirSync", 2, (_, args) =>
        {
            var path = RequireString(args, 0, "path");
            var opts = Arg(args, 1) as JsDynamicObject;
            bool recursive = opts?.Get("recursive").ToBoolean() ?? false;
            Directory.Delete(path, recursive);
            return JsValue.Undefined;
        });

        Define(fs, "unlinkSync", 1, (_, args) =>
        {
            File.Delete(RequireString(args, 0, "path"));
            return JsValue.Undefined;
        });

        Define(fs, "renameSync", 2, (_, args) =>
        {
            var from = RequireString(args, 0, "oldPath");
            var to = RequireString(args, 1, "newPath");
            if (File.Exists(from)) File.Move(from, to, overwrite: true);
            else Directory.Move(from, to);
            return JsValue.Undefined;
        });

        Define(fs, "readdirSync", 2, (_, args) =>
        {
            var path = RequireString(args, 0, "path");
            var arr = new JsArray { Prototype = realm.ArrayPrototype };
            var entries = Directory.EnumerateFileSystemEntries(path).Select(Path.GetFileName).Where(s => s is not null).Cast<string>().OrderBy(s => s, StringComparer.Ordinal);
            foreach (var e in entries) arr.Push(new JsString(e));
            return arr;
        });

        Define(fs, "statSync", 2, (_, args) => MakeStats(RequireString(args, 0, "path")));
        Define(fs, "lstatSync", 2, (_, args) => MakeStats(RequireString(args, 0, "path")));

        Define(fs, "realpathSync", 2, (_, args) =>
            new JsString(Path.GetFullPath(RequireString(args, 0, "path"))));

        Define(fs, "accessSync", 2, (_, args) =>
        {
            var path = RequireString(args, 0, "path");
            if (!File.Exists(path) && !Directory.Exists(path))
                throw new Runtime.Errors.JsErrorBase($"ENOENT: no such file or directory, access '{path}'");
            return JsValue.Undefined;
        });

        Define(fs, "copyFileSync", 3, (_, args) =>
        {
            File.Copy(RequireString(args, 0, "src"), RequireString(args, 1, "dest"), overwrite: true);
            return JsValue.Undefined;
        });

        var constants = new JsDynamicObject();
        constants.DefineOwnProperty("F_OK", PropertyDescriptor.Data(JsNumber.Create(0)));
        constants.DefineOwnProperty("R_OK", PropertyDescriptor.Data(JsNumber.Create(4)));
        constants.DefineOwnProperty("W_OK", PropertyDescriptor.Data(JsNumber.Create(2)));
        constants.DefineOwnProperty("X_OK", PropertyDescriptor.Data(JsNumber.Create(1)));
        fs.DefineOwnProperty("constants", PropertyDescriptor.Data(constants));

        Define(fs, "watch", 3, (_, args) =>
        {
            var path = RequireString(args, 0, "path");
            JsFunction? listener = null;
            bool recursive = false;
            for (int i = 1; i < args.Length; i++)
            {
                if (args[i] is JsFunction f) { listener = f; break; }
                if (args[i] is JsDynamicObject opt && opt.Get("recursive").ToBoolean()) recursive = true;
            }
            return new FsWatcherObject(path, recursive, listener, realm);
        });

        fs.DefineOwnProperty("promises", PropertyDescriptor.Data(CreatePromises(realm, fs)));
        return fs;
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
        Define(stats, "isFile", 0, (_, _) => isFile ? JsValue.True : JsValue.False);
        Define(stats, "isDirectory", 0, (_, _) => isDir ? JsValue.True : JsValue.False);
        Define(stats, "isSymbolicLink", 0, (_, _) => JsValue.False);
        Define(stats, "isBlockDevice", 0, (_, _) => JsValue.False);
        Define(stats, "isCharacterDevice", 0, (_, _) => JsValue.False);
        Define(stats, "isFIFO", 0, (_, _) => JsValue.False);
        Define(stats, "isSocket", 0, (_, _) => JsValue.False);
        return stats;
    }

    private static double ToMs(DateTime utc) => (utc - DateTime.UnixEpoch).TotalMilliseconds;

    private static JsDynamicObject CreatePromises(Realm realm, JsDynamicObject syncFs)
    {
        var p = new JsDynamicObject();
        // Wrap each sync method as a Promise-returning function
        string[] wrap = ["readFile", "writeFile", "appendFile", "mkdir", "rm", "rmdir", "unlink", "rename", "readdir", "stat", "lstat", "realpath", "access", "copyFile"];
        foreach (var name in wrap)
        {
            var syncName = name + "Sync";
            if (syncFs.Get(syncName) is not JsFunction sync) continue;
            var local = sync;
            Define(p, name, local.Length, (_, args) =>
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
            });
        }
        return p;
    }
}
