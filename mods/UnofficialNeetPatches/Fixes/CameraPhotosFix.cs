using NeetModKit.Features;

namespace UnofficialNeetPatches.Fixes;

/// <summary>See <see cref="Patches.PhotoNamePatch"/> and <see cref="Patches.PictureLoadPatch"/>.</summary>
internal sealed class CameraPhotosFix : ModFeature
{
    public const string FeatureId = "CameraPhotos";

    public override string Id => FeatureId;
    public override string Name => "Camera photos are kept";
    public override string Description =>
        "After the first 20 seconds of play every photo from the camera went to the same file, so a new photo replaced the " +
        "picture on all the older ones, and photos came back blank after loading a save. Each photo now gets its own file " +
        "and loads again.";
}
