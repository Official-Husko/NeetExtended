using Coop.Net;
using Coop.Session;
using LiteNetLib.Utils;

namespace Coop.World.Painting;

/// <summary>
/// Paint (blood, poop, piss, water, scorch, footprints, cleaner) is shared by replaying splats, not
/// by sending pictures. Every splat the game draws passes through <c>PaintTarget.PaintSplat</c>; the
/// game that draws one sends it (surface id and the splat's exact box), and the others queue the same
/// splat on the same surface. The game that paints is the one that simulates what caused it (the
/// player's own body, the host's NPCs and fires, whoever simulates a thrown item), so nothing is drawn
/// twice. The exceptions: a client's copy of a room fire doesn't scorch (the host's scorch comes, see
/// <see cref="Patches.FireScorchPatch"/>), and running taps paint their water in every game by
/// themselves, so that isn't sent (<see cref="PaintSources"/>). Clients send to the host, which
/// passes it on.
///
/// The host also sends when a surface's water dries (clients don't dry on their own), and keeps the
/// paint made while someone joins for when they're in (<see cref="PaintCatchUp"/>).
///
/// Reliable, ordered (State channel): paint stays, so a lost splat would never be fixed.
/// WorldPaint: see <see cref="PaintOutbox"/>. WorldPaintDry (host to all): surface id (ushort).
/// </summary>
internal static class PaintSync
{
    /// <summary>True while a room fire paints its scorch marks (they're merged, see <see cref="PaintRepeats"/>).</summary>
    public static bool Scorching;

    public static void Init()
    {
        Messages.On(MessageIds.WorldPaint, OnPaint);
        Messages.On(MessageIds.WorldPaintDry, OnDry);
        CoopSession.HouseWanted += player =>
        {
            if (!WorldSide.Running || !WorldSide.IsHost)
                return;
            // What's waiting is either in the snapshot already or goes out to the others now.
            PaintOutbox.Flush();
            PaintCatchUp.Start(player.Slot);
        };
        CoopSession.PlayerReady += player =>
        {
            if (WorldSide.IsHost)
                PaintCatchUp.Deliver(player.Slot);
        };
        CoopSession.PlayerLeft += player => PaintCatchUp.Forget(player.Slot);
    }

    public static void Start()
    {
        Surfaces.Rebuild();
        PaintSources.Rebuild();
        PaintStats.Clear();
    }

    public static void Clear()
    {
        PaintOutbox.Reset();
        PaintRepeats.Clear();
        PaintSources.Clear();
        PaintApplier.Clear();
        SurfaceQueues.Clear();
        PaintTextures.Clear();
        PaintCatchUp.Clear();
        Surfaces.Clear();
    }

    public static void LateTick()
    {
        PaintOutbox.Tick();
        SurfaceQueues.Tick();
    }

    /// <summary>From the PaintSplat prefix: the game is about to queue a splat on a surface.</summary>
    public static void Painted(PaintTarget target, global::Paint paint)
    {
        // A full surface drops the splat here too.
        if (!WorldSide.Running || SurfaceQueues.Replaying || paint == null || target == null || SurfaceQueues.IsFull(target))
            return;
        bool shared = Surfaces.IsShared(target);
        bool mirrored = shared && PaintSources.Mirrored();
        if (PaintTrace.On)
            PaintTrace.Note(shared && !mirrored);
        if (!shared)
        {
            PaintStats.Skipped++;
            return;
        }
        if (mirrored)
        {
            PaintStats.Mirrored++;
            return;
        }
        PaintOutbox.Add(target, paint, merge: Scorching);
    }

    /// <summary>From ClearWaterChannel: the host's water on a surface dried.</summary>
    public static void Dried(PaintTarget target)
    {
        if (!WorldSide.Running || !WorldSide.IsHost || SurfaceQueues.Replaying || !Surfaces.IsShared(target))
            return;
        // Splats made before it dried go first, as they were drawn first.
        PaintOutbox.Flush();
        var w = Outbox.Begin(MessageIds.WorldPaintDry);
        w.Put((ushort)target.saveId);
        Send(w);
    }

    /// <summary>A finished paint message from this game: to the host, or from the host to everyone.</summary>
    public static void Send(NetDataWriter message)
    {
        if (WorldSide.IsHost)
        {
            Outbox.ToPlayers(message, Channel.State);
            PaintCatchUp.Record(message);
        }
        else
            Outbox.ToHost(message, Channel.State);
    }

    private static void OnPaint(in Sender from, NetDataReader reader)
    {
        if (WorldSide.IsHost)
        {
            var w = Outbox.Begin(MessageIds.WorldPaint);
            w.Put(reader.RawData, reader.Position, reader.AvailableBytes);
            Outbox.ToPlayers(w, Channel.State, exceptSlot: from.Slot);
            PaintCatchUp.Record(w);
        }
        if (WorldSide.Running)
            PaintApplier.Apply(reader);
    }

    private static void OnDry(in Sender from, NetDataReader reader)
    {
        int id = reader.GetUShort();
        var target = WorldSide.IsHost || !WorldSide.Running ? null : Surfaces.Find(id);
        if (target == null || !target.setupComplete)
            return;
        PaintStats.Dried++;
        target.ClearWaterChannel();
    }
}
