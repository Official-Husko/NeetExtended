using Coop.Net;
using UnityEngine;

namespace Coop.World.Painting;

/// <summary>
/// Collects this game's splats and sends them in batches: a tenth of a second after the first one,
/// or as soon as a batch holds 60 (about 1.2 KB) or uses 16 different brush textures.
///
/// WorldPaint: texture count (byte), per texture its name ("" for none) and atlas size (2 bytes),
/// splat count (ushort), the splats (<see cref="PaintCodec"/>).
/// </summary>
internal static class PaintOutbox
{
    private const int MaxSplats = 60;
    private const float Interval = 0.1f;

    private static readonly SplatRecord[] Pending = new SplatRecord[MaxSplats];
    private static readonly Texture2D[] Textures = new Texture2D[PaintCodec.MaxTextures];
    private static readonly byte[] AtlasX = new byte[PaintCodec.MaxTextures];
    private static readonly byte[] AtlasY = new byte[PaintCodec.MaxTextures];
    private static int _count;
    private static int _textures;
    private static float _due;

    private static readonly HarmonyLib.AccessTools.FieldRef<PaintTarget, Renderer> SurfaceRenderer =
        HarmonyLib.AccessTools.FieldRefAccess<PaintTarget, Renderer>("paintRenderer");

    /// <summary>A splat this game is about to draw on one of the house's surfaces.</summary>
    public static void Add(PaintTarget target, global::Paint paint, bool merge)
    {
        var brush = paint.brush;
        if (brush == null || !Decompose(paint.paintMatrix, out var position, out var rotation, out var scale))
            return;
        if (!Reaches(target, position, scale))
        {
            PaintStats.OutOfReach(target.saveId);
            return;
        }
        var tf = target.transform;
        var toLocal = Quaternion.Inverse(tf.rotation);
        int atlasX = Mathf.Clamp(brush.splatsX, 1, 255);
        int atlasY = Mathf.Clamp(brush.splatsY, 1, 255);
        var splat = new SplatRecord
        {
            Target = (ushort)target.saveId,
            Channel = (byte)Mathf.Clamp(brush.splatChannel, 0, PaintCodec.MaxChannel),
            Tile = TileOf(paint.scaleBias, atlasX, atlasY),
            Position = toLocal * (position - tf.position),
            Rotation = toLocal * rotation,
            Scale = scale,
        };
        PaintStats.Captured++;

        float now = Time.unscaledTime;
        var texture = brush.splatTexture;
        if (merge && PaintRepeats.Seen(splat, texture != null ? texture.GetInstanceID() : 0, now))
        {
            PaintStats.Merged++;
            return;
        }
        splat.Texture = (byte)TextureIndex(texture, atlasX, atlasY);
        if (_count == 0)
            _due = now + Interval;
        Pending[_count++] = splat;
        PaintStats.Painted(splat.Target);
        if (_count == MaxSplats)
            Flush();
    }

    public static void Tick()
    {
        if (_count > 0 && Time.unscaledTime >= _due)
            Flush();
    }

    public static void Flush()
    {
        if (_count == 0)
        {
            Reset();
            return;
        }
        var w = Outbox.Begin(MessageIds.WorldPaint);
        w.Put((byte)_textures);
        for (int i = 0; i < _textures; i++)
        {
            w.Put(PaintTextures.NameOf(Textures[i]));
            w.Put(AtlasX[i]);
            w.Put(AtlasY[i]);
        }
        w.Put((ushort)_count);
        for (int i = 0; i < _count; i++)
            PaintCodec.Write(w, Pending[i]);
        PaintStats.Sent += _count;
        PaintStats.SentBytes += w.Length;
        PaintStats.SentMessages++;
        Reset();
        PaintSync.Send(w);
    }

    /// <summary>Drops what hasn't gone out yet (the world stopped).</summary>
    public static void Reset()
    {
        _count = 0;
        System.Array.Clear(Textures, 0, _textures);
        _textures = 0;
    }

    private static int TextureIndex(Texture2D texture, int atlasX, int atlasY)
    {
        for (int i = 0; i < _textures; i++)
        {
            if (ReferenceEquals(Textures[i], texture) && AtlasX[i] == atlasX && AtlasY[i] == atlasY)
                return i;
        }
        if (_textures == PaintCodec.MaxTextures)
            Flush();
        Textures[_textures] = texture;
        AtlasX[_textures] = (byte)atlasX;
        AtlasY[_textures] = (byte)atlasY;
        return _textures++;
    }

    // The game also paints surfaces the splat can't touch: a footprint's sphere cast goes on through
    // the floors below, and each floor it meets gets the splat at the footprint's spot, where it draws
    // nothing. A splat's box reaches at most 1.75 times its largest scale from its centre, so a surface
    // whose bounds are farther away can't take any of it.
    private static bool Reaches(PaintTarget target, Vector3 position, Vector3 scale)
    {
        var renderer = SurfaceRenderer(target);
        if (renderer == null)
            return true;
        var bounds = renderer.bounds;
        float reach = 1.75f * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
        var nearest = Vector3.Max(bounds.min, Vector3.Min(bounds.max, position));
        return (nearest - position).sqrMagnitude <= reach * reach;
    }

    // Brush.getTile picks the atlas cell (at random for most brushes) and returns its offset.
    private static byte TileOf(Vector4 scaleBias, int atlasX, int atlasY)
    {
        int column = Mathf.Clamp(Mathf.RoundToInt(scaleBias.z * atlasX), 0, atlasX - 1);
        int row = Mathf.Clamp(Mathf.RoundToInt(scaleBias.w * atlasY), 0, atlasY - 1);
        return (byte)Mathf.Min(row * atlasX + column, 255);
    }

    /// <summary>
    /// The splat's box from the matrix the game paints with (world to box space). Only the X scale
    /// is ever negative (the left footprint), so a mirrored box is read as a negative X.
    /// </summary>
    private static bool Decompose(Matrix4x4 worldToBox, out Vector3 position, out Quaternion rotation, out Vector3 scale)
    {
        var boxToWorld = worldToBox.inverse;
        Vector3 x = boxToWorld.GetColumn(0), y = boxToWorld.GetColumn(1), z = boxToWorld.GetColumn(2);
        position = boxToWorld.GetColumn(3);
        float sx = x.magnitude, sy = y.magnitude, sz = z.magnitude;
        if (sx < 1e-6f || sy < 1e-6f || sz < 1e-6f)
        {
            rotation = Quaternion.identity;
            scale = Vector3.zero;
            return false;
        }
        if (Vector3.Dot(Vector3.Cross(x, y), z) < 0f)
            sx = -sx;
        rotation = Quaternion.LookRotation(z / sz, y / sy);
        scale = new Vector3(sx, sy, sz);
        return true;
    }
}
