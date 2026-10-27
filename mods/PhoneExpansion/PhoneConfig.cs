using System.ComponentModel;
using NeetModKit.Settings;

namespace PhoneExpansion;

/// <summary>Stored in Mods/PhoneExpansion/config.json and shown in the mod menu.</summary>
public sealed class PhoneConfig
{
    [Label("New call chance")]
    [Description("When the phone rings by itself, the chance (in %) that it's one of the new calls instead of one of the game's prank calls.")]
    [Range(0, 100)]
    [Shared]
    public int NewCallChance = 50;
}
