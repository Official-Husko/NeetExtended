using System.Collections.Generic;
using System.Text;

namespace Coop.World.Painting;

/// <summary>Counters since the world started here (or the last reset), for <see cref="PaintTestApi.Stats"/>.</summary>
internal static class PaintStats
{
    private const int MaxSurfaces = 256;

    public static int Captured, Missed, Merged, Sent, SentMessages, SentBytes;
    public static int Received, Drawn, NoSurface, Dried, Skipped, Mirrored, Lost;

    private static readonly HashSet<int> Surfaces = new HashSet<int>();
    private static readonly HashSet<int> MissedSurfaces = new HashSet<int>();

    /// <summary>A splat that couldn't reach its surface wasn't sent (up to 256 surfaces kept, for tests).</summary>
    public static void OutOfReach(int surface)
    {
        Missed++;
        if (MissedSurfaces.Count < MaxSurfaces)
            MissedSurfaces.Add(surface);
    }

    /// <summary>A surface this game sent paint for (up to 256 are kept, for tests).</summary>
    public static void Painted(int surface)
    {
        if (Surfaces.Count < MaxSurfaces)
            Surfaces.Add(surface);
    }

    public static void Clear()
    {
        Captured = Missed = Merged = Sent = SentMessages = SentBytes = 0;
        Received = Drawn = NoSurface = Dried = Skipped = Mirrored = Lost = 0;
        Surfaces.Clear();
        MissedSurfaces.Clear();
    }

    public static string Report()
    {
        var text = new StringBuilder();
        text.Append($"captured {Captured}, out of reach {Missed}, merged {Merged}, sent {Sent} in {SentMessages} messages ({SentBytes} bytes); ");
        text.Append($"received {Received}, drawn {Drawn}, no surface {NoSurface}, dried {Dried}, own skipped {Skipped}, painted in every game {Mirrored}, held {SurfaceQueues.HeldCount}, lost {Lost}; surfaces");
        foreach (int id in Surfaces)
            text.Append(' ').Append(id);
        text.Append("; out of reach");
        foreach (int id in MissedSurfaces)
            text.Append(' ').Append(id);
        return text.ToString();
    }
}
