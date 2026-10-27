using System.Collections.Generic;
using UnityEngine;

namespace Coop.Players;

/// <summary>
/// Builds a remote player's body from the game's player prefab: only the model (animator, skinned
/// mesh, bones and hands) is copied, under a plain kinematic capsule, so none of the player's scripts
/// (input, stats, saving, camera, HUD) exist on it.
/// </summary>
internal static class PuppetBuilder
{
    private const string BodyChild = "Neet";
    private const string HandRenderer = "NeetHand";

    public static PlayerPuppet Build(byte slot, string name)
    {
        var prefab = HouseManager._ != null ? HouseManager._.playerPrefab : null;
        var model = prefab != null ? prefab.transform.Find(BodyChild) : null;
        if (model == null)
        {
            CoopMod.Log.Warn("Couldn't find the player model to build another player's body from.");
            return null;
        }

        var root = new GameObject("CoopPlayer_" + slot);
        var capsule = root.AddComponent<CapsuleCollider>();
        if (prefab.TryGetComponent(out CapsuleCollider template))
        {
            capsule.center = template.center;
            capsule.radius = template.radius;
            capsule.height = template.height;
            capsule.direction = template.direction;
        }
        var body = root.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.interpolation = RigidbodyInterpolation.Interpolate;

        // On the players' layer, with stand-in stats: NPCs, cops, punches and explosions find this
        // body like a player's, and what hits it goes to that player's own game (World/Forwarding).
        root.layer = LayerMask.NameToLayer("Player");
        root.AddComponent<World.Forwarding.RemoteActor>().Init(slot);

        var copy = Object.Instantiate(model.gameObject, root.transform, false);
        copy.name = BodyChild;
        copy.transform.localPosition = model.localPosition;
        copy.transform.localRotation = model.localRotation;
        copy.transform.localScale = model.localScale;
        StripScripts(copy);
        var hands = CoopMod.Config.Value.PhysicsHands ? PuppetHands.Create(copy, body) : null;
        if (hands == null)
            StripHandPhysics(copy);

        var puppet = root.AddComponent<PlayerPuppet>();
        GiveOwnMeshes(copy, puppet.OwnedMeshes);
        var skin = FindDeep(copy.transform, BodyChild)?.GetComponent<SkinnedMeshRenderer>();
        var handMeshes = new List<SkinnedMeshRenderer>();
        foreach (var renderer in copy.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (renderer != skin)
                handMeshes.Add(renderer);
        }
        puppet.Init(slot, copy.GetComponent<Animator>(), FindDeep(copy.transform, "headbone"), skin, handMeshes.ToArray());
        puppet.Hands = hands;
        puppet.Plate = Nameplate.Attach(root.transform, name, capsule.center.y + capsule.height * 0.5f + 0.15f);
        return puppet;
    }

    // The model has no game scripts today; this keeps it that way if a mod or update adds some.
    private static void StripScripts(GameObject go)
    {
        foreach (var behaviour in go.GetComponentsInChildren<MonoBehaviour>(true))
            Object.Destroy(behaviour);
    }

    // The player's first-person hands are jointed physics bodies that the player's scripts move; on a
    // copy nothing does, so they hang off the body as a floppy second pair of arms. The body mesh has
    // arms of its own, so the copy keeps the hands out of physics and out of sight.
    private static void StripHandPhysics(GameObject go)
    {
        foreach (var joint in go.GetComponentsInChildren<Joint>(true))
            Object.DestroyImmediate(joint);
        foreach (var body in go.GetComponentsInChildren<Rigidbody>(true))
            Object.DestroyImmediate(body);
        foreach (var collider in go.GetComponentsInChildren<Collider>(true))
            Object.DestroyImmediate(collider);
        foreach (var renderer in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (renderer.name == HandRenderer)
                renderer.enabled = false;
        }
    }

    // The game swaps clothing UVs on the shared mesh, which would dress every body the same.
    private static void GiveOwnMeshes(GameObject go, List<Mesh> owned)
    {
        foreach (var renderer in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (renderer.sharedMesh == null)
                continue;
            renderer.sharedMesh = Object.Instantiate(renderer.sharedMesh);
            owned.Add(renderer.sharedMesh);
        }
    }

    private static Transform FindDeep(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name)
                return child;
            var found = FindDeep(child, name);
            if (found != null)
                return found;
        }
        return null;
    }
}
