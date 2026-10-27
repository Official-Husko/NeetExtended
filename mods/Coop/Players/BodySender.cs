using Coop.Net;
using Coop.Session;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Coop.Players;

/// <summary>
/// Client: sends this player's body to the host every few seconds, and right after their clothes or masks
/// change, as the game would save it, so the host's save keeps it (<see cref="BodyBook"/>).
/// </summary>
internal static class BodySender
{
    private const float Interval = 10f;

    private static float _next;

    public static void Init() => CoopLoop.Update += Tick;

    /// <summary>
    /// Sends the body on the next frame instead of waiting: what the player wears just changed, and a
    /// player who leaves right after would otherwise come back without it (a hat they put on would stay
    /// parked under the house, see <see cref="MaskParking"/>).
    /// </summary>
    public static void SendSoon() => _next = 0f;

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
