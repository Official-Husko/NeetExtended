using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Coop.Join;

/// <summary>
/// Puts a returning player's stored body into the joiner's Player entry: where they stood, their
/// stats, status effects and clothes. Hands come back empty (whatever they held was dropped when they
/// left), and nobody comes back dead. The game looks worn items up by id while loading and stops the whole load if one is
/// missing, so clothes that aren't in the house any more fall back to the default outfit.
/// </summary>
internal static class BodyRestore
{
    private const string Abilities = "Player_Abilities";

    public static bool TryRestore(JObject brain, string storedBody, HashSet<int> itemIds)
    {
        JObject stored;
        try
        {
            stored = JObject.Parse(storedBody);
        }
        catch (Newtonsoft.Json.JsonException e)
        {
            CoopMod.Log.Warn($"A stored body couldn't be read, so the player gets a new one: {e.Message}");
            return false;
        }
        if (!(stored["subSaveData"] is JArray parts) || !(stored["position"] is JArray) || !(stored["rotation"] is JArray))
            return false;

        var kept = new JArray();
        foreach (var part in parts)
        {
            if (part.Value<string>("type") != Abilities)
                kept.Add(part);
            else if (EmptyHands((JObject)part, itemIds))
                kept.Add(part);
        }

        brain["position"] = stored["position"];
        brain["rotation"] = stored["rotation"];
        brain["velocity"] = new JArray(0f, 0f, 0f);
        brain["subSaveData"] = kept;
        return true;
    }

    /// <summary>
    /// Empties the hands and clears what the body was in the middle of (dying, peeing, puking); false
    /// when a worn item is gone (leave the part out: default outfit).
    /// </summary>
    private static bool EmptyHands(JObject abilities, HashSet<int> itemIds)
    {
        abilities["pickupLeftId"] = -1;
        abilities["pickupRightId"] = -1;
        abilities["dead"] = false;
        abilities["peeing"] = false;
        abilities["vomiting"] = false;
        if (!(abilities["clothingIds"] is JArray worn))
            return true;
        for (int i = 0; i < worn.Count; i++)
        {
            int id = worn[i].Value<int>();
            // Shoes, pants and shirt use 0 for "none", the mask -1.
            bool none = i < 3 ? id <= 0 : id == -1;
            if (!none && !itemIds.Contains(id))
                return false;
        }
        return true;
    }
}
