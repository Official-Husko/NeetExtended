using System.ComponentModel;
using NeetModKit.Settings;

namespace EmailExpansion;

/// <summary>Stored in Mods/EmailExpansion/config.json and shown in the mod menu.</summary>
public sealed class EmailConfig
{
    [Label("Fewest minutes between emails")]
    [Description("Random new emails arrive every so often while you play: at least this many minutes apart.")]
    [Range(1, 60)]
    [Shared]
    public int MinMinutes = 4;

    [Label("Most minutes between emails")]
    [Description("And at most this many minutes apart.")]
    [Range(1, 120)]
    [Shared]
    public int MaxMinutes = 10;
}
