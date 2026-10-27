using Coop.Net;
using LiteNetLib.Utils;
using UnityEngine;

namespace Coop.World.Hazards;

/// <summary>
/// A fire that starts on a client (a spark, a burning thing on the furniture) can't spread there: the
/// client asks the host, the host starts it and its flames come back like any other fire's.
/// </summary>
internal static class FireStarts
{
    private const float Gap = 0.25f;

    private static float _next;

    public static void Clear() => _next = 0f;

    /// <summary>Client: asks the host to start (or feed) the fire in a room. Quiet for a moment after each ask.</summary>
    public static void Request(Location room, Vector3 origin, bool byItem)
    {
        float now = Time.unscaledTime;
        if (now < _next)
            return;
        _next = now + Gap;
        var w = Outbox.Begin(MessageIds.WorldFireStart);
        w.Put((byte)room);
        FireSync.PutPosition(w, origin);
        w.Put(byItem);
        Outbox.ToHost(w, Channel.Events);
    }

    public static void OnStart(in Sender from, NetDataReader reader)
    {
        var room = (Location)reader.GetByte();
        var origin = new Vector3(reader.GetFloat(), reader.GetFloat(), reader.GetFloat());
        bool byItem = reader.GetBool();
        if (!WorldSide.Running || !WorldSide.IsHost || Locator.rooms == null || !Locator.rooms.ContainsKey(room))
            return;
        Locator.StartRoomFire(room, origin, byItem);
    }
}
