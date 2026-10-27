using Mixtapes.Audio;
using NeetLoader.Runtime;
using NeetModKit.Items;
using UnityEngine;

namespace Mixtapes.Tapes;

/// <summary>
/// Each tape is an item of its own, a copy of the game's cassette that plays the tape's song.
/// Saves keep it by its item name; without the mod it's left out when the save loads.
/// </summary>
internal static class TapeItems
{
    public const string BasedOn = "Cassette";

    public static void Register(ITracker owner, Tape tape) =>
        ItemRegistry.Register(owner, new ItemDefinition
        {
            Name = tape.ItemName,
            BasedOn = BasedOn,
            Description = tape.Description ?? $"Somebody's mixtape. It says \"{tape.Name}\" on it in marker and it smells like a car. Put it in the radio and press play.",
            Price = tape.Price,
            Store = tape.Sold ? Usable.StoreType.Normal : Usable.StoreType.Nowhere,
            Tint = tape.Color,
            Setup = template => GiveSong(template, tape),
        });

    // The template gets the song when it has loaded; copies made after that have it from the start,
    // and copies already in the house (from a save loaded early) get it then.
    private static void GiveSong(GameObject template, Tape tape)
    {
        var cassette = template.GetComponentInChildren<Cassette>(true);
        SongLibrary.Get(tape.File, clip =>
        {
            if (clip == null)
                return;
            if (cassette != null)
                cassette.musicClip = clip;
            foreach (var placed in Object.FindObjectsOfType<Cassette>())
            {
                var usable = placed.GetComponent<Usable>();
                if (placed.musicClip == null && usable != null && usable.prefabName == tape.ItemName)
                    placed.musicClip = clip;
            }
        });
    }
}
