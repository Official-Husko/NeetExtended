using DevBridge.Server;
using NeetModKit.Menu;
using Newtonsoft.Json.Linq;

namespace DevBridge.Commands;

/// <summary>Open and close the mod menu remotely, mainly for screenshots and UI testing.</summary>
internal static class MenuCommands
{
    [BridgeCommand("menu.open", "Open the mod menu, optionally on a page.", Params = new[] { "page?: tab title, e.g. Mods, What's New" })]
    private static object Open(JObject p)
    {
        ModMenu.Open(p.Str("page", null));
        return ModMenu.IsOpen;
    }

    [BridgeCommand("menu.close", "Close the mod menu.")]
    private static object Close(JObject p)
    {
        ModMenu.Close();
        return ModMenu.IsOpen;
    }
}
