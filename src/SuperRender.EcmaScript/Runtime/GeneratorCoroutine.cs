namespace SuperRender.EcmaScript.Runtime;

/// <summary>
/// Thread-based coroutine that runs a generator/async body on a separate thread.
/// <see cref="Yield"/> blocks the generator thread and returns control to the caller.
/// <see cref="Next"/>/<see cref="Return"/>/<see cref="Throw"/> resume the generator thread.
/// </summary>
internal sealed class GeneratorCoroutine : IDisposable
{
    private readonly SemaphoreSlim _callerReady = new(0, 1);
    private readonly SemaphoreSlim _generatorReady = new(0, 1);
    private Thread? _thread;
    private JsValue _yieldedValue = JsValue.Undefined;
    private JsValue _sentValue = JsValue.Undefined;
    private bool _done;
    private bool _shouldThrow;
    private bool _shouldReturn;
    private JsValue _thrownValue = JsValue.Undefined;
    private Exception? _exception;
    private bool _disposed;

    public void Start(Func<GeneratorCoroutine, JsValue> body)
    {
        _thread = new Thread(() =>
        {
            try
            {
                var result = body(this);
                _yieldedValue = result;
                _done = true;
            }
            catch (GeneratorReturnException gre)
            {
                _yieldedValue = gre.Value;
                _done = true;
            }
            catch (Exception ex) when (ex is not ThreadAbortException)
            {
                _exception = ex;
                _done = true;
            }
            finally
            {
                _callerReady.Release();
            }
        })
        {
            IsBackground = true,
            Name = "JS Generator"
        };
        _thread.Start();
        _callerReady.Wait();
    }

    public JsValue Yield(JsValue value)
    {
        _yieldedValue = value;
        _callerReady.Release();
        _generatorReady.Wait();

        if (_shouldThrow)
        {
            _shouldThrow = false;
            var thrown = _thrownValue;
            _thrownValue = JsValue.Undefined;
            throw new JsThrownValueException(thrown);
        }

        if (_shouldReturn)
        {
            _shouldReturn = false;
            throw new GeneratorReturnException(_sentValue);
        }

        return _sentValue;
    }

    public (JsValue Value, bool Done) GetInitialResult()
    {
        if (_exception is not null)
        {
            var ex = _exception;
            _exception = null;
            throw ex;
        }

        return (_yieldedValue, _done);
    }

    public (JsValue Value, bool Done) Next(JsValue sentValue)
    {
        if (_done)
        {
            return (JsValue.Undefined, true);
        }

        _sentValue = sentValue;
        _generatorReady.Release();
        _callerReady.Wait();

        if (_exception is not null)
        {
            var ex = _exception;
            _exception = null;
            throw ex;
        }

        return (_yieldedValue, _done);
    }

    public (JsValue Value, bool Done) Return(JsValue value)
    {
        if (_done)
        {
            return (value, true);
        }

        _sentValue = value;
        _shouldReturn = true;
        _generatorReady.Release();
        _callerReady.Wait();

        if (_exception is not null)
        {
            var ex = _exception;
            _exception = null;
            throw ex;
        }

        // If the generator yielded (e.g. inside a finally block),
        // return the yielded value with done: false.
        if (!_done)
        {
            return (_yieldedValue, false);
        }

        return (value, true);
    }

    public (JsValue Value, bool Done) Throw(JsValue error)
    {
        if (_done)
        {
            throw new JsThrownValueException(error);
        }

        _thrownValue = error;
        _shouldThrow = true;
        _generatorReady.Release();
        _callerReady.Wait();

        if (_exception is not null)
        {
            var ex = _exception;
            _exception = null;
            throw ex;
        }

        return (_yieldedValue, _done);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (!_done)
        {
            _shouldReturn = true;
            _sentValue = JsValue.Undefined;
            try
            {
                _generatorReady.Release();
            }
            catch (ObjectDisposedException)
            {
                // Already disposed
            }
        }

        _callerReady.Dispose();
        _generatorReady.Dispose();
    }
}

/// <summary>
/// Internal exception used to implement <c>generator.return(value)</c> — forces
/// the generator body to exit cleanly with a specified return value.
/// </summary>
internal sealed class GeneratorReturnException : Exception
{
    public JsValue Value { get; }

    public GeneratorReturnException(JsValue value) : base("Generator return")
    {
        Value = value;
    }
}

/// <summary>
/// Exception that preserves the original JS value thrown via <c>generator.throw(value)</c>
/// or a JS throw statement, allowing round-trip through the C# exception system.
/// </summary>
internal sealed class JsThrownValueException : Exception
{
    public JsValue ThrownValue { get; }

    public JsThrownValueException(JsValue value)
        : base(value is JsString s ? s.Value : "Thrown value")
    {
        ThrownValue = value;
    }
}
