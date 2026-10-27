using System;
using NeetLoader.Runtime;
using UnityEngine;

namespace NeetModKit.Game;

/// <summary>
/// The thunder loop's wait between strikes. The kit's patch on <c>HouseManager.Thunder</c> swaps the
/// game's <c>new WaitForSeconds(Random.Range(30f, 180f))</c> for <see cref="Delay"/> and
/// <see cref="Wait"/>: the delay comes from the mod that set a provider (the game's own range when none
/// did), and the wait can be asked about and shortened or lengthened while it runs.
/// </summary>
internal static class ThunderTiming
{
    // A strike takes about a flash (2 s at most), a fade (2 s) and a clap (up to 2.2 s) after the wait.
    private const float StrikeLasts = 10f;
    // The wait ends a frame after its time; much later means nobody is running the loop.
    private const float Overdue = 2f;

    private static Func<float, float, float> _provider;
    private static ITracker _providerOwner;
    private static ThunderWait _wait;
    private static float _endedAt = float.NegativeInfinity;
    private static bool _providerFailed;

    /// <summary>Waits the game's loop is running right now; replaced when the loop sets up its next one.</summary>
    private sealed class ThunderWait : CustomYieldInstruction
    {
        public float Due;

        public override bool keepWaiting
        {
            get
            {
                if (Time.time < Due)
                    return true;
                if (_wait == this)
                {
                    _wait = null;
                    _endedAt = Time.time;
                }
                return false;
            }
        }
    }

    /// <summary>Called by the patched loop in place of <c>Random.Range(min, max)</c>.</summary>
    public static float Delay(float min, float max)
    {
        var provider = _provider;
        if (provider != null && WorldRole.RunsWorld)
        {
            try
            {
                float seconds = provider(min, max);
                if (!float.IsNaN(seconds) && !float.IsInfinity(seconds))
                    return Mathf.Max(1f, seconds);
            }
            catch (Exception e)
            {
                if (!_providerFailed)
                {
                    _providerFailed = true;
                    KitMod.Instance?.Log.Warn($"The thunder delay provider threw, using the game's own: {e.Message}");
                }
            }
        }
        return UnityEngine.Random.Range(min, max);
    }

    /// <summary>Called by the patched loop in place of <c>new WaitForSeconds(seconds)</c>.</summary>
    public static object Wait(float seconds)
    {
        if (!WorldRole.RunsWorld)
            return new WaitForSeconds(seconds); // A co-op client's loop never gets past this wait.
        _wait = new ThunderWait { Due = Time.time + seconds };
        return _wait;
    }

    public static void SetProvider(ITracker owner, Func<float, float, float> provider)
    {
        _provider = provider;
        _providerOwner = provider != null ? owner : null;
        _providerFailed = false;
        if (provider != null)
            owner.Track(Disposable.Create(() => ClearProvider(owner)));
    }

    private static void ClearProvider(ITracker owner)
    {
        if (_providerOwner == owner)
        {
            _provider = null;
            _providerOwner = null;
        }
    }

    /// <summary>Seconds until the loop's next strike, or -1 when it isn't waiting for one.</summary>
    public static float NextIn => _wait != null && WorldRole.RunsWorld && Time.time < _wait.Due + Overdue ? Mathf.Max(0f, _wait.Due - Time.time) : -1f;

    /// <summary>Moves the next strike to <paramref name="seconds"/> from now. False when the loop isn't waiting.</summary>
    public static bool StrikeIn(float seconds)
    {
        if (_wait == null || !WorldRole.RunsWorld)
            return false;
        _wait.Due = Time.time + Mathf.Max(0f, seconds);
        return true;
    }

    /// <summary>Asks the provider (or the game's range) for a fresh delay and moves the next strike to it.</summary>
    public static bool Reschedule() => StrikeIn(Delay(30f, 180f));

    /// <summary>Whether a loop is running: waiting for its next strike or in the middle of one.</summary>
    public static bool LoopAlive
    {
        get
        {
            if (_wait != null)
                return Time.time < _wait.Due + Overdue;
            return Time.time - _endedAt < StrikeLasts;
        }
    }

    public static void Reset()
    {
        _wait = null;
        _endedAt = float.NegativeInfinity;
    }

    public static void Shutdown() => _provider = null;
}
