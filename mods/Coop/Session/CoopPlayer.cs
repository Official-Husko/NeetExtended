namespace Coop.Session;

/// <summary>One person in the session. The host is always slot 0.</summary>
public sealed class CoopPlayer
{
    public const byte HostSlot = 0;

    /// <summary>Stable within a session: message payloads and puppets use it.</summary>
    public byte Slot;

    /// <summary>The transport's peer for this player; -1 for this game's own player.</summary>
    public int Peer = -1;

    public string Name = "";

    /// <summary>The install's player id (CoopConfig.PlayerId), so a returning player can get their body back.</summary>
    public string Id = "";

    public bool IsLocal;

    /// <summary>Done loading the host's world. Only players in the world get world messages.</summary>
    public bool InWorld;

    public bool IsHost => Slot == HostSlot;

    public override string ToString() => $"{Name} (slot {Slot})";
}
