using System.Text;
using Coop.World.Objects;
using UnityEngine;

namespace Coop.World.State;

/// <summary>
/// Hands for testing item state from the dev bridge: each changes an item the way the game's own
/// logic does (a flame cooking, the clock spoiling, a tap filling), on the game it is called in.
/// <c>neet call type.call type=Coop.World.State.ItemTestApi method=Cook args=[1234,12] --instance 2</c>.
/// </summary>
public static class ItemTestApi
{
    /// <summary>Shared items whose name contains the text: id, name, owner and every state they carry.</summary>
    public static string List(string filter = "")
    {
        var text = new StringBuilder();
        foreach (var o in NetObjects.All)
        {
            if (o.Ref.Kind != NetKind.Item || !o.Alive || (filter.Length > 0 && o.PrefabName.IndexOf(filter, System.StringComparison.OrdinalIgnoreCase) < 0))
                continue;
            text.Append(o.Ref.Id).Append(' ').Append(o.PrefabName).Append(" owner ").Append(o.Owner).Append(o.IsReplica ? " replica" : " here").Append('\n');
        }
        return text.ToString();
    }

    /// <summary>The state bytes of every tracker on one item.</summary>
    public static string State(int id)
    {
        var o = Item(id);
        if (o == null)
            return $"No item {id}.";
        var text = new StringBuilder(o.ToString()).Append('\n');
        foreach (var t in StateTrackers.All)
        {
            if (t.Object != o)
                continue;
            StateSync.Capture(t);
            text.Append("  ").Append(t.Type.GetType().Name).Append(' ');
            foreach (byte b in t.Last)
                text.Append(b.ToString("x2"));
            text.Append('\n');
        }
        return text.ToString();
    }

    /// <summary>Heats a food by that many seconds, like a flame touching it.</summary>
    public static string Cook(int id, float seconds)
    {
        var food = Part<Food>(id);
        if (food == null)
            return "No food.";
        food.CookedMeter += seconds;
        return $"cooked {food.CookedMeter:0.0}/{food.timeToCook:0.0}";
    }

    /// <summary>Ages a food by that many seconds, like the clock.</summary>
    public static string Spoil(int id, float seconds)
    {
        var food = Part<Food>(id);
        if (food == null)
            return "No food.";
        food.SpoiledMeter += seconds;
        return $"spoiled {food.SpoiledMeter:0.0}/360 {food.spoiled}";
    }

    public static string Fill(int id, int liquid = 3)
    {
        var glass = Part<LiquidContainer>(id);
        if (glass == null)
            return "No container.";
        glass.Fill(liquid);
        return "filled";
    }

    /// <summary>Tips a container out the way gravity does (with the splash).</summary>
    public static string Tip(int id)
    {
        var glass = Part<LiquidContainer>(id);
        if (glass == null)
            return "No container.";
        glass.Empty(null);
        return "emptied";
    }

    public static string Sip(int id)
    {
        var glass = Part<LiquidContainer>(id);
        if (glass == null)
            return "No container.";
        glass.Drink();
        return $"left {glass.currentDrinkVolume:0.0}";
    }

    public static string Soak(int id, int liquid = 3)
    {
        var soaker = Part<Soaker>(id);
        if (soaker == null)
            return "No soaker.";
        soaker.Soak(liquid);
        return "soaked";
    }

    /// <summary>Burns an item with that much damage (4 burns it through).</summary>
    public static string Burn(int id, float damage)
    {
        var item = Part<Usable>(id);
        if (item == null)
            return "No item.";
        item.Damage(damage, EffectType.Burn);
        return $"damage {item.damage:0.00} broken {item.broken}";
    }

    /// <summary>Dirties (or cleans) a piece of clothing the way a turd (or water) does.</summary>
    public static string Dirt(int id, bool dirty = true)
    {
        var piece = Part<Wearable>(id);
        if (piece == null)
            return "No clothing.";
        if (dirty && !piece.dirty)
            piece.MakeDirty();
        else if (!dirty && piece.dirty)
            piece.Clean();
        return $"dirty {piece.dirty}";
    }

    /// <summary>Puts a fuse in a vacuum the way a hand does (the fuse goes away) and switches it on.</summary>
    public static string Power(int id, int fuseId)
    {
        var vacuum = Part<VacuumCleaner>(id);
        var fuse = Part<Fuse>(fuseId);
        if (vacuum == null || fuse == null)
            return "No vacuum or fuse.";
        vacuum.Fuse = fuse;
        fuse.transform.position = Vector3.up * 500f;
        fuse.gameObject.SetActive(false);
        vacuum.SetUse(true);
        return $"on {vacuum.on}";
    }

    /// <summary>Puts an item upright at a place (it falls from there, or hangs there when frozen).</summary>
    public static string Put(int id, float x, float y, float z, bool freeze = false)
    {
        var o = Item(id);
        if (o == null)
            return $"No item {id}.";
        o.Tf.SetPositionAndRotation(new Vector3(x, y, z), Quaternion.identity);
        if (o.Body != null)
        {
            o.Body.position = o.Tf.position;
            o.Body.velocity = Vector3.zero;
            if (freeze)
                o.Body.isKinematic = true;
        }
        return "put";
    }

    /// <summary>Puts a magazine in a gun (or takes it out again when the id is 0).</summary>
    public static string Mag(int gunId, int magId)
    {
        var gun = Part<Gun>(gunId);
        if (gun == null)
            return "No gun.";
        if (magId == 0)
            gun.EjectMag();
        else
            gun.InsertMag(Part<GunMagazine>(magId));
        return gun.mag != null ? "loaded" : "empty";
    }

    /// <summary>Every prefab with a food that turns into another object (cooked, spoiled, soaked).</summary>
    public static string Swaps()
    {
        var text = new StringBuilder();
        foreach (var pair in PickupBank.pickupsDict)
        {
            var food = pair.Value.obj != null ? pair.Value.obj.GetComponent<Food>() : null;
            if (food == null || (food.swapOnCookedObj == null && food.swapOnSpoil == null && food.swapOnWaterObj == null))
                continue;
            text.Append(pair.Key).Append(" cook ").Append(food.timeToCook)
                .Append(" -> ").Append(food.swapOnCookedObj != null ? food.swapOnCookedObj.name : "-")
                .Append(", spoil -> ").Append(food.swapOnSpoil != null ? food.swapOnSpoil.name : "-")
                .Append(", wet -> ").Append(food.swapOnWaterObj != null ? food.swapOnWaterObj.name : "-").Append('\n');
        }
        return text.ToString();
    }

    /// <summary>Cuts an item that can be cut (bread, meat), like a knife does.</summary>
    public static string Cut(int id)
    {
        var item = Part<Sectionable>(id);
        if (item == null)
            return "Nothing to cut.";
        item.Cut();
        return "cut";
    }

    public static string Level(int id, int level = 0)
    {
        var ball = Part<DustBall>(id);
        if (ball == null)
            return "No dust ball.";
        if (level > 0)
            ball.Level = level;
        return $"level {ball.Level}";
    }

    public static string Cash(int id, int amount)
    {
        var bank = Part<Piggybank>(id);
        if (bank == null)
            return "No piggy bank.";
        bank.CashValue += amount;
        return $"cash {bank.CashValue}";
    }

    public static string Bowl(int id, int units)
    {
        var bowl = Part<CatFoodBowl>(id);
        if (bowl == null)
            return "No bowl.";
        bowl.FoodUnits = units;
        return $"units {bowl.FoodUnits}";
    }

    public static string Dispense(int id)
    {
        var box = Part<PrefabContainer>(id);
        if (box == null)
            return "No container.";
        box.TakeOne();
        return $"left {box.objCount}";
    }

    private static NetObject Item(int id) => NetObjects.Find(new NetRef(NetKind.Item, id));

    private static T Part<T>(int id) where T : Component => Item(id)?.Go.GetComponentInChildren<T>(true);
}
