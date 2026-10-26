using System;
using System.Collections.Generic;
using System.Reflection;
using NeetLoader.Logging;

namespace NeetLoader.Runtime;

/// <summary>
/// Per-callback lists of the mods that actually override it, so the host never makes empty
/// virtual calls every frame. A mod that throws is reported once per callback, not every frame.
/// </summary>
internal sealed class ModCallbacks
{
    private readonly string _methodName;
    private readonly List<NeetMod> _mods = new List<NeetMod>();
    private readonly HashSet<NeetMod> _reported = new HashSet<NeetMod>();

    public ModCallbacks(string methodName)
    {
        _methodName = methodName;
    }

    public int Count => _mods.Count;

    public void AddIfOverridden(NeetMod mod)
    {
        var method = mod.GetType().GetMethod(_methodName, BindingFlags.Instance | BindingFlags.Public);
        if (method != null && method.DeclaringType != typeof(NeetMod))
            _mods.Add(mod);
    }

    public void Remove(NeetMod mod)
    {
        _mods.Remove(mod);
        _reported.Remove(mod);
    }

    public void Invoke(Action<NeetMod> call, ModLogger log)
    {
        // Index loop: no enumerator allocation in per-frame paths.
        for (int i = 0; i < _mods.Count; i++)
        {
            var mod = _mods[i];
            try
            {
                call(mod);
            }
            catch (Exception e)
            {
                if (_reported.Add(mod))
                    log.Error($"{mod.Info.Id}.{_methodName} threw (further errors from it are hidden)", e);
            }
        }
    }
}
