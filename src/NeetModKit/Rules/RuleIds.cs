using System;
using System.Collections.Generic;

namespace NeetModKit.Rules;

/// <summary>Ids of things from content files: "ModId/name", where a file may just write "name".</summary>
public static class RuleIds
{
    /// <summary>
    /// Finds an item by id. A short id ("pizza") means one from <paramref name="nearMod"/> first, then
    /// any mod's if only one has that name.
    /// </summary>
    public static T Find<T>(IReadOnlyList<T> items, Func<T, string> idOf, string id, string nearMod = null) where T : class
    {
        if (string.IsNullOrEmpty(id))
            return null;
        var exact = Exact(items, idOf, id);
        if (exact != null || id.IndexOf('/') >= 0)
            return exact;

        if (nearMod != null)
        {
            var local = Exact(items, idOf, nearMod + "/" + id);
            if (local != null)
                return local;
        }

        T only = null;
        string suffix = "/" + id;
        for (int i = 0; i < items.Count; i++)
        {
            if (!idOf(items[i]).EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                continue;
            if (only != null)
                return null; // Ambiguous.
            only = items[i];
        }
        return only;
    }

    private static T Exact<T>(IReadOnlyList<T> items, Func<T, string> idOf, string id) where T : class
    {
        for (int i = 0; i < items.Count; i++)
        {
            if (string.Equals(idOf(items[i]), id, StringComparison.OrdinalIgnoreCase))
                return items[i];
        }
        return null;
    }
}
