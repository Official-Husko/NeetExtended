using System;
using System.Collections.Generic;
using System.Linq;
using Coop.Net;
using LiteNetLib.Utils;
using NeetLoader.Mods;
using NeetLoader.Runtime;
using NeetModKit.Features;
using NeetModKit.Sharing;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Coop.Session;

/// <summary>
/// In co-op the host decides the mods that shape the shared house (<see cref="ModList"/>): which run,
/// their features and their [Shared] settings. The host sends that setup when a player is ready and
/// half a second after it changes; a client follows it, with the mod menu locked for those mods, and
/// gets its own setup back when it leaves: its feature choices and settings were never overwritten,
/// and mods it loaded or unloaded for the host are put back.
///
/// ModSetup: a kind byte. Request (client to host): send me the setup. Setup (host to clients): JSON
/// { "mods": ["Id@version"], "features": { "mod/feature": on }, "settings": { "Section": { "Field": value } } }.
/// </summary>
internal static class ModSync
{
    private const byte Request = 0;
    private const byte Setup = 1;
    private const float SendDelay = 0.5f;
    // A mod that hot reloads to a new version reloads in every game, a moment apart.
    private const float CatchUpSeconds = 10f;
    private const string Reason = "The host decides this in co-op.";
    private const string LoadedKey = "Coop.ModSync.Loaded";
    private const string UnloadedKey = "Coop.ModSync.Unloaded";

    private static CoopMod _mod;
    private static IDisposable _lock;
    private static float _sendAt = -1f;
    private static JArray _waitingFor;
    private static float _giveUpAt, _retryAt;
    private static List<string> _loadedHere = new List<string>();
    private static List<string> _unloadedHere = new List<string>();

    public static void Init(CoopMod mod)
    {
        _mod = mod;
        _lock = null;
        _sendAt = -1f;
        _waitingFor = null;
        _loadedHere = ReloadState.Take(LoadedKey, out List<string> loaded) ? loaded : new List<string>();
        _unloadedHere = ReloadState.Take(UnloadedKey, out List<string> unloaded) ? unloaded : new List<string>();

        Messages.On(MessageIds.ModSetup, OnMessage);
        CoopLoop.Update += Tick;
        CoopLoop.Unloading += Park;
        CoopSession.PlayerReady += player => SendTo(player.Slot);
        CoopSession.WorldReady += AskHost;
        CoopSession.Resumed += () =>
        {
            AskHost();
            Changed();
        };
        CoopSession.Ended += _ => Release();

        // The loader's and the framework's events outlive this copy of the mod.
        Action<ModInfo> modChanged = _ => Changed();
        Action<ModFeature> featureChanged = _ => Changed();
        Action settingsSaved = Changed;
        ModRegistry.ModLoaded += modChanged;
        ModRegistry.ModUnloading += modChanged;
        ModFeatures.Changed += featureChanged;
        SharedSettings.Saved += settingsSaved;
        mod.Track(Disposable.Create(() =>
        {
            ModRegistry.ModLoaded -= modChanged;
            ModRegistry.ModUnloading -= modChanged;
            ModFeatures.Changed -= featureChanged;
            SharedSettings.Saved -= settingsSaved;
        }));

        // Not in a session after a reload: nothing should still follow a host.
        if (!CoopSession.IsClient)
            Release();
    }

    private static void Tick()
    {
        if (CoopSession.IsClient)
        {
            _lock ??= ModControl.HandOver(_mod, Reason, ModList.IsShared);
        }
        else if (_lock != null)
        {
            Release();
        }

        if (_waitingFor != null && CoopSession.IsClient && Time.unscaledTime >= _retryAt)
            Retry();

        if (_sendAt >= 0f && Time.unscaledTime >= _sendAt)
        {
            _sendAt = -1f;
            if (CoopSession.IsHost)
            {
                var message = Begin(Setup);
                message.Put(State().ToString(Formatting.None));
                Outbox.ToPlayers(message, Channel.Events);
            }
        }
    }

    // Host: something in the setup changed; tell everyone soon (a slider sends many changes).
    private static void Changed()
    {
        if (CoopSession.IsHost)
            _sendAt = Time.unscaledTime + SendDelay;
    }

    private static void SendTo(byte slot)
    {
        if (!CoopSession.IsHost)
            return;
        var message = Begin(Setup);
        message.Put(State().ToString(Formatting.None));
        Outbox.ToSlot(slot, message, Channel.Events);
    }

    private static void AskHost()
    {
        if (CoopSession.IsClient)
            Outbox.ToHost(Begin(Request), Channel.Events);
    }

    private static NetDataWriter Begin(byte kind)
    {
        var message = Outbox.Begin(MessageIds.ModSetup);
        message.Put(kind);
        return message;
    }

    private static JObject State()
    {
        var features = new JObject();
        foreach (var feature in ModFeatures.Features)
        {
            if (ModList.IsShared(feature.Owner.Info.Id))
                features[ModFeatures.KeyOf(feature)] = feature.Enabled;
        }
        return new JObject
        {
            ["mods"] = new JArray(ModList.Shared()),
            ["features"] = features,
            ["settings"] = SharedSettings.Capture(),
        };
    }

    private static void OnMessage(in Sender from, NetDataReader reader)
    {
        byte kind = reader.GetByte();
        if (kind == Request)
        {
            SendTo(from.Slot);
            return;
        }
        if (kind != Setup || !CoopSession.IsClient)
            return;
        JObject state;
        try
        {
            state = JObject.Parse(reader.GetString());
        }
        catch (JsonException e)
        {
            CoopMod.Log.Warn($"The host's mod setup couldn't be read: {e.Message}");
            return;
        }
        _lock ??= ModControl.HandOver(_mod, Reason, ModList.IsShared);
        _waitingFor = null;
        string missing = FollowMods(state["mods"] as JArray);
        if (missing != null)
        {
            // Probably a mod that's hot reloading to the host's version here too: give it a moment.
            _waitingFor = state["mods"] as JArray;
            _giveUpAt = Time.unscaledTime + CatchUpSeconds;
            _retryAt = Time.unscaledTime + 1f;
            CoopMod.Log.Info($"Waiting for the mods to match the host's: {missing}");
        }
        if (state["features"] is JObject features)
        {
            foreach (var pair in features)
            {
                int slash = pair.Key.IndexOf('/');
                if (slash > 0 && ModList.IsShared(pair.Key.Substring(0, slash)))
                    ModFeatures.Override(pair.Key, pair.Value.Value<bool>());
            }
        }
        SharedSettings.Apply(state["settings"] as JObject);
    }

    private static void Retry()
    {
        _retryAt = Time.unscaledTime + 1f;
        string missing = FollowMods(_waitingFor);
        if (missing == null)
            _waitingFor = null;
        else if (Time.unscaledTime >= _giveUpAt)
        {
            _waitingFor = null;
            CoopSession.Leave(missing);
        }
    }

    // Loads and unloads shared mods until this game runs what the host runs. Returns why that can't be
    // done (a mod this game doesn't have, or another version of it), or null.
    private static string FollowMods(JArray hostMods)
    {
        if (hostMods == null)
            return null;
        var host = new HashSet<string>(hostMods.Select(t => t.Value<string>()));
        var mine = new HashSet<string>(ModList.Shared());
        foreach (string entry in host)
        {
            if (mine.Contains(entry))
                continue;
            Split(entry, out string id, out string version);
            var mod = ModRegistry.Find(id);
            if (mod == null || mod.Manifest.Version != version)
            {
                string have = mod == null ? "which you don't have" : $"and you have {mod.Manifest.Version}";
                return $"The host turned on {(mod?.Manifest.DisplayName ?? id)} {version}, {have}.";
            }
            if (mod.State == ModState.Loaded)
                continue;
            ModRegistry.Load(id, error => Done(id, error, "load", _loadedHere, _unloadedHere));
        }
        foreach (string entry in mine)
        {
            if (host.Contains(entry))
                continue;
            Split(entry, out string id, out _);
            if (id == CoopMod.Instance?.Info.Id)
                continue;
            // Another version of a mod the host also runs is being caught up with above, not unloaded.
            if (host.Any(h => h.StartsWith(id + "@", StringComparison.Ordinal)))
                continue;
            ModRegistry.Unload(id, error => Done(id, error, "unload", _unloadedHere, _loadedHere));
        }
        return null;
    }

    // Remembers what this game did for the host, so leaving can undo it.
    private static void Done(string id, string error, string what, List<string> did, List<string> undid)
    {
        if (error != null)
        {
            CoopMod.Log.Warn($"Couldn't {what} {id} like the host: {error}");
            return;
        }
        if (!undid.Remove(id) && !did.Contains(id))
            did.Add(id);
        ModControl.Touch();
    }

    private static void Split(string entry, out string id, out string version)
    {
        int at = entry.LastIndexOf('@');
        id = at > 0 ? entry.Substring(0, at) : entry;
        version = at > 0 ? entry.Substring(at + 1) : "";
    }

    // Back to this game's own setup.
    private static void Release()
    {
        _lock?.Dispose();
        _lock = null;
        ModFeatures.ClearOverrides();
        SharedSettings.Restore();
        foreach (string id in _loadedHere)
            ModRegistry.Unload(id);
        foreach (string id in _unloadedHere)
            ModRegistry.Load(id);
        _loadedHere.Clear();
        _unloadedHere.Clear();
    }

    // A hot reload of Co-op mid-session: the overrides live in the framework; what was loaded or
    // unloaded for the host is carried to the next copy.
    private static void Park()
    {
        _lock = null;
        ReloadState.Put(LoadedKey, new List<string>(_loadedHere));
        ReloadState.Put(UnloadedKey, new List<string>(_unloadedHere));
    }
}
