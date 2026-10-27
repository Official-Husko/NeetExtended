namespace Coop.World.Patches;

/// <summary>
/// World's Harmony patches only exist while a session runs, so single player pays nothing for them.
/// Every World patch class has one of these categories; the loader skips categorised classes and
/// removes everything of the mod's when it unloads.
/// </summary>
internal static class WorldPatches
{
    /// <summary>Host and clients: spawns, destroys, holding, money, owner-only item logic.</summary>
    public const string World = "CoopWorld";

    /// <summary>Clients only: parts of the house that act on their own, and NPC callbacks.</summary>
    public const string Client = "CoopClient";

    private static bool _world;
    private static bool _client;

    public static void Apply(bool client)
    {
        var harmony = CoopMod.Instance?.Harmony;
        if (harmony == null)
            return;
        var assembly = typeof(WorldPatches).Assembly;
        if (!_world)
        {
            harmony.PatchCategory(assembly, World);
            _world = true;
        }
        if (client && !_client)
        {
            harmony.PatchCategory(assembly, Client);
            _client = true;
        }
    }

    public static void Remove()
    {
        var harmony = CoopMod.Instance?.Harmony;
        var assembly = typeof(WorldPatches).Assembly;
        if (harmony != null && _client)
            harmony.UnpatchCategory(assembly, Client);
        if (harmony != null && _world)
            harmony.UnpatchCategory(assembly, World);
        _client = false;
        _world = false;
    }
}
