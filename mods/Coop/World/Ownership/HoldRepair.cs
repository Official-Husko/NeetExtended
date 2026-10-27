using System.Collections.Generic;
using Coop.Session;
using Coop.World.Objects;
using UnityEngine;

namespace Coop.World.Ownership;

/// <summary>
/// Lets go of holds the game lost track of. The game marks what a hand grabs with Usable.player,
/// always together with putting it in Player_Abilities.pickups, and refuses to pick up anything whose
/// player is still set (Player_Abilities.Pickup). A drag or hold that ends without the game's own
/// release (a hot reload in the middle of it, a forced hand-back) leaves player set with nothing in
/// the hand, and that item or door can't be grabbed again by anyone until the game restarts. Once a
/// second this looks at every shared item; one that is marked as held by a player whose hands don't
/// hold it, on two checks in a row, is released the game's own way (ResetPlayerRef, which also clears
/// world sync's holder).
/// </summary>
internal static class HoldRepair
{
    private const float Interval = 1f;

    private static readonly HashSet<int> Suspects = new HashSet<int>();
    private static readonly HashSet<int> NextSuspects = new HashSet<int>();
    private static readonly HashSet<int> Reported = new HashSet<int>();
    private static float _next;

    public static void Init()
    {
        CoopLoop.Update += Tick;
        CoopSession.Ended += _ => Reset();
    }

    private static void Tick()
    {
        if (!WorldSide.Running || Time.time < _next)
            return;
        _next = Time.time + Interval;

        NextSuspects.Clear();
        var all = NetObjects.All;
        for (int i = 0; i < all.Count; i++)
        {
            var usable = all[i].Usable;
            if (usable == null || usable.player == null || IsHolding(usable.player, usable.gameObject))
                continue;
            int id = usable.GetInstanceID();
            if (!Suspects.Contains(id))
            {
                NextSuspects.Add(id); // Give it one more check; it may be mid-grab.
                continue;
            }
            string holder = usable.player.name;
            usable.ResetPlayerRef();
            if (Reported.Add(id))
                CoopMod.Log.Info($"World: {usable.name} was marked as held by {holder} with nothing in hand; let go of it so it can be picked up again.");
        }
        Suspects.Clear();
        Suspects.UnionWith(NextSuspects);
    }

    private static bool IsHolding(GameObject player, GameObject item)
    {
        if (!player.TryGetComponent(out Player_Abilities hands) || hands.pickups == null)
            return false;
        var held = hands.pickups;
        for (int i = 0; i < held.Length; i++)
        {
            if (held[i] == item)
                return true;
        }
        return false;
    }

    private static void Reset()
    {
        Suspects.Clear();
        NextSuspects.Clear();
        Reported.Clear();
    }
}
