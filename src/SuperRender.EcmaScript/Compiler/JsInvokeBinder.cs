using System.Dynamic;
using SuperRender.EcmaScript.Runtime;
using Expr = System.Linq.Expressions.Expression;

namespace SuperRender.EcmaScript.Compiler;

/// <summary>
/// DLR binder for function invocation on JS values.
/// Calls <see cref="JsFunction.Call(JsValue, JsValue[])"/> with <c>undefined</c> as the this-arg.
/// </summary>
public sealed class JsInvokeBinder : InvokeBinder
{
    public JsInvokeBinder(CallInfo callInfo) : base(callInfo) { }

    public override DynamicMetaObject FallbackInvoke(DynamicMetaObject target, DynamicMetaObject[] args, DynamicMetaObject? errorSuggestion)
    {
        if (!target.HasValue)
        {
            return Defer(target);
        }

        var restrictions = BindingRestrictions.GetTypeRestriction(target.Expression, target.LimitType);

        if (!typeof(JsFunction).IsAssignableFrom(target.LimitType))
        {
            // Not a function — throw TypeError
            var throwExpr = Expr.Throw(
                Expr.New(
                    typeof(Errors.JsTypeError).GetConstructor([typeof(string)])!,
                    Expr.Constant("Value is not a function")),
                typeof(object));
            return errorSuggestion ?? new DynamicMetaObject(throwExpr, restrictions);
        }

        var self = Expr.Convert(target.Expression, typeof(JsFunction));
        var argsArray = Expr.NewArrayInit(
            typeof(JsValue),
            args.Select(a => Expr.Convert(a.Expression, typeof(JsValue))));
        var thisArg = Expr.Constant(JsValue.Undefined, typeof(JsValue));

        var call = Expr.Call(
            self,
            typeof(JsFunction).GetMethod(nameof(JsFunction.Call), [typeof(JsValue), typeof(JsValue[])])!,
            thisArg,
            argsArray);

        return errorSuggestion ?? new DynamicMetaObject(
            Expr.Convert(call, typeof(object)),
            restrictions);
    }
}
