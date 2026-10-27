using System;
using NeetModKit.Game;

namespace NeetModKit.Saves;

/// <summary>Takes a <see cref="SaveSnapshot"/> of the running house without saving anything.</summary>
public static class SaveCapture
{
    /// <summary>
    /// The game's save data (mod data included) and the current paint, as a save would hold them.
    /// Writes no file and runs none of the save hooks. Main thread, in the house.
    /// </summary>
    public static SaveSnapshot Take()
    {
        var handler = SaveLoadHandler._ ?? throw new InvalidOperationException("There's no house to capture.");
        return new SaveSnapshot
        {
            Save = handler.AllSaveDataToJSon(),
            Paint = PaintSurfaces.CapturePng(),
        };
    }
}
