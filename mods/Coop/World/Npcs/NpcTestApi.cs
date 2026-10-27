using System.Text;
using Coop.World.Objects;
using UnityEngine;

namespace Coop.World.Npcs;

/// <summary>
/// Hands for testing NPC sync from the dev bridge: <c>neet call type.call type=Coop.World.Npcs.NpcTestApi
/// method=PutMask args='["Emo","Mask_Man"]'</c>. NPCs are found by a part of their name.
/// </summary>
public static class NpcTestApi
{
    /// <summary>Makes an NPC wear a mask the way picking one up does (host).</summary>
    public static string PutMask(string npc, string mask)
    {
        var rig = Find(npc);
        if (rig == null)
            return $"No NPC like {npc}.";
        var item = NeetModKit.Game.Spawning.Item(mask, rig.Go.transform.position + Vector3.up);
        item.GetComponent<MaskFace>().PutOnMask(rig.Go, notHeld: true);
        return $"{rig} wears {rig.Rig.Abilities.masks.Count} mask(s).";
    }

    /// <summary>Takes the NPC's shirt off the way the game does, which takes a mask first (host).</summary>
    public static string Strip(string npc, int slot)
    {
        var rig = Find(npc);
        if (rig == null)
            return $"No NPC like {npc}.";
        rig.Rig.Abilities.RemoveClothing(slot, noHandInsert: true);
        return $"{rig} now wears {rig.Rig.Abilities.masks.Count} mask(s).";
    }

    /// <summary>What this game has of the NPC: collider, outfit, masks and where they are.</summary>
    public static string Look(string npc)
    {
        var o = Find(npc);
        if (o == null)
            return $"No NPC like {npc}.";
        var abilities = o.Rig.Abilities;
        var text = new StringBuilder();
        var capsule = o.Rig.Capsule;
        text.Append($"{o} at {o.Go.transform.position}, attire {abilities.currentAttire}, dead {abilities.dead}");
        if (capsule != null)
            text.Append($", capsule {capsule.height:0.00}");
        text.Append($", {abilities.masks.Count} mask(s)");
        foreach (var mask in abilities.masks)
            text.Append($"\n  {mask.name} parent {(mask.transform.parent != null ? mask.transform.parent.name : "none")} at {mask.transform.position}");
        foreach (var item in NetObjects.All)
        {
            if (item.Go != null && item.Go.TryGetComponent(out MaskFace face))
                text.Append($"\n  item {item.Go.name}: layer {LayerMask.LayerToName(item.Go.layer)}, onHead {face.onHead}, wearer {(face.playerWearingRef != null ? face.playerWearingRef.name : "none")}");
        }
        return text.ToString();
    }

    private static NetObject Find(string npc)
    {
        foreach (var o in NetObjects.Rigs)
        {
            if (o.Alive && o.Go.name.Contains(npc))
                return o;
        }
        return null;
    }

    /// <summary>Makes the NPC grunt the way a hurt one does: a need sound on its sound source (host).</summary>
    public static string Say(string npc, int need)
    {
        var rig = Find(npc);
        if (rig == null)
            return $"No NPC like {npc}.";
        var abilities = rig.Rig.Abilities;
        abilities.dynAudio.clip = abilities.needSounds[need];
        abilities.dynAudio.pitch = 1.1f;
        abilities.dynAudio.Play();
        return $"{rig} plays {abilities.needSounds[need].name}.";
    }

    /// <summary>The last sound this game played for a host NPC.</summary>
    public static string Heard() => NpcVoices.Last;

    /// <summary>The colour the newest skeleton's body shows right now (the skeleton flush).</summary>
    public static string SkeletonColor()
    {
        Color? seen = null;
        foreach (var o in NetObjects.Rigs)
        {
            if (o.Alive && o.Go.name.Contains("SkeletonNeetNPC") && o.Go.GetComponentInChildren<SkinnedMeshRenderer>() is { } renderer)
                seen = renderer.sharedMaterial.GetColor("_Color");
        }
        return seen.HasValue ? seen.Value.ToString() : "no skeleton";
    }

    /// <summary>A one-shot on the NPC's sound source, which the game uses for some sounds (host).</summary>
    public static string OneShot(string npc, int need)
    {
        var rig = Find(npc);
        if (rig == null)
            return $"No NPC like {npc}.";
        var abilities = rig.Rig.Abilities;
        abilities.dynAudio.PlayOneShot(abilities.needSounds[need]);
        return $"{rig} one-shots {abilities.needSounds[need].name}.";
    }
}
