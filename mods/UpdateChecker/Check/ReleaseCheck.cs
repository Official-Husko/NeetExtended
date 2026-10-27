using System;
using System.Collections;
using NeetModKit.About;
using Newtonsoft.Json.Linq;
using UnityEngine.Networking;

namespace UpdateChecker.Check;

/// <summary>
/// One request to GitHub's "latest release" endpoint. Any failure (offline, rate limit, a Proton
/// network hiccup) is one log line and nothing the player sees.
/// </summary>
internal static class ReleaseCheck
{
    public const string Api = "https://api.github.com/repos/Official-Husko/NeetExtended/releases/latest";
    public const string ReleasesPage = "https://github.com/Official-Husko/NeetExtended/releases";

    private static bool _busy;

    public static IEnumerator Run()
    {
        if (_busy)
            yield break;
        _busy = true;
        try
        {
            using (var request = UnityWebRequest.Get(Api))
            {
                request.SetRequestHeader("User-Agent", PackInfo.Name + "/" + PackInfo.Version);
                request.SetRequestHeader("Accept", "application/vnd.github+json");
                request.timeout = 10;
                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                    Failed(request.responseCode > 0 ? $"GitHub said {request.responseCode}" : request.error ?? "no answer");
                else
                    Read(request.downloadHandler.text);
            }
        }
        finally
        {
            _busy = false;
        }
    }

    private static void Read(string json)
    {
        try
        {
            var release = JObject.Parse(json);
            string tag = release.Value<string>("tag_name");
            string url = release.Value<string>("html_url") ?? ReleasesPage;
            if (!UpdateState.Set(tag, url))
                Failed($"the latest release is called '{tag}', not a version");
            else
                UpdateCheckerMod.Instance?.Log.Info($"Newest release: {UpdateState.Latest} (running {UpdateState.Current}).");
        }
        catch (Exception e)
        {
            Failed("unreadable answer (" + e.Message + ")");
        }
    }

    private static void Failed(string reason)
    {
        UpdateCheckerMod.Instance?.Log.Info("Update check failed: " + reason);
        UpdateState.Fail(reason);
    }
}
