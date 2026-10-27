using Coop.Net;
using UnityEngine;

namespace Coop.World.Flames;

/// <summary>
/// The puff of fire where an overdue tax bill burns up. The host's tax handler makes it for a second
/// as the bill goes (the bill itself is a shared item, and its going is shared); the others make the
/// same puff where it was. It hurts nothing and lights nothing.
///
/// WorldPuff (host to all): position.
/// </summary>
internal static class PuffSync
{
    private const string Prefab = "FireParticle_Small";
    private const float Lives = 1f;

    public static void Init() => Messages.On(MessageIds.WorldPuff, OnPuff);

    /// <summary>The host's tax bill is about to burn up.</summary>
    public static void Burnt(Vector3 at)
    {
        if (!WorldSide.Running || !WorldSide.IsHost)
            return;
        var w = Outbox.Begin(MessageIds.WorldPuff);
        w.Put(at.x);
        w.Put(at.y);
        w.Put(at.z);
        Outbox.ToPlayers(w, Channel.Events);
    }

    private static void OnPuff(in Sender from, LiteNetLib.Utils.NetDataReader reader)
    {
        var at = new Vector3(reader.GetFloat(), reader.GetFloat(), reader.GetFloat());
        var prefab = WorldSide.Running ? Resources.Load<GameObject>(Prefab) : null;
        if (prefab != null)
            Object.Destroy(Object.Instantiate(prefab, at, Quaternion.Euler(-90f, 0f, 0f)), Lives);
    }
}
