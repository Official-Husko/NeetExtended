using System.Collections.Generic;
using Coop.World.Objects;
using Coop.World.Ownership;
using UnityEngine;

namespace Coop.Players;

/// <summary>
/// The masks, hats, glasses and cigarettes another player wears, on their body here. They are shared
/// items like any other, so this game already has a copy of each; while it's worn the copy is
/// parented to the body's eye anchor (<see cref="PlayerPuppet.Eyes"/>) with the offset the wearer's
/// game gave it relative to its camera, and the item stream stops moving it (<c>NetObject.OnPuppet</c>).
/// Taking it off puts the item back where its owner's stream last said it was.
/// </summary>
internal sealed class PuppetMasks
{
    private struct Worn
    {
        public NetObject Item;
        public int Layer;
        public RigidbodyInterpolation Interpolation;
    }

    /// <summary>Tests: leave the masks to the item stream, like before bodies wore them.</summary>
    internal static bool Disabled;

    private readonly List<Worn> _worn = new List<Worn>(MaskLook.MostWorn);

    /// <summary>Puts on what <paramref name="looks"/> lists and takes off the rest; false when an item isn't in this game yet.</summary>
    public bool Apply(Transform eyes, MaskLook[] looks)
    {
        if (Disabled)
            looks = null;
        bool complete = true;
        for (int i = _worn.Count - 1; i >= 0; i--)
        {
            if (!_worn[i].Item.Alive || !Lists(looks, _worn[i].Item.Ref))
                TakeOff(i);
        }
        int count = looks?.Length ?? 0;
        for (int i = 0; i < count; i++)
        {
            var item = NetObjects.Find(looks[i].Item);
            if (item == null || !item.Alive)
            {
                complete = false;
                continue;
            }
            if (!IsWorn(item))
                PutOn(item);
            item.Tf.SetParent(eyes, false);
            item.Tf.localPosition = looks[i].Position;
            item.Tf.localRotation = looks[i].Rotation;
        }
        return complete;
    }

    /// <summary>Everything off, before the body goes away (its children would go with it).</summary>
    public void Release()
    {
        for (int i = _worn.Count - 1; i >= 0; i--)
            TakeOff(i);
    }

    private void PutOn(NetObject item)
    {
        var entry = new Worn { Item = item, Layer = item.Go.layer };
        if (item.Body != null)
        {
            entry.Interpolation = item.Body.interpolation;
            item.Body.interpolation = RigidbodyInterpolation.None;
        }
        item.OnPuppet = true;
        item.Go.layer = LayerMask.NameToLayer("NotPlayer");
        _worn.Add(entry);
    }

    private void TakeOff(int index)
    {
        var entry = _worn[index];
        _worn.RemoveAt(index);
        var item = entry.Item;
        if (item == null || !item.Alive)
            return;
        item.OnPuppet = false;
        item.Tf.SetParent(null, true);
        item.Go.layer = entry.Layer;
        if (item.Body != null)
            item.Body.interpolation = entry.Interpolation;
        // Where its owner's stream last had it (the wearer dropped it, or it's in their hand).
        if (item.IsReplica)
            ReplicaBody.SettleAtNewest(item);
    }

    private bool IsWorn(NetObject item)
    {
        foreach (var entry in _worn)
        {
            if (entry.Item == item)
                return true;
        }
        return false;
    }

    private static bool Lists(MaskLook[] looks, NetRef item)
    {
        if (looks == null)
            return false;
        foreach (var look in looks)
        {
            if (look.Item.Equals(item))
                return true;
        }
        return false;
    }
}
