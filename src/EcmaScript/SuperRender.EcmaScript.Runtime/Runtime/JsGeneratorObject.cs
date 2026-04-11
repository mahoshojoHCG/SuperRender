namespace SuperRender.EcmaScript.Runtime;

/// <summary>
/// The JS-visible generator object returned when calling a generator function.
/// Methods (next/return/throw) are installed on the <see cref="Realm.GeneratorPrototype"/>.
/// The generator body is deferred — it does not execute until the first next() call,
/// matching the ECMAScript specification.
/// </summary>
public sealed class JsGeneratorObject : JsObject
{
    private readonly GeneratorCoroutine _coroutine;
    private readonly Action? _startAction;
    private bool _started;

    public JsGeneratorObject(GeneratorCoroutine coroutine, Action startAction, JsObject generatorPrototype)
    {
        _coroutine = coroutine;
        _startAction = startAction;
        Prototype = generatorPrototype;
    }

    internal JsValue DoNext(JsValue sentValue)
    {
        if (!_started)
        {
            _started = true;
            // Start the coroutine — runs the body until the first yield or completion
            _startAction?.Invoke();
            var (value, done) = _coroutine.GetInitialResult();
            return CreateIterResult(value, done);
        }

        var (v, d) = _coroutine.Next(sentValue);
        return CreateIterResult(v, d);
    }

    internal JsValue DoReturn(JsValue value)
    {
        if (!_started)
        {
            _started = true;
            // Generator body never ran — just return done
            return CreateIterResult(value, true);
        }

        var (v, d) = _coroutine.Return(value);
        return CreateIterResult(v, d);
    }

    internal JsValue DoThrow(JsValue error)
    {
        if (!_started)
        {
            _started = true;
            // Generator body never ran — throw (no catch possible)
            throw new JsThrownValueException(error);
        }

        var (v, d) = _coroutine.Throw(error);
        return CreateIterResult(v, d);
    }

    internal static JsObject CreateIterResult(JsValue value, bool done)
    {
        var result = new JsObject();
        result.DefineOwnProperty("value", PropertyDescriptor.Data(value));
        result.DefineOwnProperty("done", PropertyDescriptor.Data(done ? JsValue.True : JsValue.False));
        return result;
    }
}
