using System.Collections.Generic;
using HarmonyLib;

namespace Coop.World.Painting;

/// <summary>
/// A surface draws up to 10 waiting splats a frame and drops new ones once 100 wait
/// (<c>PaintTarget.PaintSplat</c>). Another game's splats come a tenth of a second's worth at a time,
/// so a busy surface (a fire, a burst of blood) can get more at once than it takes; those wait here and
/// go in as the surface catches up, so none are lost. Up to 4,096 wait; past that the oldest go.
/// </summary>
internal static class SurfaceQueues
{
    public const int Limit = 100;
    private const int MaxHeld = 4096;

    private static readonly AccessTools.FieldRef<PaintTarget, List<global::Paint>> Waiting =
        AccessTools.FieldRefAccess<PaintTarget, List<global::Paint>>("m_Splats");

    private static readonly List<(PaintTarget Target, global::Paint Paint)> Held = new List<(PaintTarget, global::Paint)>(256);

    /// <summary>True while a splat from another game is queued (it must not be sent again).</summary>
    public static bool Replaying { get; private set; }

    public static int HeldCount => Held.Count;

    public static bool IsFull(PaintTarget target)
    {
        var waiting = Waiting(target);
        return waiting != null && waiting.Count >= Limit;
    }

    /// <summary>Queues another game's splat on the surface, or keeps it until the surface has room.</summary>
    public static void Put(PaintTarget target, global::Paint paint)
    {
        if (!IsFull(target))
        {
            Queue(target, paint);
            return;
        }
        if (Held.Count >= MaxHeld)
        {
            Held.RemoveAt(0);
            PaintStats.Lost++;
        }
        Held.Add((target, paint));
    }

    /// <summary>Once a frame: held splats go to surfaces that have room again.</summary>
    public static void Tick()
    {
        if (Held.Count == 0)
            return;
        int kept = 0;
        for (int i = 0; i < Held.Count; i++)
        {
            var (target, paint) = Held[i];
            if (target == null)
                continue;
            if (IsFull(target))
                Held[kept++] = Held[i];
            else
                Queue(target, paint);
        }
        Held.RemoveRange(kept, Held.Count - kept);
    }

    public static void Clear() => Held.Clear();

    private static void Queue(PaintTarget target, global::Paint paint)
    {
        Replaying = true;
        try
        {
            target.PaintSplat(paint);
            target.OnPainted?.Invoke();
        }
        finally
        {
            Replaying = false;
        }
        PaintStats.Drawn++;
    }
}
