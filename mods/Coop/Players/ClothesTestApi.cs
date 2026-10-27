using System.Text;
using Coop.World.Objects;
using HarmonyLib;
using NeetModKit.Game;
using UnityEngine;

namespace Coop.Players;

/// <summary>
/// Dev bridge hands for the clothes tests (<c>type.call type=Coop.Players.ClothesTestApi method=Looks</c>):
/// make a custom shirt the way the custom shop's box does, wear a shared item, dirty the pants, and
/// say what every body wears.
/// </summary>
public static class ClothesTestApi
{
    /// <summary>Spawns <paramref name="prefab"/> and customizes it with the picture at <paramref name="path"/>, like unpacking a custom shop's box. Returns its item id.</summary>
    public static string Custom(string prefab, string path) => IdOf(MakeCustom(prefab, path));

    private static GameObject MakeCustom(string prefab, string path)
    {
        var computer = HouseManager._ != null ? HouseManager._.mainComputer : null;
        CustomItemShop shop = null;
        if (computer != null)
        {
            foreach (var candidate in computer.GetComponentsInChildren<CustomItemShop>(true))
            {
                if (!candidate.filterForMusic)
                    shop = candidate;
            }
        }
        if (shop == null || !Spawning.IsItem(prefab))
            return null;
        var texture = new Texture2D(2, 2);
        texture.LoadImage(System.IO.File.ReadAllBytes(path));
        shop.previewImg.sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
        shop.latestFilePath = path;
        var item = Spawning.ItemNearPlayer(prefab);
        AccessTools.Method(typeof(CustomItemShop), "CustomizeItem", new[] { typeof(GameObject) }).Invoke(shop, new object[] { item });
        return item;
    }

    /// <summary>Spawns a custom piece (<see cref="Custom"/>) and puts it on in the same frame, before it is shared.</summary>
    public static string CustomAndWear(string prefab, string path)
    {
        var item = MakeCustom(prefab, path);
        var abilities = HouseManager._?.player?.GetComponent<Player_Abilities>();
        if (item == null || abilities == null || !item.TryGetComponent(out Wearable wearable))
            return "Nothing to wear.";
        if (abilities.clothings[wearable.pieceId] != null)
            abilities.RemoveClothing(wearable.pieceId, noHandInsert: true);
        wearable.Wear(abilities, dontDropHand: true);
        return "Worn at once: " + IdOf(item);
    }

    /// <summary>Spawns a piece of clothing and returns its item id.</summary>
    public static string Spawn(string prefab) => Spawning.IsItem(prefab) ? IdOf(Spawning.ItemNearPlayer(prefab)) : "No such item.";

    /// <summary>Puts a shared item on this player, taking off what is on that piece (like the game's Wear).</summary>
    public static string Wear(int itemId)
    {
        var item = NetObjects.Find(new NetRef(NetKind.Item, itemId));
        var abilities = HouseManager._?.player?.GetComponent<Player_Abilities>();
        if (item == null || abilities == null || !item.Go.TryGetComponent(out Wearable wearable))
            return "No such wearable.";
        if (abilities.clothings[wearable.pieceId] != null)
            abilities.RemoveClothing(wearable.pieceId, noHandInsert: true);
        wearable.Wear(abilities, dontDropHand: true);
        return $"Wearing {item.Go.name} on piece {wearable.pieceId}.";
    }

    /// <summary>Takes off a piece (0 shoes, 1 pants, 2 shirt).</summary>
    public static string TakeOff(int piece)
    {
        var abilities = HouseManager._?.player?.GetComponent<Player_Abilities>();
        if (abilities == null)
            return "No player.";
        abilities.RemoveClothing(piece, noHandInsert: true);
        return "Off.";
    }

    /// <summary>Puts a mask, hat, glasses or cigarette on this player's face the way a click does, and returns its item id.</summary>
    public static string Mask(string prefab)
    {
        var player = HouseManager._?.player;
        if (player == null || !Spawning.IsItem(prefab))
            return "No player or no such item.";
        var item = Spawning.ItemNearPlayer(prefab);
        item.GetComponent<MaskFace>().PutOnMask(player, notHeld: true);
        return IdOf(item);
    }

    /// <summary>Tests: other bodies wear masks (the default) or leave them to the item stream.</summary>
    public static string BodiesWearMasks(bool on)
    {
        PuppetMasks.Disabled = !on;
        return on ? "Bodies wear masks." : "Masks follow the item stream.";
    }

    /// <summary>Where a shared item is in this game: position, parent, layer, kinematic, and its distance to each body's eye point.</summary>
    public static string Where(int itemId)
    {
        var item = NetObjects.Find(new NetRef(NetKind.Item, itemId));
        if (item == null)
            return "No such item.";
        var text = new StringBuilder();
        text.Append(item.Go.name).Append(" at ").Append(item.Tf.position.ToString("F3")).Append(" parent ")
            .Append(item.Tf.parent != null ? item.Tf.parent.name : "none").Append(" layer ").Append(LayerMask.LayerToName(item.Go.layer))
            .Append(" kinematic ").Append(item.Body != null && item.Body.isKinematic).Append(" replica ").Append(item.IsReplica)
            .Append(" onPuppet ").Append(item.OnPuppet).Append(" active ").Append(item.Go.activeInHierarchy);
        foreach (var player in Session.CoopSession.Players)
        {
            var puppet = player.IsLocal ? null : RemotePlayers.Find(player.Slot);
            if (puppet != null && puppet.Eyes != null)
                text.Append("\n  eyes of slot ").Append(player.Slot).Append(" at ").Append(puppet.Eyes.position.ToString("F3"))
                    .Append(", item in eyes ").Append(puppet.Eyes.InverseTransformPoint(item.Tf.position).ToString("F3"));
        }
        var camera = HouseManager._?.mainCamera?.transform;
        if (camera != null)
            text.Append("\n  my camera at ").Append(camera.position.ToString("F3")).Append(", item in it ").Append(camera.InverseTransformPoint(item.Tf.position).ToString("F3"));
        return text.ToString();
    }

    /// <summary>Takes the last mask off (what a click on the clothes remover does).</summary>
    public static string MaskOff()
    {
        var abilities = HouseManager._?.player?.GetComponent<Player_Abilities>();
        if (abilities == null || abilities.masks.Count == 0)
            return "No mask.";
        abilities.masks[abilities.masks.Count - 1].GetComponent<MaskFace>().RemoveMask(abilities.gameObject);
        return "Off.";
    }

    /// <summary>Where the camera and the head bone are in the player's own space (and the puppet's, for the others), to place masks.</summary>
    public static string Measure()
    {
        var text = new StringBuilder();
        var player = HouseManager._?.player;
        var camera = HouseManager._?.mainCamera?.transform;
        if (player == null || camera == null)
            return "No player.";
        text.Append("camera local ").Append(camera.localPosition.ToString("F3")).Append(' ').Append(camera.localEulerAngles.ToString("F1"));
        var head = FindDeep(player.transform, "headbone");
        if (head != null)
        {
            var root = player.transform;
            text.Append("\nhead in root ").Append(root.InverseTransformPoint(head.position).ToString("F3")).Append(" rot ")
                .Append((Quaternion.Inverse(root.rotation) * head.rotation).eulerAngles.ToString("F1"))
                .Append(" lossy ").Append(head.lossyScale.ToString("F3"));
            text.Append("\ncamera in head ").Append(head.InverseTransformPoint(camera.position).ToString("F3"));
        }
        foreach (var slot in Session.CoopSession.Players)
        {
            var puppet = slot.IsLocal ? null : RemotePlayers.Find(slot.Slot);
            if (puppet == null)
                continue;
            var h = FindDeep(puppet.transform, "headbone");
            if (puppet.Eyes != null)
                text.Append("\npuppet eyes in root ").Append(puppet.transform.InverseTransformPoint(puppet.Eyes.position).ToString("F3")).Append(" rot ")
                    .Append((Quaternion.Inverse(puppet.transform.rotation) * puppet.Eyes.rotation).eulerAngles.ToString("F1")).Append(" lossy ").Append(puppet.Eyes.lossyScale.ToString("F3"));
            text.Append("\npuppet ").Append(slot.Slot).Append(" head in root ").Append(puppet.transform.InverseTransformPoint(h.position).ToString("F3"))
                .Append(" lossy ").Append(h.lossyScale.ToString("F3"));
        }
        return text.ToString();
    }

    private static Transform FindDeep(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name)
                return child;
            var found = FindDeep(child, name);
            if (found != null)
                return found;
        }
        return null;
    }

    /// <summary>Soils the pants the way the game does when the bladder gives out.</summary>
    public static string Soil()
    {
        var abilities = HouseManager._?.player?.GetComponent<Player_Abilities>();
        if (abilities == null)
            return "No player.";
        abilities.GetComponent<PlayerStats>().bladder.SetVal(0f);
        abilities.Shit();
        return "Done.";
    }

    /// <summary>Turns this player's body towards a point (flat), to take a screenshot of someone.</summary>
    public static string Face(float x, float z)
    {
        var player = HouseManager._?.player;
        if (player == null)
            return "No player.";
        var to = new Vector3(x, player.transform.position.y, z) - player.transform.position;
        player.transform.rotation = Quaternion.LookRotation(to, Vector3.up);
        return "Turned.";
    }

    /// <summary>Each worn piece here (item, picture, dirty) and what every other player's body shows.</summary>
    public static string Looks()
    {
        var text = new StringBuilder();
        var abilities = HouseManager._?.player?.GetComponent<Player_Abilities>();
        if (abilities != null)
        {
            text.Append("me:");
            Describe(text, abilities.neetRend, abilities.clothings);
        }
        foreach (var player in Session.CoopSession.Players)
        {
            var puppet = player.IsLocal ? null : RemotePlayers.Find(player.Slot);
            if (puppet == null)
                continue;
            text.Append("\nslot ").Append(player.Slot).Append(':');
            Describe(text, puppet.Skin, null);
        }
        return text.ToString();
    }

    private static void Describe(StringBuilder text, SkinnedMeshRenderer body, GameObject[] worn)
    {
        var materials = body.sharedMaterials;
        for (int piece = 0; piece < Appearance.Pieces; piece++)
        {
            text.Append("\n  ").Append(piece).Append(' ');
            if (worn != null)
            {
                var item = worn[piece] != null ? NetObjects.Find(worn[piece]) : null;
                text.Append(worn[piece] != null ? worn[piece].name : "-").Append(item != null ? " " + item.Ref : " (not shared)")
                    .Append(worn[piece] != null && worn[piece].GetComponent<Wearable>().dirty ? " dirty" : "").Append(": ");
            }
            var material = materials[piece];
            text.Append(material != null ? material.name : "none").Append(", texture ")
                .Append(material != null && material.mainTexture != null ? $"{material.mainTexture.name} {material.mainTexture.width}x{material.mainTexture.height}" : "none");
        }
    }

    private static string IdOf(GameObject item)
    {
        if (item == null)
            return "No picture shop or no such item.";
        var shared = NetObjects.Find(item);
        return shared != null ? shared.Ref.Id.ToString() : "spawned, not shared yet";
    }
}
