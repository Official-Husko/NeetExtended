using System.ComponentModel;
using NeetModKit.Settings;

namespace CardExpansion;

/// <summary>Stored in Mods/CardExpansion/config.json and shown in the mod menu.</summary>
public sealed class CardExpansionConfig
{
    [Description("Crystals one summon costs.")]
    [Range(50, 5000)]
    public int SummonCost = 500;

    [Label("Summon payout")]
    [Description("A normal summon pays this many bars of the summoned card's income.")]
    [Range(1, 10)]
    public int SummonPayoutBars = 3;

    [Label("Shiny chance")]
    [Description("Chance that a summon is shiny and gives the card 5 free levels.")]
    [Range(0f, 0.5f)]
    public float ShinyChance = 0.05f;

    [Label("Auto-clicker price")]
    [Description("One-time price in crystals.")]
    [Range(500, 50000)]
    public int AutoClickerCost = 5000;

    [Label("Auto-clicks per second")]
    [Range(0.5f, 10f)]
    public float AutoClicksPerSecond = 1f;

    [Label("Dollars per 10K crystals")]
    [Description("What the Convert button pays. The game pays $3.")]
    [Range(1f, 50f)]
    public float DollarsPer10K = 5f;
}
