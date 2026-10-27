using Coop.Session;
using UnityEngine;

namespace Coop.Net;

/// <summary>
/// Maps each player's game clock onto ours, so everything a player sends (their pose, the items they
/// move) plays back on the same timeline. Messages carry the sender's Time.time; the smallest gap seen
/// is roughly the one-way delay, and it drifts back up slowly so one lag spike doesn't stick.
/// </summary>
public static class RemoteClock
{
    /// <summary>How far behind the newest data remote things are shown, so there's always a pair to blend.</summary>
    public const float PlaybackDelay = 0.1f;

    private const int Slots = 256;
    private const float Drift = 0.01f;

    private static readonly float[] Offsets = new float[Slots];
    private static readonly bool[] Known = new bool[Slots];

    internal static void Init()
    {
        CoopSession.PlayerLeft += player => Known[player.Slot] = false;
        CoopSession.Ended += _ => System.Array.Clear(Known, 0, Slots);
    }

    /// <summary>Call for every timed message from <paramref name="slot"/>.</summary>
    public static void Observe(byte slot, float senderTime)
    {
        float gap = Time.time - senderTime;
        if (!Known[slot] || gap < Offsets[slot])
        {
            Offsets[slot] = gap;
            Known[slot] = true;
            return;
        }
        Offsets[slot] = Mathf.Lerp(Offsets[slot], gap, Drift);
    }

    /// <summary>The sender's clock as it is now, as far as we can tell.</summary>
    public static float SenderNow(byte slot) => Time.time - Offsets[slot];

    /// <summary>The sender time to show things from <paramref name="slot"/> at this frame.</summary>
    public static float PlaybackTime(byte slot) => SenderNow(slot) - PlaybackDelay;
}
