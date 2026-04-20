// CA1859: These private Compile* methods intentionally return the abstract Expr type
// for a uniform internal API — the caller (CompileNode) dispatches via pattern matching
// and always works with Expr, so narrowing return types would add no value.
// CA1822: Private Compile* methods are part of the compiler instance API even when a
// particular method does not currently access instance state (e.g. stubs for hoisted decls).
#pragma warning disable CA1859, CA1822

using System.Globalization;
using SuperRender.EcmaScript.Compiler.Ast;
using SuperRender.EcmaScript.Runtime.Errors;
using SuperRender.EcmaScript.Runtime;
using Environment = SuperRender.EcmaScript.Runtime.Environment;
using Expr = System.Linq.Expressions.Expression;

namespace SuperRender.EcmaScript.Compiler;

public sealed partial class JsCompiler
{
    // ───────────────────────── Destructuring ─────────────────────────

    private Expr CompileBindingPattern(SyntaxNode pattern, Expr value, bool mutable)
    {
        if (pattern is Identifier id)
        {
            return CallEnvMethod("CreateAndInitializeBinding",
                Expr.Constant(id.Name),
                Expr.Constant(mutable),
                EnsureJsValue(value));
        }

        if (pattern is ObjectPattern objPat)
        {
            var valVar = Expr.Parameter(typeof(JsValue), "objPatVal");
            var exprs = new List<Expr> { Expr.Assign(valVar, EnsureJsValue(value)) };

            foreach (var prop in objPat.Properties)
            {
                if (prop is RestElement rest)
                {
                    // Rest element in object pattern: let { a, ...rest } = obj
                    // Collect remaining properties
                    var usedKeys = new List<string>();
                    foreach (var p in objPat.Properties)
                    {
                        if (p is Property pp && pp.Key is Identifier ppId)
                        {
                            usedKeys.Add(ppId.Name);
                        }
                    }

                    exprs.Add(CompileBindingPattern(rest.Argument,
                        Expr.Call(
                            typeof(RuntimeHelpers).GetMethod(nameof(RuntimeHelpers.ObjectRestProperties))!,
                            valVar,
                            Expr.NewArrayInit(typeof(string),
                                usedKeys.Select(k => (Expr)Expr.Constant(k)))),
                        mutable));
                    continue;
                }

                if (prop is not Property p2)
                {
                    continue;
                }

                string propName;
                if (p2.Key is Identifier keyId)
                {
                    propName = keyId.Name;
                }
                else if (p2.Key is Literal keyLit)
                {
                    propName = Convert.ToString(keyLit.Value, CultureInfo.InvariantCulture) ?? "";
                }
                else
                {
                    continue;
                }

                var memberVal = Expr.Call(
                    typeof(RuntimeHelpers).GetMethod(nameof(RuntimeHelpers.GetMember))!,
                    valVar,
                    Expr.Constant(propName));

                if (p2.Value is AssignmentPattern ap)
                {
                    // Default value: { x = 5 } = obj
                    var tempVar = Expr.Parameter(typeof(JsValue), "defaultTemp");
                    var defaultVal = CompileNode(ap.Right);

                    exprs.Add(Expr.Block(typeof(void), [tempVar],
                        Expr.Assign(tempVar, memberVal),
                        CompileBindingPattern(ap.Left,
                            Expr.Condition(
                                Expr.TypeIs(tempVar, typeof(JsUndefined)),
                                EnsureJsValue(defaultVal),
                                tempVar,
                                typeof(JsValue)),
                            mutable)));
                }
                else
                {
                    exprs.Add(CompileBindingPattern(p2.Value, memberVal, mutable));
                }
            }

            exprs.Add(valVar);
            return Expr.Block(typeof(JsValue), [valVar], exprs);
        }

        if (pattern is ArrayPattern arrPat)
        {
            var valVar = Expr.Parameter(typeof(JsValue), "arrPatVal");
            var exprs = new List<Expr> { Expr.Assign(valVar, EnsureJsValue(value)) };

            for (int i = 0; i < arrPat.Elements.Count; i++)
            {
                var element = arrPat.Elements[i];
                if (element is null)
                {
                    continue;
                }

                if (element is RestElement rest)
                {
                    // Rest element: let [a, ...rest] = arr
                    exprs.Add(CompileBindingPattern(rest.Argument,
                        Expr.Call(
                            typeof(RuntimeHelpers).GetMethod(nameof(RuntimeHelpers.ArraySliceFrom))!,
                            valVar,
                            Expr.Constant(i),
                            Expr.Constant(_realm.ArrayPrototype, typeof(JsDynamicObject))),
                        mutable));
                    continue;
                }

                var elementVal = Expr.Call(
                    typeof(RuntimeHelpers).GetMethod(nameof(RuntimeHelpers.GetComputedMember))!,
                    valVar,
                    Expr.Convert(
                        Expr.Call(typeof(JsNumber).GetMethod(nameof(JsNumber.Create), [typeof(double)])!,
                            Expr.Constant((double)i)),
                        typeof(JsValue)));

                if (element is AssignmentPattern ap)
                {
                    var tempVar = Expr.Parameter(typeof(JsValue), "defaultTemp");
                    var defaultVal = CompileNode(ap.Right);

                    exprs.Add(Expr.Block(typeof(void), [tempVar],
                        Expr.Assign(tempVar, elementVal),
                        CompileBindingPattern(ap.Left,
                            Expr.Condition(
                                Expr.TypeIs(tempVar, typeof(JsUndefined)),
                                EnsureJsValue(defaultVal),
                                tempVar,
                                typeof(JsValue)),
                            mutable)));
                }
                else
                {
                    exprs.Add(CompileBindingPattern(element, elementVal, mutable));
                }
            }

            exprs.Add(valVar);
            return Expr.Block(typeof(JsValue), [valVar], exprs);
        }

        throw new JsSyntaxError($"Unsupported binding pattern: {pattern.GetType().Name}");
    }

    // ───────────────────────── Hoisting ─────────────────────────

    private void HoistFunctions(IReadOnlyList<SyntaxNode> body, List<Expr> exprs)
    {
        foreach (var stmt in body)
        {
            if (stmt is FunctionDeclaration fd && fd.Id is not null)
            {
                var fnExpr = CompileFunctionBody(
                    name: fd.Id.Name,
                    parameters: fd.Params,
                    body: fd.Body,
                    isExpression: false,
                    isArrow: false,
                    isGenerator: fd.IsGenerator,
                    isAsync: fd.IsAsync);

                exprs.Add(CallEnvMethod("CreateAndInitializeBinding",
                    Expr.Constant(fd.Id.Name),
                    Expr.Constant(true),
                    EnsureJsValue(fnExpr)));
            }
        }
    }

    // ───────────────────────── Helpers ─────────────────────────

    private Expr CallEnvMethod(string methodName, params Expr[] args)
    {
        var method = typeof(Environment).GetMethod(methodName,
            args.Select(a => a.Type).ToArray());
        if (method is null)
        {
            // Try with the exact parameter types we need
            method = methodName switch
            {
                "GetBinding" => typeof(Environment).GetMethod(methodName, [typeof(string)]),
                "SetBinding" => typeof(Environment).GetMethod(methodName, [typeof(string), typeof(JsValue)]),
                "CreateAndInitializeBinding" => typeof(Environment).GetMethod(methodName, [typeof(string), typeof(bool), typeof(JsValue)]),
                "HasBinding" => typeof(Environment).GetMethod(methodName, [typeof(string)]),
                _ => throw new InvalidOperationException($"Method {methodName} not found on Environment")
            };
        }

        return Expr.Call(_envParam, method!, args);
    }

    private Expr CompileCallArguments(IReadOnlyList<SyntaxNode> arguments)
    {
        // Check if any argument is a spread element
        bool hasSpread = arguments.Any(a => a is SpreadElement);

        if (!hasSpread)
        {
            // Simple case: compile each argument directly into an array
            var argExprs = arguments.Select(a => EnsureJsValue(CompileNode(a))).ToArray();
            return Expr.NewArrayInit(typeof(JsValue), argExprs);
        }

        // Complex case: need to handle spread elements
        var listVar = Expr.Parameter(typeof(List<JsValue>), "argList");
        var exprs = new List<Expr>
        {
            Expr.Assign(listVar, Expr.New(typeof(List<JsValue>)))
        };

        foreach (var arg in arguments)
        {
            if (arg is SpreadElement spread)
            {
                exprs.Add(Expr.Call(
                    typeof(RuntimeHelpers).GetMethod(nameof(RuntimeHelpers.SpreadIntoList))!,
                    listVar,
                    EnsureJsValue(CompileNode(spread.Argument))));
            }
            else
            {
                exprs.Add(Expr.Call(
                    listVar,
                    typeof(List<JsValue>).GetMethod(nameof(List<JsValue>.Add))!,
                    EnsureJsValue(CompileNode(arg))));
            }
        }

        exprs.Add(Expr.Call(listVar,
            typeof(List<JsValue>).GetMethod(nameof(List<JsValue>.ToArray))!));

        return Expr.Block(typeof(JsValue[]), [listVar], exprs);
    }

    private static Expr CompileToBool(Expr value)
    {
        if (value.Type == typeof(bool))
        {
            return value;
        }

        return Expr.Call(
            EnsureJsValue(value),
            typeof(JsValue).GetMethod(nameof(JsValue.ToBoolean))!);
    }

    private static Expr EnsureJsValue(Expr expr)
    {
        if (expr.Type == typeof(JsValue))
        {
            return expr;
        }

        if (typeof(JsValue).IsAssignableFrom(expr.Type))
        {
            return Expr.Convert(expr, typeof(JsValue));
        }

        // Void expressions (e.g. Break, Continue, Goto) represent non-local control
        // flow that never falls through. Wrap in a block with a dead-code Undefined
        // so the overall expression is typed as JsValue for use in Condition nodes.
        if (expr.Type == typeof(void))
        {
            return Expr.Block(typeof(JsValue), expr,
                Expr.Constant(JsValue.Undefined, typeof(JsValue)));
        }

        return expr;
    }

    private static Expr ToVoid(Expr expr)
    {
        if (expr.Type == typeof(void))
        {
            return expr;
        }

        return Expr.Block(typeof(void), expr);
    }

    private static string GetCompoundAssignmentHelper(string op) => op switch
    {
        "+=" => nameof(RuntimeHelpers.Add),
        "-=" => nameof(RuntimeHelpers.Sub),
        "*=" => nameof(RuntimeHelpers.Mul),
        "/=" => nameof(RuntimeHelpers.Div),
        "%=" => nameof(RuntimeHelpers.Mod),
        "**=" => nameof(RuntimeHelpers.Power),
        "&=" => nameof(RuntimeHelpers.BitwiseAnd),
        "|=" => nameof(RuntimeHelpers.BitwiseOr),
        "^=" => nameof(RuntimeHelpers.BitwiseXor),
        "<<=" => nameof(RuntimeHelpers.LeftShift),
        ">>=" => nameof(RuntimeHelpers.RightShift),
        ">>>=" => nameof(RuntimeHelpers.UnsignedRightShift),
        _ => throw new JsSyntaxError($"Unknown compound assignment operator: {op}")
    };
}
