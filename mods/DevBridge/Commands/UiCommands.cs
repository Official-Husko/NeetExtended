using System;
using DevBridge.Server;
using NeetModKit.Desktop;
using NeetModKit.Scenes;
using Newtonsoft.Json.Linq;
using UnityEngine.UI;

namespace DevBridge.Commands;

/// <summary>Driving uGUI from outside, e.g. the computer's buttons.</summary>
internal static class UiCommands
{
    [BridgeCommand("ui.click", "Run a button's click handlers, even if the game currently blocks clicks on it.",
        Params = new[] { "target: path or #id of the button" })]
    private static object Click(JObject p)
    {
        var go = SceneQuery.Resolve(p.Str("target"));
        var button = go.GetComponent<Button>() ?? throw new ArgumentException($"{go.name} isn't a button.");
        button.onClick.Invoke();
        return "clicked";
    }

    [BridgeCommand("ui.text", "Read the text of a label (TextMeshPro or legacy Text) on or under an object.",
        Params = new[] { "target: path or #id" })]
    private static object Text(JObject p)
    {
        var go = SceneQuery.Resolve(p.Str("target"));
        var tmp = go.GetComponentInChildren<TMPro.TMP_Text>(true);
        if (tmp != null)
            return tmp.text;
        var legacy = go.GetComponentInChildren<Text>(true);
        return legacy != null ? legacy.text : throw new ArgumentException($"{go.name} has no text.");
    }
}
