using System;
using System.Collections.Generic;
using System.Reflection;
using NeetModKit.Settings;
using Newtonsoft.Json.Linq;

namespace NeetModKit.Sharing;

/// <summary>
/// The settings marked [Shared], which shape the shared house. The deciding game (a co-op host)
/// <see cref="Capture"/>s them; a game that follows it <see cref="Apply"/>s them for the session,
/// without saving, and <see cref="Restore"/>s the player's own values afterwards. While another game's
/// values are in, saving a section (for one of its other settings) still writes the player's own.
/// </summary>
public static class SharedSettings
{
    private static readonly Dictionary<Type, List<FieldInfo>> Fields = new Dictionary<Type, List<FieldInfo>>();
    private static readonly Dictionary<SettingsSection, Dictionary<FieldInfo, object>> Originals =
        new Dictionary<SettingsSection, Dictionary<FieldInfo, object>>();

    /// <summary>After a section with shared settings was saved here.</summary>
    public static event Action Saved;

    public static bool IsShared(FieldInfo field) => field.IsDefined(typeof(SharedAttribute), false);

    /// <summary>Whether this field's value comes from another game right now.</summary>
    public static bool IsDecidedElsewhere(SettingsSection section, FieldInfo field) =>
        Originals.TryGetValue(section, out var fields) && fields.ContainsKey(field);

    /// <summary>Every shared setting of every section: { "Section title": { "Field": value } }.</summary>
    public static JObject Capture()
    {
        var all = new JObject();
        foreach (var section in ModSettings.Sections)
        {
            var settings = section.Value();
            var shared = SharedFields(settings.GetType());
            if (shared.Count == 0)
                continue;
            var values = new JObject();
            foreach (var field in shared)
                values[field.Name] = JToken.FromObject(field.GetValue(settings));
            all[section.Title] = values;
        }
        return all;
    }

    /// <summary>Takes another game's values for the sections of mods it decides (<see cref="ModControl"/>).</summary>
    public static void Apply(JObject values)
    {
        if (values == null)
            return;
        bool any = false;
        foreach (var section in ModSettings.Sections)
        {
            if (!(values[section.Title] is JObject decided) || section.OwnerId == null || ModControl.LockedBecause(section.OwnerId) == null)
                continue;
            var settings = section.Value();
            bool changed = false;
            foreach (var field in SharedFields(settings.GetType()))
            {
                var token = decided[field.Name];
                if (token == null)
                    continue;
                object value;
                try { value = token.ToObject(field.FieldType); }
                catch (Exception) { continue; }
                if (Equals(field.GetValue(settings), value))
                    continue;
                if (!Originals.TryGetValue(section, out var originals))
                    Originals[section] = originals = new Dictionary<FieldInfo, object>();
                if (!originals.ContainsKey(field))
                    originals[field] = field.GetValue(settings);
                field.SetValue(settings, value);
                changed = true;
            }
            if (changed)
            {
                section.Notify?.Invoke();
                any = true;
            }
        }
        if (any)
            ModControl.Touch();
    }

    /// <summary>Puts the player's own values back.</summary>
    public static void Restore()
    {
        if (Originals.Count == 0)
            return;
        foreach (var pair in Originals)
        {
            var settings = pair.Key.Value();
            foreach (var original in pair.Value)
                original.Key.SetValue(settings, original.Value);
            pair.Key.Notify?.Invoke();
        }
        Originals.Clear();
        ModControl.Touch();
    }

    internal static void Save(SettingsSection section, Action save)
    {
        if (Originals.TryGetValue(section, out var originals) && originals.Count > 0)
        {
            var settings = section.Value();
            var decided = new Dictionary<FieldInfo, object>();
            foreach (var original in originals)
            {
                decided[original.Key] = original.Key.GetValue(settings);
                original.Key.SetValue(settings, original.Value);
            }
            save();
            foreach (var value in decided)
                value.Key.SetValue(settings, value.Value);
            section.Notify?.Invoke();
        }
        else
        {
            save();
        }
        if (SharedFields(section.Value().GetType()).Count > 0)
            Saved?.Invoke();
    }

    private static List<FieldInfo> SharedFields(Type type)
    {
        if (Fields.TryGetValue(type, out var list))
            return list;
        list = new List<FieldInfo>();
        foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public))
        {
            if (IsShared(field))
                list.Add(field);
        }
        Fields[type] = list;
        return list;
    }
}
