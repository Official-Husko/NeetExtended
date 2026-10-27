using System;
using NeetLoader.Runtime;

namespace NeetModKit.Game;

// The thunder timer: how long the loop waits between strikes, and a look at it.
public static partial class Sky
{
    /// <summary>
    /// Seconds until the random loop's next strike, or -1 when it isn't waiting for one (the loop
    /// is mid-strike, was stopped, or this is a co-op client).
    /// </summary>
    public static float NextStrikeIn => ThunderTiming.NextIn;

    /// <summary>
    /// Whether the random thunder loop is running (waiting for a strike or in one). It stops for good
    /// after the story's own strike unless the Unofficial NEET Patches' ThunderLoop fix is on.
    /// </summary>
    public static bool ThunderLoopAlive => ThunderTiming.LoopAlive;

    /// <summary>
    /// Takes over the wait between random strikes. <paramref name="provider"/> gets the game's range (30 and 180
    /// seconds) and returns the seconds to wait, at least 1; it's asked each time the loop sets up its next
    /// wait. Pass null to give the timer back. One provider at a time: setting another replaces it, and it
    /// goes away when <paramref name="owner"/> unloads. The wait running now isn't changed; see
    /// <see cref="StrikeIn"/> and <see cref="Reschedule"/>.
    /// </summary>
    public static void SetThunderDelay(ITracker owner, Func<float, float, float> provider)
    {
        if (owner == null)
            throw new ArgumentNullException(nameof(owner));
        ThunderTiming.SetProvider(owner, provider);
    }

    /// <summary>Moves the wait running now so the next strike comes in <paramref name="seconds"/>. False when the loop isn't waiting.</summary>
    public static bool StrikeIn(float seconds) => ThunderTiming.StrikeIn(seconds);

    /// <summary>Asks the delay provider (or the game's range) for a new wait and applies it to the one running now.</summary>
    public static bool Reschedule() => ThunderTiming.Reschedule();
}
