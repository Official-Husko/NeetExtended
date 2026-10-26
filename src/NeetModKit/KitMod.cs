using NeetLoader;
using NeetLoader.Config;
using NeetModKit.Changelog;
using NeetModKit.Events;
using NeetModKit.Menu;
using NeetModKit.Menu.Pages;
using NeetModKit.Settings;
using UnityEngine.SceneManagement;

namespace NeetModKit;

/// <summary>Entry point of the framework mod. Its Harmony patches are applied by the loader.</summary>
public sealed class KitMod : NeetMod
{
    internal static KitMod Instance { get; private set; }
    internal static ConfigFile<KitConfig> Config { get; private set; }

    private bool _whatsNewChecked;

    public override void OnLoad()
    {
        Instance = this;
        Config = LoadConfig<KitConfig>();
        Config.Save(); // Write defaults so the file exists for editing.

        GameEvents.Init(Log);
        Saves.ModSaveData.Init(Log);
        ModMenu.Init();

        ModMenu.AddPage(this, new ModsPage());
        ModMenu.AddPage(this, new WhatsNewPage());
        ModMenu.AddPage(this, new SettingsPage());
        ModMenu.AddPage(this, new DevToolsPage());
        ModSettings.Register(this, Config, "Mod menu");

        Log.Info($"Ready. Press {Config.Value.MenuKey} for the mod menu.");
    }

    public override void OnUpdate() => ModMenu.Update();

    public override void OnGUI() => ModMenu.OnGUI();

    public override void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!_whatsNewChecked)
        {
            _whatsNewChecked = true;
            ShowWhatsNewIfUpdated();
        }
    }

    private void ShowWhatsNewIfUpdated()
    {
        string version = ChangelogFeed.PackVersion();
        if (version == null || version == Config.Value.LastSeenVersion)
            return;

        Config.Value.LastSeenVersion = version;
        Config.Save();
        if (Config.Value.ShowWhatsNewOnUpdate)
            ModMenu.Open(WhatsNewPage.PageTitle);
    }
}
