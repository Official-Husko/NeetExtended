using Coop.World.Objects;
using LiteNetLib.Utils;
using UnityEngine;

namespace Coop.Players;

/// <summary>
/// Where a player is and what their body is doing, sent about 30 times a second. Animator bools
/// travel as a bitmask in the order of the animator's own parameter list (<see cref="AnimatorBools"/>).
/// </summary>
/// <remarks>
/// Layout: slot, time, position (3 floats), rotation (4 floats), pitch (short, 1/100 degree, signed),
/// bools (ushort), animator speed (byte, 1/50), kick counter (byte), hand flags (byte), then for each
/// shown hand (left first) its position relative to the body (3 shorts, millimetres) and turn (4
/// shorts, 1/32767), then for each hand that holds a shared thing (left first) which one (kind byte,
/// int id). A hand that isn't shown is not sent.
/// </remarks>
internal struct PlayerPose
{
    private const float PointScale = 1000f;
    private const float TurnScale = 32767f;

    public const byte LeftShown = 1;
    public const byte RightShown = 2;
    public const byte LeftHolds = 4;
    public const byte RightHolds = 8;

    /// <summary>The sender's Time.time when it was taken.</summary>
    public float Time;
    public Vector3 Position;

    /// <summary>The whole body's rotation: upright it's just the facing, but a slipped or downed body lies.</summary>
    public Quaternion Rotation;

    /// <summary>Where the player looks up or down in degrees: negative looks up, positive looks down.</summary>
    public float Pitch;
    public ushort Bools;
    public float AnimSpeed;

    /// <summary>Counts the sender's kicks; a body plays a kick when the number it shows goes up.</summary>
    public byte Kicks;

    /// <summary>The ends of the first-person arms (where the hands are), relative to the body.</summary>
    public Vector3 LeftHand, RightHand;
    public Quaternion LeftHandTurn, RightHandTurn;

    /// <summary>
    /// Bit 0: the left hand is raised (held item, reaching, punching), bit 1: the right one. Bits 2 and 3:
    /// the left or right hand holds a shared thing, named in <see cref="HeldLeft"/> or <see cref="HeldRight"/>.
    /// </summary>
    public byte HandsShown;

    public NetRef HeldLeft, HeldRight;

    public bool IsShown(byte hand) => (HandsShown & hand) != 0;

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
        writer.Put((short)Mathf.Clamp(Mathf.RoundToInt(Pitch * 100f), short.MinValue, short.MaxValue));
        writer.Put(Bools);
        writer.Put((byte)Mathf.Clamp(Mathf.RoundToInt(AnimSpeed * 50f), 0, 255));
        writer.Put(Kicks);
        writer.Put(HandsShown);
        if (IsShown(LeftShown))
            PutHand(writer, LeftHand, LeftHandTurn);
        if (IsShown(RightShown))
            PutHand(writer, RightHand, RightHandTurn);
        if ((HandsShown & LeftHolds) != 0)
            HeldLeft.Write(writer);
        if ((HandsShown & RightHolds) != 0)
            HeldRight.Write(writer);
    }

    private static void PutHand(NetDataWriter writer, Vector3 at, Quaternion turn)
    {
        writer.Put(Short(at.x * PointScale));
        writer.Put(Short(at.y * PointScale));
        writer.Put(Short(at.z * PointScale));
        writer.Put(Short(turn.x * TurnScale));
        writer.Put(Short(turn.y * TurnScale));
        writer.Put(Short(turn.z * TurnScale));
        writer.Put(Short(turn.w * TurnScale));
    }

    private static short Short(float value) => (short)Mathf.Clamp(Mathf.RoundToInt(value), short.MinValue, short.MaxValue);

    private static Vector3 GetPoint(NetDataReader reader) =>
        new Vector3(reader.GetShort() / PointScale, reader.GetShort() / PointScale, reader.GetShort() / PointScale);

    // A broken rotation (a packet from another build, say) would make Unity throw when it's applied.
    private static Quaternion GetTurn(NetDataReader reader)
    {
        var turn = new Quaternion(reader.GetShort() / TurnScale, reader.GetShort() / TurnScale, reader.GetShort() / TurnScale, reader.GetShort() / TurnScale);
        return Normalized(turn);
    }

    private static Quaternion Normalized(Quaternion turn)
    {
        float length = Mathf.Sqrt(turn.x * turn.x + turn.y * turn.y + turn.z * turn.z + turn.w * turn.w);
        if (length < 0.5f || length > 1.5f || float.IsNaN(length))
            return Quaternion.identity;
        return new Quaternion(turn.x / length, turn.y / length, turn.z / length, turn.w / length);
    }

    private static Quaternion GetBodyTurn(NetDataReader reader) =>
        Normalized(new Quaternion(reader.GetFloat(), reader.GetFloat(), reader.GetFloat(), reader.GetFloat()));

    public static PlayerPose Read(NetDataReader reader, out byte slot)
    {
        slot = reader.GetByte();
        var pose = new PlayerPose
        {
            Time = reader.GetFloat(),
            Position = new Vector3(reader.GetFloat(), reader.GetFloat(), reader.GetFloat()),
            Rotation = GetBodyTurn(reader),
            Pitch = reader.GetShort() / 100f,
            Bools = reader.GetUShort(),
            AnimSpeed = reader.GetByte() / 50f,
            Kicks = reader.GetByte(),
            HandsShown = reader.GetByte(),
            LeftHandTurn = Quaternion.identity,
            RightHandTurn = Quaternion.identity,
        };
        if (pose.IsShown(LeftShown))
        {
            pose.LeftHand = GetPoint(reader);
            pose.LeftHandTurn = GetTurn(reader);
        }
        if (pose.IsShown(RightShown))
        {
            pose.RightHand = GetPoint(reader);
            pose.RightHandTurn = GetTurn(reader);
        }
        if ((pose.HandsShown & LeftHolds) != 0)
            pose.HeldLeft = NetRef.Read(reader);
        if ((pose.HandsShown & RightHolds) != 0)
            pose.HeldRight = NetRef.Read(reader);
        return pose;
    }

    public static PlayerPose Lerp(in PlayerPose a, in PlayerPose b, float t)
    {
        var pose = new PlayerPose
        {
            Time = Mathf.Lerp(a.Time, b.Time, t),
            Position = Vector3.Lerp(a.Position, b.Position, t),
            Rotation = Quaternion.Slerp(a.Rotation, b.Rotation, t),
            Pitch = Mathf.LerpAngle(a.Pitch, b.Pitch, t),
            Bools = t < 0.5f ? a.Bools : b.Bools,
            AnimSpeed = t < 0.5f ? a.AnimSpeed : b.AnimSpeed,
            Kicks = t < 0.5f ? a.Kicks : b.Kicks,
            HandsShown = t < 0.5f ? a.HandsShown : b.HandsShown,
            HeldLeft = t < 0.5f ? a.HeldLeft : b.HeldLeft,
            HeldRight = t < 0.5f ? a.HeldRight : b.HeldRight,
        };
        // A hand that isn't shown on one side has no position there: take the other side's.
        Blend(a.IsShown(LeftShown), b.IsShown(LeftShown), a.LeftHand, b.LeftHand, a.LeftHandTurn, b.LeftHandTurn, t,
            out pose.LeftHand, out pose.LeftHandTurn);
        Blend(a.IsShown(RightShown), b.IsShown(RightShown), a.RightHand, b.RightHand, a.RightHandTurn, b.RightHandTurn, t,
            out pose.RightHand, out pose.RightHandTurn);
        return pose;
    }

    private static void Blend(bool shownA, bool shownB, Vector3 atA, Vector3 atB, Quaternion turnA, Quaternion turnB, float t,
        out Vector3 at, out Quaternion turn)
    {
        if (shownA && shownB)
        {
            at = Vector3.Lerp(atA, atB, t);
            turn = Quaternion.Slerp(turnA, turnB, t);
        }
        else if (shownB)
        {
            at = atB;
            turn = turnB;
        }
        else
        {
            at = atA;
            turn = turnA;
        }
    }
}
