using System.IO;
using HarmonyLib;
using UnofficialNeetPatches.Fixes;

namespace UnofficialNeetPatches.Patches;

/// <summary>
/// A camera photo keeps its file's full path in Picture.storedImagePath, and the save stores that.
/// After a load, Picture.loadImage("") only looks for StreamingAssets/&lt;path&gt;.jpg and then for a
/// texture of that name in Resources (Picture.cs:44-53), which a full path never is, so the photo
/// comes back blank. A stored path that is a file on disk is now loaded from there. The path is also
/// known from the moment the picture starts loading (the game only sets it when the file has been
/// read), so a save or a copy of the picture made in between keeps it.
/// </summary>
[HarmonyPatch(typeof(Picture), nameof(Picture.loadImage))]
[HarmonyPatchCategory(CameraPhotosFix.FeatureId)]
internal static class PictureLoadPatch
{
    private static void Prefix(Picture __instance, ref string filePath)
    {
        if (!string.IsNullOrEmpty(filePath))
        {
            __instance.storedImagePath = filePath;
            return;
        }
        string stored = __instance.storedImagePath;
        if (!string.IsNullOrEmpty(stored) && Path.IsPathRooted(stored) && File.Exists(stored))
            filePath = stored;
    }
}
