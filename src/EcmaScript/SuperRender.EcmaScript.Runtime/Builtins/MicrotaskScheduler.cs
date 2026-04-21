namespace SuperRender.EcmaScript.Runtime.Builtins;

using System;
using System.Collections.Generic;

public static class MicrotaskScheduler
{
    private static readonly Queue<Action> Tasks = new();
    private static bool _draining;

    public static void Enqueue(Action task)
    {
        Tasks.Enqueue(task);
        if (!_draining)
        {
            Drain();
        }
    }

    private static void Drain()
    {
        _draining = true;
        try
        {
            while (Tasks.Count > 0)
            {
                Tasks.Dequeue()();
            }
        }
        finally
        {
            _draining = false;
        }
    }
}
