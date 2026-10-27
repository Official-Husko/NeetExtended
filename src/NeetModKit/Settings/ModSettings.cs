using System;
using System.Reflection;
using NeetLoader;
using NeetLoader.Config;
using NeetModKit.Registry;
using NeetModKit.Sharing;

namespace NeetModKit.Settings;

/// <summary>A config object shown on the Settings page.</summary>
public sealed class SettingsSection
{
    public string Title { get; }
    public Func<object> Value { get; }
    public Action Save { get; }

    /// <summary>The mod it belongs to, or null.</summary>
    public string OwnerId { get; }

    /// <summary>Tells the mod its values changed without saving them (another game decided them).</summary>
    public Action Notify { get; }

    public SettingsSection(string title, Func<object> value, Action save, string ownerId = null, Action notify = null)
    {
        Title = title;
        Value = value;
        Save = save;
        OwnerId = ownerId;
        Notify = notify;
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

    public static void Register<T>(NeetMod owner, ConfigFile<T> config, string title = null) where T : class, new()
    {
        SettingsSection section = null;
        Action save = config.Save;
        section = new SettingsSection(title ?? owner.Info.Manifest.DisplayName, () => config.Value,
            () => SharedSettings.Save(section, save), owner.Info.Id, Raiser(config));
        SectionList.Add(owner, section);
    }

    // ConfigFile only raises Changed when it saves or reloads; values another game decided must not be
    // saved, so its event is raised directly.
    private static Action Raiser<T>(ConfigFile<T> config) where T : class, new()
    {
        var changed = typeof(ConfigFile<T>).GetField(nameof(ConfigFile<T>.Changed), BindingFlags.Instance | BindingFlags.NonPublic);
        return () => (changed?.GetValue(config) as Action<T>)?.Invoke(config.Value);
    }
}
