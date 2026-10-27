using UnityEngine;

namespace Coop.Players;

/// <summary>
/// The last few poses of one remote player, in the sender's time. Played back a little in the past
/// (<see cref="Coop.Net.RemoteClock"/>) so there is always a pose on each side to blend between.
/// </summary>
internal sealed class PoseBuffer
{
    private const int Size = 8;

    private readonly PlayerPose[] _poses = new PlayerPose[Size];
    private int _count;
    private int _newest = -1;

    public bool IsEmpty => _count == 0;

    public void Add(in PlayerPose pose)
    {
        if (_count > 0 && pose.Time <= _poses[_newest].Time)
            return; // Old or repeated.

        _newest = (_newest + 1) % Size;
        _poses[_newest] = pose;
        if (_count < Size)
            _count++;
    }

    /// <summary>The pose at sender time <paramref name="t"/>, blended, or the nearest one at the ends.</summary>
    public PlayerPose Sample(float t)
    {
        ref var newest = ref _poses[_newest];
        if (t >= newest.Time)
            return newest;

        // Walk back from the newest to find the pair around t.
        for (int i = 1; i < _count; i++)
        {
            ref var older = ref _poses[(_newest - i + Size) % Size];
            ref var newer = ref _poses[(_newest - i + 1 + Size) % Size];
            if (t >= older.Time)
                return PlayerPose.Lerp(older, newer, Mathf.InverseLerp(older.Time, newer.Time, t));
        }
        return _poses[(_newest - _count + 1 + Size) % Size];
    }
}
