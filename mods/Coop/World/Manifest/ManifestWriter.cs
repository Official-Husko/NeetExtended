using System.Diagnostics;
using Coop.Net;
using Coop.Session;
using Coop.World.Objects;
using Coop.World.State;

namespace Coop.World.Manifest;

/// <summary>
/// Host: the whole shared house as it is now, for a player who just loaded it or asked again after a
/// hot reload. It travels on the same ordered lane as spawns and destroys, so nothing sent after it
/// can overtake it.
///
/// WorldManifest: next free id for the player's block, money, count, then per thing:
/// ref, prefab name ("" for scene objects), owner, holder, flags, position, rotation, scale;
/// then every shared component's state (see <see cref="StateSync.WriteAll"/>).
/// </summary>
internal static class ManifestWriter
{
    public static void Send(CoopPlayer player)
    {
        var timer = Stopwatch.StartNew();
        var computer = HouseManager._?.mainComputer;
        var all = NetObjects.All;
        int count = 0;
        for (int i = 0; i < all.Count; i++)
        {
            if (all[i].Alive)
                count++;
        }

        var w = Outbox.Begin(MessageIds.WorldManifest);
        w.Put(IdRanges.NextFor(player.Slot));
        w.Put(computer != null ? computer.Money : 0f);
        w.Put(count);
        for (int i = 0; i < all.Count; i++)
        {
            var o = all[i];
            if (!o.Alive)
                continue;
            var t = o.Tf;
            var p = t.position;
            var r = t.rotation;
            var s = t.localScale;
            o.Ref.Write(w);
            w.Put(o.IsSpawnable ? o.PrefabName : "");
            w.Put(o.Owner);
            w.Put(o.Holder);
            w.Put(BodyFlags.Of(o));
            w.Put(p.x);
            w.Put(p.y);
            w.Put(p.z);
            w.Put(r.x);
            w.Put(r.y);
            w.Put(r.z);
            w.Put(r.w);
            w.Put(s.x);
            w.Put(s.y);
            w.Put(s.z);
        }
        StateSync.WriteAll(w);
        int size = w.Length;
        Outbox.ToSlot(player.Slot, w, Channel.Events);
        CoopMod.Log.Info($"World: sent {player} the house's {count} shared things ({size / 1024} KB, {timer.ElapsedMilliseconds} ms).");
    }
}
