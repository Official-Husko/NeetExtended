using NeetModKit.Settings;
using UnityEngine;

namespace NeetModKit.Menu.Pages;

/// <summary>Every registered config, with changes saved half a second after the last edit.</summary>
internal sealed class SettingsPage : MenuPage
{
    private const float SaveDelay = 0.5f;

    private SettingsSection _dirty;
    private float _saveAt;

    public override string Title => "Settings";
    public override int Order => 30;

    public override void Draw()
    {
        foreach (var section in ModSettings.Sections)
        {
            Widgets.Section(section.Title, () =>
            {
                if (ConfigEditor.Draw(section.Value()))
                    MarkDirty(section);
            });
        }

        if (ModSettings.Sections.Count == 0)
            Widgets.Text("No mod has settings to show.");

        FlushIfDue();
    }

    public override void OnHide()
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

    private void FlushIfDue()
    {
        if (_dirty == null || Time.unscaledTime < _saveAt)
            return;
        _dirty.Save();
        _dirty = null;
    }
}
