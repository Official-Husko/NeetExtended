using System;
using System.Collections.Generic;
using System.Linq;
using DevBridge.Server;
using HarmonyLib;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace DevBridge.Commands;

/// <summary>What the game's save is made of: every object subscribed to SaveAll, and which of them fail.</summary>
internal static class SaveCommands
{
    private static readonly AccessTools.FieldRef<SaveLoadHandler, Func<SaveData>> OnSave =
        AccessTools.FieldRefAccess<SaveLoadHandler, Func<SaveData>>("OnSave");

    [BridgeCommand("save.check", "Run every save subscriber on its own: counts by type, plus the ones that throw or belong to destroyed objects.",
        Params = new[] { "max?: failures to list, default 20" })]
    private static object Check(JObject p)
    {
        var handler = SaveLoadHandler._ ?? throw new InvalidOperationException("Only in the house.");
        var subscribers = OnSave(handler)?.GetInvocationList() ?? Array.Empty<Delegate>();
        var counts = new SortedDictionary<string, int>();
        var failures = new JArray();
        int failed = 0, destroyed = 0, max = p.Int("max", 20);

        foreach (var subscriber in subscribers)
        {
            string type = subscriber.Target?.GetType().Name ?? subscriber.Method.DeclaringType?.Name ?? "?";
            counts[type] = counts.TryGetValue(type, out int n) ? n + 1 : 1;

            bool gone = subscriber.Target is UnityEngine.Object unityObject && unityObject == null;
            if (gone)
                destroyed++;
            try
            {
                ((Func<SaveData>)subscriber)();
                if (!gone)
                    continue;
            }
            catch (Exception e)
            {
                failed++;
                if (failures.Count < max)
                    failures.Add(Describe(subscriber, gone, e));
                continue;
            }
            if (failures.Count < max)
                failures.Add(Describe(subscriber, true, null));
        }

        return new JObject
        {
            ["subscribers"] = subscribers.Length,
            ["throwing"] = failed,
            ["destroyed"] = destroyed,
            ["byType"] = JObject.FromObject(counts),
            ["problems"] = failures,
        };
    }

    private static JObject Describe(Delegate subscriber, bool destroyed, Exception error)
    {
        var entry = new JObject
        {
            ["type"] = subscriber.Target?.GetType().FullName,
            ["method"] = subscriber.Method.Name,
            ["destroyed"] = destroyed,
        };
        if (subscriber.Target is UnityEngine.Object unityObject)
        {
            // The instance id survives destruction, the name doesn't.
            entry["instanceId"] = unityObject.GetInstanceID();
            if (!destroyed)
                entry["name"] = unityObject.name;
        }
        if (subscriber.Target is Usable usable)
        {
            entry["prefab"] = usable.prefabName;
            entry["saveId"] = usable.saveId;
        }
        if (error != null)
        {
            var root = error.GetBaseException();
            entry["error"] = $"{root.GetType().Name}: {root.Message}";
            entry["at"] = root.StackTrace?.Split('\n').FirstOrDefault(line => !line.Contains("UnityEngine.Object"))?.Trim();
        }
        return entry;
    }
}
