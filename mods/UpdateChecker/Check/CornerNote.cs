using System;
using NeetModKit.Ui;

namespace UpdateChecker.Check;

/// <summary>Puts "update available (v0.3.0)" after the version in the corner while an update is out.</summary>
internal static class CornerNote
{
    private static IDisposable _note;
    private static string _shown;

    public static void Init(UpdateCheckerMod mod)
    {
        UpdateState.Subscribe(mod, () => Update(mod));
        mod.Track(NeetLoader.Runtime.Disposable.Create(() => { _note?.Dispose(); _note = null; _shown = null; }));
        Update(mod);
    }

    private static void Update(UpdateCheckerMod mod)
    {
        string wanted = UpdateState.Available ? $"update available (v{UpdateState.Latest})" : null;
        if (wanted == _shown)
            return;
        _note?.Dispose();
        _note = wanted == null ? null : VersionTag.AddNote(mod, wanted);
        _shown = wanted;
    }
}
