using Coop.World.Objects;
using LiteNetLib.Utils;
using UnityEngine;

namespace Coop.World.State.Types;

/// <summary>
/// How worn down an item is: the damage it took and whether it burnt through (it shows burnt and
/// stays broken). Damage only builds up where the item is simulated; the others show the result.
/// Burning through also fires the item's own break reactions (fuses and lighters blow up, fire
/// extinguishers go off, containers spill) in that game; the others skip those, what they leave
/// behind arrives as spawns and destroys. Watched through hints, not sweeps.
///
/// Layout: damage in 32nds (byte), flags (bit 0 broken, bit 1 burnt look; an item cut while burnt
/// hands the look to its pieces without breaking them). All zero is undamaged.
/// </summary>
internal sealed class UsableState : StateType<Usable>
{
    private const byte Broken = 1;
    private const byte Burnt = 2;

    public override bool OnItems => true;

    public override bool Swept => false;

    // People and doors have their own health; the rest of the house's things are items.
    public override bool Accepts(Component component) =>
        component.GetComponent<Brain>() == null && component.GetComponent<Door>() == null;

    protected override void Write(Usable item, NetDataWriter w)
    {
        w.Put((byte)Mathf.Clamp(Mathf.RoundToInt(item.damage * 32f), 0, 255));
        byte flags = 0;
        if (item.broken)
            flags |= Broken;
        if (ItemMarks.IsBurnt(item))
            flags |= Burnt;
        w.Put(flags);
    }

    protected override void Apply(Usable item, NetDataReader r)
    {
        float damage = r.GetByte() / 32f;
        byte flags = r.GetByte();
        if (!item.broken)
            item.damage = damage;
        if ((flags & Burnt) != 0 && !ItemMarks.IsBurnt(item))
            BurnQuietly(item);
        if ((flags & Broken) != 0)
            item.broken = true;
    }

    // The game's own burn look (burnt material, burnt flag), minus its reactions.
    private static void BurnQuietly(Usable item)
    {
        var onBreak = item.OnBreak;
        bool destroys = item.destroyOnBurn;
        item.OnBreak = null;
        item.destroyOnBurn = false;
        try
        {
            item.Burn();
        }
        finally
        {
            item.OnBreak = onBreak;
            item.destroyOnBurn = destroys;
        }
    }
}
