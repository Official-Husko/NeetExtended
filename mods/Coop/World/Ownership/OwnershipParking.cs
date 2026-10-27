using Coop.World.Objects;
using NeetLoader.Runtime;

namespace Coop.World.Ownership;

/// <summary>
/// Carries who-simulates-what over a hot reload of the mod, as plain arrays (the next copy has its
/// own types). Bodies keep their physical state through the reload, so a replica is still kinematic;
/// what the game wanted it to be would otherwise be lost.
/// </summary>
internal static class OwnershipParking
{
    private const string Key = "Coop.World.Owners";

    public static void Park()
    {
        var all = NetObjects.All;
        int count = 0;
        for (int i = 0; i < all.Count; i++)
        {
            if (Worth(all[i]))
                count++;
        }
        var keys = new long[count];
        var owners = new byte[count];
        var holders = new byte[count];
        var states = new byte[count];
        int n = 0;
        for (int i = 0; i < all.Count; i++)
        {
            var o = all[i];
            if (!Worth(o))
                continue;
            keys[n] = o.Ref.Key;
            owners[n] = o.Owner;
            holders[n] = o.Holder;
            states[n] = (byte)((o.IsReplica ? 1 : 0) | (o.GameKinematic ? 2 : 0));
            n++;
        }
        ReloadState.Put(Key, new object[] { keys, owners, holders, states });
    }

    /// <summary>After the registry was rebuilt: puts back what was parked. Returns how many.</summary>
    public static int Restore()
    {
        if (!ReloadState.Take(Key, out object[] parked) || parked.Length != 4)
            return 0;
        var keys = (long[])parked[0];
        var owners = (byte[])parked[1];
        var holders = (byte[])parked[2];
        var states = (byte[])parked[3];
        int restored = 0;
        for (int i = 0; i < keys.Length; i++)
        {
            var o = NetObjects.Find(NetRef.FromKey(keys[i]));
            if (o == null)
                continue;
            o.Owner = owners[i];
            o.Holder = holders[i];
            o.IsReplica = (states[i] & 1) != 0;
            o.GameKinematic = (states[i] & 2) != 0;
            restored++;
        }
        return restored;
    }

    public static void Forget() => ReloadState.Take(Key, out object[] _);

    private static bool Worth(NetObject o) =>
        o.IsReplica || o.Owner != WorldSide.HostSlot || o.Holder != WorldSide.None;
}
