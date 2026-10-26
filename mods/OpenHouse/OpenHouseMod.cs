using NeetLoader;
using NeetModKit.Features;
using OpenHouse.Features;

namespace OpenHouse;

/// <summary>Each closed-off area is its own switch on the Mods page.</summary>
public sealed class OpenHouseMod : NeetMod
{
    public override void OnLoad()
    {
        ModFeatures.Register(this, new OpenAreaFeature
        {
            FeatureId = "OpenPantry",
            Title = "Open the pantry",
            Text = "Removes the demo wall in the pantry doorway next to the kitchen. Everything inside can be picked up and used.",
            BlockerPrefix = "DEMO_BLOCKER_PANTRY",
            OnByDefault = true,
        });
        ModFeatures.Register(this, new OpenAreaFeature
        {
            FeatureId = "OpenBasementBackroom",
            Title = "Open the basement backroom",
            Text = "Unlocks the black door behind the boxes in the basement. The demo has no key for it. What's behind it is story material the demo never let anyone reach.",
            DoorIds = new[] { "BASEMENT" },
            OnByDefault = true,
        });
        ModFeatures.Register(this, new OpenAreaFeature
        {
            FeatureId = "OpenDadsYard",
            Title = "Open dad's yard",
            Text = "Unlocks dad's yard door in the living room. The demo does have a key for this one (lying near the door): hold it in one hand, grab the door with the other and use it. Switch this off to do it that way.",
            DoorIds = new[] { "DADYARD" },
            OnByDefault = true,
        });
        ModFeatures.Register(this, new OpenAreaFeature
        {
            FeatureId = "OpenAttic",
            Title = "Open the attic",
            Text = "Removes the demo wall to the attic and unlocks the attic door (normally the Key of Anger's job). Parts of it may be unfinished.",
            BlockerPrefix = "DEMO_BLOCKER_ATTIC",
            DoorIds = new[] { "Attic" },
        });
        ModFeatures.Register(this, new OpenAreaFeature
        {
            FeatureId = "OpenRoof",
            Title = "Open the roof",
            Text = "Removes the demo wall to the roof. Probably unfinished; falling off is on you.",
            BlockerPrefix = "DEMO_BLOCKER_ROOF",
        });
        ModFeatures.Register(this, new FullGameRulesFeature());
    }
}
