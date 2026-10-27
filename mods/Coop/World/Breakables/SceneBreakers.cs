using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Coop.World.Breakables;

/// <summary>
/// The house's breakables that aren't items: window panes, the glass cases and table, the walls that
/// can be knocked down. They have no id of their own, so each gets one from where it stands: the names
/// from the root down plus its position in centimetres. A sibling index would shift when a neighbour
/// is broken and removed; this doesn't, so a game that reloads after a window is gone still agrees
/// with the others.
/// </summary>
internal static class SceneBreakers
{
    private static readonly Dictionary<int, BreakOnImpact> ById = new Dictionary<int, BreakOnImpact>(128);
    private static readonly Dictionary<int, int> IdOfInstance = new Dictionary<int, int>(128);
    private static readonly StringBuilder Text = new StringBuilder(256);

    public static int Count => ById.Count;

    public static IEnumerable<KeyValuePair<int, BreakOnImpact>> All => ById;

    public static void Clear()
    {
        ById.Clear();
        IdOfInstance.Clear();
    }

    /// <summary>Finds the breakables of this house (inactive ones too). Items, doors and NPCs are networked as themselves.</summary>
    public static void Rebuild()
    {
        Clear();
        var scene = SceneManager.GetActiveScene();
        int clashes = 0;
        foreach (var b in Object.FindObjectsOfType<BreakOnImpact>(true))
        {
            if (b.gameObject.scene != scene || b.GetComponentInParent<Usable>(true) != null || b.GetComponentInParent<Brain>(true) != null)
                continue;
            int id = IdFor(b.transform);
            if (ById.ContainsKey(id))
            {
                if (++clashes <= 3)
                    CoopMod.Log.Info($"World: breakable {b.name} has the same id as {ById[id].name}; it stays local.");
                continue;
            }
            ById[id] = b;
            IdOfInstance[b.GetInstanceID()] = id;
        }
        CoopMod.Log.Info($"World: {ById.Count} breakables that aren't items are shared.");
    }

    /// <summary>The id of a scene breakable, or 0 for anything else (items, doors, bodies).</summary>
    public static int IdOf(BreakOnImpact b) =>
        b != null && IdOfInstance.TryGetValue(b.GetInstanceID(), out int id) ? id : 0;

    /// <summary>The breakable with this id, or null if it's unknown here or already gone.</summary>
    public static BreakOnImpact Find(int id) => ById.TryGetValue(id, out var b) && b != null ? b : null;

    private static int IdFor(Transform t)
    {
        Text.Length = 0;
        Names(t);
        var p = t.position;
        Text.Append('@').Append(Mathf.RoundToInt(p.x * 100f)).Append(',').Append(Mathf.RoundToInt(p.y * 100f)).Append(',').Append(Mathf.RoundToInt(p.z * 100f));
        unchecked
        {
            uint hash = 2166136261;
            for (int i = 0; i < Text.Length; i++)
            {
                hash ^= Text[i];
                hash *= 16777619;
            }
            return hash == 0 ? 1 : (int)hash;
        }
    }

    private static void Names(Transform t)
    {
        if (t.parent != null)
        {
            Names(t.parent);
            Text.Append('/');
        }
        Text.Append(t.name);
    }
}
