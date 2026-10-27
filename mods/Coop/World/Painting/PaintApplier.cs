using System.Collections.Generic;
using LiteNetLib.Utils;
using UnityEngine;

namespace Coop.World.Painting;

/// <summary>
/// Draws another game's splats here. Each goes into the surface's own queue
/// (<c>PaintTarget.PaintSplat</c>) with the sender's exact box, channel and atlas cell, so it looks
/// the same as where it was painted. That skips <c>PaintTarget.PaintObject</c>: no drip effect here
/// (the sender's drips paint there and come as splats too), and nothing to send back.
/// </summary>
internal static class PaintApplier
{
    private static readonly Texture2D[] Textures = new Texture2D[PaintCodec.MaxTextures];
    private static readonly byte[] AtlasX = new byte[PaintCodec.MaxTextures];
    private static readonly byte[] AtlasY = new byte[PaintCodec.MaxTextures];
    // The game only reads a brush's texture and channel once a splat is queued, so one per pair does.
    private static readonly Dictionary<long, Brush> Brushes = new Dictionary<long, Brush>();

    public static void Apply(NetDataReader reader)
    {
        int textures = reader.GetByte();
        if (textures > PaintCodec.MaxTextures)
            return;
        for (int i = 0; i < textures; i++)
        {
            Textures[i] = PaintTextures.Find(reader.GetString());
            AtlasX[i] = (byte)Mathf.Max(1, (int)reader.GetByte());
            AtlasY[i] = (byte)Mathf.Max(1, (int)reader.GetByte());
        }
        int count = reader.GetUShort();
        PaintStats.Received += count;
        for (int i = 0; i < count; i++)
        {
            PaintCodec.Read(reader, out var splat);
            if (splat.Texture < textures)
                Draw(splat);
        }
        System.Array.Clear(Textures, 0, textures);
    }

    public static void Clear() => Brushes.Clear();

    private static void Draw(in SplatRecord splat)
    {
        var target = Surfaces.Find(splat.Target);
        if (target == null)
        {
            PaintStats.NoSurface++;
            return;
        }
        var tf = target.transform;
        var rotation = tf.rotation;
        var box = Matrix4x4.TRS(tf.position + rotation * splat.Position, rotation * splat.Rotation, splat.Scale);
        var brush = BrushFor(Textures[splat.Texture], splat.Channel);
        int atlasX = AtlasX[splat.Texture], atlasY = AtlasY[splat.Texture];
        var paint = new global::Paint
        {
            paintMatrix = box.inverse,
            channelMask = brush.getMask(),
            scaleBias = new Vector4(1f / atlasX, 1f / atlasY, (float)(splat.Tile % atlasX) / atlasX, (float)(splat.Tile / atlasX) / atlasY),
            brush = brush,
        };
        SurfaceQueues.Put(target, paint);
    }

    private static Brush BrushFor(Texture2D texture, int channel)
    {
        long key = (long)(texture != null ? texture.GetInstanceID() : 0) << 8 | (uint)channel;
        if (!Brushes.TryGetValue(key, out var brush))
            Brushes[key] = brush = new Brush { splatTexture = texture, splatChannel = channel };
        return brush;
    }
}
