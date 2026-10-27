using System.Collections.Generic;
using NeetModKit.Game;
using UnityEngine;

namespace WeatherExpansion.Lightning;

/// <summary>
/// Now and then a strike trips a breaker in the fuse box: the lever flips off the way it does when a player
/// pushes it, the room loses its power (lamps, TV, whatever is plugged in) and stays dark until somebody
/// flips it back. Off by default. The fuse box is shared state, so only the game that runs the house does
/// it; co-op sends the lever to the others. The chance per strike is in Settings.
/// </summary>
internal sealed class FusePopFeature : StrikeFeature
{
    public const string FeatureId = "FusePop";

    // The rooms the fuse slots never switch (FuseBox_Slot.ValidatePowerState skips them).
    private static readonly HashSet<Location> NotOnTheBox = new HashSet<Location>
    {
        Location.Attic, Location.Backyard, Location.FrontYard, Location.Shed,
        Location.Basement_Backroom, Location.Dream, Location.Other,
    };

    private readonly List<FuseBox_Button> _candidates = new List<FuseBox_Button>();

    public override string Id => FeatureId;
    public override string Name => "Lightning pops a fuse";
    public override string Description =>
        "Experimental. A lightning strike can trip a breaker in the fuse box and cut a room's power until you " +
        "flip it back. Off by default; the chance per strike is in Settings.";

    public override bool EnabledByDefault => false;

    protected override void End() { }

    protected override void Strike(float power)
    {
        if (!WorldRole.RunsWorld)
            return;
        float chance = Mathf.Clamp01(WeatherExpansionMod.Config.Value.FusePopChance);
        if (Random.value >= chance)
            return;
        var box = HouseManager._ != null ? HouseManager._.fuseBox_Manager : null;
        if (box == null || box.fuseButtons == null)
            return;
        _candidates.Clear();
        foreach (var button in box.fuseButtons)
        {
            if (button != null && button.IsOn && Feeds(button))
                _candidates.Add(button);
        }
        if (_candidates.Count == 0)
            return;
        var pick = _candidates[Random.Range(0, _candidates.Count)];
        pick.IsOn = false;
        AudioSource.PlayClipAtPoint(CrackleClip.Get(), pick.transform.position, Mathf.Clamp01(0.8f * Mathf.Min(power, 1.4f)));
        WeatherExpansionMod.Instance.Log.Info($"Lightning tripped the breaker for {pick.room}.");
    }

    // A breaker is worth tripping when its slot has a fuse with charge in it and the room has something plugged in.
    private static bool Feeds(FuseBox_Button button)
    {
        if (NotOnTheBox.Contains(button.room))
            return false;
        var slot = button.GetComponentInParent<FuseBox_Slot>();
        if (slot == null || slot.insertedFuse == null || !slot.insertedFuse.HasPower)
            return false;
        var room = Locator.GetRoom(button.room);
        return room != null && room.electronics != null && room.electronics.Count > 0;
    }
}
