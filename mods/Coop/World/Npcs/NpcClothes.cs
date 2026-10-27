using System;
using Coop.Net;
using Coop.World.Objects;
using LiteNetLib.Utils;
using UnityEngine;

namespace Coop.World.Npcs;

/// <summary>
/// What NPC rigs wear. Their clothes are shared items (worn ones sit hidden under the house); the host
/// says which item each rig wears in each slot, and a client dresses its puppet in its copies. NPCs
/// that loaded with the house are already dressed by the save; this matters for the ones that arrive
/// later (a client never makes their starting clothes itself).
///
/// WorldNpcEvent, kind Dress: rig ref, then shoes, pants, shirt refs (none for an empty slot).
/// </summary>
internal static class NpcClothes
{
    private const byte Dress = 1;
    private const float Interval = 1f;
    private const int Slots = 3;

    private static readonly NetRef[] Pieces = new NetRef[Slots];
    private static float _next;

    public static void Init() => Messages.On(MessageIds.WorldNpcEvent, OnEvent);

    /// <summary>Host, once a second: rigs whose clothes changed (or were never sent) go out.</summary>
    public static void Tick()
    {
        float now = Time.time;
        if (now < _next)
            return;
        _next = now + Interval;
        foreach (var o in NetObjects.Rigs)
        {
            var rig = o.Rig;
            if (rig == null || !o.Alive || rig.Abilities == null || rig.Abilities.clothings == null)
                continue;
            int signature = Signature(rig.Abilities.clothings, out bool allShared);
            // Clothes that aren't shared yet (their spawn goes out next frame) are waited for.
            if (!allShared || (rig.DressSent && signature == rig.DressSignature))
                continue;
            rig.DressSignature = signature;
            rig.DressSent = true;
            Send(o);
        }
    }

    /// <summary>Everyone gets every rig's clothes again (a player joined or resynced).</summary>
    public static void SendAllAgain()
    {
        foreach (var o in NetObjects.Rigs)
            o.Rig.DressSent = false;
    }

    private static int Signature(GameObject[] clothings, out bool allShared)
    {
        allShared = true;
        int signature = 17;
        for (int i = 0; i < Slots && i < clothings.Length; i++)
        {
            var piece = clothings[i];
            var o = piece != null ? NetObjects.Find(piece) : null;
            if (piece != null && o == null && piece.scene.IsValid())
                allShared = false;
            signature = signature * 31 + (o != null ? o.Ref.Id : 0);
        }
        return signature;
    }

    private static void Send(NetObject rigObject)
    {
        var clothings = rigObject.Rig.Abilities.clothings;
        var w = Outbox.Begin(MessageIds.WorldNpcEvent);
        w.Put(Dress);
        rigObject.Ref.Write(w);
        for (int i = 0; i < Slots; i++)
        {
            var piece = i < clothings.Length ? clothings[i] : null;
            var o = piece != null ? NetObjects.Find(piece) : null;
            (o != null ? o.Ref : default).Write(w);
        }
        Outbox.ToPlayers(w, Channel.Events);
    }

    private static void OnEvent(in Sender from, NetDataReader reader)
    {
        if (WorldSide.IsHost || !WorldSide.Running)
            return;
        byte kind = reader.GetByte();
        if (kind != Dress)
            return;
        var rigObject = NetObjects.Find(NetRef.Read(reader));
        for (int i = 0; i < Slots; i++)
            Pieces[i] = NetRef.Read(reader);
        var abilities = rigObject?.Rig?.Abilities;
        if (abilities == null || abilities.clothings == null)
            return;
        for (int i = 0; i < Slots && i < abilities.clothings.Length; i++)
        {
            var item = Pieces[i].IsValid ? NetObjects.Find(Pieces[i]) : null;
            if (item == null || abilities.clothings[i] == item.Go)
                continue;
            try
            {
                // Materials and the body's clothing UVs only: no sound, no hands, nothing moved.
                abilities.PutOnClothes(item.Go, i, updateMatsOnly: true);
            }
            catch (Exception e)
            {
                CoopMod.Log.Debug($"World: dressing {rigObject} failed: {e.Message}");
            }
        }
    }
}
