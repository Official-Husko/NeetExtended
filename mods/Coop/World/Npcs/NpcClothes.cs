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
/// later (a client never makes their starting clothes itself). Taking something off is the same
/// message with an empty slot: the puppet's skin comes back and the piece, which the host dropped,
/// shows again. The host sends right away when a rig dresses or undresses (<see cref="Changed"/>).
/// A player stripping a corpse in a client's game doesn't undress the client's copy: the request goes to
/// the host, whose NPC it is, and comes back as the message above.
///
/// WorldNpcEvent, kind Dress: rig ref, then shoes, pants, shirt refs (none for an empty slot).
/// WorldNpcUndress (client to host): rig ref, slot.
/// </summary>
internal static class NpcClothes
{
    private const byte Dress = 1;
    private const float Interval = 1f;
    private const int Slots = 3;
    private const int SkinSlot = 3;
    private const int ShirtSlot = 2;
    private const int ShirtHandSlot = 1;

    private static readonly NetRef[] Pieces = new NetRef[Slots];
    private static float _next;

    public static void Init()
    {
        Messages.On(MessageIds.WorldNpcEvent, OnEvent);
        Messages.On(MessageIds.WorldNpcUndress, OnUndressRequest);
    }

    /// <summary>
    /// Client: a player is stripping an NPC's corpse here, the piece the game would take first (shoes,
    /// then pants, then shirt). True if the request went to the host instead.
    /// </summary>
    public static bool RequestUndress(Player_Abilities abilities)
    {
        var o = NetObjects.Find(abilities.gameObject);
        if (o?.Rig == null || !o.IsReplica || abilities.clothings == null)
            return false;
        int slot = 0;
        while (slot < Slots && slot < abilities.clothings.Length && abilities.clothings[slot] == null)
            slot++;
        if (slot >= Slots)
            return false;
        var w = Outbox.Begin(MessageIds.WorldNpcUndress);
        o.Ref.Write(w);
        w.Put((byte)slot);
        Outbox.ToHost(w, Channel.Events);
        return true;
    }

    private static void OnUndressRequest(in Sender from, NetDataReader reader)
    {
        if (!WorldSide.IsHost || !WorldSide.Running)
            return;
        var o = NetObjects.Find(NetRef.Read(reader));
        int slot = reader.GetByte();
        var abilities = o?.Rig?.Abilities;
        if (abilities == null || !o.Alive || slot >= Slots || abilities.clothings == null || abilities.clothings[slot] == null)
            return;
        // The player's hands are in their own game: the piece just drops, like the game's corpse stripping.
        abilities.RemoveClothing(slot, noHandInsert: true);
    }

    /// <summary>Host, once a second: rigs whose clothes changed (or were never sent) go out.</summary>
    public static void Tick()
    {
        float now = Time.time;
        if (now < _next)
            return;
        _next = now + Interval;
        foreach (var o in NetObjects.Rigs)
            SendIfChanged(o);
    }

    /// <summary>Host: a rig just put something on or took something off; say so now, not at the next tick.</summary>
    public static void Changed(Player_Abilities abilities)
    {
        var o = NetObjects.Find(abilities.gameObject);
        if (o?.Rig != null)
            SendIfChanged(o);
    }

    private static void SendIfChanged(NetObject o)
    {
        var rig = o.Rig;
        if (rig == null || !o.Alive || rig.Abilities == null || rig.Abilities.clothings == null)
            return;
        int signature = Signature(rig.Abilities.clothings, out bool allShared);
        // Clothes that aren't shared yet (their spawn goes out next frame) are waited for.
        if (!allShared || (rig.DressSent && signature == rig.DressSignature))
            return;
        rig.DressSignature = signature;
        rig.DressSent = true;
        Send(o);
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
            if (!Pieces[i].IsValid)
            {
                TakeOff(abilities, i);
                continue;
            }
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

    /// <summary>
    /// What Player_Abilities.RemoveClothing does to the body, without touching the item: the host
    /// dropped it, and its copy here follows from the host's item stream. Only its mesh needs showing,
    /// since wearing hides it.
    /// </summary>
    private static void TakeOff(Player_Abilities abilities, int slot)
    {
        var worn = abilities.clothings[slot];
        if (worn == null || abilities.noClothUVs)
            return;
        try
        {
            var materials = abilities.neetRend.materials;
            materials[slot] = materials[SkinSlot];
            abilities.neetRend.materials = materials;
            if (worn.TryGetComponent(out MeshRenderer renderer))
                renderer.enabled = true;
            abilities.clothings[slot] = null;
            abilities.DefineCurrentAttire();
            abilities.neetRend.sharedMesh.SetUVs(0, abilities.tempUVSwaps[(int)abilities.currentAttire]);
            if (slot == ShirtSlot && abilities.rendererHands != null)
            {
                foreach (var hand in abilities.rendererHands)
                {
                    if (hand == null)
                        continue;
                    var handMaterials = hand.materials;
                    handMaterials[ShirtHandSlot] = abilities.skinMat;
                    hand.materials = handMaterials;
                }
            }
            var sounds = abilities.GetComponents<AudioSource>();
            if (sounds.Length > 1)
                sounds[1].Play();
        }
        catch (Exception e)
        {
            CoopMod.Log.Debug($"World: undressing {abilities.name} failed: {e.Message}");
        }
    }
}
