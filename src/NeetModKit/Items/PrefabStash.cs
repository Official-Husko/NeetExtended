using UnityEngine;

namespace NeetModKit.Items;

/// <summary>
/// An inactive, persistent parent for item templates. Children of an inactive object never run
/// Awake/Start/Update, but Instantiate(child) gives an active copy with no parent, which is
/// exactly how the game spawns items from its own prefabs.
/// </summary>
internal static class PrefabStash
{
    private static Transform _root;

    /// <summary>Destroys the stash (and any templates still in it) when the framework unloads.</summary>
    public static void Release()
    {
        if (_root != null)
            Object.Destroy(_root.gameObject);
        _root = null;
    }

    public static Transform Root
    {
        get
        {
            if (_root != null)
                return _root;
            var go = new GameObject("NeetModKit.Templates");
            go.SetActive(false);
            go.hideFlags = HideFlags.HideAndDontSave;
            Object.DontDestroyOnLoad(go);
            _root = go.transform;
            return _root;
        }
    }
}
