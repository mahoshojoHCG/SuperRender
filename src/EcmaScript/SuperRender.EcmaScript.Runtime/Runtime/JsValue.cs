namespace SuperRender.EcmaScript.Runtime;

using System.Dynamic;
using System.Linq.Expressions;
using System.Reflection;

public abstract class JsValue : IDynamicMetaObjectProvider
{
    public static readonly JsValue Undefined = JsUndefined.Instance;
    public static readonly JsValue Null = JsNull.Instance;
    public static readonly JsValue True = JsBoolean.True;
    public static readonly JsValue False = JsBoolean.False;

    public abstract string TypeOf { get; }
    public abstract bool ToBoolean();
    public abstract double ToNumber();
    public abstract string ToJsString();
    public virtual JsValue ToPrimitive(string? preferredType = null) => this;

    public virtual bool StrictEquals(JsValue other) => ReferenceEquals(this, other);

    public virtual bool AbstractEquals(JsValue other)
    {
        // Same type -> strict equals
        if (GetType() == other.GetType())
        {
            return StrictEquals(other);
        }

        // null == undefined
        if ((this is JsNull && other is JsUndefined) || (this is JsUndefined && other is JsNull))
        {
            return true;
        }

        // number == string -> compare as numbers
        if (this is JsNumber && other is JsString)
        {
            return StrictEquals(JsNumber.Create(other.ToNumber()));
        }

        if (this is JsString && other is JsNumber)
        {
            return JsNumber.Create(ToNumber()).StrictEquals(other);
        }

        // boolean == x -> ToNumber(bool) == x
        if (this is JsBoolean)
        {
            return JsNumber.Create(ToNumber()).AbstractEquals(other);
        }

        if (other is JsBoolean)
        {
            return AbstractEquals(JsNumber.Create(other.ToNumber()));
        }

        // object == primitive -> ToPrimitive
        if (this is JsObject && other is not JsObject)
        {
            return ToPrimitive().AbstractEquals(other);
        }

        if (this is not JsObject && other is JsObject)
        {
            return AbstractEquals(other.ToPrimitive());
        }

        return false;
    }

    public DynamicMetaObject GetMetaObject(Expression parameter) =>
        new JsValueMetaObject(parameter, this);

    public static JsValue FromObject(object? value) => value switch
    {
        null => Null,
        bool b => b ? True : False,
        int i => JsNumber.Create(i),
        long l => JsNumber.Create(l),
        float f => JsNumber.Create(f),
        double d => JsNumber.Create(d),
        string s => new JsString(s),
        JsValue js => js,
        _ => throw new Errors.JsTypeError($"Cannot convert {value.GetType().Name} to JsValue")
    };
}

internal sealed class JsValueMetaObject : DynamicMetaObject
{
    private static readonly MethodInfo _getMethod =
        typeof(JsObject).GetMethod(nameof(JsObject.Get), [typeof(string)])!;

    private static readonly MethodInfo _setMethod =
        typeof(JsObject).GetMethod(nameof(JsObject.Set), [typeof(string), typeof(JsValue)])!;

    private static readonly MethodInfo _callMethod =
        typeof(JsFunction).GetMethod(nameof(JsFunction.Call), [typeof(JsValue), typeof(JsValue[])])!;

    internal JsValueMetaObject(Expression expression, JsValue value)
        : base(expression, BindingRestrictions.Empty, value) { }

    public override DynamicMetaObject BindGetMember(GetMemberBinder binder)
    {
        var restrictions = BindingRestrictions.GetTypeRestriction(Expression, LimitType);

        var isObj = Expression.TypeIs(Expression, typeof(JsObject));
        var getSelf = Expression.Convert(Expression, typeof(JsObject));
        var call = Expression.Call(getSelf, _getMethod, Expression.Constant(binder.Name));
        var undefinedExpr = Expression.Constant(JsValue.Undefined, typeof(JsValue));
        var result = Expression.Condition(isObj, call, undefinedExpr);

        return new DynamicMetaObject(result, restrictions);
    }

    public override DynamicMetaObject BindSetMember(SetMemberBinder binder, DynamicMetaObject value)
    {
        var restrictions = BindingRestrictions.GetTypeRestriction(Expression, LimitType);

        var getSelf = Expression.Convert(Expression, typeof(JsObject));
        var val = Expression.Convert(value.Expression, typeof(JsValue));
        var call = Expression.Call(getSelf, _setMethod, Expression.Constant(binder.Name), val);
        var result = Expression.Block(typeof(JsValue), call, val);

        return new DynamicMetaObject(result, restrictions);
    }

    public override DynamicMetaObject BindInvoke(InvokeBinder binder, DynamicMetaObject[] args)
    {
        var restrictions = BindingRestrictions.GetTypeRestriction(Expression, LimitType);

        var self = Expression.Convert(Expression, typeof(JsFunction));
        var argsArray = Expression.NewArrayInit(
            typeof(JsValue),
            args.Select(a => Expression.Convert(a.Expression, typeof(JsValue))));
        var thisArg = Expression.Constant(JsValue.Undefined, typeof(JsValue));
        var call = Expression.Call(self, _callMethod, thisArg, argsArray);

        return new DynamicMetaObject(call, restrictions);
    }
}
