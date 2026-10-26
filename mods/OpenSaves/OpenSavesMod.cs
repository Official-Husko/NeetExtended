using NeetLoader;
using NeetLoader.Config;
using NeetModKit.Features;
using NeetModKit.Saves;
using NeetModKit.Settings;
using OpenSaves.Features;
using OpenSaves.Format;

namespace OpenSaves;

/// <summary>
/// Reading .nsav is always on while the mod is loaded, so a save it wrote can't become unreadable
/// by switching a feature off. Writing them and the cleanup are features.
/// </summary>
public sealed class OpenSavesMod : NeetMod
{
    internal static OpenSavesMod Instance { get; private set; }
    internal ConfigFile<OpenSavesConfig> Config { get; private set; }

    public override void OnLoad()
    {
        Instance = this;
        Config = LoadConfig<OpenSavesConfig>();
        Config.Save();
        ModSettings.Register(this, Config, "Open Saves");

        SaveFormats.Register(this, new NsavFormat());
        ModFeatures.Register(this, new CompressedSavesFeature());
        ModFeatures.Register(this, new SaveCleanupFeature());
    }

    public override void OnUnload() => Instance = null;
}
