using System;
using NeetLoader;
using NeetLoader.Config;
using NeetModKit.Registry;

namespace NeetModKit.Settings;

/// <summary>A config object shown on the Settings page.</summary>
public sealed class SettingsSection
{
    public string Title { get; }
    public Func<object> Value { get; }
    public Action Save { get; }

    public SettingsSection(string title, Func<object> value, Action save)
    {
        Title = title;
        Value = value;
        Save = save;
    }
}

/// <summary>
/// Lets a mod show its config file on the mod menu's Settings page. Public fields become
/// controls: bool -> toggle, numbers -> slider (with [Range]) or text box, enum -> buttons,
/// string -> text box. [Label] overrides the label, [Description] adds help text and
/// [Hidden] keeps a field off the page.
/// </summary>
public static class ModSettings
{
    private static readonly OwnedList<SettingsSection> SectionList = new OwnedList<SettingsSection>();

    public static System.Collections.Generic.IReadOnlyList<SettingsSection> Sections => SectionList.Items;

    public static void Register<T>(NeetMod owner, ConfigFile<T> config, string title = null) where T : class, new() =>
        SectionList.Add(owner, new SettingsSection(title ?? owner.Info.Manifest.DisplayName, () => config.Value, config.Save));
}
