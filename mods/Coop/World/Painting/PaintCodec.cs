using LiteNetLib.Utils;
using UnityEngine;

namespace Coop.World.Painting;

/// <summary>
/// One splat as it travels: the surface, the brush's channel, texture and atlas cell, and the splat's
/// box (position, rotation, scale) relative to the surface's own position and rotation, so a splat on a
/// door lands on the door wherever the door is on each machine.
/// </summary>
internal struct SplatRecord
{
    public ushort Target;
    public byte Channel;
    /// <summary>Index into the message's texture table.</summary>
    public byte Texture;
    public byte Tile;
    public Vector3 Position;
    public Quaternion Rotation;
    public Vector3 Scale;
}

/// <summary>
/// Packs a splat into 20 bytes: surface id (2), flags (1: channel, texture, wide position), atlas cell (1),
/// position in millimetres (6; 12 as floats when it's more than 32 m from the surface's pivot, which
/// no surface in the house is), rotation as its three smallest quaternion parts (4) and scale as half
/// floats (6; the scale is signed, footprints mirror the left foot).
/// </summary>
internal static class PaintCodec
{
    public const int MaxChannel = 7;
    public const int MaxTextures = 16;

    private const float Millimetre = 0.001f;
    private const float Reach = short.MaxValue * Millimetre;
    private const byte WideFlag = 0x80;
    private const float Root2 = 0.70710678f;

    public static void Write(NetDataWriter w, in SplatRecord s)
    {
        var p = s.Position;
        bool wide = Mathf.Abs(p.x) >= Reach || Mathf.Abs(p.y) >= Reach || Mathf.Abs(p.z) >= Reach;
        w.Put(s.Target);
        w.Put((byte)((s.Channel & MaxChannel) | (s.Texture & (MaxTextures - 1)) << 3 | (wide ? WideFlag : 0)));
        w.Put(s.Tile);
        if (wide)
        {
            w.Put(p.x);
            w.Put(p.y);
            w.Put(p.z);
        }
        else
        {
            w.Put((short)Mathf.RoundToInt(p.x / Millimetre));
            w.Put((short)Mathf.RoundToInt(p.y / Millimetre));
            w.Put((short)Mathf.RoundToInt(p.z / Millimetre));
        }
        w.Put(PackRotation(s.Rotation));
        w.Put(Mathf.FloatToHalf(s.Scale.x));
        w.Put(Mathf.FloatToHalf(s.Scale.y));
        w.Put(Mathf.FloatToHalf(s.Scale.z));
    }

    public static void Read(NetDataReader r, out SplatRecord s)
    {
        s.Target = r.GetUShort();
        byte flags = r.GetByte();
        s.Channel = (byte)(flags & MaxChannel);
        s.Texture = (byte)((flags >> 3) & (MaxTextures - 1));
        s.Tile = r.GetByte();
        if ((flags & WideFlag) != 0)
            s.Position = new Vector3(r.GetFloat(), r.GetFloat(), r.GetFloat());
        else
            s.Position = new Vector3(r.GetShort() * Millimetre, r.GetShort() * Millimetre, r.GetShort() * Millimetre);
        s.Rotation = UnpackRotation(r.GetUInt());
        s.Scale = new Vector3(Mathf.HalfToFloat(r.GetUShort()), Mathf.HalfToFloat(r.GetUShort()), Mathf.HalfToFloat(r.GetUShort()));
    }

    // Smallest three: the largest part is left out (it follows from the others), the other three
    // take 10 bits each. About a sixth of a degree at worst.
    public static uint PackRotation(Quaternion q)
    {
        float ax = Mathf.Abs(q.x), ay = Mathf.Abs(q.y), az = Mathf.Abs(q.z), aw = Mathf.Abs(q.w);
        int largest = 0;
        float most = ax;
        if (ay > most) { largest = 1; most = ay; }
        if (az > most) { largest = 2; most = az; }
        if (aw > most) largest = 3;
        float sign = q[largest] < 0f ? -1f : 1f;
        uint packed = (uint)largest << 30;
        int shift = 20;
        for (int i = 0; i < 4; i++)
        {
            if (i == largest)
                continue;
            packed |= Ten(q[i] * sign) << shift;
            shift -= 10;
        }
        return packed;
    }

    public static Quaternion UnpackRotation(uint packed)
    {
        int largest = (int)(packed >> 30);
        float a = Unten(packed >> 20), b = Unten(packed >> 10), c = Unten(packed);
        float rest = Mathf.Sqrt(Mathf.Max(0f, 1f - a * a - b * b - c * c));
        switch (largest)
        {
            case 0: return new Quaternion(rest, a, b, c);
            case 1: return new Quaternion(a, rest, b, c);
            case 2: return new Quaternion(a, b, rest, c);
            default: return new Quaternion(a, b, c, rest);
        }
    }

    private static uint Ten(float v) =>
        (uint)Mathf.Clamp(Mathf.RoundToInt((v / Root2 * 0.5f + 0.5f) * 1023f), 0, 1023);

    private static float Unten(uint v) => ((v & 1023) / 1023f - 0.5f) * 2f * Root2;
}
