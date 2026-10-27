using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;
using UnofficialNeetPatches.Fixes;

namespace UnofficialNeetPatches.Patches;

/// <summary>
/// PhotoCamera.PrintPicture names each photo "photograph_" + RoundToInt(1e8 * realtimeSinceStartup)
/// (PhotoCamera.cs:31). Past about 21 seconds of play that overflows an int, and every photo becomes
/// photograph_-2147483648.jpg: each new one overwrites the file all the older pictures point at. The
/// number now comes from a counter that starts after the highest photo already in StreamingAssets.
/// </summary>
[HarmonyPatch]
[HarmonyPatchCategory(CameraPhotosFix.FeatureId)]
internal static class PhotoNamePatch
{
    private static readonly MethodInfo Round = AccessTools.Method(typeof(Mathf), nameof(Mathf.RoundToInt), new[] { typeof(float) });
    private static readonly MethodInfo Next = AccessTools.Method(typeof(PhotoNamePatch), nameof(NextNumber));
    private static int _last = -1;

    private static MethodBase TargetMethod() => AccessTools.EnumeratorMoveNext(AccessTools.Method(typeof(PhotoCamera), "PrintPicture"));

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> code)
    {
        foreach (var instruction in code)
        {
            if (instruction.Calls(Round))
                yield return new CodeInstruction(OpCodes.Call, Next).MoveLabelsFrom(instruction);
            else
                yield return instruction;
        }
    }

    // Takes the game's time value only to replace it.
    private static int NextNumber(float ignored)
    {
        if (_last < 0)
            _last = HighestTaken();
        string folder = Application.streamingAssetsPath;
        do
            _last++;
        while (File.Exists(Path.Combine(folder, "photograph_" + _last + ".jpg")));
        return _last;
    }

    private static int HighestTaken()
    {
        int highest = 0;
        try
        {
            foreach (string file in Directory.GetFiles(Application.streamingAssetsPath, "photograph_*.jpg"))
            {
                string number = Path.GetFileNameWithoutExtension(file).Substring("photograph_".Length);
                if (int.TryParse(number, out int n) && n > highest)
                    highest = n;
            }
        }
        catch (Exception)
        {
            // No folder yet: start at 1.
        }
        return highest;
    }
}
