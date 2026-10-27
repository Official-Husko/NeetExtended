using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using Object = UnityEngine.Object;

namespace Mixtapes.Audio;

/// <summary>
/// Loads song files into AudioClips, once per file, in the background. Clips stay compressed in
/// memory and are decoded while they play, so long songs don't take hundreds of megabytes.
/// </summary>
internal static class SongLibrary
{
    private sealed class Song
    {
        public AudioClip Clip;
        public string Error;
        public bool Loading;
        public readonly List<Action<AudioClip>> Waiting = new List<Action<AudioClip>>();
    }

    private static readonly Dictionary<string, Song> Songs = new Dictionary<string, Song>(StringComparer.OrdinalIgnoreCase);
    private static MixtapesMod _mod;

    /// <summary>Song file types Unity plays.</summary>
    public static readonly HashSet<string> Types = new HashSet<string> { ".ogg", ".mp3", ".wav", ".aif", ".aiff" };

    /// <summary>Raised after a song has loaded (or failed to).</summary>
    public static event Action SongLoaded;

    public static void Init(MixtapesMod mod) => _mod = mod;

    /// <summary>Calls <paramref name="done"/> with the song's clip once it's loaded (right away if it is), or null if it can't be.</summary>
    public static void Get(string file, Action<AudioClip> done)
    {
        // Not there yet (co-op is still fetching the host's song): no clip, and no error kept, so
        // the next load tries again.
        if (!Songs.ContainsKey(file) && !File.Exists(file))
        {
            done(null);
            return;
        }
        if (!Songs.TryGetValue(file, out var song))
            Songs[file] = song = new Song();
        if (song.Clip != null || song.Error != null)
        {
            done(song.Clip);
            return;
        }
        song.Waiting.Add(done);
        if (!song.Loading)
        {
            song.Loading = true;
            _mod.StartCoroutine(Load(file, song));
        }
    }

    /// <summary>The clip if it's loaded, else null.</summary>
    public static AudioClip Loaded(string file) => Songs.TryGetValue(file, out var song) ? song.Clip : null;

    /// <summary>Why a file couldn't be loaded, or null.</summary>
    public static string ErrorOf(string file) => Songs.TryGetValue(file, out var song) ? song.Error : null;

    /// <summary>Drops songs that aren't in <paramref name="keep"/> anymore (files removed or renamed).</summary>
    public static void Keep(ICollection<string> keep)
    {
        var gone = new List<string>();
        foreach (var pair in Songs)
        {
            if (!keep.Contains(pair.Key) && !pair.Value.Loading)
                gone.Add(pair.Key);
        }
        foreach (string file in gone)
        {
            Destroy(Songs[file]);
            Songs.Remove(file);
        }
    }

    public static void Clear()
    {
        foreach (var song in Songs.Values)
            Destroy(song);
        Songs.Clear();
    }

    private static IEnumerator Load(string file, Song song)
    {
        using (var request = UnityWebRequestMultimedia.GetAudioClip(new Uri(file).AbsoluteUri, TypeOf(file)))
        {
            // Long songs stay compressed in memory. Not MP3s: playing a compressed MP3 on the
            // radio crashed the game under Proton, so those are decoded once while loading.
            ((DownloadHandlerAudioClip)request.downloadHandler).compressed = TypeOf(file) != AudioType.MPEG;
            yield return request.SendWebRequest();

            song.Loading = false;
            if (request.result != UnityWebRequest.Result.Success)
                song.Error = request.error ?? "couldn't be read";
            else
            {
                song.Clip = DownloadHandlerAudioClip.GetContent(request);
                if (song.Clip == null || song.Clip.length <= 0f)
                    song.Error = "isn't a song Unity can play";
                else
                    song.Clip.name = Path.GetFileNameWithoutExtension(file);
            }
        }

        if (song.Error != null)
            _mod.Log.Warn($"{Path.GetFileName(file)}: {song.Error}. OGG, MP3 and WAV work; convert others with ffmpeg -i song.flac song.ogg");
        foreach (var done in song.Waiting)
        {
            try { done(song.Clip); }
            catch (Exception e) { _mod.Log.Error("A song callback failed", e); }
        }
        song.Waiting.Clear();
        SongLoaded?.Invoke();
    }

    private static AudioType TypeOf(string file)
    {
        switch (Path.GetExtension(file).ToLowerInvariant())
        {
            case ".mp3": return AudioType.MPEG;
            case ".wav": return AudioType.WAV;
            case ".aif":
            case ".aiff": return AudioType.AIFF;
            default: return AudioType.OGGVORBIS;
        }
    }

    private static void Destroy(Song song)
    {
        if (song.Clip != null)
            Object.Destroy(song.Clip);
    }
}
