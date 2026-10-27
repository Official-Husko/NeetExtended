using System.ComponentModel;
using NeetModKit.Settings;

namespace PumpkinSmash;

public sealed class PumpkinSmashConfig
{
    [Label("Round length (seconds)")]
    [Range(10, 120)]
    [Shared]
    public int RoundSeconds = 30;

    [Label("Cash per pumpkin")]
    [Description("Paid at the end of a round, taxed like other income.")]
    [Range(0f, 2f)]
    [Shared]
    public float CashPerPumpkin = 0.25f;
}
