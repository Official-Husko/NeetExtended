using NeetModKit.Settings;
using NeetModKit.Sharing;
using NeetModKit.Ui.Toolkit;
using UnityEngine;

namespace NeetModKit.Menu.Pages;

/// <summary>Every registered config. Edits are saved half a second after the last change.</summary>
internal sealed class SettingsPage : MenuPage
{
    private const float SaveDelay = 0.5f;

    private SettingsSection _dirty;
    private float _saveAt;
    private int _builtFor = -1;

    public override string Title => "Settings";
    public override int Order => 30;

    public override void Build(UiBuilder ui)
    {
        _builtFor = ModControl.Version;
        var list = ui.Scroll(out _);
        if (ModSettings.Sections.Count == 0)
            list.Text("No mod has settings to show.");

        foreach (var section in ModSettings.Sections)
        {
            var target = section;
            var panel = list.Panel();
            panel.Header(section.Title.ToUpperInvariant());
            ConfigEditor.Build(panel, section.Value(), () => MarkDirty(target), field => LockedBecause(target, field));
        }
    }

    public override void Refresh()
    {
        if (_dirty != null && Time.unscaledTime >= _saveAt)
            Flush();
        // Another game decided something (co-op host): show it.
        if (_dirty == null && _builtFor != ModControl.Version)
        {
            _builtFor = ModControl.Version;
            Rebuild();
        }
    }

    private static string LockedBecause(SettingsSection section, System.Reflection.FieldInfo field) =>
        section.OwnerId != null && SharedSettings.IsShared(field) ? ModControl.LockedBecause(section.OwnerId) : null;

    public override void OnHide() => Flush();

    private void MarkDirty(SettingsSection section)
    {
        if (_dirty != null && _dirty != section)
            _dirty.Save();
        _dirty = section;
        _saveAt = Time.unscaledTime + SaveDelay;
    }

    private void Flush()
    {
        _dirty?.Save();
        _dirty = null;
    }
}
