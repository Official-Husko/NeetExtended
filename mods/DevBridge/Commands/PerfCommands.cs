using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DevBridge.Server;
using Newtonsoft.Json.Linq;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Scripting;

namespace DevBridge.Commands;

/// <summary>Measuring the running game: frame times, memory, render counters, and what's in the scene.</summary>
internal static class PerfCommands
{
    // Built-in counters; which ones a release player offers varies, so each reports whether it worked.
    private static readonly (ProfilerCategory Category, string Name)[] Counters =
    {
        (ProfilerCategory.Internal, "Main Thread"),
        (ProfilerCategory.Internal, "Render Thread"),
        (ProfilerCategory.Render, "Draw Calls Count"),
        (ProfilerCategory.Render, "Batches Count"),
        (ProfilerCategory.Render, "SetPass Calls Count"),
        (ProfilerCategory.Render, "Triangles Count"),
        (ProfilerCategory.Render, "Vertices Count"),
        (ProfilerCategory.Render, "Shadow Casters Count"),
        (ProfilerCategory.Render, "Visible Skinned Meshes Count"),
        (ProfilerCategory.Memory, "GC Allocated In Frame"),
        (ProfilerCategory.Memory, "GC Allocation In Frame Count"),
        (ProfilerCategory.Physics, "Physics.Simulate"),
        (ProfilerCategory.Physics, "Physics.Processing"),
        (ProfilerCategory.Scripts, "BehaviourUpdate"),
        (ProfilerCategory.Render, "Camera.Render"),
    };

    [BridgeCommand("perf.frames", "Frame times, memory, GC and render counters over a few seconds.",
        Params = new[] { "seconds?: default 5, at most 12", "uncapped?: lift the frame rate cap and vsync while sampling" })]
    private static object Frames(JObject p)
    {
        var result = new Deferred();
        DevBridgeMod.Instance.StartCoroutine(SampleFrames(Mathf.Clamp(p.Float("seconds", 5f), 1f, 12f), p.Bool("uncapped", false), result));
        return result;
    }

    [BridgeCommand("perf.scene", "What's in the scene that costs time: cameras, lights, renderers, physics, audio, scripts by type, settings.")]
    private static object Scene(JObject p)
    {
        var cameras = new JArray();
        foreach (var cam in Camera.allCameras)
        {
            cameras.Add(new JObject
            {
                ["name"] = PathOf(cam.transform),
                ["depth"] = cam.depth,
                ["target"] = cam.targetTexture != null ? cam.targetTexture.name + $" {cam.targetTexture.width}x{cam.targetTexture.height}" : "screen",
                ["cullingMask"] = cam.cullingMask,
                ["farClip"] = cam.farClipPlane,
            });
        }

        var lights = UnityEngine.Object.FindObjectsOfType<Light>();
        var bodies = UnityEngine.Object.FindObjectsOfType<Rigidbody>();
        var behaviours = UnityEngine.Object.FindObjectsOfType<MonoBehaviour>();

        return new JObject
        {
            ["camerasRendering"] = cameras,
            ["lights"] = new JObject
            {
                ["enabled"] = lights.Count(l => l.enabled),
                ["withShadows"] = lights.Count(l => l.enabled && l.shadows != LightShadows.None),
                ["byType"] = Group(lights.Where(l => l.enabled), l => l.type + (l.shadows != LightShadows.None ? " (shadows)" : "")),
                ["shadowCasting"] = new JArray(lights.Where(l => l.enabled && l.shadows != LightShadows.None).Take(30).Select(l => PathOf(l.transform) + " " + l.type + " range " + l.range).ToArray()),
            },
            ["renderers"] = new JObject
            {
                ["mesh"] = UnityEngine.Object.FindObjectsOfType<MeshRenderer>().Count(r => r.enabled),
                ["meshVisible"] = UnityEngine.Object.FindObjectsOfType<MeshRenderer>().Count(r => r.enabled && r.isVisible),
                ["skinned"] = UnityEngine.Object.FindObjectsOfType<SkinnedMeshRenderer>().Count(r => r.enabled),
                ["skinnedVisible"] = UnityEngine.Object.FindObjectsOfType<SkinnedMeshRenderer>().Count(r => r.enabled && r.isVisible),
                ["particleSystemsPlaying"] = UnityEngine.Object.FindObjectsOfType<ParticleSystem>().Count(s => s.isPlaying),
                ["canvases"] = UnityEngine.Object.FindObjectsOfType<Canvas>().Count(c => c.enabled),
            },
            ["physics"] = new JObject
            {
                ["rigidbodies"] = bodies.Length,
                ["awake"] = bodies.Count(b => !b.IsSleeping() && !b.isKinematic),
                ["kinematic"] = bodies.Count(b => b.isKinematic),
                ["colliders"] = UnityEngine.Object.FindObjectsOfType<Collider>().Length,
                ["fixedDeltaTime"] = Time.fixedDeltaTime,
                ["maximumDeltaTime"] = Time.maximumDeltaTime,
                ["autoSyncTransforms"] = Physics.autoSyncTransforms,
                ["solverIterations"] = Physics.defaultSolverIterations,
            },
            ["audioPlaying"] = UnityEngine.Object.FindObjectsOfType<AudioSource>().Count(a => a.isPlaying),
            ["scripts"] = new JObject
            {
                ["enabled"] = behaviours.Count(b => b.isActiveAndEnabled),
                ["topTypes"] = Group(behaviours.Where(b => b.isActiveAndEnabled), b => b.GetType().Name, 30),
            },
            ["gameObjects"] = UnityEngine.Object.FindObjectsOfType<Transform>().Length,
            ["settings"] = Settings(),
        };
    }

    [BridgeCommand("perf.save", "Time one save (SaveAll), in the house. It writes a real save file.")]
    private static object Save(JObject p)
    {
        if (SaveLoadHandler._ == null)
            throw new InvalidOperationException("Saving only works in the house.");
        var timer = System.Diagnostics.Stopwatch.StartNew();
        SaveLoadHandler._.SaveAll();
        return new JObject { ["saveMs"] = timer.ElapsedMilliseconds };
    }

    [BridgeCommand("perf.gc", "Time full garbage collections (how long one GC freezes the game).", Params = new[] { "count?: default 3" })]
    private static object Gc(JObject p)
    {
        var pauses = new JArray();
        for (int i = 0; i < Mathf.Clamp(p.Int("count", 3), 1, 10); i++)
        {
            var timer = System.Diagnostics.Stopwatch.StartNew();
            GC.Collect();
            pauses.Add(Math.Round(timer.Elapsed.TotalMilliseconds, 2));
        }
        return new JObject
        {
            ["pausesMs"] = pauses,
            ["monoHeapMB"] = Mb(Profiler.GetMonoHeapSizeLong()),
            ["monoUsedMB"] = Mb(Profiler.GetMonoUsedSizeLong()),
        };
    }

    private static JObject Settings() => new JObject
    {
        ["quality"] = QualitySettings.names[QualitySettings.GetQualityLevel()],
        ["vSyncCount"] = QualitySettings.vSyncCount,
        ["targetFrameRate"] = Application.targetFrameRate,
        ["maxQueuedFrames"] = QualitySettings.maxQueuedFrames,
        ["shadows"] = QualitySettings.shadows.ToString(),
        ["shadowResolution"] = QualitySettings.shadowResolution.ToString(),
        ["shadowDistance"] = QualitySettings.shadowDistance,
        ["shadowCascades"] = QualitySettings.shadowCascades,
        ["pixelLightCount"] = QualitySettings.pixelLightCount,
        ["antiAliasing"] = QualitySettings.antiAliasing,
        ["lodBias"] = QualitySettings.lodBias,
        ["realtimeReflectionProbes"] = QualitySettings.realtimeReflectionProbes,
        ["softParticles"] = QualitySettings.softParticles,
        ["screen"] = $"{Screen.width}x{Screen.height} {Screen.fullScreenMode}",
        ["graphics"] = $"{SystemInfo.graphicsDeviceType} {SystemInfo.graphicsDeviceName} ({SystemInfo.graphicsMemorySize} MB)",
        ["cpu"] = $"{SystemInfo.processorType} x{SystemInfo.processorCount}",
        ["systemMemoryMB"] = SystemInfo.systemMemorySize,
        ["is64BitProcess"] = IntPtr.Size == 8,
        ["gcMode"] = GarbageCollector.GCMode.ToString(),
        ["gcIncremental"] = GarbageCollector.isIncremental,
    };

    private static IEnumerator SampleFrames(float seconds, bool uncapped, Deferred result)
    {
        int targetFrameRate = Application.targetFrameRate, vSync = QualitySettings.vSyncCount;
        if (uncapped)
        {
            Application.targetFrameRate = -1;
            QualitySettings.vSyncCount = 0;
        }

        var recorders = new List<(string Name, ProfilerRecorder Recorder)>();
        foreach (var (category, name) in Counters)
        {
            try { recorders.Add((name, ProfilerRecorder.StartNew(category, name, 1))); }
            catch (Exception) { /* Not offered by this player. */ }
        }

        var frames = new List<float>(2048);
        var counterSums = new double[recorders.Count];
        var counterMax = new double[recorders.Count];
        var counterSamples = new int[recorders.Count];
        int gc0 = GC.CollectionCount(0);
        long monoBefore = Profiler.GetMonoUsedSizeLong();
        // Growth of the Mono heap between frames, ignoring the drops when a collection runs: roughly
        // what the game allocates, which decides how often the (non-incremental) GC freezes it.
        long allocated = 0, lastUsed = monoBefore;
        float end = Time.realtimeSinceStartup + seconds;

        yield return null; // Skip the frame the request arrived in.
        while (Time.realtimeSinceStartup < end)
        {
            yield return null;
            frames.Add(Time.unscaledDeltaTime * 1000f);
            long used = Profiler.GetMonoUsedSizeLong();
            if (used > lastUsed)
                allocated += used - lastUsed;
            lastUsed = used;
            for (int i = 0; i < recorders.Count; i++)
            {
                var recorder = recorders[i].Recorder;
                if (!recorder.Valid || recorder.Count == 0)
                    continue;
                double value = recorder.LastValue;
                counterSums[i] += value;
                counterMax[i] = Math.Max(counterMax[i], value);
                counterSamples[i]++;
            }
        }

        if (uncapped)
        {
            Application.targetFrameRate = targetFrameRate;
            QualitySettings.vSyncCount = vSync;
        }

        var counters = new JObject();
        for (int i = 0; i < recorders.Count; i++)
        {
            var (name, recorder) = recorders[i];
            bool time = name.Contains("Thread") || name.Contains(".") || name == "BehaviourUpdate";
            counters[name] = !recorder.Valid || counterSamples[i] == 0
                ? (JToken)"not available"
                : time
                    ? $"avg {counterSums[i] / counterSamples[i] / 1e6:0.00} ms, max {counterMax[i] / 1e6:0.00} ms"
                    : $"avg {counterSums[i] / counterSamples[i]:0}, max {counterMax[i]:0}";
            recorder.Dispose();
        }

        frames.Sort();
        float total = frames.Sum();
        result.Complete(new JObject
        {
            ["seconds"] = seconds,
            ["frames"] = frames.Count,
            ["fps"] = Math.Round(frames.Count / (total / 1000f), 1),
            ["frameMs"] = new JObject
            {
                ["avg"] = Math.Round(total / frames.Count, 2),
                ["p50"] = Percentile(frames, 0.5f),
                ["p95"] = Percentile(frames, 0.95f),
                ["p99"] = Percentile(frames, 0.99f),
                ["max"] = frames.Count > 0 ? Math.Round(frames[frames.Count - 1], 2) : 0,
                ["over33ms"] = frames.Count(f => f > 33.3f),
                ["over50ms"] = frames.Count(f => f > 50f),
            },
            ["gcCollections"] = GC.CollectionCount(0) - gc0,
            ["allocated"] = new JObject
            {
                ["kbPerFrame"] = frames.Count > 0 ? Math.Round(allocated / 1024.0 / frames.Count, 1) : 0,
                ["mbPerSecond"] = Math.Round(allocated / 1048576.0 / Math.Max(0.001, total / 1000f), 2),
            },
            ["memoryMB"] = new JObject
            {
                ["monoUsed"] = Mb(Profiler.GetMonoUsedSizeLong()),
                ["monoHeap"] = Mb(Profiler.GetMonoHeapSizeLong()),
                ["monoUsedChange"] = Mb(Profiler.GetMonoUsedSizeLong() - monoBefore),
                ["unityAllocated"] = Mb(Profiler.GetTotalAllocatedMemoryLong()),
                ["unityReserved"] = Mb(Profiler.GetTotalReservedMemoryLong()),
                ["graphicsDriver"] = Mb(Profiler.GetAllocatedMemoryForGraphicsDriver()),
            },
            ["counters"] = counters,
        });
    }

    private static double Percentile(List<float> sorted, float p) =>
        sorted.Count == 0 ? 0 : Math.Round(sorted[Mathf.Clamp((int)(sorted.Count * p), 0, sorted.Count - 1)], 2);

    private static double Mb(long bytes) => Math.Round(bytes / 1048576.0, 1);

    private static JObject Group<T>(IEnumerable<T> items, Func<T, string> key, int top = 20)
    {
        var groups = new JObject();
        foreach (var group in items.GroupBy(key).OrderByDescending(g => g.Count()).Take(top))
            groups[group.Key] = group.Count();
        return groups;
    }

    private static string PathOf(Transform t)
    {
        string path = t.name;
        for (var parent = t.parent; parent != null; parent = parent.parent)
            path = parent.name + "/" + path;
        return path;
    }
}
