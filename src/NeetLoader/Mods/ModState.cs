namespace NeetLoader.Mods;

public enum ModState
{
    /// <summary>Found but not loaded yet.</summary>
    Pending,
    /// <summary>Switched off by the player.</summary>
    Disabled,
    /// <summary>Loaded and running.</summary>
    Loaded,
    /// <summary>Something went wrong; see <see cref="ModInfo.Error"/>.</summary>
    Failed,
}
