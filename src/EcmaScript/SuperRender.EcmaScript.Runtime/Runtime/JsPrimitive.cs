namespace SuperRender.EcmaScript.Runtime;

using System.Globalization;

public sealed class JsUndefined : JsValue
{
    public static readonly JsUndefined Instance = new();
    private JsUndefined() { }

    public override string TypeOf => "undefined";
    public override bool ToBoolean() => false;
    public override double ToNumber() => double.NaN;
    public override string ToJsString() => "undefined";
    public override string ToString() => "undefined";
}

public sealed class JsNull : JsValue
{
    public static readonly JsNull Instance = new();
    private JsNull() { }

    public override string TypeOf => "object";
    public override bool ToBoolean() => false;
    public override double ToNumber() => 0;
    public override string ToJsString() => "null";
    public override string ToString() => "null";
}

public sealed class JsBoolean : JsValue
{
    public static new readonly JsBoolean True = new(true);
    public static new readonly JsBoolean False = new(false);

    public bool Value { get; }

    private JsBoolean(bool value) => Value = value;

    public override string TypeOf => "boolean";
    public override bool ToBoolean() => Value;
    public override double ToNumber() => Value ? 1 : 0;
    public override string ToJsString() => Value ? "true" : "false";
    public override string ToString() => Value ? "true" : "false";

    public override bool StrictEquals(JsValue other) =>
        other is JsBoolean b && Value == b.Value;
}

public sealed class JsNumber : JsValue
{
    public static readonly JsNumber NaN = new(double.NaN);
    public static readonly JsNumber PositiveInfinity = new(double.PositiveInfinity);
    public static readonly JsNumber NegativeInfinity = new(double.NegativeInfinity);
    public static readonly JsNumber Zero = new(0);
    public static readonly JsNumber NegativeZero = new(NegativeZeroValue);

    private const double NegativeZeroValue = -0.0;

    public double Value { get; }

    private JsNumber(double value) => Value = value;

    public static JsNumber Create(double value)
    {
        if (double.IsNaN(value)) return NaN;
        if (double.IsPositiveInfinity(value)) return PositiveInfinity;
        if (double.IsNegativeInfinity(value)) return NegativeInfinity;
        if (value == 0)
        {
            return double.IsNegative(value) ? NegativeZero : Zero;
        }

        return new JsNumber(value);
    }

    public override string TypeOf => "number";

    public override bool ToBoolean() =>
        Value != 0 && !double.IsNaN(Value);

    public override double ToNumber() => Value;

    public override string ToJsString()
    {
        if (double.IsNaN(Value)) return "NaN";
        if (double.IsPositiveInfinity(Value)) return "Infinity";
        if (double.IsNegativeInfinity(Value)) return "-Infinity";
        if (Value == 0) return "0"; // both +0 and -0 produce "0"

        // For integers, no decimal point
        // ReSharper disable once CompareOfFloatsByEqualityOperator
        if (Value == Math.Truncate(Value) && Math.Abs(Value) < 1e15)
        {
            return Value.ToString("0", CultureInfo.InvariantCulture);
        }

        return Value.ToString("R", CultureInfo.InvariantCulture);
    }

    public override string ToString() => ToJsString();

    public override bool StrictEquals(JsValue other)
    {
        if (other is not JsNumber n) return false;
        // NaN !== NaN
        if (double.IsNaN(Value) || double.IsNaN(n.Value)) return false;
        // +0 === -0
        return Value == n.Value;
    }
}

public sealed class JsString : JsValue
{
    public static readonly JsString Empty = new(string.Empty);

    public string Value { get; }
    public int Length => Value.Length;

    public JsString(string value) => Value = value;

    public JsValue this[int index]
    {
        get
        {
            if (index < 0 || index >= Value.Length) return Undefined;
            return new JsString(Value[index].ToString());
        }
    }

    public override string TypeOf => "string";
    public override bool ToBoolean() => Value.Length > 0;

    public override double ToNumber()
    {
        var trimmed = Value.Trim();
        if (trimmed.Length == 0) return 0;
        if (double.TryParse(trimmed, NumberStyles.Float | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var result))
        {
            return result;
        }

        return double.NaN;
    }

    public override string ToJsString() => Value;
    public override string ToString() => Value;

    public override bool StrictEquals(JsValue other) =>
        other is JsString s && string.Equals(Value, s.Value, StringComparison.Ordinal);
}

public sealed class JsSymbol : JsValue
{
    private static int _nextId;

    public static readonly JsSymbol Iterator = new("Symbol.iterator");
    public static readonly JsSymbol AsyncIterator = new("Symbol.asyncIterator");
    public static readonly JsSymbol ToPrimitiveSymbol = new("Symbol.toPrimitive");
    public static readonly JsSymbol HasInstance = new("Symbol.hasInstance");
    public static readonly JsSymbol ToStringTag = new("Symbol.toStringTag");
    public static readonly JsSymbol Species = new("Symbol.species");

    private readonly int _id;
    public string? Description { get; }

    public JsSymbol(string? description = null)
    {
        Description = description;
        _id = Interlocked.Increment(ref _nextId);
    }

    public override string TypeOf => "symbol";
    public override bool ToBoolean() => true;

    public override double ToNumber() =>
        throw new Errors.JsTypeError("Cannot convert a Symbol value to a number", ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);

    public override string ToJsString() =>
        throw new Errors.JsTypeError("Cannot convert a Symbol value to a string", ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);

    public override string ToString() =>
        Description is not null ? $"Symbol({Description})" : "Symbol()";

    public override bool StrictEquals(JsValue other) =>
        other is JsSymbol sym && _id == sym._id;

    public override int GetHashCode() => _id;

    public override bool Equals(object? obj) =>
        obj is JsSymbol sym && _id == sym._id;
}
