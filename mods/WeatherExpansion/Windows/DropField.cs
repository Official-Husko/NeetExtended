using System;
using UnityEngine;

namespace WeatherExpansion.Windows;

/// <summary>
/// Draws a <see cref="DropSimulation"/> as the normal map every window shares. The game ships no shader
/// that draws drops by itself, but it does ship a refracting glass (Glass/DiffuseBumped) that bends what's
/// behind it by a normal map, so a bumpy map is all that is needed. The map is rebuilt 15 times a
/// second without allocating, and only the pixels that hold a drop or a trail are touched (everything
/// else stays flat from the last time).
/// </summary>
internal sealed class DropField
{
    private const int Width = DropSimulation.Width;
    private const int Height = DropSimulation.Height;
    private const float TickRate = 15f;
    private const int MaxTouched = 40000;
    private const float TrailSlope = 0.8f;  // how steep a wet streak looks to the shader
    private const float ShapeScale = 0.85f; // the game's drop is longer than a round one of the same radius
    private const float DropTilt = 1.0f;    // slope at the edge of a drop (a drop is a cap of a sphere: it tilts the same at any size)

    // A normal map as the game's shader reads it: Unity's DXT5nm layout (x in alpha, y in green),
    // packed as RGBA bytes of a little-endian uint.
    private const uint Flat = 255u | (128u << 8) | (255u << 16) | (128u << 24);

    private readonly Texture2D _texture;
    private readonly DropSimulation _water = new DropSimulation();
    private readonly uint[] _pixels = new uint[Width * Height];
    private readonly int[] _touched = new int[MaxTouched];
    private int _touchedCount;
    private readonly DropShape _shape;
    private float _carry;

    public Texture2D Texture => _texture;

    /// <summary>0 to 1: how fast new drops land and grow.</summary>
    public float Wetness { set => _water.Wetness = value; }

    /// <summary>How fast running drops slide; 1 is the usual.</summary>
    public float Speed { set => _water.Speed = value; }

    public DropField()
    {
        _texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false, true)
        {
            name = "WeatherExpansion rain on glass",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.HideAndDontSave,
        };
        _shape = DropShape.Create(DropTilt);
        for (int i = 0; i < _pixels.Length; i++)
            _pixels[i] = Flat;
        Render();
    }

    public void Destroy()
    {
        if (_texture != null)
            UnityEngine.Object.Destroy(_texture);
    }

    /// <summary>Advances the water by real time and redraws the texture at <see cref="TickRate"/>.</summary>
    public void Advance(float dt)
    {
        _carry += dt;
        float step = 1f / TickRate;
        if (_carry < step)
            return;
        _carry = Mathf.Min(_carry - step, step);
        _water.Step(step);
        Render();
    }

    private static uint Pack(float nx, float ny) => DropShape.Pack(nx, ny);

    private void Render()
    {
        uint[] px = _pixels;
        // Put back what was drawn last time, and only that.
        for (int k = 0; k < _touchedCount; k++)
            px[_touched[k]] = Flat;
        _touchedCount = 0;

        // The wet streaks, drawn from their slope.
        float[] trail = _water.Trail;
        int[] cells = _water.TrailCells;
        for (int k = 0; k < _water.TrailCount && _touchedCount < MaxTouched; k++)
        {
            int i = cells[k];
            px[i] = Pack((trail[i - 1] - trail[i + 1]) * TrailSlope * 0.5f, (trail[i - Width] - trail[i + Width]) * TrailSlope * 0.5f);
            _touched[_touchedCount++] = i;
        }

        // Drops on top.
        foreach (var d in _water.Still)
            if (d.Alive) Stamp(d.X, d.Y, d.Radius);
        foreach (var d in _water.Running)
            if (d.Alive) Stamp(d.X, d.Y, d.Radius);

        _texture.SetPixelData(px, 0);
        _texture.Apply(false);
    }

    // The normal of a rounded drop, worked out for just the pixels it covers.
    // Drops are the game's own drop shape when it's there, plain soft round lenses when it isn't.
    private void Stamp(float cx, float cy, float r)
    {
        if (_shape != null)
        {
            StampShape(cx, cy, r * ShapeScale);
            return;
        }
        int x0 = Mathf.Max(1, (int)(cx - r - 1)), x1 = Mathf.Min(Width - 2, (int)(cx + r + 1));
        int y0 = Mathf.Max(1, (int)(cy - r - 1)), y1 = Mathf.Min(Height - 2, (int)(cy + r + 1));
        float r2 = r * r;
        float tilt = DropTilt * 2.6f / r; // peak tilt of DropTilt about halfway to the rim, none at the rim: a soft lens, not a disc
        for (int y = y0; y <= y1; y++)
        {
            for (int x = x0; x <= x1; x++)
            {
                float dx = x - cx, dy = y - cy;
                float d2 = dx * dx + dy * dy;
                if (d2 >= r2)
                    continue;
                if (_touchedCount >= MaxTouched)
                    return;
                float slope = tilt * (1f - d2 / r2);
                int i = y * Width + x;
                _pixels[i] = Pack(dx * slope, dy * slope);
                _touched[_touchedCount++] = i;
            }
        }
    }

    // A drop in the game's shape: the table gives the normal at each pixel inside it.
    private void StampShape(float cx, float cy, float r)
    {
        float half = r * DropShape.Span * 0.5f;
        int x0 = Mathf.Max(1, (int)(cx - half)), x1 = Mathf.Min(Width - 2, (int)(cx + half));
        int y0 = Mathf.Max(1, (int)(cy - half)), y1 = Mathf.Min(Height - 2, (int)(cy + half));
        float inv = 1f / r;
        for (int y = y0; y <= y1; y++)
        {
            for (int x = x0; x <= x1; x++)
            {
                uint pixel = _shape.At((x - cx) * inv, (y - cy) * inv);
                if (pixel == 0u)
                    continue;
                if (_touchedCount >= MaxTouched)
                    return;
                int i = y * Width + x;
                _pixels[i] = pixel;
                _touched[_touchedCount++] = i;
            }
        }
    }
}
