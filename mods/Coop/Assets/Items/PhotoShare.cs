using Coop.Session;

namespace Coop.Assets.Items;

/// <summary>
/// The photo camera writes its picture to a file on this machine and loads it onto a new photo item
/// (PhotoCamera.PrintPicture, then Picture.loadImage with the file). In a session that file goes into
/// the cache, the photo loads it from there and keeps its shared path, and the others are told once
/// the photo is shared. (Unofficial NEET Patches gives every photo its own file and keeps the path
/// through saves.)
/// </summary>
internal static class PhotoShare
{
    /// <summary>From the loadImage hook with a plain file path: true when it's now a shared one.</summary>
    public static bool Taken(Picture picture, ref string filePath)
    {
        if (!CoopSession.IsActive || !SharedPaths.IsLocalFile(filePath))
            return false;
        string shared = SharedFiles.Share(filePath, "a photo");
        if (shared == filePath)
            return false;
        filePath = shared;
        ItemPaths.Announce(picture, ItemPaths.Photo, shared);
        return true;
    }
}
