using HarmonyLib;

namespace Coop.Players;

/// <summary>
/// Kicks travel in the pose as a counter: this game's player kicking bumps it (the game fires the
/// animator's Kick trigger itself), and another player's body plays the kick when the counter it
/// shows goes up. A counter, not an event, so a lost packet only makes a kick late, never missing.
/// </summary>
[HarmonyPatch]
internal static class KickSignal
{
    /// <summary>The kicks this game's player has made; wraps around.</summary>
    public static byte Count { get; private set; }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Player_Abilities), nameof(Player_Abilities.Kick))]
    private static void Kicked(Player_Abilities __instance)
    {
        if (HouseManager._ != null && __instance.gameObject == HouseManager._.player)
            Count++;
    }

    /// <summary>True when a body that showed <paramref name="seen"/> kicks has one to play now.</summary>
    public static bool IsNew(byte seen, byte now)
    {
        // Counts only move forward, a few at a time; anything else is a restart and not replayed.
        int ahead = (now - seen + 256) % 256;
        return ahead > 0 && ahead < 8;
    }
}
