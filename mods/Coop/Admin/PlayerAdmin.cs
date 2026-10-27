using Coop.Net;
using Coop.Players;
using Coop.Session;
using Coop.World;
using Coop.World.Hostiles;
using LiteNetLib.Utils;
using NeetModKit.Game;
using NeetModKit.Ui.Notifications;
using UnityEngine;

namespace Coop.Admin;

/// <summary>
/// The host's hand on the players (the Players tab). What happens in the house (an item, a fire,
/// a spider, the hitman) is done in the host's world and shared like anything else; what happens to
/// a body goes to that player's game and is done to their real body (<see cref="AdminEffects"/>),
/// and they're told who did it. Only the host can do any of it.
///
/// PlayerAdmin (Events, host to one client): action, a point (where to go), a label (what they got).
/// </summary>
internal static class PlayerAdmin
{
    private const float HitmanDelay = 5f;
    private const float ToldSeconds = 4f;

    public static void Init()
    {
        Messages.On(MessageIds.PlayerAdmin, OnAdmin);
        CoopSession.Ended += _ => AdminEffects.Unfreeze();
        CoopLoop.Unloading += AdminEffects.Unfreeze;
    }

    /// <summary>Host: does it to the player in <paramref name="slot"/>. Says what happened, for the menu.</summary>
    public static string Do(byte slot, AdminAction action, string item = null)
    {
        if (!CoopSession.IsHost || !WorldSide.Running)
            return "Only the host of a game in the house can do that.";
        var player = CoopSession.FindSlot(slot);
        if (player == null || (!player.IsLocal && !player.InWorld))
            return "They're not in the house.";
        string who = player.IsLocal ? "yourself" : player.Name;

        if (AdminActions.OnTheirBody(action))
        {
            var point = action == AdminAction.BringHere ? Spawning.InFrontOfPlayer(1.2f) : Vector3.zero;
            if (action == AdminAction.BringHere && player.IsLocal)
                return "You're already here.";
            if (player.IsLocal)
                AdminEffects.Apply(action, point);
            else
                Send(slot, action, point, "");
            return Done(action, who);
        }

        var body = player.IsLocal ? GameRefs.Player?.transform : RemotePlayers.Find(slot)?.transform;
        if (body == null)
            return "Their body isn't here yet.";
        var near = body.position + Vector3.up * 0.5f + body.forward * 0.9f;
        switch (action)
        {
            case AdminAction.GoThere:
                if (player.IsLocal)
                    return "You're already there.";
                PlayerControl.Teleport(body.position - body.forward * 1.2f);
                PlayerControl.Look(body.forward);
                return $"Went to {who}.";
            case AdminAction.Give:
                if (string.IsNullOrEmpty(item) || !Spawning.IsItem(item))
                    return "Pick an item.";
                Spawning.Item(item, near);
                if (!player.IsLocal)
                    Send(slot, action, Vector3.zero, item);
                return $"Gave {who} a {item}.";
            case AdminAction.Kick:
                if (player.IsLocal)
                    return "You can't kick yourself. Leave from the Co-op tab.";
                CoopMod.Log.Info($"The host kicked {player}.");
                CoopSession.Transport.Kick(player.Peer, "The host kicked you out of the game.");
                return $"Kicked {who}.";
            case AdminAction.Hitman:
                if (Hitman.Busy)
                    return "The hitman is already on a job.";
                return Hitman.Send(slot, HitmanDelay) ? $"The hitman is coming for {who}." : "The hitman can't find them.";
            case AdminAction.Fire:
                HouseSystems.StartFire(Rooms.At(body.position));
                return $"Set {who}'s room on fire.";
            case AdminAction.Spider:
                return SpawnNear("Spider_House", near, who);
            case AdminAction.Rat:
                return SpawnNear("Rat", near, who);
            case AdminAction.Poopman:
                return SpawnNear("Poopman", near, who);
        }
        return "Nothing happened.";
    }

    private static string SpawnNear(string npc, Vector3 near, string who)
    {
        Spawning.Npc(npc, near);
        return $"Sent {who} a {npc.Replace('_', ' ')}.";
    }

    private static string Done(AdminAction action, string who) => action switch
    {
        AdminAction.Heal => $"Patched {who} up.",
        AdminAction.BringHere => $"Pulled {who} over.",
        AdminAction.Slap => $"Slapped {who}.",
        AdminAction.Yeet => $"Yeeted {who}.",
        AdminAction.Spin => $"Spun {who} around.",
        AdminAction.Freeze => $"Froze {who} for a few seconds.",
        AdminAction.Starve => $"Made {who} starving.",
        AdminAction.PissAlarm => $"Filled {who}'s bladder.",
        AdminAction.Shit => $"Made {who} shit.",
        AdminAction.KnockDown => $"Knocked {who} down.",
        _ => "Done.",
    };

    private static void Send(byte slot, AdminAction action, Vector3 point, string label)
    {
        var w = Outbox.Begin(MessageIds.PlayerAdmin);
        w.Put((byte)action);
        w.Put(point.x);
        w.Put(point.y);
        w.Put(point.z);
        w.Put(label ?? "");
        Outbox.ToSlot(slot, w, Channel.Events);
    }

    private static void OnAdmin(in Sender from, NetDataReader reader)
    {
        if (!CoopSession.IsClient || from.Slot != CoopPlayer.HostSlot || !CoopSession.InWorld)
            return;
        var action = (AdminAction)reader.GetByte();
        var point = new Vector3(reader.GetFloat(), reader.GetFloat(), reader.GetFloat());
        string label = reader.GetString(128);
        if (AdminActions.OnTheirBody(action))
            AdminEffects.Apply(action, point);
        string told = AdminActions.Told(action);
        if (told == null || CoopMod.Instance == null)
            return;
        string host = CoopSession.FindSlot(CoopPlayer.HostSlot)?.Name ?? "The host";
        Notices.Show(CoopMod.Instance, $"{host} {told}", action == AdminAction.Give ? label : "", seconds: ToldSeconds);
    }
}
