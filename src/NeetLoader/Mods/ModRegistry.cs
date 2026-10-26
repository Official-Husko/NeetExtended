using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NeetLoader.Config;
using NeetLoader.Logging;
using NeetLoader.Runtime;

namespace NeetLoader.Mods;

/// <summary>
/// Every mod the loader knows about, the player's on/off switches, and runtime control
/// (load, unload, hot reload). Runtime operations are queued and run between frames;
/// pass a callback to learn the outcome (null means success, otherwise the reason it failed).
/// </summary>
public static class ModRegistry
{
    private static readonly List<ModInfo> All = new List<ModInfo>();
    private static readonly Dictionary<string, ModInfo> ById = new Dictionary<string, ModInfo>(StringComparer.OrdinalIgnoreCase);
    private static ConfigFile<LoaderSettings> _settings;
    private static ModLogger _log;

    public static IReadOnlyList<ModInfo> Mods => All;

    /// <summary>Raised after a mod (re)loads.</summary>
    public static event Action<ModInfo> ModLoaded
    {
        add => ModLifecycle.Loaded += value;
        remove => ModLifecycle.Loaded -= value;
    }

    /// <summary>
    /// Raised just before a mod unloads or reloads. Registries use it to drop whatever that mod
    /// registered, so cleanup happens even if the mod forgets.
    /// </summary>
    public static event Action<ModInfo> ModUnloading
    {
        add => ModLifecycle.Unloading += value;
        remove => ModLifecycle.Unloading -= value;
    }

    public static ModInfo Find(string id) => id != null && ById.TryGetValue(id, out var mod) ? mod : null;

    public static bool IsLoaded(string id) => Find(id)?.State == ModState.Loaded;

    /// <summary>Gets a loaded mod's entry object, e.g. to call into another mod's API.</summary>
    public static T Get<T>() where T : NeetMod
    {
        foreach (var mod in All)
        {
            if (mod.Instance is T match)
                return match;
        }
        return null;
    }

    /// <summary>Finds the loaded mod that owns a type (useful for owner-tracked registrations).</summary>
    public static ModInfo OwnerOf(Type type)
    {
        var assembly = type.Assembly;
        foreach (var mod in All)
        {
            if (mod.State == ModState.Loaded && mod.Assemblies.Contains(assembly))
                return mod;
        }
        return null;
    }

    // ---- Player switches (apply on next launch) ----

    public static bool IsEnabled(string id)
    {
        if (!_settings.Value.DisabledMods.Contains(id))
            return true;
        var mod = Find(id);
        return mod != null && AlwaysOnReason(mod) != null;
    }

    /// <summary>Switches a mod on or off for the next launch. Returns why not, or null when done.</summary>
    public static string SetEnabled(string id, bool enabled)
    {
        var mod = Find(id);
        string reason = mod != null && !enabled ? AlwaysOnReason(mod) : null;
        if (reason != null)
            return $"Can't switch off {mod.Manifest.DisplayName}: {reason}.";

        var disabled = _settings.Value.DisabledMods;
        if (enabled)
            disabled.Remove(id);
        else if (!disabled.Contains(id))
            disabled.Add(id);
        _settings.Save();
        return null;
    }

    /// <summary>
    /// The essential mod that keeps <paramref name="mod"/> from being switched off or unloaded: the
    /// mod itself if its mod.json says "essential", or an essential mod that needs it. Null if none.
    /// </summary>
    public static ModInfo AlwaysOnBecause(ModInfo mod) =>
        mod.Manifest.Essential ? mod : EssentialDependent(mod);

    /// <summary>"it's always on", "X needs it and is always on", or null.</summary>
    public static string AlwaysOnReason(ModInfo mod)
    {
        var because = AlwaysOnBecause(mod);
        if (because == null)
            return null;
        return because == mod ? "it's always on" : $"{because.Manifest.DisplayName} needs it and is always on";
    }

    /// <summary>An essential mod that depends on <paramref name="mod"/>, directly or further down.</summary>
    private static ModInfo EssentialDependent(ModInfo mod)
    {
        var seen = new HashSet<ModInfo> { mod };
        var queue = new Queue<ModInfo>();
        queue.Enqueue(mod);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var other in All)
            {
                if (seen.Contains(other) || !other.Manifest.Dependencies.ContainsKey(current.Id))
                    continue;
                if (other.Manifest.Essential)
                    return other;
                seen.Add(other);
                queue.Enqueue(other);
            }
        }
        return null;
    }

    /// <summary>True if any on/off switch differs from what is running right now.</summary>
    public static bool RestartNeeded => All.Any(m => m.State != ModState.Failed && IsEnabled(m.Id) != (m.State == ModState.Loaded));

    public static bool AutoReload
    {
        get => _settings.Value.AutoReload;
        set
        {
            _settings.Value.AutoReload = value;
            _settings.Save();
            Watcher.Enabled = value;
        }
    }

    // ---- Runtime control ----

    public static void Reload(string id, Action<string> done = null) => Queue(id, done, ModLifecycle.Reload);

    public static void Unload(string id, Action<string> done = null) => Queue(id, done, ModLifecycle.Unload);

    public static void Load(string id, Action<string> done = null) => Queue(id, done, ModLifecycle.Load);

    /// <summary>Reloads several mods (plus everything built on them) as one batch.</summary>
    internal static void ReloadSet(IReadOnlyList<ModInfo> mods, Action<string> done = null)
    {
        MainThread.Post(() =>
        {
            string error;
            try { error = ModLifecycle.ReloadSet(mods); }
            catch (Exception e)
            {
                error = e.Message;
                _log.Error("Reload failed", e);
            }
            if (error != null)
                _log.Warn(error);
            done?.Invoke(error);
        });
    }

    /// <summary>Reloads every loaded hot-reload mod as one batch.</summary>
    public static void ReloadAll(Action<string> done = null)
    {
        MainThread.Post(() =>
        {
            string error;
            try
            {
                error = ModLifecycle.ReloadSet(All.Where(m => m.State == ModState.Loaded && m.CanHotReload).ToList());
            }
            catch (Exception e)
            {
                error = e.Message;
                _log.Error("Reload all failed", e);
            }
            done?.Invoke(error);
        });
    }

    /// <summary>Looks for mod folders added since startup. New mods are listed but not loaded.</summary>
    public static IReadOnlyList<ModInfo> Rescan()
    {
        var added = new List<ModInfo>();
        foreach (var mod in ModDiscovery.Scan(LoaderPaths.ModsDir, _log))
        {
            if (ById.ContainsKey(mod.Id))
                continue;
            mod.State = ModState.Disabled;
            Add(mod);
            added.Add(mod);
            _log.Info($"Found new mod {mod}");
        }
        return added;
    }

    // ---- Internal ----

    internal static LoaderSettings Settings => _settings.Value;
    internal static HotReloadWatcher Watcher { get; } = new HotReloadWatcher();

    internal static void Init(ModLogger log)
    {
        _log = log;
        _settings = new ConfigFile<LoaderSettings>(Path.Combine(LoaderPaths.DataDir, "settings.json"), log);
        Watcher.Enabled = _settings.Value.AutoReload;
    }

    internal static void Add(ModInfo mod)
    {
        All.Add(mod);
        ById[mod.Id] = mod;
        foreach (string dll in ModDiscovery.AssemblyPaths(mod))
            AssemblyResolver.RegisterPath(dll);
    }

    private static void Queue(string id, Action<string> done, Func<ModInfo, string> operation)
    {
        MainThread.Post(() =>
        {
            var mod = Find(id);
            string error;
            try
            {
                error = mod == null ? $"No mod with id '{id}'." : operation(mod);
            }
            catch (Exception e)
            {
                error = e.Message;
                _log.Error($"Operation on {id} failed", e);
            }
            if (error != null)
                _log.Warn(error);
            done?.Invoke(error);
        });
    }
}
