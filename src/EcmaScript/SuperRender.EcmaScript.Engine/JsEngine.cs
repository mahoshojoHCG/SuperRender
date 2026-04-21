using SuperRender.EcmaScript.Runtime.Builtins;
using SuperRender.EcmaScript.Compiler;
using SuperRender.EcmaScript.Compiler.Parsing;
using SuperRender.EcmaScript.Runtime;
using Environment = SuperRender.EcmaScript.Runtime.Environment;

namespace SuperRender.EcmaScript.Engine;

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
        InstallEval();
        InstallFunctionFactory();
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
            throw new SuperRender.EcmaScript.Runtime.Errors.JsTypeError($"{functionName} is not a function");

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
        _realm
            .Install<ObjectConstructor>()
            .Install<FunctionConstructor>()
            .Install<ArrayConstructor>()
            .Install<StringConstructor>()
            .Install<NumberConstructor>()
            .Install<JsBooleanObject>()
            .Install<SymbolConstructor>()
            .Install<MathObject>()
            .Install<JsonObject>()
            .Install<DateConstructor>()
            .Install<RegExpConstructor>()
            .Install<ErrorConstructor>()
            .Install<MapConstructor>()
            .Install<SetConstructor>()
            .Install<JsWeakMapObject>()
            .Install<JsWeakSetObject>()
            .Install<PromiseConstructor>()
            .Install<ProxyConstructor>()
            .Install<ReflectObject>()
            .Install<ConsoleObject>()
            .Install<IteratorConstructor>()
            .Install<JsWeakRefObject>()
            .Install<JsFinalizationRegistryObject>()
            .Install<StructuredCloneHelper>()
            .Install<BigIntConstructor>()
            .Install<IntlObject>()
            .Install<TemporalObject>()
            .Install<ArrayBufferConstructor>()
            .Install<TypedArrayConstructor>()
            .Install<AtomicsObject>()
            .Install<ShadowRealmConstructor>();
    }

    private void InstallEval()
    {
        _realm.EvalFactory = (code, targetRealm) =>
        {
            var parser = new Parser(code);
            var program = parser.Parse();
            var compiler = new JsCompiler(targetRealm);
            var compiled = compiler.Compile(program);
            Compiler.RuntimeHelpers.CurrentRealm = targetRealm;
            return compiled(targetRealm.GlobalEnvironment);
        };

        _realm.InstallGlobal("eval", JsFunction.CreateNative("eval", (_, args) =>
        {
            var codeArg = args.Length > 0 ? args[0] : JsValue.Undefined;
            if (codeArg is not JsString codeStr) return codeArg;
            try
            {
                var parser = new Parser(codeStr.Value);
                var program = parser.Parse();
                var compiled = _compiler.Compile(program);
                Compiler.RuntimeHelpers.CurrentRealm = _realm;
                return compiled(_realm.GlobalEnvironment);
            }
            catch (Exception ex) when (ex is Runtime.Errors.JsErrorBase)
            {
                throw;
            }
        }, 1));
    }

    private void InstallFunctionFactory()
    {
        _realm.FunctionFactory = (paramNames, body) =>
        {
            var paramList = string.Join(",", paramNames);
            var source = $"(function({paramList}){{{body}}})";
            var parser = new Parser(source);
            var program = parser.Parse();
            var compiled = _compiler.Compile(program);
            Compiler.RuntimeHelpers.CurrentRealm = _realm;
            var result = compiled(_realm.GlobalEnvironment);
            if (result is JsFunction fn)
                return fn;
            throw new Runtime.Errors.JsTypeError("Failed to create function");
        };
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

    private ObjectProxy WrapObject(object value)
    {
        var type = value.GetType();
        if (_registeredTypes.TryGetValue(type, out var typeProxy))
        {
            return new ObjectProxy(value, typeProxy);
        }

        // For unregistered types, create a simple JsDynamicObject wrapper with no methods
        return new ObjectProxy(value, null);
    }
}
