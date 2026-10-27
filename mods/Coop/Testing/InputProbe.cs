using UnityEngine;

namespace Coop.Testing;

/// <summary>
/// For tests (CoopApi.ProbeInput): logs every hand or use press of this game's player with what the
/// click would hit, and what the hands hold a moment later, to see where an interaction stops.
/// </summary>
internal static class InputProbe
{
    private static bool _on;
    private static float _checkAt = -1f;

    public static void Set(bool on)
    {
        if (on == _on)
            return;
        _on = on;
        if (on)
            CoopLoop.Update += Tick;
        else
            CoopLoop.Update -= Tick;
    }

    private static void Tick()
    {
        var player = HouseManager._ != null ? HouseManager._.player : null;
        if (player == null || !player.TryGetComponent(out BrainPlayer brain) || brain.intents == null)
            return;
        var intents = brain.intents;
        string pressed = intents.LeftHand.Started ? "left hand" : intents.RightHand.Started ? "right hand" : intents.Use.Started ? "use" : null;
        if (pressed != null)
        {
            CoopMod.Log.Info($"[probe] {pressed} pressed, looking at: {CoopApi.LookAt()}");
            _checkAt = Time.time + 0.3f;
        }
        if (_checkAt > 0f && Time.time >= _checkAt)
        {
            _checkAt = -1f;
            var hands = player.GetComponent<Player_Abilities>().pickups;
            CoopMod.Log.Info($"[probe] holding: {Name(hands, 0)} / {Name(hands, 1)}");
        }
    }

    private static string Name(GameObject[] hands, int i) =>
        hands != null && i < hands.Length && hands[i] != null ? hands[i].name : "nothing";
}
