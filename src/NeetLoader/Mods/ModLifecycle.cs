using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NeetLoader.Config;
using NeetLoader.Logging;
using NeetLoader.Runtime;
using UnityEngine.SceneManagement;

namespace NeetLoader.Mods;

/// <summary>
/// Loading, unloading and reloading mods, both at startup and while the game runs.
/// Runtime operations must run on the main thread between frames; <see cref="ModRegistry"/>
/// takes care of queueing them.
/// </summary>
internal static class ModLifecycle
{
    private static ModHost _host;
    private static ModLogger _log;

    public static event Action<ModInfo> Loaded;
    public static event Action<ModInfo> Unloading;

    public static void Init(ModHost host, ModLogger log)
    {
        _host = host;
        _log = log;
    }

    /// <summary>Loads a set of mods in dependency order. <paramref name="atRuntime"/> replays the current scene to them.</summary>
    public static void LoadBatch(IReadOnlyList<ModInfo> mods, bool atRuntime)
    {
        var ordered = DependencySorter.Sort(mods);
        var loaded = new List<ModInfo>(ordered.Count);

        foreach (var mod in ordered)
        {
            if (TryActivate(mod))
                loaded.Add(mod);
        }

        foreach (var mod in loaded)
            Safe(mod, "OnAllLoaded", () => mod.Instance?.OnAllLoaded());

        if (atRuntime)
            ReplayScene(loaded);

        foreach (var mod in loaded)
            Loaded?.Invoke(mod);
    }

    public static string Unload(ModInfo mod)
    {
        if (mod.State != ModState.Loaded)
            return $"{mod.Id} isn't loaded.";

        // Take down anything built on top of this mod first.
        foreach (var dependent in LoadedDependents(mod))
            Unload(dependent);

        Unloading?.Invoke(mod);
        ModActivator.Deactivate(mod, _host, _log);
        _log.Info($"Unloaded {mod}");
        return null;
    }

    public static string Reload(ModInfo mod)
    {
        if (!mod.CanHotReload)
            return $"{mod.Id} doesn't support hot reload (set \"hotReload\": true in its mod.json). Restart the game to update it.";

        var dependents = LoadedDependents(mod).ToList();
        if (dependents.Count > 0)
            return $"{mod.Id} is used by {string.Join(", ", dependents.Select(d => d.Id))}. Restart the game to update it.";

        var timer = Stopwatch.StartNew();
        if (mod.State == ModState.Loaded)
        {
            Unloading?.Invoke(mod);
            ModActivator.Deactivate(mod, _host, _log);
        }

        RefreshManifest(mod);
        LoadBatch(new[] { mod }, atRuntime: true);

        if (mod.State != ModState.Loaded)
            return $"{mod.Id} failed to load: {mod.Error}";
        _log.Info($"Reloaded {mod} in {timer.ElapsedMilliseconds} ms");
        return null;
    }

    public static string Load(ModInfo mod)
    {
        if (mod.State == ModState.Loaded)
            return $"{mod.Id} is already loaded.";
        if (mod.LoadCount > 0 && !mod.CanHotReload)
            return $"{mod.Id} was loaded earlier this session and can't be loaded twice without hot reload. Restart the game.";

        mod.State = ModState.Pending;
        mod.Error = null;
        LoadBatch(new[] { mod }, atRuntime: true);
        return mod.State == ModState.Loaded ? null : $"{mod.Id} failed to load: {mod.Error}";
    }

    private static bool TryActivate(ModInfo mod)
    {
        string missing = mod.Manifest.Dependencies.Keys.FirstOrDefault(id => !ModRegistry.IsLoaded(id));
        if (missing != null)
        {
            mod.Fail($"Needs '{missing}', which isn't loaded.");
            _log.Error($"{mod}: {mod.Error}");
            return false;
        }

        try
        {
            ModActivator.Activate(mod, _host);
            _log.Info($"Loaded {mod}{(mod.CanHotReload ? " (hot reload)" : "")}");
            return true;
        }
        catch (Exception e)
        {
            // Roll back whatever got patched before the failure.
            try { ModActivator.Deactivate(mod, _host, _log); } catch { }
            mod.Fail(e.GetBaseException().Message);
            _log.Error($"Couldn't load {mod}", e);
            return false;
        }
    }

    private static IEnumerable<ModInfo> LoadedDependents(ModInfo mod) =>
        ModRegistry.Mods.Where(m => m.State == ModState.Loaded && m.Manifest.Dependencies.ContainsKey(mod.Id));

    private static void ReplayScene(List<ModInfo> mods)
    {
        var scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
            return;
        foreach (var mod in mods)
            Safe(mod, "OnSceneLoaded", () => mod.Instance?.OnSceneLoaded(scene, LoadSceneMode.Single));
    }

    private static void RefreshManifest(ModInfo mod)
    {
        string path = Path.Combine(mod.Directory, ModDiscovery.ManifestFile);
        if (JsonFile.TryRead(path, out ModManifest manifest, out _))
        {
            manifest.Id = mod.Id; // The id can't change while running.
            mod.Manifest = manifest;
        }
    }

    private static void Safe(ModInfo mod, string what, Action action)
    {
        try { action(); }
        catch (Exception e) { _log.Error($"{mod.Id}.{what} threw", e); }
    }
}
