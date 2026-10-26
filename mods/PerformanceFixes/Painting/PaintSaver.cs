using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace PerformanceFixes.Painting;

/// <summary>
/// Replaces SaveLoadHandler.SavePaintSplats. The game deletes every paint picture and writes all of
/// them again on each save, each with a GPU readback, a PNG encode and a file write on the main
/// thread (about 4 ms per surface, 370 ms for 87 surfaces). This writes only the surfaces that
/// changed since they were last saved or loaded, and leaves encoding and writing to
/// <see cref="PaintWriter"/>. The main thread only copies pixels.
/// </summary>
internal static class PaintSaver
{
    private const int FlushTimeoutMs = 15000;

    private static readonly HashSet<int> Keep = new HashSet<int>();
    private static readonly HashSet<int> Failed = new HashSet<int>();
    private static readonly List<string> Errors = new List<string>();
    private static PaintWriter _writer;

    public static void Start()
    {
        PaintTracker.Forget();
        _writer ??= new PaintWriter();
    }

    public static void Stop()
    {
        _writer?.Dispose();
        _writer = null;
        PixelBuffers.Clear();
        PaintTracker.Forget();
    }

    /// <summary>Waits for pictures still being written (before the game reads them back, and on quit).</summary>
    public static void Flush()
    {
        if (_writer != null && !_writer.Flush(FlushTimeoutMs))
            PerformanceFixesMod.Instance?.Log.Warn($"Paint pictures were still being written after {FlushTimeoutMs / 1000} s.");
    }

    /// <param name="root">The folder the game passes in, Application.persistentDataPath.</param>
    public static void Save(string root)
    {
        var clock = Stopwatch.StartNew();
        string folder = Path.GetFullPath(Path.Combine(root, "Textures", "PaintTargetSplats"));
        Directory.CreateDirectory(folder);
        ReportFailures();

        Keep.Clear();
        int written = 0;
        foreach (var target in HouseManager._.paintTargets)
        {
            if (target == null || !target.setupComplete)
                continue;
            Keep.Add(target.saveId);
            if (PaintTracker.IsSaved(target) && !Failed.Contains(target.saveId))
                continue;

            var texture = PaintReadback.Latest(target);
            // The game destroys paint textures when it clears paint for a load; nothing to save then.
            if (texture == null)
                continue;
            var pixels = PaintReadback.Read(texture, out var format);
            _writer.Write(Path.Combine(folder, target.saveId + ".png"), target.saveId, pixels, format, texture.width, texture.height);
            // Splats still queued on the surface land in its texture next frame, so it isn't saved yet.
            if (PaintTracker.Pending(target) == 0)
                PaintTracker.MarkSaved(target);
            written++;
        }
        Failed.Clear();
        _writer.Prune(folder, new HashSet<int>(Keep));

        PerformanceFixesMod.Instance?.Log.Debug(
            $"Paint: queued {written} of {Keep.Count} painted surfaces in {clock.Elapsed.TotalMilliseconds:0.0} ms.");
    }

    // Pictures the writer couldn't write are written again on the next save.
    private static void ReportFailures()
    {
        _writer.TakeFailures(Failed, Errors);
        if (Errors.Count == 0)
            return;
        PerformanceFixesMod.Instance?.Log.Warn($"Couldn't write {Errors.Count} paint picture(s), trying again now: {string.Join("; ", Errors)}");
        Errors.Clear();
    }
}
