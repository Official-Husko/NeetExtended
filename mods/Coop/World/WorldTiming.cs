using System.Diagnostics;
using System.Text;

namespace Coop.World;

/// <summary>
/// How long World's own frame work takes (the game's script timer doesn't see mod code). Three
/// running totals, read and reset through <see cref="WorldApi.Timing"/>.
/// </summary>
internal static class WorldTiming
{
    public const int Update = 0;
    public const int Fixed = 1;
    public const int Late = 2;

    private static readonly string[] Names = { "Update", "FixedUpdate", "LateUpdate" };
    private static readonly long[] Ticks = new long[3];
    private static readonly int[] Calls = new int[3];
    private static readonly long[] Worst = new long[3];

    public static long Begin() => Stopwatch.GetTimestamp();

    public static void End(int part, long started)
    {
        long took = Stopwatch.GetTimestamp() - started;
        Ticks[part] += took;
        Calls[part]++;
        if (took > Worst[part])
            Worst[part] = took;
    }

    public static string Report()
    {
        var text = new StringBuilder();
        double toMs = 1000.0 / Stopwatch.Frequency;
        for (int i = 0; i < Names.Length; i++)
        {
            double average = Calls[i] > 0 ? Ticks[i] * toMs / Calls[i] : 0;
            text.Append(Names[i]).Append(": ").Append(Calls[i]).Append(" calls, ")
                .Append(average.ToString("0.000")).Append(" ms each, worst ")
                .Append((Worst[i] * toMs).ToString("0.000")).Append(" ms\n");
            Ticks[i] = 0;
            Calls[i] = 0;
            Worst[i] = 0;
        }
        return text.ToString();
    }
}
