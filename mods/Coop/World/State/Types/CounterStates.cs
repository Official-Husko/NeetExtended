using Coop.World.Objects;
using LiteNetLib.Utils;
using UnityEngine;

namespace Coop.World.State.Types;

/// <summary>
/// The money in a piggy bank. A coin dropped in is used up where the coin is simulated, and the
/// bank's total follows from there, so it also reads right when it's picked up (its price is what it
/// holds) or smashed.
/// </summary>
internal sealed class PiggyBankState : StateType<Piggybank>
{
    public override bool OnItems => true;

    protected override void Write(Piggybank bank, NetDataWriter w) => w.Put(Mathf.RoundToInt(bank.CashValue));

    protected override void Apply(Piggybank bank, NetDataReader r)
    {
        int cash = r.GetInt();
        if (Mathf.RoundToInt(bank.CashValue) != cash)
            bank.CashValue = cash;
    }
}

/// <summary>
/// How much the cat's bowl holds. The bowl is part of the house, not an item. The cat eats on the
/// host; players top it up and take from it anywhere, and the game that did it sends the new count.
/// </summary>
internal sealed class CatBowlState : StateType<CatFoodBowl>
{
    protected override void Write(CatFoodBowl bowl, NetDataWriter w) => w.Put((byte)Mathf.Clamp(bowl.foodUnits, 0, 255));

    protected override void Apply(CatFoodBowl bowl, NetDataReader r)
    {
        int units = r.GetByte();
        if (bowl.foodUnits != units)
            bowl.FoodUnits = units;
    }
}

/// <summary>The bullets left in a gun magazine.</summary>
internal sealed class MagazineState : StateType<GunMagazine>
{
    public override bool OnItems => true;

    protected override void Write(GunMagazine mag, NetDataWriter w) => w.Put((short)mag.bulletCount);

    protected override void Apply(GunMagazine mag, NetDataReader r) => mag.bulletCount = r.GetShort();
}

/// <summary>
/// Which magazine sits in a gun. Putting one in and taking it out use the gun's own methods (the
/// click, the magazine hidden inside or let go); the magazine is a shared item moved by its own stream.
/// </summary>
internal sealed class GunState : StateType<Gun>
{
    public override bool OnItems => true;

    protected override void Write(Gun gun, NetDataWriter w) =>
        ItemRefs.Of(gun.mag != null ? gun.mag.gameObject : null).Write(w);

    protected override void Apply(Gun gun, NetDataReader r)
    {
        var mag = ItemRefs.Find<GunMagazine>(NetRef.Read(r), out bool known);
        if (!known || gun.mag == mag)
            return;
        if (gun.mag != null)
            gun.EjectMag();
        if (mag != null)
            gun.InsertMag(mag);
    }
}

/// <summary>
/// A piece of clothing's dirt (after a turd or an accident): the dirty material and the smearing
/// painter come from the game's own MakeDirty and Clean. It gets dirty and clean where the piece is
/// simulated; the others take it from there.
/// </summary>
internal sealed class WearableState : StateType<Wearable>
{
    public override bool OnItems => true;

    protected override void Write(Wearable piece, NetDataWriter w) => w.Put(piece.dirty);

    protected override void Apply(Wearable piece, NetDataReader r)
    {
        bool dirty = r.GetBool();
        if (dirty && !piece.dirty)
            piece.MakeDirty();
        else if (!dirty && piece.dirty)
            piece.Clean();
    }
}
