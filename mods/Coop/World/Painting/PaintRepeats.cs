using UnityEngine;

namespace Coop.World.Painting;

/// <summary>
/// A room fire scorches under each of its burning spheres on every physics step (50 times a second
/// per sphere, up to 60 spheres). Painting a channel raises it to the splat's value rather than adding
/// up, so a scorch mark close to one just sent adds little: per surface and spot (a cell a tenth of the
/// mark's size) and about the same size, two go out a second and the rest are dropped. That halves
/// what a fire sends, and the client's scorch covers a few percent less than the host's (measured
/// 2 to 9%). Other paint isn't merged: blood blobs slide while they paint, and merging them cost up to
/// a third of a puddle for little gain. Fixed table, nothing allocated.
/// </summary>
internal static class PaintRepeats
{
    private const int Size = 2048;
    private const int Probes = 8;
    // Tunable from the probes while testing.
    public static float Window = 1f;
    public static float SmallestCell = 0.02f;
    public static float CellPerSize = 0.1f;
    public static int Allowed = 2;

    private struct Entry
    {
        public int Head;
        public int Texture;
        public int X, Y, Z;
        public int Count;
        public float Time;
    }

    private static readonly Entry[] Table = new Entry[Size];

    /// <summary>For tests (<see cref="PaintTestApi.Merge"/>).</summary>
    public static bool Enabled = true;

    /// <summary>
    /// True when enough splats like this one went out in the last second; otherwise it's counted and
    /// false comes back.
    /// </summary>
    public static bool Seen(in SplatRecord s, int textureId, float now)
    {
        if (!Enabled)
            return false;
        float size = Mathf.Max(Mathf.Abs(s.Scale.x), Mathf.Abs(s.Scale.y), Mathf.Abs(s.Scale.z), 0.0001f);
        int bucket = Mathf.RoundToInt(Mathf.Log(size, 2f) * 8f);
        float cell = Mathf.Max(SmallestCell, size * CellPerSize);
        var key = new Entry
        {
            Head = s.Target | s.Channel << 16 | (bucket & 0xFF) << 20 | 1 << 30,
            Texture = textureId,
            X = Mathf.FloorToInt(s.Position.x / cell),
            Y = Mathf.FloorToInt(s.Position.y / cell),
            Z = Mathf.FloorToInt(s.Position.z / cell),
            Count = 1,
            Time = now,
        };

        int start;
        unchecked
        {
            int hash = key.Head * 486187739 ^ key.Texture * 16777619 ^ key.X * 73856093 ^ key.Y * 19349663 ^ key.Z * 83492791;
            start = hash & (Size - 1);
        }
        int free = -1, oldest = start;
        for (int i = 0; i < Probes; i++)
        {
            int slot = (start + i) & (Size - 1);
            ref var entry = ref Table[slot];
            bool live = entry.Head != 0 && now - entry.Time < Window;
            if (entry.Head == key.Head && entry.Texture == key.Texture && entry.X == key.X && entry.Y == key.Y && entry.Z == key.Z)
            {
                if (!live)
                {
                    entry.Time = now;
                    entry.Count = 1;
                    return false;
                }
                if (entry.Count >= Allowed)
                    return true;
                entry.Count++;
                return false;
            }
            if (!live)
            {
                if (free < 0)
                    free = slot;
            }
            else if (entry.Time < Table[oldest].Time)
                oldest = slot;
        }
        Table[free >= 0 ? free : oldest] = key;
        return false;
    }

    public static void Clear() => System.Array.Clear(Table, 0, Table.Length);
}
