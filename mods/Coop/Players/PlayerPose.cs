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

    /// <summary>The whole body's rotation: upright it's just the facing, but a slipped or downed body lies.</summary>
    public Quaternion Rotation;
    public float Pitch;
    public ushort Bools;
    public float AnimSpeed;

    /// <summary>The ends of the first-person arms (where the hands are), relative to the body.</summary>
    public Vector3 LeftHand, RightHand;
    public Quaternion LeftHandTurn, RightHandTurn;

    /// <summary>Bit 0: the left hand shows, bit 1: the right one.</summary>
    public byte HandsShown;

    public void Write(NetDataWriter writer, byte slot)
    {
        writer.Put(slot);
        writer.Put(Time);
        writer.Put(Position.x);
        writer.Put(Position.y);
        writer.Put(Position.z);
        writer.Put(Rotation.x);
        writer.Put(Rotation.y);
        writer.Put(Rotation.z);
        writer.Put(Rotation.w);
        writer.Put(Pitch);
        writer.Put(Bools);
        writer.Put((byte)Mathf.Clamp(Mathf.RoundToInt(AnimSpeed * 50f), 0, 255));
        PutHand(writer, LeftHand, LeftHandTurn);
        PutHand(writer, RightHand, RightHandTurn);
        writer.Put(HandsShown);
    }

    private static void PutHand(NetDataWriter writer, Vector3 at, Quaternion turn)
    {
        writer.Put(at.x);
        writer.Put(at.y);
        writer.Put(at.z);
        writer.Put(turn.x);
        writer.Put(turn.y);
        writer.Put(turn.z);
        writer.Put(turn.w);
    }

    private static Vector3 GetPoint(NetDataReader reader) => new Vector3(reader.GetFloat(), reader.GetFloat(), reader.GetFloat());

    // A broken rotation (a packet from another build, say) would make Unity throw when it's applied.
    private static Quaternion GetTurn(NetDataReader reader)
    {
        var turn = new Quaternion(reader.GetFloat(), reader.GetFloat(), reader.GetFloat(), reader.GetFloat());
        float length = Mathf.Sqrt(turn.x * turn.x + turn.y * turn.y + turn.z * turn.z + turn.w * turn.w);
        if (length < 0.5f || length > 1.5f || float.IsNaN(length))
            return Quaternion.identity;
        return new Quaternion(turn.x / length, turn.y / length, turn.z / length, turn.w / length);
    }

    public static PlayerPose Read(NetDataReader reader, out byte slot)
    {
        slot = reader.GetByte();
        return new PlayerPose
        {
            Time = reader.GetFloat(),
            Position = new Vector3(reader.GetFloat(), reader.GetFloat(), reader.GetFloat()),
            Rotation = GetTurn(reader),
            Pitch = reader.GetFloat(),
            Bools = reader.GetUShort(),
            AnimSpeed = reader.GetByte() / 50f,
            LeftHand = GetPoint(reader),
            LeftHandTurn = GetTurn(reader),
            RightHand = GetPoint(reader),
            RightHandTurn = GetTurn(reader),
            HandsShown = reader.GetByte(),
        };
    }

    public static PlayerPose Lerp(in PlayerPose a, in PlayerPose b, float t) => new PlayerPose
    {
        Time = Mathf.Lerp(a.Time, b.Time, t),
        Position = Vector3.Lerp(a.Position, b.Position, t),
        Rotation = Quaternion.Slerp(a.Rotation, b.Rotation, t),
        Pitch = Mathf.LerpAngle(a.Pitch, b.Pitch, t),
        Bools = t < 0.5f ? a.Bools : b.Bools,
        AnimSpeed = t < 0.5f ? a.AnimSpeed : b.AnimSpeed,
        LeftHand = Vector3.Lerp(a.LeftHand, b.LeftHand, t),
        RightHand = Vector3.Lerp(a.RightHand, b.RightHand, t),
        LeftHandTurn = Quaternion.Slerp(a.LeftHandTurn, b.LeftHandTurn, t),
        RightHandTurn = Quaternion.Slerp(a.RightHandTurn, b.RightHandTurn, t),
        HandsShown = t < 0.5f ? a.HandsShown : b.HandsShown,
    };
}
