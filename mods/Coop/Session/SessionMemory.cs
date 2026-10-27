using System.Collections.Generic;
using Coop.Net;
using NeetLoader.Runtime;
using Newtonsoft.Json.Linq;

namespace Coop.Session;

/// <summary>
/// Carries the session across a hot reload as plain JSON (the next copy of the mod has its own types).
/// The connection itself is carried by the transport.
/// </summary>
internal static class SessionMemory
{
    private const string Key = "Coop.Session";

    public static void Park()
    {
        if (!CoopSession.IsActive)
            return;
        var players = new JArray();
        foreach (var p in CoopSession.Players)
        {
            players.Add(new JObject
            {
                ["slot"] = p.Slot,
                ["peer"] = p.Peer,
                ["name"] = p.Name,
                ["id"] = p.Id,
                ["local"] = p.IsLocal,
                ["inWorld"] = p.InWorld,
            });
        }
        var state = new JObject
        {
            ["transport"] = CoopSession.TransportId,
            ["state"] = (int)CoopSession.State,
            ["hostPeer"] = CoopSession.HostPeer,
            ["localSlot"] = CoopSession.Local?.Slot ?? 0,
            ["players"] = players,
        };
        ReloadState.Put(Key, state.ToString());
    }

    /// <summary>A session parked by the previous copy of the mod, as JSON, or null.</summary>
    public static JObject TakeParked() =>
        ReloadState.Take(Key, out string json) ? JObject.Parse(json) : null;

    public static string TransportOf(JObject state) => state.Value<string>("transport") ?? Transports.DirectId;

    /// <summary>Restores a parked session once its transport is running again.</summary>
    public static void Restore(JObject state)
    {
        var players = new List<CoopPlayer>();
        foreach (var p in (JArray)state["players"])
        {
            players.Add(new CoopPlayer
            {
                Slot = (byte)p.Value<int>("slot"),
                Peer = p.Value<int>("peer"),
                Name = p.Value<string>("name"),
                Id = p.Value<string>("id"),
                IsLocal = p.Value<bool>("local"),
                InWorld = p.Value<bool>("inWorld"),
            });
        }
        CoopSession.Restore((SessionState)state.Value<int>("state"), state.Value<int>("hostPeer"), players, (byte)state.Value<int>("localSlot"));
    }
}
