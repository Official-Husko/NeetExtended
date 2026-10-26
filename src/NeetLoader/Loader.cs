using System.Linq;
using NeetLoader.Logging;
using NeetLoader.Mods;
using NeetLoader.Runtime;

namespace NeetLoader;

/// <summary>Startup sequence: logging, discovery, then the first load batch.</summary>
public static class Loader
{
    public const string Version = "0.1.0";

    private static readonly ModLogger Log = new ModLogger("NeetLoader");

    internal static void Start()
    {
        LoaderPaths.Init();
        LogHub.Init(LoaderPaths.LogsDir);
        ModRegistry.Init(Log);
        if (ModRegistry.Settings.VerboseLog)
            LogHub.MinFileLevel = LogLevel.Debug;

        Log.Info($"NeetLoader {Version} starting in {LoaderPaths.GameDir}");

        ModLifecycle.Init(ModHost.Create(Log), Log);

        var found = ModDiscovery.Scan(LoaderPaths.ModsDir, Log);
        foreach (var mod in found)
            ModRegistry.Add(mod);
        // Only once every mod is known: a mod an essential mod needs stays on even if switched off.
        foreach (var mod in found)
        {
            if (!ModRegistry.IsEnabled(mod.Id))
                mod.State = ModState.Disabled;
        }

        ModLifecycle.LoadBatch(found.Where(m => m.State == ModState.Pending).ToList(), atRuntime: false);

        foreach (var mod in found.Where(m => m.State == ModState.Failed))
            Log.Error($"{mod}: {mod.Error}");

        Log.Info($"Loaded {found.Count(m => m.State == ModState.Loaded)} of {found.Count} mods");
        LogHub.Flush();
    }
}
