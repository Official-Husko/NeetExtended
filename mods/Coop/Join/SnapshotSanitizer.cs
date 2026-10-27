using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Coop.Join;

/// <summary>
/// Turns the host's save into the one a joining player loads: the world stays exactly the host's,
/// but the body in it is the joiner's own (the one they had last time, or a new one), and the host's
/// open conversation is closed.
/// </summary>
internal static class SnapshotSanitizer
{
    // How far in front of the host a new player turns up.
    private const float SpawnDistance = 1.2f;

    /// <param name="storedBody">The joiner's body from an earlier session (BodyBook), or null.</param>
    public static void ForJoiner(JObject save, string storedBody)
    {
        var entries = save["Save"] as JArray;
        if (entries == null)
            return;
        foreach (var entry in entries)
        {
            switch (entry.Value<string>("type"))
            {
                case "Brain":
                    if (entry["basicBrainData"]?.Value<string>("instanceName") == "Player")
                    {
                        var brain = (JObject)entry["basicBrainData"];
                        if (storedBody == null || !BodyRestore.TryRestore(brain, storedBody, ItemIds(entries)))
                            FreshBody(brain);
                    }
                    break;
                case "Dialogue":
                    CloseConversation((JObject)entry);
                    break;
            }
        }
    }

    /// <summary>
    /// The game loads a body's parts (movement, hands and clothes, stats, status effects) from its
    /// sub-saves; without them it starts like a new player: full needs, empty hands, the default clothes.
    /// </summary>
    private static void FreshBody(JObject brain)
    {
        brain["subSaveData"] = new JArray();
        brain["velocity"] = new JArray(0f, 0f, 0f);

        var position = brain["position"] as JArray;
        var rotation = brain["rotation"] as JArray;
        if (position == null || rotation == null)
            return;
        var at = new Vector3(position[0].Value<float>(), position[1].Value<float>(), position[2].Value<float>());
        var facing = new Quaternion(rotation[0].Value<float>(), rotation[1].Value<float>(), rotation[2].Value<float>(), rotation[3].Value<float>());
        var forward = facing * Vector3.forward;
        forward.y = 0f;
        at += forward.normalized * SpawnDistance;
        brain["position"] = new JArray(at.x, at.y, at.z);
        // Face the host.
        var turned = facing * Quaternion.Euler(0f, 180f, 0f);
        brain["rotation"] = new JArray(turned.x, turned.y, turned.z, turned.w);
    }

    // A conversation the host is in would reopen on the joiner's screen with a speaker that may not load.
    private static void CloseConversation(JObject dialogue)
    {
        dialogue["currSpeakerId"] = "x";
        dialogue["storyTextEnabled"] = false;
        dialogue["currentText"] = "";
        dialogue["bucketActive"] = false;
        dialogue["currKnot"] = "";
    }

    private static HashSet<int> ItemIds(JArray entries)
    {
        var ids = new HashSet<int>();
        foreach (var entry in entries)
        {
            if (entry.Value<string>("type") == "Usable" && entry["saveId"] != null)
                ids.Add(entry.Value<int>("saveId"));
        }
        return ids;
    }
}
