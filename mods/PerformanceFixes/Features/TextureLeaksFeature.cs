using NeetModKit.Features;
using PerformanceFixes.Leaks;
using PerformanceFixes.Rendering;

namespace PerformanceFixes.Features;

/// <summary>See <see cref="Patches.TextureLeakPatches"/> and <see cref="SceneOwned"/>.</summary>
internal sealed class TextureLeaksFeature : ModFeature
{
    public const string FeatureId = "TextureLeaks";

    public override string Id => FeatureId;
    public override string Name => "Free unused textures";
    public override string Description =>
        "The game keeps the paint textures of the whole house from every earlier load (each death and each night's " +
        "sleep loads the house again), about 1.5 GB of video memory each time in a messy house. It also makes a new " +
        "texture every time you walk up to a door or a wet spot dries. This frees them once they're no longer used.";

    protected override void OnEnable()
    {
        SceneOwned.Start();
        PaintTextures.OwnExisting();
    }

    protected override void OnDisable()
    {
        SceneOwned.Stop();
        ScratchTextures.Release();
    }
}
