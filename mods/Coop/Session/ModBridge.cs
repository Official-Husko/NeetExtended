using System.Collections.Generic;
using Coop.Net;
using LiteNetLib.Utils;
using NeetModKit.Game;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Coop.Session;

/// <summary>
/// Carries the framework's <see cref="HostActions"/> and <see cref="SharedState"/> between games, so
/// content mods (Email Expansion and others) work in co-op without knowing about it: a client's
/// actions run on the host, and what the host publishes reaches everyone, joiners included.
/// </summary>
internal static class ModBridge
{
    private static System.Action<string, JToken> _forward;

    public static void Init(CoopMod mod)
    {
        Messages.On(MessageIds.ModAction, OnAction);
        Messages.On(MessageIds.ModState, OnState);
        SharedState.Published.Subscribe(mod, OnPublished);
        CoopSession.PlayerReady += SendAll;
        CoopSession.WorldReady += UpdateForwarding;
        CoopSession.Resumed += UpdateForwarding;
        CoopSession.Ended += _ => StopForwarding();
        CoopLoop.Unloading += StopForwarding;
        UpdateForwarding();
    }

    private static void UpdateForwarding()
    {
        if (CoopSession.State != SessionState.Playing)
            return;
        _forward ??= Forward;
        HostActions.Forward = _forward;
    }

    private static void StopForwarding()
    {
        if (_forward != null && HostActions.Forward == _forward)
            HostActions.Forward = null;
    }

    private static void Forward(string name, JToken args)
    {
        var message = Outbox.Begin(MessageIds.ModAction);
        message.Put(name);
        message.Put(args?.ToString(Formatting.None) ?? "");
        Outbox.ToHost(message, Channel.Events);
    }

    private static void OnAction(in Sender from, NetDataReader reader)
    {
        if (!CoopSession.IsHost)
            return;
        string name = reader.GetString();
        string json = reader.GetString();
        HostActions.Invoke(name, json.Length > 0 ? JToken.Parse(json) : null, from.Slot);
    }

    private static void OnPublished(KeyValuePair<string, JToken> change)
    {
        if (!CoopSession.IsHost)
            return;
        Outbox.ToPlayers(Write(change.Key, change.Value), Channel.Events);
    }

    // A player who just came in (or resynced) gets everything published so far.
    private static void SendAll(CoopPlayer player)
    {
        if (!CoopSession.IsHost)
            return;
        foreach (var entry in SharedState.All)
            Outbox.ToSlot(player.Slot, Write(entry.Key, entry.Value), Channel.Events);
    }

    private static NetDataWriter Write(string key, JToken value)
    {
        var message = Outbox.Begin(MessageIds.ModState);
        message.Put(key);
        message.Put(value?.ToString(Formatting.None) ?? "null");
        return message;
    }

    private static void OnState(in Sender from, NetDataReader reader)
    {
        if (!CoopSession.IsClient)
            return;
        string key = reader.GetString();
        SharedState.Apply(key, JToken.Parse(reader.GetString()));
    }
}
