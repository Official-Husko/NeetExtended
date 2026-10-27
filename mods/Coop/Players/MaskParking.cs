using Coop.World.Objects;
using Coop.World.Ownership;
using Coop.World.Streaming;
using UnityEngine;

namespace Coop.Players;

/// <summary>
/// Where the masks, hats, glasses and cigarettes of a player who left go: parked under the house,
/// kinematic, the way the game parks worn clothes (<c>Wearable.Wear</c>, y -500). A returning body puts
/// them on again with the game's own <c>PutOnMask</c>, which moves the item to the wearer's camera
/// wherever it is, and a player who never comes back leaves nothing hanging in the air. The host's
/// stream carries the new place to the other games; each game also parks its own copy so it doesn't
/// wait for it. After a save load they are parked again (<see cref="MaskParkingPatch"/>).
/// </summary>
internal static class MaskParking
{
    /// <summary>The height <c>Wearable.Wear</c> parks clothes at.</summary>
    public static readonly Vector3 Spot = new Vector3(0f, -500f, 0f);

    /// <summary>Parks what <paramref name="looks"/> lists (the things a leaving player wore) that is in this game.</summary>
    public static void ParkAll(MaskLook[] looks)
    {
        if (looks == null)
            return;
        foreach (var look in looks)
        {
            var item = NetObjects.Find(look.Item);
            if (item != null && item.Alive)
                Park(item);
        }
    }

    private static void Park(NetObject item)
    {
        // Off the head first (a body that was never built here leaves it where the stream put it).
        item.OnPuppet = false;
        if (item.Tf.parent != null)
            item.Tf.SetParent(null, true);
        item.Buffer.Clear();
        ReplicaDriver.Stop(item);
        var body = item.Body;
        if (body != null)
        {
            if (!body.isKinematic)
            {
                body.velocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
            body.isKinematic = true;
        }
        item.GameKinematic = true;
        ReplicaBody.Place(item, Spot, item.Tf.rotation);
        // The owner's game tells the others; a replica here is moved by its owner's stream.
        if (!item.IsReplica)
            NetObjects.MarkMoving(item, Time.time);
    }
}
