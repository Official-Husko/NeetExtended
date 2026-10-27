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
/// Layout: damage in 32nds (byte), broken (bool). All zero is undamaged.
/// </summary>
internal sealed class UsableState : StateType<Usable>
{
    public override bool OnItems => true;

    public override bool Swept => false;

    // People and doors have their own health; the rest of the house's things are items.
    public override bool Accepts(Component component) =>
        component.GetComponent<Brain>() == null && component.GetComponent<Door>() == null;

    protected override void Write(Usable item, NetDataWriter w)
    {
        w.Put((byte)Mathf.Clamp(Mathf.RoundToInt(item.damage * 32f), 0, 255));
        w.Put(item.broken);
    }

    protected override void Apply(Usable item, NetDataReader r)
    {
        float damage = r.GetByte() / 32f;
        bool broken = r.GetBool();
        if (!item.broken)
            item.damage = damage;
        if (broken && !item.broken)
            BurnQuietly(item);
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
        item.broken = true;
    }
}
