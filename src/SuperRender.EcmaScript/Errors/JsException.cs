namespace SuperRender.EcmaScript.Errors;

public class JsErrorBase : Exception
{
    public int Line { get; }
    public int Column { get; }
    public string? SourceContext { get; init; }

    public JsErrorBase(string message, int line = 0, int column = 0)
        : base(message)
    {
        Line = line;
        Column = column;
    }

    public JsErrorBase(string message, Exception innerException, int line = 0, int column = 0)
        : base(message, innerException)
    {
        Line = line;
        Column = column;
    }
}

public sealed class JsSyntaxError : JsErrorBase
{
    public JsSyntaxError(string message, int line = 0, int column = 0)
        : base(message, line, column) { }
}

public sealed class JsReferenceError : JsErrorBase
{
    public JsReferenceError(string message, int line = 0, int column = 0)
        : base(message, line, column) { }
}

public sealed class JsTypeError : JsErrorBase
{
    public JsTypeError(string message, int line = 0, int column = 0)
        : base(message, line, column) { }
}

public sealed class JsRangeError : JsErrorBase
{
    public JsRangeError(string message, int line = 0, int column = 0)
        : base(message, line, column) { }
}

public sealed class JsUriError : JsErrorBase
{
    public JsUriError(string message, int line = 0, int column = 0)
        : base(message, line, column) { }
}

public sealed class JsEvalError : JsErrorBase
{
    public JsEvalError(string message, int line = 0, int column = 0)
        : base(message, line, column) { }
}
