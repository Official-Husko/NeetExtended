using System;
using System.Collections.Generic;
using NeetLoader;
using NeetLoader.Logging;
using NeetLoader.Runtime;
using Newtonsoft.Json.Linq;

namespace NeetModKit.Saves;

/// <summary>
/// Lets mods keep their own data inside the game's save file. Everything is stored in one extra
/// entry with the type "NeetModKit.ModData", which the game ignores when loading, so saves stay
/// loadable without the mods installed.
/// </summary>
public static class ModSaveData
{
    public const string EntryType = "NeetModKit.ModData";

    private sealed class Slot
    {
        public Func<JToken> Save;
        public Action<JToken> Load;
        public Action Reset;
    }

    private static readonly Dictionary<string, Slot> Slots = new Dictionary<string, Slot>();
    private static JObject _pending;
    private static ModLogger _log;

    /// <summary>
    /// Registers data under <paramref name="key"/> (default: the mod id). <paramref name="save"/> is called
    /// when the game saves; <paramref name="load"/> after a save is loaded, or with a new T when the save
    /// has nothing for this key (fresh game, or the mod was just installed).
    /// </summary>
    public static void Register<T>(NeetMod owner, Func<T> save, Action<T> load, string key = null) where T : class, new()
    {
        key ??= owner.Info.Id;
        Slots[key] = new Slot
        {
            Save = () => JToken.FromObject(save()),
            Load = token => load(token.ToObject<T>() ?? new T()),
            Reset = () => load(new T()),
        };
        owner.Track(Disposable.Create(() => Slots.Remove(key)));
    }

    internal static void Init(ModLogger log) => _log = log;

    /// <summary>Appends the mod entry to the save JSON (never first: Save[0] carries game data).</summary>
    internal static void Write(JObject save)
    {
        if (Slots.Count == 0 || !(save?["Save"] is JArray entries))
            return;

        var mods = new JObject();
        foreach (var pair in Slots)
        {
            try { mods[pair.Key] = pair.Value.Save(); }
            catch (Exception e) { _log.Error($"Saving data for '{pair.Key}' failed; the game still saves.", e); }
        }
        entries.Add(new JObject { ["type"] = EntryType, ["mods"] = mods });
    }

    /// <summary>Reads the mod entry from the file LoadAll is about to load.</summary>
    internal static void BeforeLoad()
    {
        _pending = null;
        try
        {
            var save = SaveFiles.ReadNewest();
            if (save?["Save"] is JArray entries)
            {
                foreach (var entry in entries)
                {
                    if (entry.Value<string>("type") == EntryType)
                        _pending = entry["mods"] as JObject;
                }
            }
        }
        catch (Exception e)
        {
            _log.Error("Couldn't read mod data from the save", e);
        }
    }

    /// <summary>Hands each registered mod its data (or a fresh object).</summary>
    internal static void AfterLoad()
    {
        foreach (var pair in Slots)
        {
            try
            {
                var token = _pending?[pair.Key];
                if (token != null && token.Type != JTokenType.Null)
                    pair.Value.Load(token);
                else
                    pair.Value.Reset();
            }
            catch (Exception e)
            {
                _log.Error($"Loading data for '{pair.Key}' failed", e);
            }
        }
        _pending = null;
    }
}
