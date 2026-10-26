using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HarmonyLib;

namespace PerformanceFixes.Painting;

/// <summary>
/// Which paint surfaces match their picture on disk. A surface counts as saved after its picture
/// was written or after it was loaded from one, and stops counting once anything paints on it or
/// clears it. Everything starts out unsaved, so the first save after loading this mod writes all.
/// </summary>
internal static class PaintTracker
{
    private static readonly AccessTools.FieldRef<PaintTarget, List<Paint>> Splats =
        AccessTools.FieldRefAccess<PaintTarget, List<Paint>>("m_Splats");

    private static readonly HashSet<PaintTarget> Saved = new HashSet<PaintTarget>(ByReference.Instance);

    /// <summary>True while the game loads paint from disk (SaveLoadHandler.LoadPaintTargets).</summary>
    public static bool Loading { get; set; }

    public static bool IsSaved(PaintTarget target) => Saved.Contains(target);

    public static void MarkSaved(PaintTarget target) => Saved.Add(target);

    public static void Changed(PaintTarget target) => Saved.Remove(target);

    public static void Forget() => Saved.Clear();

    /// <summary>Splats queued on the surface but not drawn into its texture yet.</summary>
    public static int Pending(PaintTarget target) => Splats(target)?.Count ?? 0;

    // UnityEngine.Object's == treats every destroyed object as equal, so compare references.
    private sealed class ByReference : IEqualityComparer<PaintTarget>
    {
        public static readonly ByReference Instance = new ByReference();

        public bool Equals(PaintTarget x, PaintTarget y) => ReferenceEquals(x, y);

        public int GetHashCode(PaintTarget obj) => RuntimeHelpers.GetHashCode(obj);
    }
}
