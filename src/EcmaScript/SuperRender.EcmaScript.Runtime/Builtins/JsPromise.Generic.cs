namespace SuperRender.EcmaScript.Runtime.Builtins;

using System;
using System.Threading.Tasks;
using SuperRender.EcmaScript.Runtime;

#pragma warning disable CA1000 // per-T static factory is intentional

/// <summary>
/// Typed C# view over a <see cref="JsPromise"/>. The JS runtime never sees the generic type —
/// every promise still pattern-matches as <see cref="JsPromise"/>. <typeparamref name="T"/> only exists
/// for C# ergonomics: typed <see cref="FromTask"/> factory and typed <see cref="GetAwaiter"/>.
/// </summary>
public sealed class JsPromise<T> : JsPromise
{
    public JsPromise()
    {
    }

    public JsPromise(JsDynamicObject? prototype) : base(prototype)
    {
    }

    public static JsPromise<T> FromTask(Task<T> task)
    {
        var p = new JsPromise<T>();
        if (task.IsCompleted)
        {
            SettleTyped(p, task);
            return p;
        }

        task.ContinueWith(t => SettleTyped(p, t), TaskScheduler.Default);
        return p;
    }

    private static void SettleTyped(JsPromise<T> p, Task<T> task)
    {
        if (task.IsFaulted)
        {
            var msg = task.Exception?.InnerException?.Message ?? task.Exception?.Message ?? "Task faulted";
            p.Reject(new JsString(msg));
        }
        else if (task.IsCanceled)
        {
            p.Reject(new JsString("Task was cancelled"));
        }
        else
        {
            p.Resolve(BoxResult(task.Result));
        }
    }

    private static JsValue BoxResult(T result)
    {
        return result switch
        {
            null => JsValue.Null,
            JsValue jv => jv,
            string s => new JsString(s),
            bool b => b ? JsValue.True : JsValue.False,
            double d => JsNumber.Create(d),
            float f => JsNumber.Create(f),
            decimal m => JsNumber.Create((double)m),
            int i => JsNumber.Create(i),
            uint u => JsNumber.Create(u),
            long l => JsNumber.Create(l),
            ulong ul => JsNumber.Create(ul),
            short sh => JsNumber.Create(sh),
            ushort us => JsNumber.Create(us),
            byte by => JsNumber.Create(by),
            sbyte sb => JsNumber.Create(sb),
            _ => throw new InvalidOperationException(
                $"JsPromise<{typeof(T).Name}>: cannot box value of type {result.GetType().FullName} into a JsValue."),
        };
    }

    public new JsPromiseAwaiter<T> GetAwaiter() => new(this);
}
