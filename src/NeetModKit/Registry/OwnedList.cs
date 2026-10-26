using System;
using System.Collections.Generic;
using NeetLoader;
using NeetLoader.Runtime;

namespace NeetModKit.Registry;

/// <summary>
/// A list where each entry belongs to a mod and disappears when that mod unloads. Used for
/// menu pages, settings sections and other things mods hand to the framework.
/// </summary>
public sealed class OwnedList<T>
{
    private readonly List<T> _items = new List<T>();

    public IReadOnlyList<T> Items => _items;

    /// <summary>Raised after an add or remove.</summary>
    public event Action Changed;

    public void Add(NeetMod owner, T item)
    {
        _items.Add(item);
        owner?.Track(Disposable.Create(() => Remove(item)));
        Changed?.Invoke();
    }

    public void Remove(T item)
    {
        if (_items.Remove(item))
            Changed?.Invoke();
    }

    public void Sort(Comparison<T> comparison) => _items.Sort(comparison);
}
