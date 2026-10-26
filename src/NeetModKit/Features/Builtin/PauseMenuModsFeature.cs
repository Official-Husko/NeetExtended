using NeetModKit.Events;
using NeetModKit.Ui;

namespace NeetModKit.Features.Builtin;

internal sealed class PauseMenuModsFeature : ModFeature
{
    public override string Id => "PauseMenuMods";
    public override string Name => "MODS in the pause menu";
    public override string Description =>
        "Replaces the pause menu's feedback form with a MODS section that opens the mod menu and What's New.";

    protected override void OnEnable()
    {
        // The pause menu can be rebuilt with a scene, so check again after every load.
        GameEvents.SceneLoaded.Subscribe(this, _ => PauseMenuMods.Apply(this));
        PauseMenuMods.Apply(this);
    }
}
