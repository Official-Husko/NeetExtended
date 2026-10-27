using Coop.Net;
using LiteNetLib.Utils;
using UnityEngine;

namespace Coop.World.Flames;

/// <summary>
/// A fuse or a BBQ lighter going off leaves a fireball (a flame with a body that burns for a while).
/// The item blows up in the game that simulates it; that game tells the others, and each makes the
/// same fireball where it went off, the same way the game does. The fireball burns everywhere and
/// hurts whoever is in it in their own game. Its blast (damage and push) is passed on separately.
///
/// WorldBlast (any game, passed on by the host): the item's name, where it went off.
/// </summary>
internal static class BlastSync
{
    public static void Init() => Messages.On(MessageIds.WorldBlast, OnBlast);

    /// <summary>The game that simulates the item, as it goes off.</summary>
    public static void Exploded(Usable item, Vector3 at)
    {
        if (!WorldSide.Running || item == null || string.IsNullOrEmpty(item.prefabName))
            return;
        var w = Outbox.Begin(MessageIds.WorldBlast);
        w.Put(item.prefabName);
        w.Put(at.x);
        w.Put(at.y);
        w.Put(at.z);
        Outbox.ToPlayers(w, Channel.Events);
    }

    private static void OnBlast(in Sender from, NetDataReader reader)
    {
        int start = reader.Position;
        int length = reader.AvailableBytes;
        if (WorldSide.IsHost)
            Relay.Forward(MessageIds.WorldBlast, reader.RawData, start, length, Channel.Events, from.Slot);
        string name = reader.GetString();
        var at = new Vector3(reader.GetFloat(), reader.GetFloat(), reader.GetFloat());
        if (!WorldSide.Running)
            return;
        var fireball = FireballOf(name);
        if (fireball != null)
            Object.Instantiate(fireball, at, Quaternion.identity);
    }

    // What the item's own explosion makes: the prefab's explosion object (a fuse) or firecracker (a lighter).
    private static GameObject FireballOf(string prefab)
    {
        if (!PickupBank.pickupsDict.TryGetValue(prefab, out var known) || known.obj == null)
            return null;
        var fuse = known.obj.GetComponentInChildren<Fuse>(true);
        if (fuse != null)
            return fuse.explosionObj;
        var lighter = known.obj.GetComponentInChildren<Lighter_BBQ>(true);
        return lighter != null ? lighter.firecracker : null;
    }
}
