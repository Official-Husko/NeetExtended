using System.Text;
using Coop.World.Objects;
using UnityEngine;

namespace Coop.World.Flames;

/// <summary>
/// Hands for testing pan heat, sprays and the laser from the dev bridge:
/// <c>neet call type.call type=Coop.World.Flames.HeatTestApi method=Pans</c>.
/// </summary>
public static class HeatTestApi
{
    /// <summary>Every heat holder here: item id, owner, heat, level and whether its flame is lit.</summary>
    public static string Pans()
    {
        var text = new StringBuilder();
        foreach (var h in Object.FindObjectsOfType<HeatHolder>(true))
        {
            var flame = h.GetComponent<Flame>();
            var o = NetObjects.Find(h.gameObject);
            text.Append(h.name).Append(' ').Append(o != null ? o.Ref.ToString() : "-").Append(o != null ? (o.IsReplica ? " replica" : " here") : "")
                .Append(" heat ").Append(h.HeatIndex.ToString("0.00")).Append(" level ").Append(flame.HeatLevel)
                .Append(" lit ").Append(flame.lit).Append(" at ").Append(h.transform.position.ToString("0.0")).Append('\n');
        }
        return text.ToString();
    }

    /// <summary>Spray cans: the sphere each shoots and whether that has a flame.</summary>
    public static string Sprays()
    {
        var text = new StringBuilder();
        foreach (var pair in PickupBank.pickupsDict)
        {
            var bottle = pair.Value.obj != null ? pair.Value.obj.GetComponentInChildren<SprayBottle>(true) : null;
            if (bottle == null)
                continue;
            var sphere = bottle.spraySphere;
            var flame = sphere != null ? sphere.GetComponentInChildren<Flame>(true) : null;
            text.Append(pair.Key).Append(" -> ").Append(sphere != null ? sphere.name : "none");
            if (flame != null)
                text.Append(" flame lit ").Append(flame.lit).Append(" inert ").Append(flame.inert).Append(" sterile ").Append(flame.sterile)
                    .Append(" starts ").Append(flame.canStartFires).Append(" max ").Append(flame.maxHeatLevel)
                    .Append(" on ").Append(flame.gameObject == sphere ? "root" : flame.name);
            text.Append('\n');
        }
        return text.ToString();
    }

    /// <summary>Sets a pan's heat here (the owner's game decides; this is for trying a replica).</summary>
    public static string SetHeat(int id, float heat)
    {
        var o = NetObjects.Find(new NetRef(NetKind.Item, id));
        var holder = o != null && o.Go != null ? o.Go.GetComponentInChildren<HeatHolder>(true) : null;
        if (holder == null)
            return "No heat holder.";
        holder.HeatIndex = heat;
        return holder.HeatIndex.ToString("0.0");
    }

    /// <summary>Puts an item over a stove's flame, where a pan on that burner sits.</summary>
    public static string OnStove(int id, int stove)
    {
        foreach (var gas in GasSync.Stoves)
        {
            if (gas.SaveID != stove)
                continue;
            var o = NetObjects.Find(new NetRef(NetKind.Item, id));
            if (o == null || o.Go == null)
                return "No item.";
            var at = GasSync.FlameOfStove(gas).transform.position + Vector3.up * 0.1f;
            o.Go.transform.position = at;
            if (o.Go.TryGetComponent(out Rigidbody body))
                body.velocity = Vector3.zero;
            return "put at " + at.ToString("0.00");
        }
        return "No stove " + stove;
    }

    /// <summary>Burning spray spheres in this game: how many are lit, whether each is a copy, and where.</summary>
    public static string Fireballs()
    {
        var text = new StringBuilder(FireballSync.Counts()).Append('\n');
        foreach (var sphere in Object.FindObjectsOfType<SpraySphere>())
        {
            var flame = sphere.GetComponentInChildren<Flame>();
            if (flame == null || !flame.lit)
                continue;
            text.Append(sphere.name).Append(FireballSync.IsCopy(flame) ? " copy" : " own").Append(" at ")
                .Append(sphere.transform.position.ToString("0.00")).Append(" starts ").Append(flame.canStartFires).Append('\n');
        }
        return text.ToString();
    }

    /// <summary>Turns this game's player and camera towards a point, for screenshots.</summary>
    public static string Look(float x, float y, float z)
    {
        var player = HouseManager._.player;
        var cam = Camera.main;
        var to = new Vector3(x, y, z) - cam.transform.position;
        player.transform.rotation = Quaternion.Euler(0f, Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg, 0f);
        float pitch = -Mathf.Atan2(to.y, new Vector2(to.x, to.z).magnitude) * Mathf.Rad2Deg;
        cam.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        return "looking";
    }

    /// <summary>The puff of a tax bill burning up, as the host's tax handler sends it.</summary>
    public static string Puff(float x, float y, float z)
    {
        PuffSync.Burnt(new Vector3(x, y, z));
        return "sent";
    }

    /// <summary>How many puffs of fire are in this game right now.</summary>
    public static int Puffs()
    {
        int n = 0;
        foreach (var go in Object.FindObjectsOfType<FireParticle>())
            n += go.name.StartsWith("FireParticle_Small") ? 1 : 0;
        return n;
    }
}
