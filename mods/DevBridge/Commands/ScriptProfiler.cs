using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DevBridge.Server;
using HarmonyLib;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace DevBridge.Commands;

/// <summary>Profiling commands built on <see cref="MethodTimer"/>: per-frame script cost, and what a save spends its time on.</summary>
internal static class ScriptProfiler
{
    private static readonly string[] Messages = { "Update", "LateUpdate", "FixedUpdate", "OnGUI", "OnTriggerStay", "OnCollisionStay" };
    private static readonly string[] GameAssemblies = { "Assembly-CSharp", "Assembly-CSharp-firstpass" };

    [BridgeCommand("perf.scripts", "Time every game script's Update/LateUpdate/FixedUpdate/OnGUI/Stay callbacks for a few seconds.",
        Params = new[] { "seconds?: default 5, at most 10", "top?: default 30" })]
    private static object Scripts(JObject p)
    {
        var result = new Deferred();
        var timer = MethodTimer.Start(GameMethods(Messages));
        DevBridgeMod.Instance.StartCoroutine(RunFor(timer, Mathf.Clamp(p.Float("seconds", 5f), 1f, 10f), p.Int("top", 30), result));
        return result;
    }

    [BridgeCommand("perf.savebreakdown", "Save once and show which steps and which objects' Save methods took the time. Writes a real save file.",
        Params = new[] { "top?: default 25" })]
    private static object SaveBreakdown(JObject p)
    {
        if (SaveLoadHandler._ == null)
            throw new InvalidOperationException("Saving only works in the house.");
        var steps = new List<MethodBase>
        {
            AccessTools.Method(typeof(SaveLoadHandler), "AllSaveDataToJSon"),
            AccessTools.Method(typeof(SaveLoadHandler), "SavePaintSplats"),
            AccessTools.Method(typeof(PaintTarget), "SaveSplat"),
            AccessTools.Method(typeof(JArray), nameof(JArray.FromObject), new[] { typeof(object) }),
            AccessTools.Method(typeof(JObject), nameof(JObject.Parse), new[] { typeof(string) }),
            AccessTools.Method(typeof(Ink.Runtime.StoryState), nameof(Ink.Runtime.StoryState.ToJson)),
            AccessTools.Method(typeof(System.IO.File), nameof(System.IO.File.WriteAllText), new[] { typeof(string), typeof(string) }),
            AccessTools.Method(typeof(System.IO.File), nameof(System.IO.File.WriteAllBytes)),
        };
        var timer = MethodTimer.Start(steps.Where(m => m != null).Concat(GameMethods(new[] { "Save" })));
        var clock = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            SaveLoadHandler._.SaveAll();
        }
        finally
        {
            timer.Stop();
        }
        return new JObject
        {
            ["saveMs"] = clock.ElapsedMilliseconds,
            ["note"] = "Steps contain the methods they call, so their times overlap.",
            ["top"] = timer.Top(p.Int("top", 25)),
        };
    }

    private static IEnumerator RunFor(MethodTimer timer, float seconds, int top, Deferred result)
    {
        int frames = 0;
        float end = Time.realtimeSinceStartup + seconds;
        try
        {
            while (Time.realtimeSinceStartup < end)
            {
                yield return null;
                frames++;
            }
        }
        finally
        {
            timer.Stop();
        }
        result.Complete(new JObject
        {
            ["frames"] = frames,
            ["methodsTimed"] = timer.Patched,
            ["couldNotPatch"] = timer.Failed,
            ["patchingTookMs"] = timer.PatchingMs,
            ["scriptMsPerFrame"] = Math.Round(timer.TotalMs() / Math.Max(1, frames), 2),
            ["top"] = timer.Top(top, frames),
        });
    }

    /// <summary>Declared, non-abstract methods with these names on the game's MonoBehaviours.</summary>
    private static IEnumerable<MethodBase> GameMethods(string[] names)
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (Array.IndexOf(GameAssemblies, assembly.GetName().Name) < 0)
                continue;
            Type[] types;
            try { types = assembly.GetTypes(); }
            catch (ReflectionTypeLoadException e) { types = e.Types.Where(t => t != null).ToArray(); }

            foreach (var type in types)
            {
                if (!typeof(MonoBehaviour).IsAssignableFrom(type) || type.IsGenericTypeDefinition)
                    continue;
                foreach (string name in names)
                {
                    var method = type.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly, null, Type.EmptyTypes, null);
                    if (method != null && !method.IsAbstract && method.GetMethodBody() != null)
                        yield return method;
                }
            }
        }
    }
}
