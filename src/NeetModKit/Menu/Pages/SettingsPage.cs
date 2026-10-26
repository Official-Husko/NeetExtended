using NeetModKit.Settings;
using NeetModKit.Ui.Toolkit;
using UnityEngine;

namespace NeetModKit.Menu.Pages;

/// <summary>Every registered config. Edits are saved half a second after the last change.</summary>
internal sealed class SettingsPage : MenuPage
{
    private const float SaveDelay = 0.5f;

    private SettingsSection _dirty;
    private float _saveAt;

    public override string Title => "Settings";
    public override int Order => 30;

    public override void Build(UiBuilder ui)
    {
        var list = ui.Scroll(out _);
        if (ModSettings.Sections.Count == 0)
            list.Text("No mod has settings to show.");

        foreach (var section in ModSettings.Sections)
        {
            var target = section;
            var panel = list.Panel();
            panel.Header(section.Title.ToUpperInvariant());
            ConfigEditor.Build(panel, section.Value(), () => MarkDirty(target));
        }
    }

    public override void Refresh()
    {
        if (_dirty != null && Time.unscaledTime >= _saveAt)
            Flush();
    }

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
