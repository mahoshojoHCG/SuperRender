using System.Dynamic;
using System.Linq.Expressions;
using SuperRender.EcmaScript.Runtime;
using Expr = System.Linq.Expressions.Expression;

namespace SuperRender.EcmaScript.Compiler;

/// <summary>
/// DLR binder for JavaScript binary operations. Delegates to
/// <see cref="RuntimeHelpers"/> static methods for correct JS coercion semantics.
/// </summary>
public sealed class JsBinaryOperationBinder : BinaryOperationBinder
{
    public JsBinaryOperationBinder(ExpressionType operation) : base(operation) { }

    public override DynamicMetaObject FallbackBinaryOperation(DynamicMetaObject target, DynamicMetaObject arg, DynamicMetaObject? errorSuggestion)
    {
        if (!target.HasValue || !arg.HasValue)
        {
            return Defer(target, arg);
        }

        var restrictions = BindingRestrictions.GetTypeRestriction(target.Expression, target.LimitType)
            .Merge(BindingRestrictions.GetTypeRestriction(arg.Expression, arg.LimitType));

        var left = Expr.Convert(target.Expression, typeof(JsValue));
        var right = Expr.Convert(arg.Expression, typeof(JsValue));

        string? methodName = Operation switch
        {
            ExpressionType.Add => nameof(RuntimeHelpers.Add),
            ExpressionType.Subtract => nameof(RuntimeHelpers.Sub),
            ExpressionType.Multiply => nameof(RuntimeHelpers.Mul),
            ExpressionType.Divide => nameof(RuntimeHelpers.Div),
            ExpressionType.Modulo => nameof(RuntimeHelpers.Mod),
            ExpressionType.Power => nameof(RuntimeHelpers.Power),
            ExpressionType.Equal => nameof(RuntimeHelpers.StrictEqual),
            ExpressionType.NotEqual => nameof(RuntimeHelpers.StrictNotEqual),
            ExpressionType.LessThan => nameof(RuntimeHelpers.LessThan),
            ExpressionType.GreaterThan => nameof(RuntimeHelpers.GreaterThan),
            ExpressionType.LessThanOrEqual => nameof(RuntimeHelpers.LessThanOrEqual),
            ExpressionType.GreaterThanOrEqual => nameof(RuntimeHelpers.GreaterThanOrEqual),
            ExpressionType.And => nameof(RuntimeHelpers.BitwiseAnd),
            ExpressionType.Or => nameof(RuntimeHelpers.BitwiseOr),
            ExpressionType.ExclusiveOr => nameof(RuntimeHelpers.BitwiseXor),
            ExpressionType.LeftShift => nameof(RuntimeHelpers.LeftShift),
            ExpressionType.RightShift => nameof(RuntimeHelpers.RightShift),
            _ => null
        };

        if (methodName is null)
        {
            var throwExpr = Expr.Throw(
                Expr.New(
                    typeof(SuperRender.EcmaScript.Runtime.Errors.JsTypeError).GetConstructor([typeof(string)])!,
                    Expr.Constant($"Unsupported binary operation: {Operation}")),
                typeof(object));
            return errorSuggestion ?? new DynamicMetaObject(throwExpr, restrictions);
        }

        var method = typeof(RuntimeHelpers).GetMethod(methodName, [typeof(JsValue), typeof(JsValue)])!;
        var call = Expr.Call(method, left, right);

        return errorSuggestion ?? new DynamicMetaObject(
            Expr.Convert(call, typeof(object)),
            restrictions);
    }
}
