using System.ComponentModel;
using NeetModKit.Settings;

namespace Mixtapes;

/// <summary>Stored in Mods/Mixtapes/config.json and shown in the mod menu.</summary>
public sealed class MixtapesConfig
{
    [Label("Tape price")]
    [Description("What a mixtape costs in the store, unless its tapes.jsonc entry says otherwise.")]
    [Range(0, 200)]
    public int Price = 15;
}
