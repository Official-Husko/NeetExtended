using System;
using System.Threading;

namespace DevBridge.Server;

/// <summary>A result that arrives later (after a coroutine, a few frames...).</summary>
public sealed class Deferred
{
    private readonly ManualResetEventSlim _done = new ManualResetEventSlim(false);

    public object Result { get; private set; }
    public string Error { get; private set; }

    public void Complete(object result)
    {
        Result = result;
        _done.Set();
    }

    public void Fail(string error)
    {
        Error = error;
        _done.Set();
    }

    public void Fail(Exception e) => Fail(e.GetBaseException().Message);

    internal bool Wait(int timeoutMs) => _done.Wait(timeoutMs);
}
