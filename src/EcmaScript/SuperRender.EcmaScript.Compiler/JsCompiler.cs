// CA1859: These private Compile* methods intentionally return the abstract Expr type
// for a uniform internal API — the caller (CompileNode) dispatches via pattern matching
// and always works with Expr, so narrowing return types would add no value.
// CA1822: Private Compile* methods are part of the compiler instance API even when a
// particular method does not currently access instance state (e.g. stubs for hoisted decls).
#pragma warning disable CA1859, CA1822

using System.Globalization;
using System.Runtime.CompilerServices;
using SuperRender.EcmaScript.Compiler.Ast;
using SuperRender.EcmaScript.Runtime.Builtins;
using SuperRender.EcmaScript.Runtime.Errors;
using SuperRender.EcmaScript.Runtime;
using Environment = SuperRender.EcmaScript.Runtime.Environment;
using Expr = System.Linq.Expressions.Expression;
using ParameterExpression = System.Linq.Expressions.ParameterExpression;
using LabelTarget = System.Linq.Expressions.LabelTarget;

namespace SuperRender.EcmaScript.Compiler;

/// <summary>
/// Compiles a JavaScript AST (<see cref="Program"/>) into a DLR expression tree
/// that can be executed as a <c>Func&lt;Environment, JsValue&gt;</c> delegate.
/// </summary>
public sealed partial class JsCompiler
{
    private readonly Realm _realm;

    // Current compilation state
    private ParameterExpression _envParam = null!;
    private ParameterExpression _thisParam = null!;
    private LabelTarget? _returnLabel;
    private LabelTarget? _breakLabel;
    private LabelTarget? _continueLabel;
    private ParameterExpression? _coroutineParam;

    // Label tracking for break/continue with named labels
    private readonly Dictionary<string, (LabelTarget breakTarget, LabelTarget? continueTarget)> _labelTargets = new();
    private LabelTarget? _pendingLabelBreak;
    private LabelTarget? _pendingLabelContinue;

    public JsCompiler(Realm realm)
    {
        _realm = realm;
    }

    // ───────────────────────── Public entry point ─────────────────────────

    /// <summary>
    /// Compiles a <see cref="Program"/> AST into an executable delegate.
    /// The delegate takes an <see cref="Environment"/> and returns the completion value.
    /// </summary>
    public Func<Environment, JsValue> Compile(Program program)
    {
        var envParam = Expr.Parameter(typeof(Environment), "env");
        var thisParam = Expr.Parameter(typeof(JsValue), "this");

        var prevEnv = _envParam;
        var prevThis = _thisParam;
        var prevReturn = _returnLabel;
        _envParam = envParam;
        _thisParam = thisParam;
        _returnLabel = null;

        try
        {
            var bodyExprs = new List<Expr>();

            // Hoist function declarations
            HoistFunctions(program.Body, bodyExprs);

            // Compile each statement
            foreach (var stmt in program.Body)
            {
                if (stmt is FunctionDeclaration)
                {
                    // Already hoisted
                    continue;
                }

                // Emit location tracking
                if (stmt.Location is not null)
                {
                    bodyExprs.Add(Expr.Call(
                        typeof(RuntimeHelpers).GetMethod(nameof(RuntimeHelpers.SetLocation))!,
                        Expr.Constant(stmt.Location.Line),
                        Expr.Constant(stmt.Location.Column)));
                }

                bodyExprs.Add(CompileNode(stmt));
            }

            // Ensure the block returns a JsValue
            if (bodyExprs.Count == 0 || !typeof(JsValue).IsAssignableFrom(bodyExprs[^1].Type))
            {
                bodyExprs.Add(Expr.Constant(JsValue.Undefined, typeof(JsValue)));
            }

            var body = Expr.Block(typeof(JsValue), bodyExprs);

            // The outer lambda captures thisParam; we bind it to the global object at call time
            var inner = Expr.Lambda<Func<Environment, JsValue, JsValue>>(body, envParam, thisParam);
            var compiled = inner.Compile();

            var globalObj = _realm.GlobalObject;
            return env => compiled(env, globalObj);
        }
        finally
        {
            _envParam = prevEnv;
            _thisParam = prevThis;
            _returnLabel = prevReturn;
        }
    }

    // ───────────────────────── Node dispatch ─────────────────────────

    private Expr CompileNode(SyntaxNode node) => node switch
    {
        // Statements
        BlockStatement block => CompileBlock(block),
        ExpressionStatement es => CompileNode(es.Expression),
        EmptyStatement => Expr.Constant(JsValue.Undefined, typeof(JsValue)),
        IfStatement ifs => CompileIf(ifs),
        ForStatement fs => CompileFor(fs),
        ForInStatement fi => CompileForIn(fi),
        ForOfStatement fo => CompileForOf(fo),
        WhileStatement ws => CompileWhile(ws),
        DoWhileStatement dw => CompileDoWhile(dw),
        SwitchStatement ss => CompileSwitch(ss),
        TryStatement ts => CompileTry(ts),
        ReturnStatement rs => CompileReturn(rs),
        ThrowStatement ths => CompileThrow(ths),
        BreakStatement bs => CompileBreak(bs),
        ContinueStatement cs => CompileContinue(cs),
        LabeledStatement ls => CompileLabeledStatement(ls),
        VariableDeclaration vd => CompileVariableDeclaration(vd),
        FunctionDeclaration fd => CompileFunctionDecl(fd),
        ClassDeclaration cd => CompileClassDecl(cd),

        // Expressions
        Identifier id => CompileIdentifier(id),
        Literal lit => CompileLiteral(lit),
        ThisExpression => _thisParam,
        BinaryExpression be => CompileBinary(be),
        LogicalExpression le => CompileLogical(le),
        UnaryExpression ue => CompileUnary(ue),
        UpdateExpression upd => CompileUpdate(upd),
        AssignmentExpression ae => CompileAssignment(ae),
        ConditionalExpression ce => CompileConditional(ce),
        CallExpression call => CompileCall(call),
        NewExpression ne => CompileNew(ne),
        MemberExpression me => CompileMember(me),
        ArrowFunctionExpression arrow => CompileArrow(arrow),
        FunctionExpression fe => CompileFunctionExpr(fe),
        ObjectExpression oe => CompileObject(oe),
        ArrayExpression arrExpr => CompileArray(arrExpr),
        SpreadElement se => CompileSpread(se),
        TemplateLiteral tl => CompileTemplateLiteral(tl),
        TaggedTemplateExpression tte => CompileTaggedTemplate(tte),
        SequenceExpression seq => CompileSequence(seq),
        ClassExpression clsExpr => CompileClassExpr(clsExpr),
        ChainExpression chain => CompileChain(chain),
        YieldExpression ye => CompileYield(ye),
        AwaitExpression ae => CompileAwait(ae),

        _ => throw new JsSyntaxError($"Unsupported AST node: {node.GetType().Name}")
    };

}

/// <summary>
/// Static helper methods called from compiled expression trees for complex JS operations
/// that are easier to implement in C# than directly in expression trees.
/// </summary>
public static class RuntimeHelpers
{
    // Set by JsEngine before each execution to enable primitive autoboxing
    [ThreadStatic]
#pragma warning disable CS0649, CA2211 // Assigned by JsEngine in a separate assembly
    public static Realm? CurrentRealm;
#pragma warning restore CS0649, CA2211

    // ───────────────────────── Source location tracking ─────────────────────────

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void SetLocation(int line, int column)
    {
        Runtime.ExecutionContext.CurrentLine = line;
        Runtime.ExecutionContext.CurrentColumn = column;
    }

    // ───────────────────────── Arithmetic ─────────────────────────

    public static JsValue Add(JsValue left, JsValue right)
    {
        var l = left.ToPrimitive();
        var r = right.ToPrimitive();

        if (l is JsString || r is JsString)
        {
            return new JsString(l.ToJsString() + r.ToJsString());
        }

        return JsNumber.Create(l.ToNumber() + r.ToNumber());
    }

    public static JsValue Sub(JsValue left, JsValue right) =>
        JsNumber.Create(left.ToNumber() - right.ToNumber());

    public static JsValue Mul(JsValue left, JsValue right) =>
        JsNumber.Create(left.ToNumber() * right.ToNumber());

    public static JsValue Div(JsValue left, JsValue right) =>
        JsNumber.Create(left.ToNumber() / right.ToNumber());

    public static JsValue Mod(JsValue left, JsValue right) =>
        JsNumber.Create(left.ToNumber() % right.ToNumber());

    public static JsValue Power(JsValue left, JsValue right) =>
        JsNumber.Create(Math.Pow(left.ToNumber(), right.ToNumber()));

    // ───────────────────────── Comparison ─────────────────────────

    public static JsValue StrictEqual(JsValue left, JsValue right) =>
        left.StrictEquals(right) ? JsValue.True : JsValue.False;

    public static JsValue StrictNotEqual(JsValue left, JsValue right) =>
        left.StrictEquals(right) ? JsValue.False : JsValue.True;

    public static JsValue AbstractEqual(JsValue left, JsValue right) =>
        left.AbstractEquals(right) ? JsValue.True : JsValue.False;

    public static JsValue AbstractNotEqual(JsValue left, JsValue right) =>
        left.AbstractEquals(right) ? JsValue.False : JsValue.True;

    public static bool StrictEqualBool(JsValue left, JsValue right) =>
        left.StrictEquals(right);

    public static JsValue LessThan(JsValue left, JsValue right)
    {
        var l = left.ToPrimitive("number");
        var r = right.ToPrimitive("number");

        if (l is JsString ls && r is JsString rs)
        {
            return string.Compare(ls.Value, rs.Value, StringComparison.Ordinal) < 0
                ? JsValue.True
                : JsValue.False;
        }

        var ln = l.ToNumber();
        var rn = r.ToNumber();
        if (double.IsNaN(ln) || double.IsNaN(rn)) return JsValue.Undefined;
        return ln < rn ? JsValue.True : JsValue.False;
    }

    public static JsValue GreaterThan(JsValue left, JsValue right)
    {
        var l = left.ToPrimitive("number");
        var r = right.ToPrimitive("number");

        if (l is JsString ls && r is JsString rs)
        {
            return string.Compare(ls.Value, rs.Value, StringComparison.Ordinal) > 0
                ? JsValue.True
                : JsValue.False;
        }

        var ln = l.ToNumber();
        var rn = r.ToNumber();
        if (double.IsNaN(ln) || double.IsNaN(rn)) return JsValue.Undefined;
        return ln > rn ? JsValue.True : JsValue.False;
    }

    public static JsValue LessThanOrEqual(JsValue left, JsValue right)
    {
        var gt = GreaterThan(left, right);
        if (gt is JsUndefined) return JsValue.False;
        return gt.ToBoolean() ? JsValue.False : JsValue.True;
    }

    public static JsValue GreaterThanOrEqual(JsValue left, JsValue right)
    {
        var lt = LessThan(left, right);
        if (lt is JsUndefined) return JsValue.False;
        return lt.ToBoolean() ? JsValue.False : JsValue.True;
    }

    // ───────────────────────── Bitwise ─────────────────────────

    public static JsValue BitwiseAnd(JsValue left, JsValue right) =>
        JsNumber.Create(ToInt32(left) & ToInt32(right));

    public static JsValue BitwiseOr(JsValue left, JsValue right) =>
        JsNumber.Create(ToInt32(left) | ToInt32(right));

    public static JsValue BitwiseXor(JsValue left, JsValue right) =>
        JsNumber.Create(ToInt32(left) ^ ToInt32(right));

    public static JsValue LeftShift(JsValue left, JsValue right) =>
        JsNumber.Create(ToInt32(left) << (ToInt32(right) & 0x1F));

    public static JsValue RightShift(JsValue left, JsValue right) =>
        JsNumber.Create(ToInt32(left) >> (ToInt32(right) & 0x1F));

    public static JsValue UnsignedRightShift(JsValue left, JsValue right) =>
        JsNumber.Create((double)(ToUint32(left) >> (ToInt32(right) & 0x1F)));

    // ───────────────────────── Unary ─────────────────────────

    public static JsValue Typeof(JsValue value) => new JsString(value.TypeOf);

    public static JsValue TypeofSafe(Environment env, string name)
    {
        if (!env.HasBinding(name))
        {
            return new JsString("undefined");
        }

        return new JsString(env.GetBinding(name).TypeOf);
    }

    public static JsValue Not(JsValue value) =>
        value.ToBoolean() ? JsValue.False : JsValue.True;

    public static JsValue Negate(JsValue value) =>
        JsNumber.Create(-value.ToNumber());

    public static JsValue Plus(JsValue value) =>
        JsNumber.Create(value.ToNumber());

    public static JsValue BitwiseNot(JsValue value) =>
        JsNumber.Create(~ToInt32(value));

    public static JsValue Void(JsValue _) => JsValue.Undefined;

    // ───────────────────────── Type checks ─────────────────────────

    public static JsValue InstanceOf(JsValue obj, JsValue ctor)
    {
        if (ctor is not JsFunction ctorFn)
        {
            throw new JsTypeError("Right-hand side of instanceof is not callable", Runtime.ExecutionContext.CurrentLine, Runtime.ExecutionContext.CurrentColumn);
        }

        var proto = ctorFn.PrototypeObject;
        if (proto is null)
        {
            return JsValue.False;
        }

        if (obj is not JsObject jsObj)
        {
            return JsValue.False;
        }

        var current = jsObj.Prototype;
        while (current is not null)
        {
            if (ReferenceEquals(current, proto))
            {
                return JsValue.True;
            }

            current = current.Prototype;
        }

        return JsValue.False;
    }

    public static JsValue In(JsValue key, JsValue obj)
    {
        if (obj is not JsObject jsObj)
        {
            throw new JsTypeError("Cannot use 'in' operator to search for '" +
                key.ToJsString() + "' in " + obj.ToJsString(), Runtime.ExecutionContext.CurrentLine, Runtime.ExecutionContext.CurrentColumn);
        }

        return jsObj.HasProperty(key.ToJsString()) ? JsValue.True : JsValue.False;
    }

    // ───────────────────────── Member access ─────────────────────────

    public static JsValue GetMember(JsValue obj, string name)
    {
        if (obj is JsNull or JsUndefined)
        {
            throw new JsTypeError($"Cannot read properties of {obj.TypeOf} (reading '{name}')", Runtime.ExecutionContext.CurrentLine, Runtime.ExecutionContext.CurrentColumn);
        }

        if (obj is JsObject jsObj)
        {
            return jsObj.Get(name);
        }

        // Primitive autoboxing for strings
        if (obj is JsString str)
        {
            if (name == "length") return JsNumber.Create(str.Length);
            if (int.TryParse(name, NumberStyles.None, CultureInfo.InvariantCulture, out var idx))
            {
                return str[idx];
            }
            // Look up method on StringPrototype
            if (CurrentRealm?.StringPrototype is { } strProto)
            {
                var method = strProto.Get(name);
                if (method is not JsUndefined) return method;
            }
        }

        // Primitive autoboxing for numbers
        if (obj is JsNumber)
        {
            if (CurrentRealm?.NumberPrototype is { } numProto)
            {
                var method = numProto.Get(name);
                if (method is not JsUndefined) return method;
            }
        }

        // Primitive autoboxing for booleans
        if (obj is JsBoolean)
        {
            if (CurrentRealm?.BooleanPrototype is { } boolProto)
            {
                var method = boolProto.Get(name);
                if (method is not JsUndefined) return method;
            }
        }

        return JsValue.Undefined;
    }

    public static JsValue GetComputedMember(JsValue obj, JsValue key)
    {
        var name = key.ToJsString();
        return GetMember(obj, name);
    }

    public static void SetMember(JsValue obj, string name, JsValue value)
    {
        if (obj is JsNull or JsUndefined)
        {
            throw new JsTypeError($"Cannot set properties of {obj.TypeOf} (setting '{name}')", Runtime.ExecutionContext.CurrentLine, Runtime.ExecutionContext.CurrentColumn);
        }

        if (obj is JsObject jsObj)
        {
            jsObj.Set(name, value);
        }

        // Setting on primitives is a silent no-op
    }

    public static void SetComputedMember(JsValue obj, JsValue key, JsValue value)
    {
        var name = key.ToJsString();
        SetMember(obj, name, value);
    }

    // ───────────────────────── Function calls ─────────────────────────

    public static JsValue CallFunction(JsValue callee, JsValue thisArg, JsValue[] args)
    {
        if (callee is not JsFunction fn)
        {
            throw new JsTypeError($"{callee.ToJsString()} is not a function", Runtime.ExecutionContext.CurrentLine, Runtime.ExecutionContext.CurrentColumn);
        }

        return fn.Call(thisArg, args);
    }

    public static JsValue NewCall(JsValue callee, JsValue[] args)
    {
        if (callee is not JsFunction fn)
        {
            throw new JsTypeError($"{callee.ToJsString()} is not a constructor", Runtime.ExecutionContext.CurrentLine, Runtime.ExecutionContext.CurrentColumn);
        }

        return fn.Construct(args);
    }

    // ───────────────────────── Delete ─────────────────────────

    public static JsValue DeleteMember(JsValue obj, string name)
    {
        if (obj is JsObject jsObj)
        {
            return jsObj.Delete(name) ? JsValue.True : JsValue.False;
        }

        return JsValue.True;
    }

    public static JsValue DeleteComputedMember(JsValue obj, JsValue key)
    {
        return DeleteMember(obj, key.ToJsString());
    }

    // ───────────────────────── Object/Array helpers ─────────────────────────

    public static void ObjectSpread(JsObject target, JsValue source)
    {
        if (source is not JsObject srcObj)
        {
            return;
        }

        foreach (var key in srcObj.OwnPropertyKeys())
        {
            var desc = srcObj.GetOwnProperty(key);
            if (desc is not null && (desc.Enumerable ?? true))
            {
                target.Set(key, srcObj.Get(key));
            }
        }
    }

    public static void ArraySpread(JsArray target, JsValue source)
    {
        if (source is JsArray arr)
        {
            for (int i = 0; i < arr.DenseLength; i++)
            {
                target.Push(arr.GetIndex(i));
            }
        }
        else if (source is JsString str)
        {
            foreach (char c in str.Value)
            {
                target.Push(new JsString(c.ToString()));
            }
        }
        else if (source is JsObject obj
            && obj.TryGetSymbolProperty(JsSymbol.Iterator, out var iterFn)
            && iterFn is JsFunction fn)
        {
            // Iterator protocol — supports generators, Sets, Maps, etc.
            var iterator = fn.Call(source, []);
            while (true)
            {
                var result = CallFunction(GetMember(iterator, "next"), iterator, []);
                if (GetMember(result, "done").ToBoolean()) break;
                target.Push(GetMember(result, "value"));
            }
        }
        else if (source is JsObject genObj)
        {
            // Fallback: iterate using numeric keys
            for (int i = 0; ; i++)
            {
                var idx = i.ToString(CultureInfo.InvariantCulture);
                if (!genObj.HasProperty(idx)) break;
                target.Push(genObj.Get(idx));
            }
        }
    }

    public static void SpreadIntoList(List<JsValue> list, JsValue source)
    {
        if (source is JsArray arr)
        {
            for (int i = 0; i < arr.DenseLength; i++)
            {
                list.Add(arr.GetIndex(i));
            }
        }
        else if (source is JsString str)
        {
            foreach (char c in str.Value)
            {
                list.Add(new JsString(c.ToString()));
            }
        }
        else if (source is JsObject obj
            && obj.TryGetSymbolProperty(JsSymbol.Iterator, out var iterFn)
            && iterFn is JsFunction fn)
        {
            var iterator = fn.Call(source, []);
            while (true)
            {
                var result = CallFunction(GetMember(iterator, "next"), iterator, []);
                if (GetMember(result, "done").ToBoolean()) break;
                list.Add(GetMember(result, "value"));
            }
        }
    }

    // ───────────────────────── For-in/of helpers ─────────────────────────

    public static string[] GetForInKeys(JsValue obj)
    {
        if (obj is not JsObject jsObj)
        {
            return [];
        }

        var keys = new List<string>();
        var current = jsObj;
        while (current is not null)
        {
            foreach (var key in current.OwnPropertyKeys())
            {
                var desc = current.GetOwnProperty(key);
                if (desc is not null && (desc.Enumerable ?? true) && !keys.Contains(key))
                {
                    keys.Add(key);
                }
            }

            current = current.Prototype;
        }

        return [.. keys];
    }

    // For-of values: stored as stringified keys for the simple string[] return
    // But we need actual JsValues, so we use a two-step approach
    private static readonly ConditionalWeakTable<JsValue, JsValue[]> ForOfCache = new();

    public static string[] GetForOfValues(JsValue obj)
    {
        JsValue[] values;

        if (obj is JsArray arr)
        {
            values = new JsValue[arr.DenseLength];
            for (int i = 0; i < arr.DenseLength; i++)
            {
                values[i] = arr.GetIndex(i);
            }
        }
        else if (obj is JsString str)
        {
            values = new JsValue[str.Length];
            for (int i = 0; i < str.Length; i++)
            {
                values[i] = new JsString(str.Value[i].ToString());
            }
        }
        else
        {
            values = [];
        }

        ForOfCache.AddOrUpdate(obj, values);

        var indices = new string[values.Length];
        for (int i = 0; i < values.Length; i++)
        {
            indices[i] = i.ToString(CultureInfo.InvariantCulture);
        }

        return indices;
    }

    public static JsValue GetForOfValueAt(JsValue obj, int index)
    {
        if (ForOfCache.TryGetValue(obj, out var values) && index < values.Length)
        {
            return values[index];
        }

        return JsValue.Undefined;
    }

    // ───────────────────────── Rest/Arguments ─────────────────────────

    public static JsValue CreateRestArray(JsValue[] args, int startIndex, JsObject arrayPrototype)
    {
        var arr = new JsArray();
        arr.Prototype = arrayPrototype;
        for (int i = startIndex; i < args.Length; i++)
        {
            arr.Push(args[i]);
        }

        return arr;
    }

    public static JsValue CreateArgumentsObject(JsValue[] args, JsObject arrayPrototype)
    {
        var obj = new JsObject();
        for (int i = 0; i < args.Length; i++)
        {
            obj.Set(i.ToString(CultureInfo.InvariantCulture), args[i]);
        }

        obj.DefineOwnProperty("length", PropertyDescriptor.Data(
            JsNumber.Create(args.Length), writable: true, enumerable: false, configurable: true));

        return obj;
    }

    // ───────────────────────── Destructuring helpers ─────────────────────────

    public static JsValue ObjectRestProperties(JsValue source, string[] excludeKeys)
    {
        var result = new JsObject();
        if (source is not JsObject srcObj)
        {
            return result;
        }

        var excludeSet = new HashSet<string>(excludeKeys, StringComparer.Ordinal);
        foreach (var key in srcObj.OwnPropertyKeys())
        {
            if (!excludeSet.Contains(key))
            {
                var desc = srcObj.GetOwnProperty(key);
                if (desc is not null && (desc.Enumerable ?? true))
                {
                    result.Set(key, srcObj.Get(key));
                }
            }
        }

        return result;
    }

    public static JsValue ArraySliceFrom(JsValue source, int startIndex, JsObject arrayPrototype)
    {
        var arr = new JsArray();
        arr.Prototype = arrayPrototype;

        if (source is JsArray srcArr)
        {
            for (int i = startIndex; i < srcArr.DenseLength; i++)
            {
                arr.Push(srcArr.GetIndex(i));
            }
        }

        return arr;
    }

    // ───────────────────────── Class helpers ─────────────────────────

    public static JsValue CreateDefaultConstructor(string name)
    {
        var fn = new JsFunction
        {
            Name = name,
            CallTarget = static (_, _) => JsValue.Undefined,
            IsConstructor = true
        };
        return fn;
    }

    public static JsValue CreateDerivedDefaultConstructor(string name, JsValue superClass)
    {
        // Default derived constructor: constructor(...args) { super(...args); }
        var fn = new JsFunction
        {
            Name = name,
            IsConstructor = true,
            CallTarget = (thisArg, args) =>
            {
                if (superClass is JsFunction superFn)
                {
                    superFn.Call(thisArg, args);
                }
                return JsValue.Undefined;
            }
        };
        return fn;
    }

    public static void SetupInheritance(JsFunction ctor, JsObject proto, JsValue superClass)
    {
        if (superClass is not JsFunction superFn)
        {
            throw new JsTypeError("Super expression must be a function", Runtime.ExecutionContext.CurrentLine, Runtime.ExecutionContext.CurrentColumn);
        }

        proto.Prototype = superFn.PrototypeObject;
        ctor.Prototype = superFn as JsObject;
    }

    public static void DefineGetter(JsObject obj, string name, JsValue getter)
    {
        var existing = obj.GetOwnProperty(name);
        if (existing is not null && existing.IsAccessorDescriptor)
        {
            existing.Get = getter;
        }
        else
        {
            obj.DefineOwnProperty(name, PropertyDescriptor.Accessor(getter, null));
        }
    }

    public static void DefineSetter(JsObject obj, string name, JsValue setter)
    {
        var existing = obj.GetOwnProperty(name);
        if (existing is not null && existing.IsAccessorDescriptor)
        {
            existing.Set = setter;
        }
        else
        {
            obj.DefineOwnProperty(name, PropertyDescriptor.Accessor(null, setter));
        }
    }

    // ───────────────────────── Exception helpers ─────────────────────────

    public static Exception CreateException(JsValue value)
    {
        if (value is JsString str)
        {
            return new JsTypeError(str.Value, Runtime.ExecutionContext.CurrentLine, Runtime.ExecutionContext.CurrentColumn);
        }

        if (value is JsObject obj)
        {
            var msg = obj.Get("message");
            return new JsTypeError(msg is JsString s ? s.Value : value.ToJsString(), Runtime.ExecutionContext.CurrentLine, Runtime.ExecutionContext.CurrentColumn);
        }

        return new JsTypeError(value.ToJsString(), Runtime.ExecutionContext.CurrentLine, Runtime.ExecutionContext.CurrentColumn);
    }

    public static JsValue ExceptionToJsValue(Exception ex)
    {
        if (ex is JsThrownValueException jtv)
        {
            return jtv.ThrownValue;
        }

        if (ex is JsErrorBase jsErr)
        {
            var errObj = new JsObject();
            if (CurrentRealm?.ErrorPrototype is not null)
            {
                errObj.Prototype = CurrentRealm.ErrorPrototype;
            }

            errObj.Set("message", new JsString(jsErr.Message));
            errObj.Set("name", new JsString(ex.GetType().Name switch
            {
                nameof(JsTypeError) => "TypeError",
                nameof(JsReferenceError) => "ReferenceError",
                nameof(JsSyntaxError) => "SyntaxError",
                nameof(JsRangeError) => "RangeError",
                _ => "Error"
            }));
            return errObj;
        }

        var obj = new JsObject();
        if (CurrentRealm?.ErrorPrototype is not null)
        {
            obj.Prototype = CurrentRealm.ErrorPrototype;
        }

        obj.Set("message", new JsString(ex.Message));
        obj.Set("name", new JsString("Error"));
        return obj;
    }

    // ───────────────────────── Numeric conversion helpers ─────────────────────────

    private static int ToInt32(JsValue value)
    {
        var n = value.ToNumber();
        if (double.IsNaN(n) || double.IsInfinity(n) || n == 0)
        {
            return 0;
        }

        return (int)(long)n;
    }

    private static uint ToUint32(JsValue value)
    {
        var n = value.ToNumber();
        if (double.IsNaN(n) || double.IsInfinity(n) || n == 0)
        {
            return 0;
        }

        return (uint)(long)n;
    }

    // ───────────────────────── Generator / Async helpers ─────────────────────────

    public static JsValue GeneratorYield(GeneratorCoroutine coroutine, JsValue value) =>
        coroutine.Yield(value);

    public static JsValue CreateGeneratorObject(
        Func<JsValue, JsValue[], GeneratorCoroutine, JsValue> body,
        JsValue thisArg,
        JsValue[] args,
        JsObject generatorPrototype)
    {
        var coroutine = new GeneratorCoroutine();
        // Defer starting the body until the first next() call
        Action startAction = () => coroutine.Start(co => body(thisArg, args, co));
        return new JsGeneratorObject(coroutine, startAction, generatorPrototype);
    }

    public static JsValue RunAsyncFunction(
        Func<JsValue, JsValue[], GeneratorCoroutine, JsValue> body,
        JsValue thisArg,
        JsValue[] args,
        JsObject promisePrototype,
        Realm realm)
    {
        var outerPromise = new JsPromiseObject { Prototype = promisePrototype };
        var coroutine = new GeneratorCoroutine();

        void Advance(JsValue sent, bool isThrow)
        {
            while (true)
            {
                (JsValue value, bool done) result;
                try
                {
                    result = isThrow ? coroutine.Throw(sent) : coroutine.Next(sent);
                }
                catch (Exception ex)
                {
                    SuperRender.EcmaScript.Runtime.Builtins.PromiseConstructor.RejectPromise(outerPromise, ExceptionToJsValue(ex));
                    return;
                }

                if (result.done)
                {
                    SuperRender.EcmaScript.Runtime.Builtins.PromiseConstructor.ResolvePromise(outerPromise, result.value, realm);
                    return;
                }

                // The yielded value is what was awaited
                if (result.value is JsPromiseObject promise)
                {
                    SuperRender.EcmaScript.Runtime.Builtins.PromiseConstructor.PromiseThen(promise,
                        JsFunction.CreateNative("", (_, a) =>
                        {
                            Advance(a.Length > 0 ? a[0] : JsValue.Undefined, false);
                            return JsValue.Undefined;
                        }, 1),
                        JsFunction.CreateNative("", (_, a) =>
                        {
                            Advance(a.Length > 0 ? a[0] : JsValue.Undefined, true);
                            return JsValue.Undefined;
                        }, 1),
                        realm);
                    return;
                }

                // Not a promise — resume immediately with the value
                sent = result.value;
                isThrow = false;
            }
        }

        coroutine.Start(co => body(thisArg, args, co));

        (JsValue Value, bool Done) initial;
        try
        {
            initial = coroutine.GetInitialResult();
        }
        catch (Exception ex)
        {
            SuperRender.EcmaScript.Runtime.Builtins.PromiseConstructor.RejectPromise(outerPromise, ExceptionToJsValue(ex));
            return outerPromise;
        }

        if (initial.Done)
        {
            SuperRender.EcmaScript.Runtime.Builtins.PromiseConstructor.ResolvePromise(outerPromise, initial.Value, realm);
        }
        else if (initial.Value is JsPromiseObject initPromise)
        {
            SuperRender.EcmaScript.Runtime.Builtins.PromiseConstructor.PromiseThen(initPromise,
                JsFunction.CreateNative("", (_, a) =>
                {
                    Advance(a.Length > 0 ? a[0] : JsValue.Undefined, false);
                    return JsValue.Undefined;
                }, 1),
                JsFunction.CreateNative("", (_, a) =>
                {
                    Advance(a.Length > 0 ? a[0] : JsValue.Undefined, true);
                    return JsValue.Undefined;
                }, 1),
                realm);
        }
        else
        {
            // Non-promise initial value — resume immediately
            Advance(initial.Value, false);
        }

        return outerPromise;
    }

    public static JsValue YieldStar(GeneratorCoroutine coroutine, JsValue iterable)
    {
        var iterator = GetIterator(iterable);
        var nextFn = GetMember(iterator, "next");

        var result = CallFunction(nextFn, iterator, []);
        while (!GetMember(result, "done").ToBoolean())
        {
            var innerValue = GetMember(result, "value");
            var sent = coroutine.Yield(innerValue);
            result = CallFunction(nextFn, iterator, [sent]);
        }

        return GetMember(result, "value");
    }

    // ───────────────────────── Iterator protocol ─────────────────────────

    public static JsValue GetIterator(JsValue obj)
    {
        if (obj is JsNull or JsUndefined)
        {
            throw new JsTypeError($"Cannot read properties of {obj.TypeOf}", Runtime.ExecutionContext.CurrentLine, Runtime.ExecutionContext.CurrentColumn);
        }

        // Check for Symbol.iterator on JsObject (includes arrays, generators, etc.)
        if (obj is JsObject jsObj
            && jsObj.TryGetSymbolProperty(JsSymbol.Iterator, out var iterFn)
            && iterFn is JsFunction fn)
        {
            var iterator = fn.Call(obj, []);
            if (iterator is JsObject)
            {
                return iterator;
            }

            throw new JsTypeError("Result of the Symbol.iterator method is not an object", Runtime.ExecutionContext.CurrentLine, Runtime.ExecutionContext.CurrentColumn);
        }

        // Autoboxing for primitives: check prototype for Symbol.iterator
        JsObject? proto = null;
        if (obj is JsString)
        {
            proto = CurrentRealm?.StringPrototype;
        }

        if (proto is not null
            && proto.TryGetSymbolProperty(JsSymbol.Iterator, out var primIterFn)
            && primIterFn is JsFunction primFn)
        {
            var iterator = primFn.Call(obj, []);
            if (iterator is JsObject)
            {
                return iterator;
            }
        }

        throw new JsTypeError(obj.TypeOf + " is not iterable", Runtime.ExecutionContext.CurrentLine, Runtime.ExecutionContext.CurrentColumn);
    }

    public static JsValue IteratorNext(JsValue iterator)
    {
        var nextFn = GetMember(iterator, "next");
        return CallFunction(nextFn, iterator, []);
    }

    public static bool IteratorComplete(JsValue result)
    {
        return GetMember(result, "done").ToBoolean();
    }

    public static JsValue IteratorValue(JsValue result)
    {
        return GetMember(result, "value");
    }
}
