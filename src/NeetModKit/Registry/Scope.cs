using System;
using System.Collections.Generic;
using NeetLoader.Runtime;

namespace NeetModKit.Registry;

/// <summary>
/// A throwaway owner for things registered together, so they can be removed together and
/// registered again (for example everything loaded from a set of data files). Dispose it to
/// clean up; it also cleans up when its parent does.
/// </summary>
public sealed class Scope : ITracker, IDisposable
{
    private readonly List<IDisposable> _tracked = new List<IDisposable>();
    private IDisposable _parentLink;

    public Scope(ITracker parent = null)
    {
        if (parent != null)
            _parentLink = parent.Track(Disposable.Create(Dispose));
    }

    public bool IsDisposed { get; private set; }

    public T Track<T>(T disposable) where T : IDisposable
    {
        if (IsDisposed)
        {
            disposable.Dispose();
            return disposable;
        }
        _tracked.Add(disposable);
        return disposable;
    }

    /// <summary>Cleans up in reverse order. Errors are collected and thrown together at the end.</summary>
    public void Dispose()
    {
        if (IsDisposed)
            return;
        IsDisposed = true;
        _parentLink?.Dispose();
        _parentLink = null;

        List<Exception> errors = null;
        for (int i = _tracked.Count - 1; i >= 0; i--)
        {
            try { _tracked[i].Dispose(); }
            catch (Exception e) { (errors ??= new List<Exception>()).Add(e); }
        }
        _tracked.Clear();
        if (errors != null)
            throw new AggregateException("Cleaning up a scope failed", errors);
    }
}
