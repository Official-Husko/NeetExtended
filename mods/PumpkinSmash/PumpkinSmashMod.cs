using NeetLoader;
using NeetLoader.Config;
using NeetModKit.Desktop;
using NeetModKit.Game;
using NeetModKit.Saves;
using NeetModKit.Settings;
using PumpkinSmash.Game;
using PumpkinSmash.UI;
using UnityEngine;

namespace PumpkinSmash;

/// <summary>Best score kept in the save file.</summary>
public sealed class SmashRecord
{
    public int BestScore;
    public int Rounds;
}

/// <summary>Registers the game as a computer app and runs a round while the page is open.</summary>
public sealed class PumpkinSmashMod : NeetMod
{
    private readonly SmashRound _round = new SmashRound();
    private SmashBoard _board;

    internal static PumpkinSmashMod Instance { get; private set; }
    internal ConfigFile<PumpkinSmashConfig> Config { get; private set; }
    internal SmashRecord Best { get; private set; } = new SmashRecord();

    public override void OnLoad()
    {
        Instance = this;
        Config = LoadConfig<PumpkinSmashConfig>();
        Config.Save();
        ModSettings.Register(this, Config, "Pumpkin Smash");
        ModSaveData.Register(this, () => Best, record => Best = record);

        _board = new SmashBoard(_round);
        _round.Changed += _board.Refresh;
        _round.Finished += EndRound;

        ComputerApps.Register(this, new ComputerAppDefinition
        {
            Id = "PumpkinSmash",
            Title = "Pumpkin Smash",
            Description = "Whack-a-pumpkin. Smash as many as you can before time runs out.",
            Url = "pumpkin",
            Height = 120f,
            StartMenuLabel = "Smash",
            Build = _board.Build,
        });
    }

    public override void OnUnload() => Instance = null;

    public override void OnUpdate()
    {
        // Only play while the page is on screen; leaving the site pauses the round.
        if (_board.Page != null && _board.Page.activeInHierarchy)
            _round.Tick(Time.deltaTime);
    }

    private void EndRound(int score)
    {
        Best.Rounds++;
        if (score > Best.BestScore)
            Best.BestScore = score;

        float cash = score * Config.Value.CashPerPumpkin;
        if (cash > 0f && GameRefs.Computer != null)
            Money.Add(cash);
        Log.Info($"Round over: {score} pumpkins, ${cash:0.00}");
        _board.Refresh();
    }
}
