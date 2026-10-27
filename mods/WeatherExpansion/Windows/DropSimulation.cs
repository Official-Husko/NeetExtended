using System;
using UnityEngine;

namespace WeatherExpansion.Windows;

/// <summary>One drop of water on the glass, in pixels of the <see cref="DropSimulation"/> grid.</summary>
internal struct Drop
{
    public float X, Y, Radius;
    public float Speed;   // pixels per second, while moving
    public float Pause;   // seconds until it moves again
    public float Phase;   // sideways wobble
    public bool Alive;
}

/// <summary>
/// The water on a window pane, as numbers: small drops that sit still, grow while it's wet and dry up,
/// and bigger ones that start to run, slide down in stop and go, shrink, leave a wet trail and a bead
/// where they stop. <see cref="DropField"/> draws it. One simulation serves every window.
/// Nothing here allocates after it is built.
/// </summary>
internal sealed class DropSimulation
{
    public const int Width = 256;
    public const int Height = 144;
    public const int MaxStatic = 420;
    public const int MaxSliding = 12;
    public const int MaxTrailCells = 12000;

    private const float RunRadius = 2.6f;        // a still drop this big starts to run
    private const float TrailLife = 0.55f;       // fraction of a trail left after one second

    public readonly Drop[] Still = new Drop[MaxStatic];
    public readonly Drop[] Running = new Drop[MaxSliding];
    public readonly float[] Trail = new float[Width * Height];
    public readonly int[] TrailCells = new int[MaxTrailCells];
    public int TrailCount;

    private readonly System.Random _random = new System.Random(7);
    private float _spawnCarry;

    /// <summary>0 = dry glass, 1 = soaked: how fast new drops land and grow.</summary>
    public float Wetness { get; set; } = 0.5f;

    /// <summary>How fast running drops slide (1 is the usual; a storm is faster).</summary>
    public float Speed { get; set; } = 1f;

    public DropSimulation()
    {
        for (int i = 0; i < MaxStatic * 2 / 3; i++)
            SpawnStill();
        for (int i = 0; i < 160; i++) // let it run a while so the glass doesn't start out new
            Step(0.1f);
    }

    private float Rand() => (float)_random.NextDouble();

    private void SpawnStill()
    {
        for (int i = 0; i < MaxStatic; i++)
        {
            if (Still[i].Alive)
                continue;
            float r = 0.8f + Rand() * Rand() * 2.0f; // mostly tiny, a few bigger
            Still[i] = new Drop { X = Rand() * (Width - 1), Y = Rand() * (Height - 1), Radius = r, Alive = true };
            return;
        }
    }

    public void Step(float dt)
    {
        // New small drops land at a rate that follows the wetness.
        _spawnCarry += dt * (8f + 120f * Wetness);
        while (_spawnCarry >= 1f)
        {
            _spawnCarry -= 1f;
            SpawnStill();
        }

        // A drop that gets big enough starts to run.
        for (int i = 0; i < MaxStatic; i++)
        {
            ref var d = ref Still[i];
            if (!d.Alive)
                continue;
            if (d.Radius < RunRadius)
                d.Radius += dt * (0.15f + 0.7f * Wetness) * Rand();
            else
                StartRunning(ref d);
            if (Rand() < dt * 0.01f) // old drops dry up
                d.Alive = false;
        }

        for (int i = 0; i < MaxSliding; i++)
            StepRunner(ref Running[i], dt);

        FadeTrails(dt);
    }

    private void StepRunner(ref Drop d, float dt)
    {
        if (!d.Alive)
            return;
        if (d.Pause > 0f)
        {
            d.Pause -= dt;
            return;
        }
        float move = d.Speed * Speed * dt;
        // Stamp the trail along the way so a fast drop leaves a line, not dots.
        int steps = Mathf.Max(1, (int)move);
        float sideways = (float)Math.Sin(d.Phase) * 0.35f;
        for (int s = 0; s < steps; s++)
        {
            d.Y -= move / steps;
            d.X += sideways / steps;
            AddTrail(d.X, d.Y, d.Radius);
        }
        d.Phase += dt * (3f + 5f * Rand());
        d.Radius -= dt * 0.35f;
        if (d.Y < 1f || d.X < 1f || d.X > Width - 2f)
        {
            d.Alive = false;
            return;
        }
        if (d.Radius < 1.1f)
        {
            // Out of water: it stays where it stopped as a small drop.
            d.Alive = false;
            PlaceStill(d.X, d.Y, 1.1f);
            return;
        }
        if (Rand() < dt * 1.6f) // stops and goes
        {
            d.Pause = (0.15f + Rand() * 1.3f) / Mathf.Max(0.3f, Speed);
            d.Speed = 18f + Rand() * 40f;
        }
    }

    private void StartRunning(ref Drop source)
    {
        for (int i = 0; i < MaxSliding; i++)
        {
            if (Running[i].Alive)
                continue;
            Running[i] = new Drop
            {
                X = source.X, Y = source.Y, Radius = source.Radius,
                Speed = 20f + Rand() * 35f, Phase = Rand() * 6.28f, Alive = true,
            };
            source.Alive = false;
            return;
        }
        source.Radius = RunRadius - 0.1f; // all runners busy: wait
    }

    private void PlaceStill(float x, float y, float r)
    {
        for (int i = 0; i < MaxStatic; i++)
        {
            if (Still[i].Alive)
                continue;
            Still[i] = new Drop { X = x, Y = y, Radius = r, Alive = true };
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
            if (Trail[i] == 0f)
            {
                if (TrailCount >= MaxTrailCells)
                    continue;
                TrailCells[TrailCount++] = i;
            }
            if (Trail[i] < v)
                Trail[i] = v;
        }
    }

    // Trails dry; the ones that are gone drop out of the list.
    private void FadeTrails(float dt)
    {
        float fade = Mathf.Pow(TrailLife, dt);
        for (int k = 0; k < TrailCount; k++)
        {
            int i = TrailCells[k];
            float t = Trail[i] * fade;
            if (t < 0.01f)
            {
                Trail[i] = 0f;
                TrailCells[k--] = TrailCells[--TrailCount];
            }
            else
                Trail[i] = t;
        }
    }
}
