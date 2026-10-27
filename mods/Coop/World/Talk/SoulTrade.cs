using Coop.Net;
using LiteNetLib.Utils;
using UnityEngine;

namespace Coop.World.Talk;

/// <summary>
/// The story's TradeSoulForClone for a remote talker (Dialogue.TradeSoulForClone). Their own game
/// has their real stats, so it checks and takes the 20 soul and tells the host; the host makes the
/// clone at the speaker, like the game does, with the talker's body type and soul.
///
/// WorldSoulTrade: kind, the speaker's position and forward, then (Made only) body type, soul.
/// Ask goes from the host to the talker; Made comes back.
/// </summary>
internal static class SoulTrade
{
    private const byte Ask = 0;
    private const byte Made = 1;
    private const float Price = 20f;
    private const string CloneBrain = "Player_NPC";

    public static void Init() => Messages.On(MessageIds.WorldSoulTrade, OnMessage);

    /// <summary>Host, from the story's function during <paramref name="c"/>'s step.</summary>
    public static void Request(Conversation c)
    {
        if (!WorldSide.Running || !WorldSide.IsHost || c.Speaker == null)
            return;
        var w = Outbox.Begin(MessageIds.WorldSoulTrade);
        w.Put(Ask);
        Put(w, c.Speaker.transform.position);
        Put(w, c.Speaker.transform.forward);
        Outbox.ToSlot(c.Slot, w, Channel.Events);
    }

    private static void OnMessage(in Sender from, NetDataReader r)
    {
        byte kind = r.GetByte();
        var at = Get(r);
        var forward = Get(r);
        if (!WorldSide.Running)
            return;
        if (kind == Ask && WorldSide.IsClient)
            Pay(at, forward);
        else if (kind == Made && WorldSide.IsHost)
            MakeClone(at, forward, r.GetFloat(), r.GetFloat());
    }

    // The trade's half on the player's own game: enough soul, the sound, the price.
    private static void Pay(Vector3 at, Vector3 forward)
    {
        var player = WorldSide.LocalPlayer;
        if (player == null || !player.TryGetComponent(out PlayerStats stats) || !player.TryGetComponent(out Player_Abilities rig)
            || !stats.stats.TryGetValue(typeof(Soul), out var soul) || soul.GetVal() <= Price)
            return;
        if (Feedback_Sender._ != null && Feedback_Sender._.menuAudio != null && Feedback_Sender._.menuAudio.Length > 3)
            AudioSource.PlayClipAtPoint(Feedback_Sender._.menuAudio[3], player.transform.position);
        float bodyType = rig.neetRend != null ? rig.neetRend.GetBlendShapeWeight(3) : 0f;
        float before = soul.GetVal();
        soul.AddVal(-Price);
        var w = Outbox.Begin(MessageIds.WorldSoulTrade);
        w.Put(Made);
        Put(w, at);
        Put(w, forward);
        w.Put(bodyType);
        w.Put(before);
        Outbox.ToHost(w, Channel.Events);
    }

    // The trade's half on the host: the clone, as Dialogue.TradeSoulForClone makes it.
    private static void MakeClone(Vector3 at, Vector3 forward, float bodyType, float soul)
    {
        if (!BrainBank.brainsDict.TryGetValue(CloneBrain, out var clone) || clone.obj == null)
            return;
        var obj = Object.Instantiate(clone.obj, at + forward * 0.3f, Quaternion.identity);
        if (obj.TryGetComponent(out PlayerStats stats))
        {
            stats.stats[typeof(BodyType)].SetVal(bodyType);
            stats.stats[typeof(Soul)].SetVal(soul);
        }
    }

    private static void Put(NetDataWriter w, Vector3 v)
    {
        w.Put(v.x);
        w.Put(v.y);
        w.Put(v.z);
    }

    private static Vector3 Get(NetDataReader r) => new Vector3(r.GetFloat(), r.GetFloat(), r.GetFloat());
}
