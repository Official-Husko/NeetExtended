using System.Collections.Generic;
using Coop.Net;
using Coop.World.Objects;
using LiteNetLib.Utils;
using UnityEngine;

namespace Coop.World.Npcs;

/// <summary>
/// The masks an NPC wears (masks, glasses, a cigarette in the mouth: anything with a MaskFace). The
/// masks are shared items and ride on the NPC's head through the item stream in every game, so what
/// a client's copy lacks is the game's own state for a worn mask: it isn't on the head, it's out of
/// reach of grabbing (layer NotPlayer), and the wearer isn't known to it. The host says which items
/// each rig wears (the same WorldNpcEvent as the clothes, kind Masks), and a client sets that state
/// on its copies, and puts it back for the ones taken off. The puppet's own mask list stays empty, so
/// nothing in the client's game tries to take them off; the host's NPC does that.
///
/// WorldNpcEvent, kind Masks: rig ref, count, then one ref per mask.
/// </summary>
internal static class NpcMasks
{
    public const byte Kind = 2;

    private static readonly int Worn = LayerMask.NameToLayer("NotPlayer");
    private static readonly int Loose = LayerMask.NameToLayer("Usable");
    private static readonly List<GameObject> Now = new List<GameObject>(4);

    /// <summary>Host: sends a rig's masks if they changed (or never went out) and are all shared.</summary>
    public static void SendIfChanged(NetObject o)
    {
        var rig = o.Rig;
        var masks = rig?.Abilities?.masks;
        if (masks == null || !o.Alive)
            return;
        int signature = 17;
        for (int i = 0; i < masks.Count; i++)
        {
            var item = masks[i] != null ? NetObjects.Find(masks[i]) : null;
            if (item == null)
                return; // Not shared yet: its spawn goes out next frame.
            signature = signature * 31 + item.Ref.Id;
        }
        if (rig.MasksSent && signature == rig.MaskSignature)
            return;
        rig.MaskSignature = signature;
        rig.MasksSent = true;
        var w = Outbox.Begin(MessageIds.WorldNpcEvent);
        w.Put(Kind);
        o.Ref.Write(w);
        w.Put((byte)masks.Count);
        for (int i = 0; i < masks.Count; i++)
            NetObjects.Find(masks[i]).Ref.Write(w);
        Outbox.ToPlayers(w, Channel.Events);
    }

    public static void SendAllAgain()
    {
        foreach (var o in NetObjects.Rigs)
            o.Rig.MasksSent = false;
    }

    /// <summary>Client: the rest of a Masks event, after its kind.</summary>
    public static void Read(NetDataReader reader)
    {
        var o = NetObjects.Find(NetRef.Read(reader));
        int count = reader.GetByte();
        Now.Clear();
        for (int i = 0; i < count; i++)
        {
            var item = NetObjects.Find(NetRef.Read(reader));
            if (item != null)
                Now.Add(item.Go);
        }
        var rig = o?.Rig;
        if (rig == null)
            return;
        foreach (var gone in rig.Masked)
        {
            if (gone != null && !Now.Contains(gone))
                Set(gone, null);
        }
        rig.Masked.Clear();
        foreach (var item in Now)
        {
            Set(item, o.Go);
            rig.Masked.Add(item);
        }
    }

    private static void Set(GameObject item, GameObject wearer)
    {
        if (!item.TryGetComponent(out MaskFace face))
            return;
        face.onHead = wearer != null;
        face.playerWearingRef = wearer;
        item.layer = wearer != null ? Worn : Loose;
    }
}
