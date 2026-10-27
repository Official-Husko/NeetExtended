using Coop.Players;
using LiteNetLib.Utils;
using UnityEngine;

namespace Coop.World.Forwarding;

/// <summary>
/// Things done to another player's body here that only mean something in their own game, where the
/// real body is: a kill plane or a teleporter sending it somewhere, and an explosion close enough to
/// gib it. Both go to that player's game (<see cref="Forwarder"/>'s WorldForward):
/// - Move: destination (x, y, z), bring held items (bool). Where to put the body, not how far to move
///   it, so a game that already did it itself (its own body touched the same hazard) isn't moved twice:
///   a body within <see cref="AlreadyThere"/> of the spot, or one this game's own hazards moved in the
///   last <see cref="OwnMoveSeconds"/> (<see cref="OwnBodyMoved"/>), stays where it is;
/// - Gib: nothing more. The game's own gibbing, which goes down first like any death.
/// </summary>
internal static class BodyMoves
{
    // A body already this close to the destination has been moved.
    private const float AlreadyThere = 0.75f;
    private const float OwnMoveSeconds = 3f;

    private static float _ownMoveAt = -100f;

    /// <summary>A hazard in this game just reached <paramref name="body"/>; if it's the player, that's their move done.</summary>
    public static void OwnBodyMoved(GameObject body)
    {
        if (body != null && body == WorldSide.LocalPlayer)
            _ownMoveAt = Time.time;
    }

    /// <summary>A hazard here sent the player's body to <paramref name="destination"/>.</summary>
    public static void Moved(byte slot, Vector3 destination, bool bringHeld)
    {
        if (!WorldSide.Running)
            return;
        var w = Forwarder.Begin(slot, Forwarder.Move);
        Forwarder.Put(w, destination);
        w.Put(bringHeld);
        Forwarder.Send(slot, w);
    }

    /// <summary>An explosion here gibbed the player's body.</summary>
    public static void Gibbed(byte slot)
    {
        if (WorldSide.Running)
            Forwarder.Send(slot, Forwarder.Begin(slot, Forwarder.Gib));
    }

    public static void Receive(byte kind, NetDataReader reader)
    {
        var player = WorldSide.LocalPlayer;
        if (player == null)
            return;
        if (kind == Forwarder.Gib)
        {
            if (player.TryGetComponent(out Player_Abilities rig))
                rig.Obliterate();
            return;
        }
        var destination = Forwarder.Get(reader);
        bool bringHeld = reader.GetBool();
        if (Time.time - _ownMoveAt < OwnMoveSeconds)
            return;
        var delta = destination - player.transform.position;
        if (delta.sqrMagnitude < AlreadyThere * AlreadyThere)
            return;
        player.transform.position = destination;
        if (player.TryGetComponent(out Rigidbody body))
            body.position = destination;
        if (bringHeld && player.TryGetComponent(out Player_Abilities hands))
        {
            foreach (var held in hands.pickups)
            {
                if (held != null)
                    held.transform.position += delta;
            }
        }
    }
}
