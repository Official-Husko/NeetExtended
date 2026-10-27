using System;
using UnityEngine;

namespace WeatherExpansion.Windows;

/// <summary>
/// Water on a window pane, simulated on the CPU into one normal map that every window shares: small
/// drops that sit still and a few bigger ones that slide down in jerks, shrinking and leaving a thin
/// wet trail. The game ships no shader that can draw this by itself, but it does ship a refracting
/// glass (Glass/DiffuseBumped) that bends what's behind it by a normal map, so a bumpy map is all
/// that is needed. The map is rebuilt 15 times a second without allocating, and only the pixels that
/// hold a drop or a trail are touched (everything else stays flat from the last time).
/// </summary>
internal sealed class DropField
{
    public const int Width = 256;
    public const int Height = 144;

    private const int MaxStatic = 150;
    private const int MaxSliding = 12;
    private const float TickRate = 15f;
    private const int MaxTrailCells = 12000;
    private const int MaxTouched = 30000;
    private const float TrailFade = 0.55f; // fraction of a trail left after one second
    private const float BumpScale = 1.8f; // how steep the glass looks to the shader

    private struct Drop
    {
        public float X, Y, Radius;
        public float Speed;   // pixels per second, while moving
        public float Pause;   // seconds until it moves again
        public float Phase;   // sideways wobble
        public bool Alive;
    }

    private readonly Texture2D _texture;
    private readonly float[] _trail = new float[Width * Height];
    private readonly uint[] _pixels = new uint[Width * Height];
    private readonly Drop[] _static = new Drop[MaxStatic];
    private readonly Drop[] _sliding = new Drop[MaxSliding];
    private readonly System.Random _random = new System.Random(7);
    private float _fade = 1f;
    private readonly int[] _trailCells = new int[MaxTrailCells];
    private int _trailCount;
    private readonly int[] _touched = new int[MaxTouched];
    private int _touchedCount;
    private float _carry;
    private float _spawnCarry;

    /// <summary>0 = dry glass, 1 = soaked. How many drops sit on the glass and how often one starts to run.</summary>
    public float Wetness { get; set; } = 0.6f;

    public Texture2D Texture => _texture;

    public DropField()
    {
        _texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false, true)
        {
            name = "WeatherExpansion rain on glass",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.HideAndDontSave,
        };
        FillFlat();
        SeedStatic(MaxStatic * 2 / 3);
        for (int i = 0; i < 160; i++) // let it run a while so the glass doesn't start out new
            Step(0.1f);
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
        Step(step);
        Render();
    }

    private float Rand() => (float)_random.NextDouble();

    private void SeedStatic(int count)
    {
        for (int i = 0; i < count; i++)
            SpawnStatic();
    }

    private void SpawnStatic()
    {
        for (int i = 0; i < MaxStatic; i++)
        {
            if (_static[i].Alive)
                continue;
            float r = 1.3f + Rand() * Rand() * 3.2f;
            _static[i] = new Drop { X = Rand() * (Width - 1), Y = Rand() * (Height - 1), Radius = r, Alive = true };
            return;
        }
    }

    private void Step(float dt)
    {
        // New small drops land at a rate that follows the wetness.
        _spawnCarry += dt * (4f + 60f * Wetness);
        while (_spawnCarry >= 1f)
        {
            _spawnCarry -= 1f;
            SpawnStatic();
        }

        // A drop that gets big enough starts to run.
        for (int i = 0; i < MaxStatic; i++)
        {
            ref var d = ref _static[i];
            if (!d.Alive)
                continue;
            if (d.Radius < 3.4f)
                d.Radius += dt * (0.15f + 0.7f * Wetness) * Rand();
            else
                StartRunning(ref d);
            if (Rand() < dt * 0.01f) // old drops dry up
                d.Alive = false;
        }

        for (int i = 0; i < MaxSliding; i++)
        {
            ref var d = ref _sliding[i];
            if (!d.Alive)
                continue;
            if (d.Pause > 0f)
            {
                d.Pause -= dt;
                continue;
            }
            float move = d.Speed * dt;
            // Stamp the trail along the way so a fast drop leaves a line, not dots.
            int steps = Mathf.Max(1, (int)move);
            float sx = (float)Math.Sin(d.Phase) * 0.35f;
            for (int s = 0; s < steps; s++)
            {
                d.Y -= move / steps;
                d.X += sx / steps;
                AddTrail(d.X, d.Y, d.Radius);
            }
            d.Phase += dt * (3f + 5f * Rand());
            d.Radius -= dt * 0.35f;
            if (d.Y < 1f || d.X < 1f || d.X > Width - 2f)
            {
                d.Alive = false;
                continue;
            }
            if (d.Radius < 1.4f)
            {
                // Out of water: it stays where it stopped as a small drop.
                d.Alive = false;
                PlaceStatic(d.X, d.Y, 1.5f);
                continue;
            }
            if (Rand() < dt * 1.6f) // stops and goes
            {
                d.Pause = 0.15f + Rand() * 1.3f;
                d.Speed = 18f + Rand() * 40f;
            }
        }

        _fade = Mathf.Pow(TrailFade, dt);
    }

    private void StartRunning(ref Drop source)
    {
        for (int i = 0; i < MaxSliding; i++)
        {
            if (_sliding[i].Alive)
                continue;
            _sliding[i] = new Drop
            {
                X = source.X, Y = source.Y, Radius = source.Radius,
                Speed = 20f + Rand() * 35f, Phase = Rand() * 6.28f, Alive = true,
            };
            source.Alive = false;
            return;
        }
        source.Radius = 3.2f; // all runners busy: wait
    }

    private void PlaceStatic(float x, float y, float r)
    {
        for (int i = 0; i < MaxStatic; i++)
        {
            if (_static[i].Alive)
                continue;
            _static[i] = new Drop { X = x, Y = y, Radius = r, Alive = true };
            return;
        }
    }

    private void AddTrail(float x, float y, float radius)
    {
        int ix = (int)x, iy = (int)y;
        if (iy < 1 || iy >= Height - 1)
            return;
        float amount = 1.0f + 0.15f * radius;
        for (int dx = -1; dx <= 1; dx++)
        {
            int px = ix + dx;
            if (px < 1 || px >= Width - 1)
                continue;
            int i = iy * Width + px;
            float v = dx == 0 ? amount : amount * 0.5f;
            if (_trail[i] == 0f)
            {
                if (_trailCount >= MaxTrailCells)
                    continue;
                _trailCells[_trailCount++] = i;
            }
            if (_trail[i] < v)
                _trail[i] = v;
        }
    }

    private void FillFlat()
    {
        for (int i = 0; i < _pixels.Length; i++)
            _pixels[i] = Flat;
        _texture.SetPixelData(_pixels, 0);
        _texture.Apply(false);
    }

    // A normal map as the game's shader reads it: Unity's DXT5nm layout (x in alpha, y in green),
    // packed as RGBA bytes of a little-endian uint.
    private const uint Flat = 255u | (128u << 8) | (255u << 16) | (128u << 24);

    private static uint Pack(float nx, float ny)
    {
        float k = 127.5f / (float)Math.Sqrt(nx * nx + ny * ny + 1f);
        return 255u | ((uint)(127.5f - ny * k) << 8) | (255u << 16) | ((uint)(127.5f - nx * k) << 24);
    }

    private void Render()
    {
        uint[] px = _pixels;
        // Put back what was drawn last time, and only that.
        for (int k = 0; k < _touchedCount; k++)
            px[_touched[k]] = Flat;
        _touchedCount = 0;

        // Trails dry; the ones that are gone drop out of the list.
        float[] trail = _trail;
        int[] cells = _trailCells;
        float fade = _fade;
        for (int k = 0; k < _trailCount; k++)
        {
            int i = cells[k];
            float t = trail[i] * fade;
            if (t < 0.01f)
            {
                trail[i] = 0f;
                cells[k--] = cells[--_trailCount];
            }
            else
                trail[i] = t;
        }
        // What's left is drawn from the slope of the wet streak.
        for (int k = 0; k < _trailCount && _touchedCount < MaxTouched; k++)
        {
            int i = cells[k];
            px[i] = Pack((trail[i - 1] - trail[i + 1]) * BumpScale * 0.5f, (trail[i - Width] - trail[i + Width]) * BumpScale * 0.5f);
            _touched[_touchedCount++] = i;
        }

        // Drops on top: the normal of a rounded dome, worked out for just the pixels it covers.
        for (int i = 0; i < MaxStatic; i++)
            if (_static[i].Alive) Stamp(_static[i].X, _static[i].Y, _static[i].Radius);
        for (int i = 0; i < MaxSliding; i++)
            if (_sliding[i].Alive) Stamp(_sliding[i].X, _sliding[i].Y, _sliding[i].Radius);

        _texture.SetPixelData(px, 0);
        _texture.Apply(false);
    }

    private void Stamp(float cx, float cy, float r)
    {
        int x0 = Mathf.Max(1, (int)(cx - r - 1)), x1 = Mathf.Min(Width - 2, (int)(cx + r + 1));
        int y0 = Mathf.Max(1, (int)(cy - r - 1)), y1 = Mathf.Min(Height - 2, (int)(cy + r + 1));
        float r2 = r * r;
        float slope = 2f * BumpScale / r; // the parabola's slope per pixel away from the middle
        for (int y = y0; y <= y1; y++)
        {
            for (int x = x0; x <= x1; x++)
            {
                float dx = x - cx, dy = y - cy;
                if (dx * dx + dy * dy >= r2)
                    continue;
                if (_touchedCount >= MaxTouched)
                    return;
                int i = y * Width + x;
                _pixels[i] = Pack(dx * slope, dy * slope);
                _touched[_touchedCount++] = i;
            }
        }
    }
}
