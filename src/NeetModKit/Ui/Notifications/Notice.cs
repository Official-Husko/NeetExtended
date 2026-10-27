using System;
using UnityEngine;

namespace NeetModKit.Ui.Notifications;

/// <summary>
/// One message in the bottom right corner of the screen (<see cref="Notices.Show"/>): a title, a
/// line of detail and, if <see cref="Progress"/> is set, a bar. Change it while it's up; it shows
/// the new text on the next frame. <see cref="Close"/> (or <see cref="Dispose"/>) fades it out.
/// </summary>
public sealed class Notice : IDisposable
{
    private string _title;
    private string _detail;
    private float? _progress;

    internal Notice(string title, string detail, float? progress)
    {
        _title = title ?? "";
        _detail = detail ?? "";
        _progress = Clamp(progress);
        Changed = true;
    }

    public string Title
    {
        get => _title;
        set => Set(ref _title, value ?? "");
    }

    public string Detail
    {
        get => _detail;
        set => Set(ref _detail, value ?? "");
    }

    /// <summary>0 to 1 fills the bar; null hides it.</summary>
    public float? Progress
    {
        get => _progress;
        set
        {
            var clamped = Clamp(value);
            if (clamped.HasValue != _progress.HasValue || (clamped.HasValue && Mathf.Abs(clamped.Value - _progress.Value) > 0.001f))
            {
                _progress = clamped;
                Changed = true;
            }
        }
    }

    /// <summary>False once it's closed (it may still be fading out).</summary>
    public bool IsOpen => CloseAt < 0f || Time.unscaledTime < CloseAt;

    /// <summary>Unscaled time it starts fading out, or -1 while it stays.</summary>
    internal float CloseAt { get; private set; } = -1f;

    internal bool Changed { get; set; }

    /// <summary>Fades it out after <paramref name="seconds"/> (now with 0). Calling it again moves the time.</summary>
    public void Close(float seconds = 0f) => CloseAt = Time.unscaledTime + Mathf.Max(0f, seconds);

    /// <summary>Keeps it up after all (a <see cref="Close"/> with a delay that hasn't run out yet).</summary>
    public void KeepOpen()
    {
        if (IsOpen)
            CloseAt = -1f;
    }

    public void Dispose() => Close();

    private void Set(ref string field, string value)
    {
        if (field == value)
            return;
        field = value;
        Changed = true;
    }

    private static float? Clamp(float? value) => value.HasValue ? Mathf.Clamp01(value.Value) : (float?)null;
}
