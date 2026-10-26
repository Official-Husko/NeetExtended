using System;
using System.Linq;
using System.Reflection;
using DevBridge.Server;
using NeetModKit.Settings;
using NeetModKit.Values;
using Newtonsoft.Json.Linq;

namespace DevBridge.Commands;

/// <summary>The settings mods show on the mod menu's Settings page, read and changed like the page does.</summary>
internal static class SettingsCommands
{
    [BridgeCommand("settings.list", "Every settings section on the mod menu's Settings page, with its values.")]
    private static object List(JObject p) =>
        new JArray(ModSettings.Sections.Select(section => new JObject
        {
            ["section"] = section.Title,
            ["values"] = JsonValues.ToJson(section.Value(), 2),
        }).ToArray());

    [BridgeCommand("settings.set", "Change one setting and save it, which applies it like the Settings page does.",
        Params = new[] { "section: title, e.g. Performance Fixes", "name: field name", "value" })]
    private static object Set(JObject p)
    {
        string title = p.Str("section"), name = p.Str("name");
        var section = ModSettings.Sections.FirstOrDefault(s => string.Equals(s.Title, title, StringComparison.OrdinalIgnoreCase))
            ?? throw new ArgumentException($"No settings section '{title}'. Known: {string.Join(", ", ModSettings.Sections.Select(s => s.Title))}");
        object config = section.Value();
        var field = config.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public)
            .FirstOrDefault(f => string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase))
            ?? throw new ArgumentException($"No setting '{name}' in {section.Title}.");
        field.SetValue(config, JsonValues.FromJson(p["value"], field.FieldType));
        section.Save();
        return JsonValues.ToJson(field.GetValue(config));
    }
}
