using System;
using System.Collections.Generic;
using NeetLoader.Runtime;
using UnityEngine;

namespace NeetModKit.Game;

/// <summary>
/// A scale on one of the scene's own weather values (<see cref="Sky.RainAmount"/>, <see cref="Sky.FogAmount"/>,
/// <see cref="Sky.StormVolume"/>). Every mod sets its own factor and the kit applies the product of all of
/// them to the value the scene started with. A factor goes away when its mod clears it or unloads, and with
/// no factors left the scene's own value is back.
/// </summary>
public sealed class SkyMultiplier
{
    private sealed class Entry
    {
        public ITracker Owner;
        public float Value;
    }

    private readonly List<Entry> _entries = new List<Entry>();
    private readonly Action _apply;

    internal SkyMultiplier(Action apply)
    {
        _apply = apply;
    }

    /// <summary>All the factors multiplied; 1 when nobody set one.</summary>
    public float Value { get; private set; } = 1f;

    /// <summary>True once any factor was set since the scene's own value was last written back.</summary>
    internal bool Applied { get; set; }

    /// <summary>Sets <paramref name="owner"/>'s factor (0 and up; 1 changes nothing). Setting it again replaces the old one.</summary>
    public void Set(ITracker owner, float factor)
    {
        if (owner == null)
            throw new ArgumentNullException(nameof(owner));
        factor = float.IsNaN(factor) ? 1f : Mathf.Clamp(factor, 0f, 20f);
        var entry = Find(owner);
        if (entry == null)
        {
            entry = new Entry { Owner = owner, Value = factor };
            _entries.Add(entry);
            owner.Track(Disposable.Create(() => Clear(owner)));
        }
        else
        {
            entry.Value = factor;
        }
        Recalculate();
    }

    /// <summary>Takes <paramref name="owner"/>'s factor away.</summary>
    public void Clear(ITracker owner)
    {
        var entry = Find(owner);
        if (entry == null)
            return;
        _entries.Remove(entry);
        Recalculate();
    }

    /// <summary>Takes every factor away (for tools; mods clear their own).</summary>
    internal void ClearAll()
    {
        if (_entries.Count == 0)
            return;
        _entries.Clear();
        Recalculate();
    }

    private Entry Find(ITracker owner)
    {
        for (int i = 0; i < _entries.Count; i++)
        {
            if (_entries[i].Owner == owner)
                return _entries[i];
        }
        return null;
    }

    private void Recalculate()
    {
        float product = 1f;
        for (int i = 0; i < _entries.Count; i++)
            product *= _entries[i].Value;
        Value = product;
        Applied = true;
        _apply();
    }
}
