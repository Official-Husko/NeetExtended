using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Newtonsoft.Json.Linq;

namespace DevBridge.Commands;

/// <summary>
/// Times methods by patching each with a stopwatch, for profiling a release build that has no
/// profiler. Start, let the game run (or do one thing), Stop, read the results. Timers add a
/// little overhead per call, so compare methods with each other. With allocations on, it also adds
/// up how much the Mono heap grew during each call (a method's callees count towards it, like time).
/// The heap grows in blocks, so this is only meaningful over many calls.
/// </summary>
internal sealed class MethodTimer
{
    private sealed class Timing
    {
        public long Ticks;
        public long Calls;
        public long MaxTicks;
        public long Allocated;
    }

    private static readonly Dictionary<MethodBase, Timing> Timings = new Dictionary<MethodBase, Timing>();
    private static MethodTimer _active;

    private readonly Harmony _harmony = new Harmony("DevBridge.MethodTimer");

    public int Patched { get; private set; }
    public int Failed { get; private set; }
    public long PatchingMs { get; private set; }

    public static bool Running => _active != null;

    public bool TracksAllocations { get; private set; }

    public static MethodTimer Start(IEnumerable<MethodBase> methods, bool allocations = false)
    {
        if (_active != null)
            throw new InvalidOperationException("A timing is already running.");
        var timer = new MethodTimer { TracksAllocations = allocations };
        _active = timer;
        Timings.Clear();
        var prefix = new HarmonyMethod(AccessTools.Method(typeof(MethodTimer), allocations ? nameof(AllocPrefix) : nameof(Prefix)));
        var postfix = new HarmonyMethod(AccessTools.Method(typeof(MethodTimer), allocations ? nameof(AllocPostfix) : nameof(Postfix)));
        var clock = Stopwatch.StartNew();
        foreach (var method in methods.Distinct())
        {
            try
            {
                timer._harmony.Patch(method, prefix, postfix);
                Timings[method] = new Timing();
                timer.Patched++;
            }
            catch (Exception)
            {
                timer.Failed++;
            }
        }
        timer.PatchingMs = clock.ElapsedMilliseconds;
        return timer;
    }

    public void Stop()
    {
        _harmony.UnpatchAll(_harmony.Id);
        _active = null;
    }

    /// <summary>
    /// The slowest methods by total time, or the biggest allocators when allocations are tracked.
    /// <paramref name="frames"/> > 0 adds per-frame figures.
    /// </summary>
    public JArray Top(int count, int frames = 0)
    {
        double tickMs = 1000.0 / Stopwatch.Frequency;
        var rows = new JArray();
        var ordered = TracksAllocations
            ? Timings.Where(t => t.Value.Calls > 0).OrderByDescending(t => t.Value.Allocated)
            : Timings.Where(t => t.Value.Calls > 0).OrderByDescending(t => t.Value.Ticks);
        foreach (var pair in ordered.Take(count))
        {
            var t = pair.Value;
            var row = new JObject
            {
                ["method"] = pair.Key.DeclaringType?.Name + "." + pair.Key.Name,
                ["totalMs"] = Math.Round(t.Ticks * tickMs, 2),
                ["calls"] = t.Calls,
                ["usPerCall"] = Math.Round(t.Ticks * tickMs * 1000.0 / t.Calls, 2),
                ["maxMs"] = Math.Round(t.MaxTicks * tickMs, 2),
            };
            if (TracksAllocations)
                row["allocatedKB"] = Math.Round(t.Allocated / 1024.0, 1);
            if (frames > 0)
            {
                row["msPerFrame"] = Math.Round(t.Ticks * tickMs / frames, 3);
                row["callsPerFrame"] = Math.Round((double)t.Calls / frames, 1);
                if (TracksAllocations)
                    row["allocatedKBPerFrame"] = Math.Round(t.Allocated / 1024.0 / frames, 2);
            }
            rows.Add(row);
        }
        return rows;
    }

    public double TotalMs() => Timings.Values.Sum(t => t.Ticks) * 1000.0 / Stopwatch.Frequency;

    private struct AllocState
    {
        public long Start;
        public long Used;
    }

    private static void Prefix(out long __state) => __state = Stopwatch.GetTimestamp();

    private static void Postfix(MethodBase __originalMethod, long __state)
    {
        long elapsed = Stopwatch.GetTimestamp() - __state;
        if (!Timings.TryGetValue(__originalMethod, out var timing))
            return;
        timing.Ticks += elapsed;
        timing.Calls++;
        if (elapsed > timing.MaxTicks)
            timing.MaxTicks = elapsed;
    }

    private static void AllocPrefix(out AllocState __state) =>
        __state = new AllocState { Start = Stopwatch.GetTimestamp(), Used = UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong() };

    private static void AllocPostfix(MethodBase __originalMethod, AllocState __state)
    {
        long elapsed = Stopwatch.GetTimestamp() - __state.Start;
        long grown = UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong() - __state.Used;
        if (!Timings.TryGetValue(__originalMethod, out var timing))
            return;
        timing.Ticks += elapsed;
        timing.Calls++;
        if (elapsed > timing.MaxTicks)
            timing.MaxTicks = elapsed;
        if (grown > 0)
            timing.Allocated += grown;
    }
}
