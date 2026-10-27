using System.Text.Json.Nodes;

namespace NeetTool.Compat;

/// <summary>
/// The newest public build of a Steam app, from api.steamcmd.net (a public mirror of Steam's app
/// info; SteamDB has no API). Null when it can't be reached.
/// </summary>
public static class SteamBuilds
{
    public static async Task<string?> LatestPublicAsync(int appId, CancellationToken ct = default)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            string json = await http.GetStringAsync($"https://api.steamcmd.net/v1/info/{appId}", ct);
            return JsonNode.Parse(json)?["data"]?[appId.ToString()]?["depots"]?["branches"]?["public"]?["buildid"]?.GetValue<string>();
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException or InvalidOperationException)
        {
            return null;
        }
    }
}
