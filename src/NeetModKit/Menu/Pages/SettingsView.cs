using System.Collections.Generic;
using NeetModKit.Settings;
using NeetModKit.Sharing;
using NeetModKit.Ui.Toolkit;
using UnityEngine;

namespace NeetModKit.Menu.Pages;

/// <summary>A mod's settings sections as controls. Edits are saved half a second after the last change.</summary>
internal sealed class SettingsView
{
    private const float SaveDelay = 0.5f;

    private SettingsSection _dirty;
    private float _saveAt;

    public bool IsDirty => _dirty != null;

    public void Build(UiBuilder ui, IReadOnlyList<SettingsSection> sections)
    {
        var list = ui.Scroll(out _);
        foreach (var section in sections)
        {
            var target = section;
            var panel = list.Panel();
            if (sections.Count > 1)
                panel.Header(section.Title.ToUpperInvariant());
            ConfigEditor.Build(panel, section.Value(), () => MarkDirty(target), field => LockedBecause(target, field));
        }
    }

    public void Refresh()
    {
        if (_dirty != null && Time.unscaledTime >= _saveAt)
            Flush();
    }

    public void Flush()
    {
        _dirty?.Save();
        _dirty = null;
    }

    private void MarkDirty(SettingsSection section)
    {
        if (_dirty != null && _dirty != section)
            _dirty.Save();
        _dirty = section;
        _saveAt = Time.unscaledTime + SaveDelay;
    }

    private static string LockedBecause(SettingsSection section, System.Reflection.FieldInfo field) =>
        section.OwnerId != null && SharedSettings.IsShared(field) ? ModControl.LockedBecause(section.OwnerId) : null;
}
