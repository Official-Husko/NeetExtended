using UnityEngine;

namespace Mixtapes.Tapes;

/// <summary>A song file as a cassette.</summary>
public sealed class Tape
{
    /// <summary>What's written on it; the item is "Mixtape: " + Name.</summary>
    public string Name;

    /// <summary>Full path of the song.</summary>
    public string File;

    /// <summary>Where it came from ("ModId/mixtapes/song.ogg"), for messages.</summary>
    public string Source;

    /// <summary>Store text, or null for the default.</summary>
    public string Description;

    public float Price;

    /// <summary>Sold in the computer's store.</summary>
    public bool Sold = true;

    /// <summary>A room one copy is left in, once per save, or null.</summary>
    public string Room;

    /// <summary>Tints the cassette, or null for the game's colour.</summary>
    public Color? Color;

    public const string ItemPrefix = "Mixtape: ";

    public string ItemName => ItemPrefix + Name;
}
