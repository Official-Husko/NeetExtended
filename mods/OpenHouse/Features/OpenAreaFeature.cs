using System.Collections.Generic;
using NeetLoader.Runtime;
using NeetModKit.Events;
using NeetModKit.Features;
using NeetModKit.Game;
using NeetModKit.Scenes;

namespace OpenHouse.Features;

/// <summary>
/// Opens one area the demo keeps closed. Two kinds of lock are used in the house:
/// - demo walls: scene objects named DEMO_BLOCKER_* (a panel with scrolling "demo" text and a
///   solid collider across a doorway), sometimes in more than one piece;
/// - locked doors that need a key, matched by the door's doorId. The demo doesn't ship a key for
///   every one of them.
/// Switching the feature off puts the walls back and re-locks doors it unlocked.
/// </summary>
internal sealed class OpenAreaFeature : ModFeature
{
    public string FeatureId;
    public string Title;
    public string Text;
    public string BlockerPrefix;
    public string[] DoorIds = new string[0];
    public bool OnByDefault;

    public override string Id => FeatureId;
    public override string Name => Title;
    public override string Description => Text;
    public override bool EnabledByDefault => OnByDefault;

    protected override void OnEnable() => GameEvents.WhenHouseReady(this, Open);

    private void Open()
    {
        if (BlockerPrefix != null)
        {
            var blockers = SceneEdits.RootsNamed(BlockerPrefix);
            foreach (var blocker in blockers)
                SceneEdits.Hide(this, blocker);
            if (blockers.Count == 0)
                Owner.Log.Warn($"No {BlockerPrefix} objects found in this house.");
        }

        foreach (var door in DoorsWithIds())
        {
            if (!door.Locked)
                continue;
            door.Locked = false;
            var unlocked = door;
            Track(Disposable.Create(() =>
            {
                if (unlocked != null)
                    unlocked.Locked = true;
            }));
        }
    }

    private IEnumerable<Door> DoorsWithIds()
    {
        var house = GameRefs.House;
        if (house == null || DoorIds.Length == 0)
            yield break;
        foreach (var door in house.doors)
        {
            if (door != null && System.Array.IndexOf(DoorIds, door.doorId) >= 0)
                yield return door;
        }
    }
}
