using Coop.Session;
using Coop.World;
using Coop.World.Talk;

namespace Coop.Content;

/// <summary>The players as content sees them: who is on the line, who is in the house, their names.</summary>
internal static class StoryPlayers
{
    public const int MaxSlots = 4;

    /// <summary>The player on the line: a remote player whose conversation is running, else this game's.</summary>
    public static byte Caller => TalkRunner.Current != null ? TalkRunner.Current.Slot : WorldSide.LocalSlot;

    public static bool IsHere(int slot)
    {
        if (slot < 0 || slot >= MaxSlots)
            return false;
        foreach (var player in CoopSession.Players)
        {
            if (player.Slot == slot)
                return player.InWorld;
        }
        return false;
    }

    /// <summary>In the house and not the one on the line.</summary>
    public static bool IsOther(int slot) => IsHere(slot) && slot != Caller;

    public static string NameOf(int slot)
    {
        string name = slot >= 0 && slot < MaxSlots ? Speakers.NameOf((byte)slot) : "";
        return string.IsNullOrWhiteSpace(name) ? "that guy" : name;
    }

    /// <summary>A slot from a content file: a number, or "me" for the one on the line.</summary>
    public static int ParseSlot(string text)
    {
        text = (text ?? "").Trim();
        if (string.Equals(text, "me", System.StringComparison.OrdinalIgnoreCase))
            return Caller;
        return int.TryParse(text, out int slot) ? slot : -1;
    }
}
