using System.Diagnostics;
using DevBridge.Server;
using NeetModKit.Saves;
using Newtonsoft.Json.Linq;

namespace DevBridge.Commands;

/// <summary>What a co-op join snapshot costs. Reads the house only: no file, no save hooks.</summary>
internal static class SnapshotCommands
{
    [BridgeCommand("save.snapshot", "Time taking a save snapshot (save data plus paint) and encoding it. Writes nothing.")]
    private static object Snapshot(JObject p)
    {
        var clock = Stopwatch.StartNew();
        var snapshot = SaveCapture.Take();
        double takeMs = clock.Elapsed.TotalMilliseconds;

        clock.Restart();
        string json = snapshot.Save.ToString(Newtonsoft.Json.Formatting.None);
        double jsonMs = clock.Elapsed.TotalMilliseconds;

        clock.Restart();
        byte[] blob = snapshot.Encode();
        double encodeMs = clock.Elapsed.TotalMilliseconds;

        long paintBytes = 0;
        foreach (var png in snapshot.Paint.Values)
            paintBytes += png.Length;

        return new JObject
        {
            ["entries"] = (snapshot.Save["Save"] as JArray)?.Count,
            ["paintSurfaces"] = snapshot.Paint.Count,
            ["takeMs"] = takeMs,
            ["jsonMs"] = jsonMs,
            ["encodeMs"] = encodeMs,
            ["jsonBytes"] = json.Length,
            ["paintBytes"] = paintBytes,
            ["encodedBytes"] = blob.Length,
        };
    }
}
