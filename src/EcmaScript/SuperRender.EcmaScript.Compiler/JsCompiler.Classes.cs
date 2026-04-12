// CA1859: These private Compile* methods intentionally return the abstract Expr type
// for a uniform internal API — the caller (CompileNode) dispatches via pattern matching
// and always works with Expr, so narrowing return types would add no value.
// CA1822: Private Compile* methods are part of the compiler instance API even when a
// particular method does not currently access instance state (e.g. stubs for hoisted decls).
#pragma warning disable CA1859, CA1822

using System.Globalization;
using SuperRender.EcmaScript.Compiler.Ast;
using SuperRender.EcmaScript.Runtime;
using Expr = System.Linq.Expressions.Expression;

namespace SuperRender.EcmaScript.Compiler;

public sealed partial class JsCompiler
{
    // ───────────────────────── Classes ─────────────────────────

    private Expr CompileClassDecl(ClassDeclaration node)
    {
        var classExpr = CompileClassBody(node.Id?.Name, node.SuperClass, node.Body);

        if (node.Id is not null)
        {
            var resultVar = Expr.Parameter(typeof(JsValue), "classVal");
            return Expr.Block(typeof(JsValue), [resultVar],
                Expr.Assign(resultVar, classExpr),
                CallEnvMethod("CreateAndInitializeBinding",
                    Expr.Constant(node.Id.Name),
                    Expr.Constant(false),
                    resultVar),
                resultVar);
        }

        return classExpr;
    }

    private Expr CompileClassExpr(ClassExpression node)
    {
        return CompileClassBody(node.Id?.Name, node.SuperClass, node.Body);
    }

    private Expr CompileClassBody(string? className, SyntaxNode? superClass, ClassBody body)
    {
        var ctorVar = Expr.Parameter(typeof(JsFunction), "classCtor");
        var protoVar = Expr.Parameter(typeof(JsObject), "classProto");
        var exprs = new List<Expr>();

        // Find constructor method
        MethodDefinition? ctorDef = null;
        foreach (var member in body.Body)
        {
            if (member is MethodDefinition md && md.Kind == MethodKind.Constructor)
            {
                ctorDef = md;
                break;
            }
        }

        // Compile constructor function
        if (ctorDef is not null)
        {
            var ctorFn = CompileFunctionBody(
                name: className ?? "",
                parameters: ctorDef.Value.Params,
                body: ctorDef.Value.Body,
                isExpression: false,
                isArrow: false);

            exprs.Add(Expr.Assign(ctorVar, Expr.Convert(ctorFn, typeof(JsFunction))));
            // Mark as constructor
            exprs.Add(Expr.Assign(
                Expr.Property(ctorVar, nameof(JsFunction.IsConstructor)),
                Expr.Constant(true)));
        }
        else
        {
            // Default constructor — for derived classes, calls super(...args)
            if (superClass is not null)
            {
                var superVal = CompileNode(superClass);
                exprs.Add(Expr.Assign(ctorVar, Expr.Convert(
                    Expr.Call(
                        typeof(RuntimeHelpers).GetMethod(nameof(RuntimeHelpers.CreateDerivedDefaultConstructor))!,
                        Expr.Constant(className ?? ""),
                        EnsureJsValue(superVal)),
                    typeof(JsFunction))));
            }
            else
            {
                exprs.Add(Expr.Assign(ctorVar, Expr.Convert(
                    Expr.Call(
                        typeof(RuntimeHelpers).GetMethod(nameof(RuntimeHelpers.CreateDefaultConstructor))!,
                        Expr.Constant(className ?? "")),
                    typeof(JsFunction))));
            }
        }

        // Create prototype
        exprs.Add(Expr.Assign(protoVar, Expr.New(typeof(JsObject))));

        // Set up inheritance
        if (superClass is not null)
        {
            var superVal = CompileNode(superClass);
            exprs.Add(Expr.Call(
                typeof(RuntimeHelpers).GetMethod(nameof(RuntimeHelpers.SetupInheritance))!,
                ctorVar,
                protoVar,
                EnsureJsValue(superVal)));
        }
        else
        {
            exprs.Add(Expr.Assign(
                Expr.Property(protoVar, nameof(JsObject.Prototype)),
                Expr.Constant(_realm.ObjectPrototype, typeof(JsObject))));
        }

        // Link constructor and prototype
        exprs.Add(Expr.Assign(
            Expr.Property(ctorVar, nameof(JsFunction.PrototypeObject)),
            protoVar));

        exprs.Add(Expr.Call(
            Expr.Convert(protoVar, typeof(JsObject)),
            typeof(JsObject).GetMethod(nameof(JsObject.Set), [typeof(string), typeof(JsValue)])!,
            Expr.Constant("constructor"),
            Expr.Convert(ctorVar, typeof(JsValue))));

        // Compile methods and properties
        foreach (var member in body.Body)
        {
            if (member is MethodDefinition md && md.Kind != MethodKind.Constructor)
            {
                var target = md.IsStatic ? (Expr)ctorVar : protoVar;
                var fnExpr = CompileFunctionBody(
                    name: md.Key is Identifier mid ? mid.Name : "",
                    parameters: md.Value.Params,
                    body: md.Value.Body,
                    isExpression: false,
                    isArrow: false);

                Expr keyExpr;
                if (md.Computed)
                {
                    keyExpr = Expr.Call(
                        EnsureJsValue(CompileNode(md.Key)),
                        typeof(JsValue).GetMethod(nameof(JsValue.ToJsString))!);
                }
                else if (md.Key is Identifier mkid)
                {
                    keyExpr = Expr.Constant(mkid.Name);
                }
                else if (md.Key is Literal mlit)
                {
                    keyExpr = Expr.Constant(Convert.ToString(mlit.Value, CultureInfo.InvariantCulture) ?? "");
                }
                else
                {
                    continue;
                }

                if (md.Kind == MethodKind.Get)
                {
                    exprs.Add(Expr.Call(
                        typeof(RuntimeHelpers).GetMethod(nameof(RuntimeHelpers.DefineGetter))!,
                        Expr.Convert(target, typeof(JsObject)),
                        keyExpr,
                        EnsureJsValue(fnExpr)));
                }
                else if (md.Kind == MethodKind.Set)
                {
                    exprs.Add(Expr.Call(
                        typeof(RuntimeHelpers).GetMethod(nameof(RuntimeHelpers.DefineSetter))!,
                        Expr.Convert(target, typeof(JsObject)),
                        keyExpr,
                        EnsureJsValue(fnExpr)));
                }
                else
                {
                    exprs.Add(Expr.Call(
                        Expr.Convert(target, typeof(JsObject)),
                        typeof(JsObject).GetMethod(nameof(JsObject.Set), [typeof(string), typeof(JsValue)])!,
                        keyExpr,
                        EnsureJsValue(fnExpr)));
                }
            }
            else if (member is PropertyDefinition pd)
            {
                // Static fields go on constructor, instance fields would need constructor initialization
                if (pd.IsStatic && pd.Value is not null)
                {
                    Expr keyExpr;
                    if (pd.Computed)
                    {
                        keyExpr = Expr.Call(
                            EnsureJsValue(CompileNode(pd.Key)),
                            typeof(JsValue).GetMethod(nameof(JsValue.ToJsString))!);
                    }
                    else if (pd.Key is Identifier pkid)
                    {
                        keyExpr = Expr.Constant(pkid.Name);
                    }
                    else
                    {
                        continue;
                    }

                    var val = CompileNode(pd.Value);
                    exprs.Add(Expr.Call(
                        Expr.Convert(ctorVar, typeof(JsObject)),
                        typeof(JsObject).GetMethod(nameof(JsObject.Set), [typeof(string), typeof(JsValue)])!,
                        keyExpr,
                        EnsureJsValue(val)));
                }
            }
        }

        exprs.Add(Expr.Convert(ctorVar, typeof(JsValue)));
        return Expr.Block(typeof(JsValue), [ctorVar, protoVar], exprs);
    }

}
