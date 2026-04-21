namespace SuperRender.EcmaScript.Runtime.Builtins;

using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using SuperRender.EcmaScript.Runtime;

/// <summary>
/// C# awaiter for a <see cref="JsPromise"/>. Returns the settled <see cref="JsValue"/> result;
/// throws <see cref="PromiseRejectedException"/> if the promise rejected.
/// </summary>
public readonly struct JsPromiseAwaiter : ICriticalNotifyCompletion
{
    private readonly JsPromise _promise;

    internal JsPromiseAwaiter(JsPromise promise)
    {
        _promise = promise;
    }

    public bool IsCompleted => _promise.State != JsPromise.PromiseState.Pending;

    public JsValue GetResult()
    {
        if (_promise.State == JsPromise.PromiseState.Fulfilled)
        {
            return _promise.Result;
        }

        if (_promise.State == JsPromise.PromiseState.Rejected)
        {
            throw new PromiseRejectedException(_promise.Result);
        }

        throw new InvalidOperationException("Promise is still pending");
    }

    public void OnCompleted(Action continuation) => AttachContinuation(continuation);
    public void UnsafeOnCompleted(Action continuation) => AttachContinuation(continuation);

    private void AttachContinuation(Action continuation)
    {
        _promise.AttachContinuation(
            _ => { continuation(); return JsValue.Undefined; },
            _ => { continuation(); return JsValue.Undefined; });
    }
}

/// <summary>
/// Typed awaiter for <see cref="JsPromise{T}"/>. Unboxes the settled <see cref="JsValue"/>
/// result to <typeparamref name="T"/>.
/// </summary>
public readonly struct JsPromiseAwaiter<T> : ICriticalNotifyCompletion
{
    private readonly JsPromise<T> _promise;

    internal JsPromiseAwaiter(JsPromise<T> promise)
    {
        _promise = promise;
    }

    public bool IsCompleted => _promise.State != JsPromise.PromiseState.Pending;

    public T GetResult()
    {
        if (_promise.State == JsPromise.PromiseState.Fulfilled)
        {
            return UnboxResult(_promise.Result);
        }

        if (_promise.State == JsPromise.PromiseState.Rejected)
        {
            throw new PromiseRejectedException(_promise.Result);
        }

        throw new InvalidOperationException("Promise is still pending");
    }

    public void OnCompleted(Action continuation) => AttachContinuation(continuation);
    public void UnsafeOnCompleted(Action continuation) => AttachContinuation(continuation);

    private void AttachContinuation(Action continuation)
    {
        _promise.AttachContinuation(
            _ => { continuation(); return JsValue.Undefined; },
            _ => { continuation(); return JsValue.Undefined; });
    }

    private static T UnboxResult(JsValue value)
    {
        if (typeof(T) == typeof(JsValue) || typeof(JsValue).IsAssignableFrom(typeof(T)))
        {
            return (T)(object)value;
        }

        if (typeof(T) == typeof(string)) return (T)(object)value.ToJsString();
        if (typeof(T) == typeof(bool)) return (T)(object)value.ToBoolean();
        if (typeof(T) == typeof(double)) return (T)(object)value.ToNumber();
        if (typeof(T) == typeof(float)) return (T)(object)(float)value.ToNumber();
        if (typeof(T) == typeof(decimal)) return (T)(object)(decimal)value.ToNumber();
        if (typeof(T) == typeof(int)) return (T)(object)(int)value.ToNumber();
        if (typeof(T) == typeof(uint)) return (T)(object)(uint)value.ToNumber();
        if (typeof(T) == typeof(long)) return (T)(object)(long)value.ToNumber();
        if (typeof(T) == typeof(ulong)) return (T)(object)(ulong)value.ToNumber();
        if (typeof(T) == typeof(short)) return (T)(object)(short)value.ToNumber();
        if (typeof(T) == typeof(ushort)) return (T)(object)(ushort)value.ToNumber();
        if (typeof(T) == typeof(byte)) return (T)(object)(byte)value.ToNumber();
        if (typeof(T) == typeof(sbyte)) return (T)(object)(sbyte)value.ToNumber();

        throw new InvalidOperationException(
            $"JsPromiseAwaiter<{typeof(T).Name}>: cannot unbox JsValue of type {value.GetType().Name} to {typeof(T).FullName}.");
    }
}
