using System.Text;
using UnityEngine;

namespace Coop.World.Objects;

/// <summary>
/// An id for a scene object that has none of its own, from where it sits in the scene: the names
/// from the root down, each with its place among same-named siblings. Sibling indexes alone aren't
/// stable, because items spawned at the scene root shift them. Built once per scan.
/// </summary>
internal static class ScenePath
{
    private static readonly StringBuilder Text = new StringBuilder(256);

    public static int Hash(Transform t)
    {
        Text.Length = 0;
        Append(t);
        return Fnv1a(Text);
    }

    public static string Describe(Transform t)
    {
        Text.Length = 0;
        Append(t);
        return Text.ToString();
    }

    private static void Append(Transform t)
    {
        if (t.parent != null)
        {
            Append(t.parent);
            Text.Append('/');
        }
        Text.Append(t.name).Append('#').Append(SameNamedBefore(t));
    }

    private static int SameNamedBefore(Transform t)
    {
        int count = 0;
        var parent = t.parent;
        if (parent == null)
        {
            foreach (var root in t.gameObject.scene.GetRootGameObjects())
            {
                if (root.transform == t)
                    break;
                if (root.name == t.name)
                    count++;
            }
            return count;
        }
        for (int i = 0; i < parent.childCount; i++)
        {
            var sibling = parent.GetChild(i);
            if (sibling == t)
                break;
            if (sibling.name == t.name)
                count++;
        }
        return count;
    }

    private static int Fnv1a(StringBuilder text)
    {
        unchecked
        {
            uint hash = 2166136261;
            for (int i = 0; i < text.Length; i++)
            {
                hash ^= text[i];
                hash *= 16777619;
            }
            return (int)hash;
        }
    }
}
