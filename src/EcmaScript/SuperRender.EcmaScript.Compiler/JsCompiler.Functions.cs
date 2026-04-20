// CA1859: These private Compile* methods intentionally return the abstract Expr type
// for a uniform internal API — the caller (CompileNode) dispatches via pattern matching
// and always works with Expr, so narrowing return types would add no value.
// CA1822: Private Compile* methods are part of the compiler instance API even when a
// particular method does not currently access instance state (e.g. stubs for hoisted decls).
#pragma warning disable CA1859, CA1822

using SuperRender.EcmaScript.Compiler.Ast;
using SuperRender.EcmaScript.Runtime.Errors;
using SuperRender.EcmaScript.Runtime;
using Environment = SuperRender.EcmaScript.Runtime.Environment;
using Expr = System.Linq.Expressions.Expression;
using ParameterExpression = System.Linq.Expressions.ParameterExpression;
using LabelTarget = System.Linq.Expressions.LabelTarget;

namespace SuperRender.EcmaScript.Compiler;

public sealed partial class JsCompiler
{
    // ───────────────────────── Functions ─────────────────────────

    // TODO: Tail Call Optimization (TCO) — When the last statement of a function body is a
    // ReturnStatement containing a CallExpression to the function itself, convert to a loop.
    // This requires detecting self-recursive tail calls during compilation and emitting a
    // goto-based loop instead of the recursive call. Deferred due to complexity: requires
    // tracking function identity through closures and handling all argument rebinding cases.

    private Expr CompileFunctionBody(
        string name,
        IReadOnlyList<SyntaxNode> parameters,
        SyntaxNode body,
        bool isExpression,
        bool isArrow,
        bool isGenerator = false,
        bool isAsync = false)
    {
        // Save current state
        var outerEnv = _envParam;
        var outerThis = _thisParam;
        var outerReturn = _returnLabel;
        var outerBreak = _breakLabel;
        var outerContinue = _continueLabel;
        var outerCoroutine = _coroutineParam;
        var outerLabelTargets = new Dictionary<string, (LabelTarget, LabelTarget?)>(_labelTargets);
        var outerPendingBreak = _pendingLabelBreak;
        var outerPendingContinue = _pendingLabelContinue;

        // New parameters for the function body
        var fnThisParam = Expr.Parameter(typeof(JsValue), "fnThis");
        var fnArgsParam = Expr.Parameter(typeof(JsValue[]), "fnArgs");
        var fnEnv = Expr.Parameter(typeof(Environment), "fnEnv");

        var returnLabel = Expr.Label(typeof(JsValue), "fnReturn");
        _envParam = fnEnv;
        _thisParam = isArrow ? outerThis : fnThisParam;
        _returnLabel = returnLabel;
        _breakLabel = null;
        _continueLabel = null;
        _labelTargets.Clear();
        _pendingLabelBreak = null;
        _pendingLabelContinue = null;

        // Set up coroutine parameter for generators/async
        ParameterExpression? coroutineParam = null;
        if (isGenerator || isAsync)
        {
            coroutineParam = Expr.Parameter(typeof(GeneratorCoroutine), "co");
            _coroutineParam = coroutineParam;
        }
        else
        {
            _coroutineParam = null;
        }

        try
        {
            var bodyExprs = new List<Expr>();

            // Create function-scoped environment (will be closed over the caller's env)
            // At runtime, we'll create the env from the closure scope captured in JsFunction
            // For now, fnEnv is a lambda parameter that we'll set up in the CallTarget

            // Bind parameters
            for (int i = 0; i < parameters.Count; i++)
            {
                var param = parameters[i];

                if (param is Identifier paramId)
                {
                    var argVal = Expr.Condition(
                        Expr.LessThan(Expr.Constant(i), Expr.ArrayLength(fnArgsParam)),
                        Expr.ArrayIndex(fnArgsParam, Expr.Constant(i)),
                        Expr.Constant(JsValue.Undefined, typeof(JsValue)),
                        typeof(JsValue));
                    bodyExprs.Add(CallEnvMethod("CreateAndInitializeBinding",
                        Expr.Constant(paramId.Name),
                        Expr.Constant(true),
                        argVal));
                }
                else if (param is AssignmentPattern ap)
                {
                    // Default parameter: param = defaultValue
                    var argVal = Expr.Condition(
                        Expr.LessThan(Expr.Constant(i), Expr.ArrayLength(fnArgsParam)),
                        Expr.ArrayIndex(fnArgsParam, Expr.Constant(i)),
                        Expr.Constant(JsValue.Undefined, typeof(JsValue)),
                        typeof(JsValue));

                    var argVar = Expr.Parameter(typeof(JsValue), "arg");
                    var defaultVal = CompileNode(ap.Right);

                    var finalVal = Expr.Condition(
                        Expr.TypeIs(argVar, typeof(JsUndefined)),
                        EnsureJsValue(defaultVal),
                        argVar,
                        typeof(JsValue));

                    if (ap.Left is Identifier defId)
                    {
                        bodyExprs.Add(Expr.Block(typeof(void), [argVar],
                            Expr.Assign(argVar, argVal),
                            CallEnvMethod("CreateAndInitializeBinding",
                                Expr.Constant(defId.Name),
                                Expr.Constant(true),
                                finalVal)));
                    }
                    else
                    {
                        // Destructuring with default
                        bodyExprs.Add(Expr.Block(typeof(void), [argVar],
                            Expr.Assign(argVar, argVal),
                            CompileBindingPattern(ap.Left, finalVal, mutable: true)));
                    }
                }
                else if (param is RestElement rest)
                {
                    // Rest parameter: ...args
                    if (rest.Argument is Identifier restId)
                    {
                        bodyExprs.Add(CallEnvMethod("CreateAndInitializeBinding",
                            Expr.Constant(restId.Name),
                            Expr.Constant(true),
                            Expr.Call(
                                typeof(RuntimeHelpers).GetMethod(nameof(RuntimeHelpers.CreateRestArray))!,
                                fnArgsParam,
                                Expr.Constant(i),
                                Expr.Constant(_realm.ArrayPrototype, typeof(JsDynamicObject)))));
                    }
                }
                else if (param is ObjectPattern or ArrayPattern)
                {
                    // Destructuring parameter
                    var argVal = Expr.Condition(
                        Expr.LessThan(Expr.Constant(i), Expr.ArrayLength(fnArgsParam)),
                        Expr.ArrayIndex(fnArgsParam, Expr.Constant(i)),
                        Expr.Constant(JsValue.Undefined, typeof(JsValue)),
                        typeof(JsValue));
                    bodyExprs.Add(CompileBindingPattern(param, argVal, mutable: true));
                }
            }

            // Bind 'arguments' object (not for arrow functions)
            if (!isArrow)
            {
                bodyExprs.Add(CallEnvMethod("CreateAndInitializeBinding",
                    Expr.Constant("arguments"),
                    Expr.Constant(true),
                    Expr.Call(
                        typeof(RuntimeHelpers).GetMethod(nameof(RuntimeHelpers.CreateArgumentsObject))!,
                        fnArgsParam,
                        Expr.Constant(_realm.ArrayPrototype, typeof(JsDynamicObject)))));
            }

            // Compile function body
            if (isExpression)
            {
                // Arrow with expression body: () => expr
                bodyExprs.Add(Expr.Return(returnLabel, EnsureJsValue(CompileNode(body)), typeof(JsValue)));
            }
            else if (body is BlockStatement block)
            {
                // Hoist function declarations within function body
                HoistFunctions(block.Body, bodyExprs);

                foreach (var stmt in block.Body)
                {
                    if (stmt is FunctionDeclaration)
                    {
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
            }
            else
            {
                bodyExprs.Add(CompileNode(body));
            }

            // Default return value
            bodyExprs.Add(Expr.Label(returnLabel, Expr.Constant(JsValue.Undefined, typeof(JsValue))));

            var fnBody = Expr.Block(typeof(JsValue), bodyExprs);

            // Build the CallTarget lambda: (thisArg, args) => { create env from closure; body }
            // We need to capture the current environment at compile time for closure
            var closureEnvCapture = outerEnv;

            // The actual lambda wraps environment creation
            var fullBody = Expr.Block(typeof(JsValue), [fnEnv],
                Expr.Assign(fnEnv, Expr.New(
                    typeof(Environment).GetConstructor([typeof(Environment)])!,
                    closureEnvCapture)),
                fnBody);

            // Create the CallTarget based on function kind
            Expr callTarget;

            if (isGenerator || isAsync)
            {
                // Generator/async: create a 3-parameter inner lambda (this, args, coroutine)
                var innerLambda = Expr.Lambda<Func<JsValue, JsValue[], GeneratorCoroutine, JsValue>>(
                    fullBody, fnThisParam, fnArgsParam, coroutineParam!);

                // The outer CallTarget wraps the inner lambda
                var wrapperThis = Expr.Parameter(typeof(JsValue), "wrapThis");
                var wrapperArgs = Expr.Parameter(typeof(JsValue[]), "wrapArgs");

                Expr wrapperBody;
                if (isGenerator)
                {
                    wrapperBody = Expr.Call(
                        typeof(RuntimeHelpers).GetMethod(nameof(RuntimeHelpers.CreateGeneratorObject))!,
                        innerLambda,
                        wrapperThis,
                        wrapperArgs,
                        Expr.Constant(_realm.GeneratorPrototype, typeof(JsDynamicObject)));
                }
                else
                {
                    wrapperBody = Expr.Call(
                        typeof(RuntimeHelpers).GetMethod(nameof(RuntimeHelpers.RunAsyncFunction))!,
                        innerLambda,
                        wrapperThis,
                        wrapperArgs,
                        Expr.Constant(_realm.PromisePrototype, typeof(JsDynamicObject)),
                        Expr.Constant(_realm, typeof(Realm)));
                }

                callTarget = Expr.Lambda<Func<JsValue, JsValue[], JsValue>>(
                    wrapperBody, wrapperThis, wrapperArgs);
            }
            else
            {
                // Normal function: 2-parameter lambda
                callTarget = Expr.Lambda<Func<JsValue, JsValue[], JsValue>>(
                    fullBody, fnThisParam, fnArgsParam);
            }

            // Create JsFunction
            var fnVar = Expr.Parameter(typeof(JsFunction), "fn");
            var createFn = new List<Expr>
            {
                Expr.Assign(fnVar, Expr.New(typeof(JsFunction))),
                Expr.Assign(Expr.Property(fnVar, nameof(JsFunction.Name)), Expr.Constant(name)),
                Expr.Assign(Expr.Property(fnVar, nameof(JsFunction.CallTarget)), callTarget),
                Expr.Assign(Expr.Property(fnVar, nameof(JsFunction.ClosureScope)), closureEnvCapture),
                Expr.Assign(
                    Expr.Property(fnVar, nameof(JsDynamicObject.Prototype)),
                    Expr.Constant(_realm.FunctionPrototype, typeof(JsDynamicObject)))
            };

            if (!isArrow && !isGenerator && !isAsync)
            {
                // Non-arrow, non-generator, non-async functions get a prototype object for construction
                var protoObj = Expr.Parameter(typeof(JsDynamicObject), "fnProto");
                createFn.Add(Expr.Block(typeof(void), [protoObj],
                    Expr.Assign(protoObj, Expr.New(typeof(JsDynamicObject))),
                    Expr.Assign(
                        Expr.Property(protoObj, nameof(JsDynamicObject.Prototype)),
                        Expr.Constant(_realm.ObjectPrototype, typeof(JsDynamicObject))),
                    Expr.Assign(Expr.Property(fnVar, nameof(JsFunction.PrototypeObject)), protoObj)));
            }

            createFn.Add(Expr.Convert(fnVar, typeof(JsValue)));

            return Expr.Block(typeof(JsValue), [fnVar], createFn);
        }
        finally
        {
            _envParam = outerEnv;
            _thisParam = outerThis;
            _returnLabel = outerReturn;
            _breakLabel = outerBreak;
            _continueLabel = outerContinue;
            _coroutineParam = outerCoroutine;
            _labelTargets.Clear();
            foreach (var kvp in outerLabelTargets)
            {
                _labelTargets[kvp.Key] = kvp.Value;
            }

            _pendingLabelBreak = outerPendingBreak;
            _pendingLabelContinue = outerPendingContinue;
        }
    }

    // ───────────────────────── Generators / Async ─────────────────────────

    private Expr CompileYield(YieldExpression ye)
    {
        if (_coroutineParam is null)
        {
            throw new JsSyntaxError("yield is only valid in generator functions");
        }

        if (ye.Delegate)
        {
            // yield* iterable — delegate to another iterator
            var iterable = CompileNode(ye.Argument!);
            return Expr.Call(
                typeof(RuntimeHelpers).GetMethod(nameof(RuntimeHelpers.YieldStar))!,
                _coroutineParam,
                EnsureJsValue(iterable));
        }

        var value = ye.Argument is not null
            ? CompileNode(ye.Argument)
            : Expr.Constant(JsValue.Undefined, typeof(JsValue));

        return Expr.Call(
            typeof(RuntimeHelpers).GetMethod(nameof(RuntimeHelpers.GeneratorYield))!,
            _coroutineParam,
            EnsureJsValue(value));
    }

    private Expr CompileAwait(AwaitExpression ae)
    {
        if (_coroutineParam is null)
        {
            throw new JsSyntaxError("await is only valid in async functions");
        }

        var value = CompileNode(ae.Argument);

        return Expr.Call(
            typeof(RuntimeHelpers).GetMethod(nameof(RuntimeHelpers.GeneratorYield))!,
            _coroutineParam,
            EnsureJsValue(value));
    }

}
