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

        GameEvents.Init(this);
        GameEvents.SceneLoaded.Subscribe(this, _ => Ui.Toolkit.UiInputLock.OnSceneLoaded());
        GameEvents.SceneLoaded.Subscribe(this, _ => Game.SharedState.Clear());
        Saves.ModSaveData.Init(Log);
        Saves.SaveSource.RestoreAfterReload();
        Game.PlayerControl.RestoreAfterReload();
        Items.ItemRegistry.Init(Log);
        Conversations.InkStory.Init(Log);
        Conversations.InkFunctions.Init(this);
        Conversations.InkScripts.Init(this);
        Rules.RuleSystem.Init(this);
        GameEvents.ItemsRegistering.Subscribe(this, Items.ItemRegistry.RegisterAll);
        ModMenu.Init(this);

        ModMenu.AddPage(this, new ModsPage());
        ModMenu.AddPage(this, new WhatsNewPage());
        ModMenu.AddPage(this, new PcAppsPage());
        ModMenu.AddPage(this, new SettingsPage());
        ModMenu.AddPage(this, new ErrorsPage());
        ModSettings.Register(this, Config, About.PackInfo.Name);
        About.GameCompat.Check(this);
        Ui.VersionTag.Create(this, Config.Value.ShowVersion);
        Config.Changed += OnConfigChanged;

        Features.ModFeatures.Init(this);
        Desktop.ComputerApps.Init(this);
        Features.ModFeatures.Register(this, new Features.Builtin.PauseMenuModsFeature());

        Items.StoreListings.RestoreAfterReload();
        ModMenu.ReopenIfReloaded();

        Log.Info($"Ready. Press {Config.Value.MenuKey} for the mod menu.");
    }

    private static void OnConfigChanged(KitConfig config) => Ui.VersionTag.SetVisible(config.ShowVersion);

    /// <summary>The framework can be reloaded too; leave nothing behind for the next copy to trip over.</summary>
    public override void OnUnload()
    {
        Config.Changed -= OnConfigChanged;
        Saves.SaveSource.KeepForReload();
        Game.PlayerControl.KeepForReload();
        ModMenu.Shutdown();
        Items.StoreListings.KeepForReload();
        Items.PrefabStash.Release();
        Instance = null;
    }

    public override void OnUpdate()
    {
        ModMenu.Update();
        Features.ModFeatures.Update();
        IO.FileWatch.TickAll();
        IO.ModContent.TickAll();
    }

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
