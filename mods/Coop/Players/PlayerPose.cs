using LiteNetLib.Utils;
using UnityEngine;

namespace Coop.Players;

/// <summary>
/// Where a player is and what their body is doing, sent about 30 times a second. Animator bools
/// travel as a bitmask in the order of the animator's own parameter list (<see cref="AnimatorBools"/>).
/// </summary>
internal struct PlayerPose
{
    /// <summary>The sender's Time.time when it was taken.</summary>
    public float Time;
    public Vector3 Position;
    public float Yaw;
    public float Pitch;
    public ushort Bools;
    public float AnimSpeed;

    public void Write(NetDataWriter writer, byte slot)
    {
        writer.Put(slot);
        writer.Put(Time);
        writer.Put(Position.x);
        writer.Put(Position.y);
        writer.Put(Position.z);
        writer.Put(Yaw);
        writer.Put(Pitch);
        writer.Put(Bools);
        writer.Put((byte)Mathf.Clamp(Mathf.RoundToInt(AnimSpeed * 50f), 0, 255));
    }

    public static PlayerPose Read(NetDataReader reader, out byte slot)
    {
        slot = reader.GetByte();
        return new PlayerPose
        {
            Time = reader.GetFloat(),
            Position = new Vector3(reader.GetFloat(), reader.GetFloat(), reader.GetFloat()),
            Yaw = reader.GetFloat(),
            Pitch = reader.GetFloat(),
            Bools = reader.GetUShort(),
            AnimSpeed = reader.GetByte() / 50f,
        };
    }

    public static PlayerPose Lerp(in PlayerPose a, in PlayerPose b, float t) => new PlayerPose
    {
        Time = Mathf.Lerp(a.Time, b.Time, t),
        Position = Vector3.Lerp(a.Position, b.Position, t),
        Yaw = Mathf.LerpAngle(a.Yaw, b.Yaw, t),
        Pitch = Mathf.LerpAngle(a.Pitch, b.Pitch, t),
        Bools = t < 0.5f ? a.Bools : b.Bools,
        AnimSpeed = t < 0.5f ? a.AnimSpeed : b.AnimSpeed,
    };
}
