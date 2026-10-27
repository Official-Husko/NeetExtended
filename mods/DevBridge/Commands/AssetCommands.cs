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
