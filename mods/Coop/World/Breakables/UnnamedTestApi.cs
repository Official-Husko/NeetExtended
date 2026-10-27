using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace Coop.World.Breakables;

/// <summary>
/// Survey hooks for the dev bridge (<c>type.call type=Coop.World.Breakables.UnnamedTestApi ...</c>):
/// which things the game can make that have an item part and a body but no item name, i.e. the ones
/// World can't spawn elsewhere (the "has no item name" line in the log). Run it again after a game update.
/// </summary>
public static class UnnamedTestApi
{
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    /// <summary>Every loaded prefab with an item part and a body but no item name.</summary>
    public static string Prefabs()
    {
        var sb = new StringBuilder();
        var seen = new HashSet<string>();
        foreach (var u in Resources.FindObjectsOfTypeAll<Usable>())
        {
            if (u == null || u.gameObject.scene.IsValid() || !string.IsNullOrEmpty(u.prefabName))
                continue;
            if (u.lever || u.button || u.GetComponent<Rigidbody>() == null)
                continue;
            if (seen.Add(u.name))
                sb.AppendLine(u.name);
        }
        return sb.ToString();
    }

    /// <summary>Fields of any component (prefab references, lists of them) that point at such a prefab: who can make one.</summary>
    public static string Sources()
    {
        var sb = new StringBuilder();
        var seen = new HashSet<string>();
        foreach (var mb in Resources.FindObjectsOfTypeAll<MonoBehaviour>())
        {
            if (mb == null)
                continue;
            var type = mb.GetType();
            if (type.Namespace != null && (type.Namespace.StartsWith("UnityEngine") || type.Namespace.StartsWith("TMPro") || type.Namespace.StartsWith("Coop")))
                continue;
            foreach (var f in type.GetFields(Fields))
            {
                object v;
                try { v = f.GetValue(mb); } catch { continue; }
                string where = type.Name + "." + f.Name;
                if (v is GameObject g)
                    Check(sb, seen, where, g);
                else if (v is Component c)
                    Check(sb, seen, where, c.gameObject);
                else if (v is Spawnable s)
                    Check(sb, seen, where, s.obj);
                else if (v is IEnumerable<GameObject> gos)
                {
                    foreach (var e in gos)
                        Check(sb, seen, where, e);
                }
                else if (v is IEnumerable<Component> cs)
                {
                    foreach (var e in cs)
                        Check(sb, seen, where, e != null ? e.gameObject : null);
                }
            }
        }
        return sb.ToString();
    }

    /// <summary>Bank items whose own prefabName is empty or differs from the bank key.</summary>
    public static string BankMismatch()
    {
        var sb = new StringBuilder();
        foreach (var pair in PickupBank.pickupsDict)
        {
            var u = pair.Value.obj != null ? pair.Value.obj.GetComponentInChildren<Usable>(true) : null;
            if (u == null)
                sb.AppendLine($"{pair.Key}: no Usable");
            else if (u.prefabName != pair.Key)
                sb.AppendLine($"{pair.Key}: prefabName '{u.prefabName}'");
        }
        return sb.ToString();
    }

    /// <summary>The parts of a prefab, by name: a loaded one, or one that Resources.Load finds.</summary>
    public static string Describe(string prefab)
    {
        GameObject root = null;
        foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (go.name == prefab && !go.scene.IsValid() && go.transform.parent == null)
            {
                root = go;
                break;
            }
        }
        root = root != null ? root : Resources.Load<GameObject>(prefab);
        if (root == null)
            return "No such prefab.";
        var sb = new StringBuilder();
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            sb.Append(t == root.transform ? "" : "  ").Append(t.name).Append(':');
            foreach (var c in t.GetComponents<Component>())
                sb.Append(' ').Append(c == null ? "null" : c.GetType().Name);
            var u = t.GetComponent<Usable>();
            if (u != null)
                sb.Append($" [item name '{u.prefabName}']");
            sb.AppendLine();
        }
        return sb.ToString();
    }

    private static void Check(StringBuilder sb, HashSet<string> seen, string where, GameObject g)
    {
        if (g == null)
            return;
        foreach (var u in g.GetComponentsInChildren<Usable>(true))
        {
            if (!string.IsNullOrEmpty(u.prefabName) || u.lever || u.button || u.GetComponent<Rigidbody>() == null)
                continue;
            if (u.GetComponentInParent<Brain>(true) != null || u.GetComponent<SpraySphere>() != null)
                continue;
            if (seen.Add(where + ">" + g.name))
                sb.AppendLine($"{where} -> {g.name} ({u.name}, {(g.scene.IsValid() ? "scene" : "prefab")})");
        }
    }
}
