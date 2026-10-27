using System;
using HarmonyLib;
using NeetModKit.Saves;

namespace NeetModKit.Patches;

/// <summary>Ends a load from a <see cref="SaveSnapshot"/>: clears it and tells whoever started the load.</summary>
[HarmonyPatch(typeof(SaveLoadHandler), nameof(SaveLoadHandler.LoadAll))]
internal static class SaveSourcePatches
{
    // Returning the exception leaves the game's own handling of it as it was.
    private static Exception Finalizer(Exception __exception)
    {
        SaveSource.Finish(__exception);
        return __exception;
    }
}
