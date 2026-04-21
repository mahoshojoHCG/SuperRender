namespace SuperRender.EcmaScript.Runtime.Builtins;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SuperRender.EcmaScript.Runtime;

/// <summary>
/// JS Promise — tracks settlement state, runs reactions via <see cref="MicrotaskScheduler"/>,
/// and provides <see cref="FromTask(Task)"/> / <see cref="GetAwaiter"/> for C# interop.
/// Instance methods (then/catch/finally) are SG-generated; static JS helpers
/// (resolve/reject/all/race/allSettled/any/withResolvers) are wired in <see cref="PromiseConstructor.Install"/>.
/// </summary>
[JsObject]
public partial class JsPromise : JsObject
{
    public enum PromiseState
    {
        Pending,
        Fulfilled,
        Rejected,
    }

    public static JsDynamicObject? DefaultPrototype { get; set; }

    public PromiseState State { get; private set; } = PromiseState.Pending;
    public JsValue Result { get; private set; } = Undefined;

    internal List<PromiseReaction> PendingReactions { get; } = new();

    public JsPromise()
    {
        Prototype = DefaultPrototype;
    }

    public JsPromise(JsDynamicObject? prototype)
    {
        Prototype = prototype;
    }

    public static JsPromise Resolved(JsValue value)
    {
        var p = new JsPromise();
        p.Resolve(value);
        return p;
    }

    public static JsPromise Rejected(JsValue reason)
    {
        var p = new JsPromise();
        p.Reject(reason);
        return p;
    }

    public void Resolve(JsValue value)
    {
        if (State != PromiseState.Pending)
        {
            return;
        }

        if (value is JsPromise thenable)
        {
            thenable.AttachContinuation(
                v => { Resolve(v); return Undefined; },
                r => { Reject(r); return Undefined; });
            return;
        }

        State = PromiseState.Fulfilled;
        Result = value;
        TriggerReactions();
    }

    public void Reject(JsValue reason)
    {
        if (State != PromiseState.Pending)
        {
            return;
        }

        State = PromiseState.Rejected;
        Result = reason;
        TriggerReactions();
    }

#pragma warning disable JSGEN005, JSGEN006 // spec-untyped: handlers accept/return arbitrary JsValue
    [JsMethod("then")]
    public JsPromise Then(JsValue onFulfilled, JsValue onRejected)
    {
        return AttachHandlers(onFulfilled as JsFunction, onRejected as JsFunction);
    }

    [JsMethod("catch")]
    public JsPromise Catch(JsValue onRejected) => AttachHandlers(null, onRejected as JsFunction);

    [JsMethod("finally")]
    public JsPromise Finally(JsValue onFinally)
    {
        if (onFinally is not JsFunction fn)
        {
            return AttachHandlers(null, null);
        }

        var onF = JsFunction.CreateNative(string.Empty, (_, args) =>
        {
            fn.Call(Undefined, Array.Empty<JsValue>());
            return args.Length > 0 ? args[0] : Undefined;
        }, 1);

        var onR = JsFunction.CreateNative(string.Empty, (_, args) =>
        {
            fn.Call(Undefined, Array.Empty<JsValue>());
            throw new PromiseRejectedException(args.Length > 0 ? args[0] : Undefined);
        }, 1);

        return AttachHandlers(onF, onR);
    }
#pragma warning restore JSGEN005, JSGEN006

    internal JsPromise AttachHandlers(JsFunction? onFulfilled, JsFunction? onRejected)
    {
        var result = new JsPromise();
        var reaction = new PromiseReaction(result, onFulfilled, onRejected);

        if (State == PromiseState.Pending)
        {
            PendingReactions.Add(reaction);
        }
        else
        {
            var state = State;
            var value = Result;
            MicrotaskScheduler.Enqueue(() => ProcessReaction(reaction, state, value));
        }

        return result;
    }

    internal JsPromise AttachContinuation(Func<JsValue, JsValue> onFulfilled, Func<JsValue, JsValue> onRejected)
    {
        var onF = JsFunction.CreateNative(string.Empty, (_, a) => onFulfilled(a.Length > 0 ? a[0] : Undefined), 1);
        var onR = JsFunction.CreateNative(string.Empty, (_, a) => onRejected(a.Length > 0 ? a[0] : Undefined), 1);
        return AttachHandlers(onF, onR);
    }

    private void TriggerReactions()
    {
        var reactions = PendingReactions.ToArray();
        PendingReactions.Clear();

        foreach (var reaction in reactions)
        {
            var state = State;
            var value = Result;
            MicrotaskScheduler.Enqueue(() => ProcessReaction(reaction, state, value));
        }
    }

    private static void ProcessReaction(PromiseReaction reaction, PromiseState state, JsValue value)
    {
        var handler = state == PromiseState.Fulfilled ? reaction.OnFulfilled : reaction.OnRejected;

        try
        {
            if (handler is not null)
            {
                var result = handler.Call(Undefined, [value]);
                reaction.ResultPromise.Resolve(result);
            }
            else if (state == PromiseState.Fulfilled)
            {
                reaction.ResultPromise.Resolve(value);
            }
            else
            {
                reaction.ResultPromise.Reject(value);
            }
        }
        catch (PromiseRejectedException ex)
        {
            reaction.ResultPromise.Reject(ex.Reason);
        }
        catch (Errors.JsErrorBase ex)
        {
            reaction.ResultPromise.Reject(new JsString(ex.Message));
        }
    }

    // ---- C# Task interop ----

    public static JsPromise FromTask(Task task)
    {
        var p = new JsPromise();
        if (task.IsCompleted)
        {
            SettleFromCompletedTask(p, task, Undefined);
            return p;
        }

        task.ContinueWith(t => SettleFromCompletedTask(p, t, Undefined), TaskScheduler.Default);
        return p;
    }

    public static JsPromise FromTask<T>(Task<T> task) where T : JsValue
        => FromTaskCore(task, static v => v);

    public static JsPromise FromTask(Task<string> task) => FromTaskCore(task, Interop.InteropConversions.ToJs);
    public static JsPromise FromTask(Task<bool> task) => FromTaskCore(task, Interop.InteropConversions.ToJs);
    public static JsPromise FromTask(Task<double> task) => FromTaskCore(task, Interop.InteropConversions.ToJs);
    public static JsPromise FromTask(Task<float> task) => FromTaskCore(task, Interop.InteropConversions.ToJs);
    public static JsPromise FromTask(Task<int> task) => FromTaskCore(task, Interop.InteropConversions.ToJs);
    public static JsPromise FromTask(Task<uint> task) => FromTaskCore(task, Interop.InteropConversions.ToJs);
    public static JsPromise FromTask(Task<long> task) => FromTaskCore(task, Interop.InteropConversions.ToJs);
    public static JsPromise FromTask(Task<ulong> task) => FromTaskCore(task, Interop.InteropConversions.ToJs);
    public static JsPromise FromTask(Task<short> task) => FromTaskCore(task, Interop.InteropConversions.ToJs);
    public static JsPromise FromTask(Task<ushort> task) => FromTaskCore(task, Interop.InteropConversions.ToJs);
    public static JsPromise FromTask(Task<byte> task) => FromTaskCore(task, Interop.InteropConversions.ToJs);
    public static JsPromise FromTask(Task<sbyte> task) => FromTaskCore(task, Interop.InteropConversions.ToJs);
    public static JsPromise FromTask(Task<decimal> task) => FromTaskCore(task, Interop.InteropConversions.ToJs);

    private static JsPromise FromTaskCore<T>(Task<T> task, Func<T, JsValue> boxer)
    {
        var p = new JsPromise();
        if (task.IsCompleted)
        {
            SettleFromCompletedTask(p, task, task.IsCompletedSuccessfully ? boxer(task.Result) : Undefined);
            return p;
        }

        task.ContinueWith(t => SettleFromCompletedTask(p, t, t.IsCompletedSuccessfully ? boxer(t.Result) : Undefined), TaskScheduler.Default);
        return p;
    }

    private static void SettleFromCompletedTask(JsPromise p, Task task, JsValue successValue)
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
            p.Resolve(successValue);
        }
    }

    public JsPromiseAwaiter GetAwaiter() => new(this);
}

public sealed record PromiseReaction(JsPromise ResultPromise, JsFunction? OnFulfilled, JsFunction? OnRejected);

public sealed class PromiseRejectedException : Exception
{
    public JsValue Reason { get; }
    public PromiseRejectedException(JsValue reason)
        : base("Promise rejected")
    {
        Reason = reason;
    }
}
