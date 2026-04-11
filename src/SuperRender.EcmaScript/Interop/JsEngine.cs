using SuperRender.EcmaScript.Builtins;
using SuperRender.EcmaScript.Compiler;
using SuperRender.EcmaScript.Parsing;
using SuperRender.EcmaScript.Runtime;
using Environment = SuperRender.EcmaScript.Runtime.Environment;

namespace SuperRender.EcmaScript.Interop;

/// <summary>
/// The main entry point for executing JavaScript from C#.
/// Provides a sandboxed ECMAScript 2025 runtime where only explicitly
/// mounted .NET types and values are accessible from JavaScript.
/// </summary>
public sealed class JsEngine
{
    private readonly Realm _realm;
    private readonly JsCompiler _compiler;
    private readonly Dictionary<string, Func<Environment, JsValue>> _cache = new(StringComparer.Ordinal);
    private readonly Dictionary<Type, TypeProxy> _registeredTypes = [];

    public Realm Realm => _realm;

    public JsEngine()
    {
        _realm = new Realm();
        _compiler = new JsCompiler(_realm);
        InstallBuiltins();
    }

    /// <summary>
    /// Execute JavaScript source code and return the result of the last expression.
    /// </summary>
    public JsValue Execute(string script)
    {
        if (!_cache.TryGetValue(script, out var compiled))
        {
            var parser = new Parser(script);
            var program = parser.Parse();
            compiled = _compiler.Compile(program);
            _cache[script] = compiled;
        }

        Compiler.RuntimeHelpers.CurrentRealm = _realm;
        return compiled(_realm.GlobalEnvironment);
    }

    /// <summary>
    /// Execute JavaScript source code and coerce the result to a .NET type.
    /// </summary>
    public T? Execute<T>(string script)
    {
        var result = Execute(script);
        return (T?)TypeProxy.MarshalFromJs(result, typeof(T));
    }

    /// <summary>
    /// Mount a .NET value into the global JavaScript scope.
    /// </summary>
    public JsEngine SetValue(string name, object? value)
    {
        JsValue jsValue = value switch
        {
            null => JsValue.Null,
            JsValue js => js,
            bool b => b ? JsValue.True : JsValue.False,
            int i => JsNumber.Create(i),
            long l => JsNumber.Create(l),
            float f => JsNumber.Create(f),
            double d => JsNumber.Create(d),
            string s => new JsString(s),
            Delegate del => WrapDelegate(name, del),
            _ => WrapObject(value)
        };

        _realm.InstallGlobal(name, jsValue);
        return this;
    }

    /// <summary>
    /// Mount a .NET delegate as a JavaScript function.
    /// </summary>
    public JsEngine SetValue(string name, Delegate function)
    {
        var fn = WrapDelegate(name, function);
        _realm.InstallGlobal(name, fn);
        return this;
    }

    /// <summary>
    /// Register a .NET type as a constructor accessible from JavaScript.
    /// </summary>
    public JsEngine RegisterType<T>()
    {
        return RegisterType<T>(typeof(T).Name);
    }

    /// <summary>
    /// Register a .NET type with a custom JavaScript name.
    /// </summary>
    public JsEngine RegisterType<T>(string jsName)
    {
        var type = typeof(T);
        if (_registeredTypes.ContainsKey(type))
            return this;

        var proxy = new TypeProxy(type, _realm);
        _registeredTypes[type] = proxy;
        _realm.InstallGlobal(jsName, proxy);
        return this;
    }

    /// <summary>
    /// Register a .NET type as a constructor accessible from JavaScript.
    /// </summary>
    public JsEngine RegisterType(Type type)
    {
        return RegisterType(type, type.Name);
    }

    /// <summary>
    /// Register a .NET type with a custom JavaScript name.
    /// </summary>
    public JsEngine RegisterType(Type type, string jsName)
    {
        if (_registeredTypes.ContainsKey(type))
            return this;

        var proxy = new TypeProxy(type, _realm);
        _registeredTypes[type] = proxy;
        _realm.InstallGlobal(jsName, proxy);
        return this;
    }

    /// <summary>
    /// Get a value from the global JavaScript scope.
    /// </summary>
    public JsValue GetValue(string name)
    {
        return _realm.GlobalEnvironment.HasBinding(name)
            ? _realm.GlobalEnvironment.GetBinding(name)
            : JsValue.Undefined;
    }

    /// <summary>
    /// Configure the output writers for console.log/warn/error.
    /// </summary>
    public JsEngine SetConsoleOutput(TextWriter output, TextWriter? error = null, TextWriter? warn = null)
    {
        ConsoleObject.SetOutput(output);
        if (error is not null)
            ConsoleObject.SetErrorOutput(error);
        if (warn is not null)
            ConsoleObject.SetWarnOutput(warn);
        return this;
    }

    /// <summary>
    /// Invoke a JavaScript function by name with the given arguments.
    /// </summary>
    public JsValue Invoke(string functionName, params object?[] args)
    {
        var fn = GetValue(functionName);
        if (fn is not JsFunction func)
            throw new Errors.JsTypeError($"{functionName} is not a function");

        var jsArgs = new JsValue[args.Length];
        for (int i = 0; i < args.Length; i++)
        {
            jsArgs[i] = args[i] switch
            {
                null => JsValue.Null,
                JsValue js => js,
                bool b => b ? JsValue.True : JsValue.False,
                int n => JsNumber.Create(n),
                long n => JsNumber.Create(n),
                float n => JsNumber.Create(n),
                double n => JsNumber.Create(n),
                string s => new JsString(s),
                _ => WrapObject(args[i]!)
            };
        }

        return func.Call(JsValue.Undefined, jsArgs);
    }

    // ═══════════════════════════════════════════
    //  Private helpers
    // ═══════════════════════════════════════════

    private void InstallBuiltins()
    {
        ObjectConstructor.Install(_realm);
        FunctionConstructor.Install(_realm);
        ArrayConstructor.Install(_realm);
        StringConstructor.Install(_realm);
        NumberConstructor.Install(_realm);
        BooleanConstructor.Install(_realm);
        SymbolConstructor.Install(_realm);
        MathObject.Install(_realm);
        JsonObject.Install(_realm);
        DateConstructor.Install(_realm);
        RegExpConstructor.Install(_realm);
        ErrorConstructor.Install(_realm);
        MapConstructor.Install(_realm);
        SetConstructor.Install(_realm);
        WeakMapConstructor.Install(_realm);
        WeakSetConstructor.Install(_realm);
        PromiseConstructor.Install(_realm);
        ProxyConstructor.Install(_realm);
        ReflectObject.Install(_realm);
        ConsoleObject.Install(_realm);
    }

    private static JsFunction WrapDelegate(string name, Delegate del)
    {
        var method = del.Method;
        var paramCount = method.GetParameters().Length;

        return JsFunction.CreateNative(name, (_, args) =>
        {
            var paramInfos = method.GetParameters();
            var clrArgs = new object?[paramInfos.Length];
            for (int i = 0; i < paramInfos.Length; i++)
            {
                clrArgs[i] = TypeProxy.MarshalFromJs(
                    i < args.Length ? args[i] : JsValue.Undefined,
                    paramInfos[i].ParameterType);
            }

            var result = del.DynamicInvoke(clrArgs);
            return TypeProxy.MarshalToJs(result);
        }, paramCount);
    }

    private JsValue WrapObject(object value)
    {
        var type = value.GetType();
        if (_registeredTypes.TryGetValue(type, out var typeProxy))
        {
            return new ObjectProxy(value, typeProxy);
        }

        // For unregistered types, create a simple JsObject wrapper with no methods
        return new ObjectProxy(value, null);
    }
}
