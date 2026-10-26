using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using NeetLoader.Logging;
using NeetLoader.Runtime;

namespace NeetLoader.Mods;

/// <summary>Brings a single mod up (load code, create entry, patch) and back down again.</summary>
internal static class ModActivator
{
    public static void Activate(ModInfo mod, ModHost host)
    {
        var log = new ModLogger(mod.Id);

        mod._assemblies.Clear();
        mod._assemblies.AddRange(AssemblyLoading.Load(mod));
        mod.LoadedWriteTime = AssemblyLoading.LastWrite(mod);
        mod.Instance = CreateEntry(mod, log);

        if (mod.Manifest.AutoPatch)
        {
            var harmony = mod.Instance?.Harmony ?? new Harmony(mod.Id);
            foreach (var assembly in mod.Assemblies)
                harmony.PatchAll(assembly);
        }

        mod.Instance?.OnLoad();
        if (mod.Instance != null)
            host.Register(mod.Instance);

        mod.LoadCount++;
        mod.State = ModState.Loaded;
        mod.Error = null;
    }

    /// <summary>Undoes <see cref="Activate"/> as far as possible. The assemblies themselves stay in memory.</summary>
    public static void Deactivate(ModInfo mod, ModHost host, ModLogger log)
    {
        var instance = mod.Instance;
        if (instance != null)
        {
            host.Unregister(instance);
            Try(log, mod, "OnUnload", instance.OnUnload);
            Try(log, mod, "cleanup", instance.DisposeTracked);
        }

        // Unpatch by id only: a bare UnpatchAll() would strip every mod's patches.
        new Harmony(mod.Id).UnpatchAll(mod.Id);

        mod.Instance = null;
        mod.State = ModState.Disabled;
    }

    private static NeetMod CreateEntry(ModInfo mod, ModLogger log)
    {
        var entryTypes = mod.Assemblies
            .SelectMany(SafeGetTypes)
            .Where(t => t != null && !t.IsAbstract && typeof(NeetMod).IsAssignableFrom(t))
            .ToList();

        if (entryTypes.Count == 0)
            return null; // Patch-only or library mod.
        if (entryTypes.Count > 1)
            log.Warn($"Found {entryTypes.Count} NeetMod classes, using {entryTypes[0].FullName}");

        var instance = (NeetMod)Activator.CreateInstance(entryTypes[0]);
        instance.Info = mod;
        instance.Log = log;
        return instance;
    }

    private static Type[] SafeGetTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException e)
        {
            return e.Types;
        }
    }

    private static void Try(ModLogger log, ModInfo mod, string what, Action action)
    {
        try { action(); }
        catch (Exception e) { log.Error($"{mod.Id} {what} failed", e); }
    }
}
