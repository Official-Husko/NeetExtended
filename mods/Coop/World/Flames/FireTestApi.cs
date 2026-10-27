using System.Text;
using Coop.World.Hazards;
using Coop.World.Objects;
using UnityEngine;

namespace Coop.World.Flames;

/// <summary>
/// Hands for testing fires, flames and the stove from the dev bridge:
/// <c>neet call type.call type=Coop.World.Flames.FireTestApi method=Stoves</c>.
/// </summary>
public static class FireTestApi
{
    /// <summary>The room's fire: how many spheres and flames it has here, and how many are lit.</summary>
    public static string Fire(string room)
    {
        if (!System.Enum.TryParse(room, out Location where))
            return "No room " + room;
        var spreader = FireRooms.SpreaderIn(where);
        if (spreader == null)
            return "no fire";
        var flames = new System.Collections.Generic.List<Vector3>();
        FireRooms.Collect(spreader, flames);
        return $"{spreader.spheres.Count} spheres, {flames.Count} flames";
    }

    /// <summary>Every flame of a room's fire as x,y,z (for comparing the games).</summary>
    public static string FireFlames(string room)
    {
        if (!System.Enum.TryParse(room, out Location where) || FireRooms.SpreaderIn(where) == null)
            return "";
        var flames = new System.Collections.Generic.List<Vector3>();
        FireRooms.Collect(FireRooms.SpreaderIn(where), flames);
        flames.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.z.CompareTo(b.z));
        var text = new StringBuilder();
        foreach (var p in flames)
            text.Append(p.x.ToString("0.00")).Append(',').Append(p.y.ToString("0.00")).Append(',').Append(p.z.ToString("0.00")).Append(' ');
        return text.ToString();
    }

    /// <summary>Puts out some flames of a room's fire here, the way the extinguisher does (Kill on each).</summary>
    public static string Douse(string room, int count)
    {
        if (!System.Enum.TryParse(room, out Location where) || FireRooms.SpreaderIn(where) == null)
            return "no fire";
        var flames = new System.Collections.Generic.List<Vector3>();
        FireRooms.Collect(FireRooms.SpreaderIn(where), flames);
        int done = 0;
        for (int i = 0; i < count && i < flames.Count; i++)
        {
            FireRooms.Nearest(FireRooms.SpreaderIn(where), flames[i], 0.05f)?.Kill();
            done++;
        }
        return $"put out {done}";
    }

    /// <summary>Starts a fire in a room the way a spark does (on a client, the host is asked).</summary>
    public static string Start(string room)
    {
        if (!System.Enum.TryParse(room, out Location where))
            return "No room " + room;
        Locator.StartRoomFire(where, Locator.GetRoomCenter(where) + Vector3.up);
        return "started";
    }

    public static string Stoves()
    {
        var text = new StringBuilder();
        foreach (var gas in GasSync.Stoves)
        {
            var flame = GasSync.FlameOfStove(gas);
            text.Append(gas.SaveID).Append(" at ").Append(gas.transform.position.ToString("0.0"))
                .Append(": leaking ").Append(gas.gasLeaking).Append(", flame lit ").Append(flame != null && flame.lit)
                .Append(", heat ").Append(flame != null ? flame.HeatLevel.ToString() : "?")
                .Append(", gas ").Append(gas.gasZone.transform.localScale.x.ToString("0.00")).Append('\n');
        }
        return text.ToString();
    }

    public static string Knob(int id)
    {
        foreach (var gas in GasSync.Stoves)
        {
            if (gas.SaveID != id)
                continue;
            gas.Use();
            return "gas " + (gas.gasLeaking ? "on" : "off");
        }
        return "No stove " + id;
    }

    /// <summary>What a lit lighter touching the gas does: asks the flame to ignite.</summary>
    public static string Light(int id)
    {
        foreach (var gas in GasSync.Stoves)
        {
            if (gas.SaveID != id)
                continue;
            var flame = GasSync.FlameOfStove(gas);
            flame.Ignite();
            return "lit " + flame.lit;
        }
        return "No stove " + id;
    }

    public static string PutOut(int id)
    {
        foreach (var gas in GasSync.Stoves)
        {
            if (gas.SaveID != id)
                continue;
            GasSync.FlameOfStove(gas).Extinguish();
            return "done";
        }
        return "No stove " + id;
    }

    public static string Gas(int id)
    {
        foreach (var gas in GasSync.Stoves)
        {
            if (gas.SaveID == id)
                return $"gas {gas.gasZone.transform.localScale.x:0.00}, part {gas.ignitePart.transform.localScale.x:0.00}, part active {gas.ignitePart.activeSelf}";
        }
        return "No stove " + id;
    }

    /// <summary>Every shared flame here: ref, lit, heat, item or scene.</summary>
    public static string Flames()
    {
        var text = new StringBuilder();
        foreach (var info in FlameBook.All)
        {
            if (info.Flame == null)
                continue;
            text.Append(info.Ref).Append('/').Append(info.Index).Append(' ').Append(info.Flame.name).Append(info.Scene ? " (scene)" : "")
                .Append(": lit ").Append(info.Flame.lit).Append(" heat ").Append(info.Flame.HeatLevel).Append(info.Flame.runtimeAddedFlame ? " (burning)" : "").Append('\n');
        }
        return text.ToString();
    }

    /// <summary>The flame of a shared item: lit or not.</summary>
    public static string Of(int itemId)
    {
        var o = NetObjects.Find(new NetRef(NetKind.Item, itemId));
        var flame = o != null ? o.Go.GetComponentInChildren<Flame>(true) : null;
        return flame == null ? "no flame" : $"lit {flame.lit} heat {flame.HeatLevel} runtime {flame.runtimeAddedFlame} replica {o.IsReplica}";
    }

    public static string Ignite(int itemId)
    {
        var o = NetObjects.Find(new NetRef(NetKind.Item, itemId));
        var flame = o != null ? o.Go.GetComponentInChildren<Flame>(true) : null;
        if (flame == null)
            return "no flame";
        flame.Ignite();
        return "lit " + flame.lit;
    }

    public static string Extinguish(int itemId)
    {
        var o = NetObjects.Find(new NetRef(NetKind.Item, itemId));
        var flame = o != null ? o.Go.GetComponentInChildren<Flame>(true) : null;
        if (flame == null)
            return "no flame";
        flame.Extinguish();
        return "lit " + flame.lit;
    }

    /// <summary>Sets an item on fire the way a flame touching it does.</summary>
    public static string Burn(int itemId)
    {
        var o = NetObjects.Find(new NetRef(NetKind.Item, itemId));
        if (o == null)
            return "no item";
        Flame.AddBurnCollider(o.Go);
        return "burning";
    }
}
