using NeetLoader;
using NeetModKit.Events;

namespace NeetModKit;

/// <summary>Entry point of the framework mod. Its Harmony patches are applied by the loader.</summary>
public sealed class KitMod : NeetMod
{
    internal static KitMod Instance { get; private set; }

    public override void OnLoad()
    {
        Instance = this;
        GameEvents.Init(Log);
        Log.Info("Ready");
    }
}
