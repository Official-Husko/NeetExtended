using Coop.Net;
using Coop.Session;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Coop.Players;

/// <summary>
/// Client: sends this player's body to the host every few seconds, as the game would save it, so the
/// host's save keeps it (<see cref="BodyBook"/>).
/// </summary>
internal static class BodySender
{
    private const float Interval = 10f;

    private static float _next;

    public static void Init() => CoopLoop.Update += Tick;

    private static void Tick()
    {
        if (CoopSession.State != SessionState.Playing || Time.time < _next)
            return;
        _next = Time.time + Interval;
        var player = HouseManager._ != null ? HouseManager._.player : null;
        if (player == null || !player.TryGetComponent(out BrainPlayer brain))
            return;

        var body = JObject.FromObject(brain.Save())["basicBrainData"];
        if (body == null)
            return;
        var message = Outbox.Begin(MessageIds.Body);
        message.Put(body.ToString(Formatting.None));
        Outbox.ToHost(message, Channel.State);
    }
}
