using System;
using System.IO;
using System.Linq;
using DevBridge.Server;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace DevBridge.Commands;

/// <summary>The game's loaded assets: text assets such as the compiled Ink stories.</summary>
internal static class AssetCommands
{
    [BridgeCommand("assets.texts", "Every loaded TextAsset by name and size.")]
    private static object Texts(JObject p) =>
        new JArray(Resources.FindObjectsOfTypeAll<TextAsset>()
            .Where(t => t != null)
            .OrderBy(t => t.name)
            .Select(t => (JToken)$"{t.name} ({Length(t)} chars)")
            .ToArray());

    // Some engine-internal text assets throw when read.
    private static int Length(TextAsset asset)
    {
        try { return asset.text?.Length ?? 0; }
        catch (Exception) { return -1; }
    }

    [BridgeCommand("items.banks", "Every PickupBank (the game's item lists: the main one, each store, the custom shops) with its items and their components.",
        Params = new[] { "filter?: bank name substring" })]
    private static object Banks(JObject p)
    {
        string filter = p.Value<string>("filter");
        var result = new JArray();
        foreach (var bank in Resources.FindObjectsOfTypeAll<PickupBank>().OrderBy(b => b.name))
        {
            if (filter != null && bank.name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0)
                continue;
            var items = new JArray();
            foreach (var pickup in bank.pickups ?? new System.Collections.Generic.List<Spawnable>())
            {
                if (pickup.obj == null)
                    continue;
                var components = pickup.obj.GetComponentsInChildren<MonoBehaviour>(true)
                    .Where(c => c != null)
                    .Select(c => c.GetType().Name)
                    .Distinct();
                items.Add(new JObject { ["name"] = pickup.obj.name, ["components"] = new JArray(components.Cast<object>().ToArray()) });
            }
            result.Add(new JObject { ["bank"] = bank.name, ["type"] = bank.pickupBankType.ToString(), ["items"] = items });
        }
        return result;
    }

    [BridgeCommand("assets.text", "Write a loaded TextAsset to a file (a Windows or Z: path inside Proton).",
        Params = new[] { "name", "path" })]
    private static object Text(JObject p)
    {
        string name = p.Str("name");
        var asset = Resources.FindObjectsOfTypeAll<TextAsset>().FirstOrDefault(t => t.name == name)
                    ?? throw new ArgumentException($"No TextAsset called '{name}'. See assets.texts.");
        string path = p.Str("path");
        File.WriteAllText(path, asset.text);
        return new JObject { ["path"] = path, ["chars"] = asset.text.Length };
    }
}
