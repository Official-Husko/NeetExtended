using System.Collections.Generic;

namespace NeetModKit.Conversations;

/// <summary>
/// The functions the game itself binds to its story (in Dialogue.Start, SequenceManager.Start and
/// QueenOfDust). Mod scripts can call all of them; they're declared for the compiler automatically.
/// </summary>
public static class GameInkFunctions
{
    /// <summary>Name and parameter names of each function.</summary>
    public static readonly IReadOnlyList<KeyValuePair<string, string[]>> All = new[]
    {
        F("GiveItem", "itemName"),
        F("AddMoney", "amount"),
        F("SetMoney", "amount"),
        F("TradeSoulForClone"),
        F("KillPlayer"),
        F("KillSpeaker"),
        F("SkeletonizePlayer"),
        F("KnockoutPlayer", "seconds"),
        F("SkeletonizeSpeaker", "noSkeleton"),
        F("DestroySpeaker", "delay"),
        F("DisableLootSpeaker"),
        F("SceneSwitchKnockout", "scene"),
        F("SceneSwitchInstant", "scene"),
        F("GiveChange", "offeringValue", "price"),
        F("DestroyReceivedOffering"),
        F("RestoreReceivedOffering"),
        F("ExpelAllFuses"),
        F("SwitchSegmentLine", "character", "segmentLine"),
        F("SetOverrideKnot", "character", "knot"),
        F("AddPlayerSceneSwitchOnDeath", "scene"),
        F("EncounterIncrement"),
        F("TriggerSequence", "name", "arg"),
        F("AbsorbDusts"),
    };

    public static bool Contains(string name)
    {
        foreach (var pair in All)
        {
            if (pair.Key == name)
                return true;
        }
        return false;
    }

    private static KeyValuePair<string, string[]> F(string name, params string[] parameters) =>
        new KeyValuePair<string, string[]>(name, parameters);
}
