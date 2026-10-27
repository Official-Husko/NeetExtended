using UnityEngine;

namespace Coop.World.Streaming;

internal struct BodySample
{
    public Vector3 Position;
    public Quaternion Rotation;
}

/// <summary>
/// Where a replica should be. Samples are stamped with the owner's clock and played back at
/// <c>RemoteClock.PlaybackTime(owner)</c>, the same timeline as that player's body.
/// </summary>
internal sealed class BodyBuffer
{
    private readonly TimedRing<BodySample> _samples = new TimedRing<BodySample>(4);

    public bool IsEmpty => _samples.IsEmpty;

    public float NewestTime => _samples.NewestTime;

    public void Clear() => _samples.Clear();

    public bool Add(float senderTime, Vector3 position, Quaternion rotation) =>
        _samples.Add(senderTime, new BodySample { Position = position, Rotation = rotation });

    /// <summary>Starts over at one pose (a snap, a fresh replica).</summary>
    public void Reset(float senderTime, Vector3 position, Quaternion rotation)
    {
        _samples.Clear();
        Add(senderTime, position, rotation);
    }

    public bool TryNewest(out Vector3 position, out Quaternion rotation)
    {
        if (_samples.IsEmpty)
        {
            position = default;
            rotation = Quaternion.identity;
            return false;
        }
        position = _samples.Newest.Position;
        rotation = _samples.Newest.Rotation;
        return true;
    }

    public void Sample(float playbackTime, out Vector3 position, out Quaternion rotation)
    {
        _samples.Find(playbackTime, out int older, out int newer, out float t);
        ref var a = ref _samples.At(older);
        ref var b = ref _samples.At(newer);
        position = Vector3.LerpUnclamped(a.Position, b.Position, t);
        rotation = Quaternion.SlerpUnclamped(a.Rotation, b.Rotation, t);
    }

    /// <summary>True once playback is past the newest sample by <paramref name="after"/> seconds.</summary>
    public bool IsSettled(float playbackTime, float after) => playbackTime - NewestTime > after;
}
