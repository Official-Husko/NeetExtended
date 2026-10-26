using System.Linq;
using DevBridge.Server;
using NeetLoader;
using NeetLoader.Mods;
using NeetModKit.Events;
using NeetModKit.Game;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DevBridge.Commands;

internal static class CoreCommands
{
    [BridgeCommand("ping", "Check the bridge is up.")]
    private static object Ping(JObject p) => "pong";

    [BridgeCommand("help", "List every bridge method.")]
    private static object Help(JObject p) =>
        new JArray(DevBridgeMod.Instance.Commands.All.Select(c => new JObject
        {
            ["method"] = c.Name,
            ["description"] = c.Description,
            ["params"] = new JArray(c.Params.Cast<object>().ToArray()),
        }));

    [BridgeCommand("game.info", "Scene, versions, mods, fps and time.")]
    private static object Info(JObject p) => new JObject
    {
        ["scene"] = SceneManager.GetActiveScene().name,
        ["houseReady"] = GameEvents.IsHouseReady,
        ["loaderVersion"] = Loader.Version,
        ["unityVersion"] = Application.unityVersion,
        ["fps"] = Mathf.Round(1f / Mathf.Max(Time.smoothDeltaTime, 0.0001f)),
        ["timeScale"] = Time.timeScale,
        ["time"] = Mathf.Round(Time.time),
        ["mods"] = new JArray(ModRegistry.Mods.Select(m => $"{m.Id} {m.Manifest.Version} ({m.State})").ToArray()),
    };

    [BridgeCommand("game.quit", "Close the game.")]
    private static object Quit(JObject p)
    {
        Application.Quit();
        return "bye";
    }

    [BridgeCommand("game.save", "Save now, like the autosave.")]
    private static object Save(JObject p)
    {
        if (SaveLoadHandler._ == null)
            throw new System.InvalidOperationException("Saving only works in the house.");
        SaveLoadHandler._.SaveAll();
        return "saved";
    }

    [BridgeCommand("game.timescale", "Set Time.timeScale.", Params = new[] { "value: 0 pauses, 1 is normal" })]
    private static object TimeScale(JObject p)
    {
        Time.timeScale = Mathf.Max(0f, p.Float("value"));
        return Time.timeScale;
    }

    [BridgeCommand("scene.load", "Load a scene by name.", Params = new[] { "name: MainMenu, Tutorial, NEET_House, DreamZone, Nightmare1" })]
    private static object LoadScene(JObject p)
    {
        SceneManager.LoadScene(p.Str("name"));
        return "loading";
    }

    [BridgeCommand("rooms.list", "Every room in the scene with its centre.")]
    private static object RoomsList(JObject p) =>
        new JObject(Rooms.All.Where(Rooms.Exists).Select(r => new JProperty(r.ToString(), NeetModKit.Values.JsonValues.ToJson(Rooms.Center(r).Value))));
}
