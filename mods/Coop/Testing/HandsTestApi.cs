using System.Text;
using NeetModKit.Game;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Coop.Testing;

/// <summary>
/// For tests from the dev bridge (<c>type.call type=Coop.Testing.HandsTestApi method=Kick</c>): puts
/// things in this game's player's hands, looks up and down, kicks and holds a hand up the way the
/// player's own input would, to see how another game shows that body.
/// </summary>
public static class HandsTestApi
{
    /// <summary>Picks up the nearest item whose name contains <paramref name="name"/> with a hand (0 left, 1 right).</summary>
    public static string Hold(string name, int hand = 0)
    {
        var abilities = Abilities();
        if (abilities == null)
            return "No player.";
        Usable best = null;
        float bestDistance = float.MaxValue;
        foreach (var usable in Object.FindObjectsOfType<Usable>())
        {
            if (!usable.name.Contains(name) || usable.player != null)
                continue;
            float distance = (usable.transform.position - abilities.transform.position).sqrMagnitude;
            if (distance < bestDistance)
            {
                best = usable;
                bestDistance = distance;
            }
        }
        if (best == null)
            return $"No free {name} anywhere.";
        abilities.Pickup((global::Hands)hand, best.gameObject);
        return abilities.pickups[hand] == best.gameObject ? $"Holding {best.name}." : $"Couldn't pick up {best.name}.";
    }

    public static string Drop(int hand = 0)
    {
        var abilities = Abilities();
        if (abilities == null || abilities.pickups[hand] == null)
            return "Nothing to drop.";
        abilities.Drop((global::Hands)hand);
        return "Dropped.";
    }

    /// <summary>Looks up or down (degrees, negative up, positive down) and, when <paramref name="heading"/> is 0-360, turns to it (0 is +Z, 90 is +X).</summary>
    public static string Look(float pitch, float heading = -1f)
    {
        var player = GameRefs.Player;
        if (player == null)
            return "No player.";
        var facing = Quaternion.Euler(pitch, heading >= 0f ? heading : player.transform.eulerAngles.y, 0f) * Vector3.forward;
        PlayerControl.Look(facing);
        return $"Looking {facing}.";
    }

    public static string Kick()
    {
        var abilities = Abilities();
        if (abilities == null)
            return "No player.";
        abilities.Kick();
        return "Kicked.";
    }

    /// <summary>Presses (or lets go of) the mouse buttons that hold the hands up: 0 none, 1 left, 2 right, 3 both.</summary>
    public static string Press(int buttons)
    {
        var mouse = Mouse.current;
        if (mouse == null)
            return "No mouse.";
        using (StateEvent.From(mouse, out var eventPtr))
        {
            mouse.leftButton.WriteValueIntoEvent((buttons & 1) != 0 ? 1f : 0f, eventPtr);
            mouse.rightButton.WriteValueIntoEvent((buttons & 2) != 0 ? 1f : 0f, eventPtr);
            InputSystem.QueueEvent(eventPtr);
        }
        return $"Buttons {buttons}.";
    }

    /// <summary>The input bindings of the two hands.</summary>
    public static string Bindings()
    {
        var text = new StringBuilder();
        foreach (var action in new[] { HouseManager._.i_left_hand, HouseManager._.i_right_hand })
        {
            text.Append(action.name).Append(':');
            foreach (var binding in action.bindings)
                text.Append(' ').Append(binding.path);
            text.Append('\n');
        }
        return text.ToString();
    }

    /// <summary>This player's root, camera and hand ends, in the root's own frame (what the pose carries).</summary>
    public static string Mine()
    {
        var abilities = Abilities();
        if (abilities == null)
            return "No player.";
        var root = abilities.transform;
        var text = new StringBuilder();
        text.AppendLine($"root {root.position} yaw {root.eulerAngles}");
        var camera = HouseManager._.mainCamera.transform;
        text.AppendLine($"camera local {root.InverseTransformPoint(camera.position)} pitch {camera.localEulerAngles.x}");
        for (int i = 0; i < 2; i++)
        {
            text.AppendLine($"hand {i}: {root.InverseTransformPoint(abilities.rigidHands[i].position)} shown {abilities.rendererHands[i].enabled}");
            if (abilities.pickups[i] != null)
                text.AppendLine($"  holds {abilities.pickups[i].name} at {root.InverseTransformPoint(abilities.pickups[i].transform.position)}");
        }
        return text.ToString();
    }

    /// <summary>The same for another player's body here: where its bones are in the body's frame.</summary>
    public static string Theirs(int slot)
    {
        var puppet = Players.RemotePlayers.Find((byte)slot);
        if (puppet == null)
            return "No body.";
        var root = puppet.transform;
        var text = new StringBuilder();
        text.AppendLine($"root {root.position} yaw {root.eulerAngles}, physics hands {puppet.Hands != null}");
        foreach (var renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            text.AppendLine($"mesh {renderer.name} enabled {renderer.enabled} shapes {Shapes(renderer)}");
        foreach (var name in new[] { "chestbone", "headbone", "rightshoulder", "rightupperarm", "rightforearm", "rightforearm_end", "leftupperarm", "leftforearm_end" })
        {
            var bone = Players.PuppetBuilder.FindDeep(root, name);
            if (bone != null)
                text.AppendLine($"{name} {root.InverseTransformPoint(bone.position)}");
        }
        return text.ToString();
    }

    private static string Shapes(SkinnedMeshRenderer renderer)
    {
        var mesh = renderer.sharedMesh;
        if (mesh == null || mesh.blendShapeCount < 2)
            return "-";
        return renderer.GetBlendShapeWeight(0) + "/" + renderer.GetBlendShapeWeight(1);
    }

    private static Player_Abilities Abilities() =>
        GameRefs.Player != null ? GameRefs.Player.GetComponent<Player_Abilities>() : null;
}
