namespace Coop.World.Objects;

/// <summary>
/// Every player slot hands out save ids from its own block, so things spawned on different machines
/// never get the same id. The host keeps the game's own counter (far below the first block); slot n
/// counts from n * <see cref="BlockSize"/>. Ids stay positive: the game treats 0 and -1 as "none".
/// </summary>
internal static class IdRanges
{
    public const int BlockSize = 1 << 28;

    public static int Start(byte slot) => slot == WorldSide.HostSlot ? 1 : slot * BlockSize;

    public static byte SlotOf(int id) => (byte)(id / BlockSize);

    public static bool IsLocal(int id) => id > 0 && SlotOf(id) == WorldSide.LocalSlot;

    /// <summary>
    /// Client: moves the game's id counter into this slot's block, past any id from the block that's
    /// already in the house (earlier sessions leave theirs in the host's save).
    /// </summary>
    public static void UseLocalBlock(int atLeast = 0)
    {
        byte slot = WorldSide.LocalSlot;
        if (slot == WorldSide.HostSlot)
            return;
        int next = System.Math.Max(Start(slot), atLeast);
        if (SaveLoadHandler.idSequence < next || SlotOf(SaveLoadHandler.idSequence) != slot)
            SaveLoadHandler.idSequence = next;
    }

    /// <summary>The highest id in <paramref name="slot"/>'s block that the registry knows, plus one.</summary>
    public static int NextFor(byte slot)
    {
        int next = Start(slot);
        foreach (var o in NetObjects.All)
        {
            if (o.Ref.IsDynamic && SlotOf(o.Ref.Id) == slot && o.Ref.Id >= next)
                next = o.Ref.Id + 1;
        }
        return next;
    }
}
