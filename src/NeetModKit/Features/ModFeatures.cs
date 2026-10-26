using System;
using System.Collections.Generic;
using System.IO;
using NeetLoader;
using NeetLoader.Config;
using NeetLoader.Logging;
using NeetLoader.Mods;
using NeetLoader.Runtime;

namespace NeetModKit.Features;

/// <summary>
/// Every feature of every loaded mod, with the player's on/off choices saved in
/// Mods/NeetModKit/features.json (keyed "modId/featureId").
/// </summary>
public static class ModFeatures
{
    private static readonly List<ModFeature> All = new List<ModFeature>();
    private static readonly List<ModFeature> Updating = new List<ModFeature>();
    private static Dictionary<string, bool> _choices = new Dictionary<string, bool>();
    private static string _path;
    private static ModLogger _log;

    public static IReadOnlyList<ModFeature> Features => All;

    /// <summary>Features of one mod, in registration order.</summary>
    public static List<ModFeature> Of(ModInfo mod) => All.FindAll(f => f.Owner.Info == mod);

    /// <summary>Adds a feature and switches it on if the player (or its default) wants it on.</summary>
    public static void Register(NeetMod owner, ModFeature feature)
    {
        feature.Owner = owner;
        All.Add(feature);
        if (feature.OverridesUpdate)
            Updating.Add(feature);

        owner.Track(Disposable.Create(() =>
        {
            if (feature.Enabled)
                Run(feature, feature.Disable, "switching off");
            All.Remove(feature);
            Updating.Remove(feature);
        }));

        if (IsWanted(feature))
            Run(feature, feature.Enable, "switching on");
    }

    public static bool IsWanted(ModFeature feature) =>
        _choices.TryGetValue(Key(feature), out bool on) ? on : feature.EnabledByDefault;

    /// <summary>Switches a feature on or off right now and remembers the choice.</summary>
    public static void SetEnabled(ModFeature feature, bool on)
    {
        _choices[Key(feature)] = on;
        Save();
        if (on && !feature.Enabled)
            Run(feature, feature.Enable, "switching on");
        else if (!on && feature.Enabled)
            Run(feature, feature.Disable, "switching off");
    }

    internal static void Init(NeetMod kit)
    {
        _log = kit.Log;
        _path = kit.PathTo("features.json");
        _choices = File.Exists(_path) ? JsonFile.Read<Dictionary<string, bool>>(_path) : new Dictionary<string, bool>();
    }

    internal static void Update()
    {
        for (int i = 0; i < Updating.Count; i++)
        {
            var feature = Updating[i];
            if (!feature.Enabled)
                continue;
            try
            {
                feature.OnUpdate();
            }
            catch (Exception e)
            {
                _log.Error($"Feature {Key(feature)} failed and was switched off", e);
                Run(feature, feature.Disable, "switching off");
            }
        }
    }

    private static void Run(ModFeature feature, Action action, string what)
    {
        try
        {
            action();
        }
        catch (Exception e)
        {
            _log.Error($"{what} {Key(feature)} failed", e);
            if (feature.Enabled)
            {
                try { feature.Disable(); } catch { }
            }
        }
    }

    private static string Key(ModFeature f) => f.Owner.Info.Id + "/" + f.Id;

    private static void Save()
    {
        try { JsonFile.Write(_path, _choices); }
        catch (Exception e) { _log.Error("Couldn't save feature choices", e); }
    }
}
