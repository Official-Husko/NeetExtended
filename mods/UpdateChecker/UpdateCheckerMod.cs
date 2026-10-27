using NeetLoader;
using NeetLoader.Config;
using NeetModKit.Menu;
using NeetModKit.Settings;
using UpdateChecker.Check;
using UpdateChecker.Menu;
using UpdateChecker.Notice;
using UpdateChecker.Rules;

namespace UpdateChecker;

/// <summary>
/// Looks on GitHub for a newer release at start and then every hour. While one is out, the version
/// in the corner says so, and (if the player allows it) the game sends an email and rings the phone
/// about it, a few times per save at most.
/// </summary>
public sealed class UpdateCheckerMod : NeetMod
{
    internal static UpdateCheckerMod Instance { get; private set; }
    internal static ConfigFile<UpdateCheckerConfig> Config { get; private set; }

    private bool _wasChecking;

    public override void OnLoad()
    {
        Instance = this;
        Config = LoadConfig<UpdateCheckerConfig>();
        Config.Save();
        ModSettings.Register(this, Config, "Update Checker");
        _wasChecking = Config.Value.Check;
        Config.Changed += OnConfigChanged;

        UpdateRules.Register(this);
        CornerNote.Init(this);
        UpdateNotice.Init(this);
        ModMenu.AddPage(this, new UpdatePage(this));

        CheckSchedule.Start(this);
    }

    public override void OnUpdate() => UpdateNotice.Tick();

    public override void OnUnload()
    {
        Config.Changed -= OnConfigChanged;
        Instance = null;
    }

    private void OnConfigChanged(UpdateCheckerConfig config)
    {
        if (config.Check == _wasChecking)
            return;
        _wasChecking = config.Check;
        if (config.Check)
            CheckSchedule.CheckNow(this);
        else
            UpdateState.Clear();
    }
}
