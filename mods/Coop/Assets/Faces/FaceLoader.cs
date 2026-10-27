using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace Coop.Assets.Faces;

/// <summary>Reads a face picture from this game's copy, in the background like the game reads its own.</summary>
internal static class FaceLoader
{
    public static IEnumerator Load(string path, Action<Texture2D> done)
    {
        using (var request = UnityWebRequestTexture.GetTexture(new Uri(path).AbsoluteUri))
        {
            yield return request.SendWebRequest();
            if (request.result != UnityWebRequest.Result.Success)
            {
                CoopMod.Log.Warn($"Files: couldn't read the face {path}: {request.error}");
                done(null);
                yield break;
            }
            var texture = DownloadHandlerTexture.GetContent(request);
            if (texture != null)
                texture.name = "Co-op face";
            done(texture);
        }
    }
}
