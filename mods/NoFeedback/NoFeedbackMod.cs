using NeetLoader;
using NeetModKit.Events;
using NeetModKit.Ui;

namespace NoFeedback;

/// <summary>Blocks sending (patch) and hides the feedback form in the pause menu.</summary>
public sealed class NoFeedbackMod : NeetMod
{
    private static readonly string[] FormParts = { "ReportType", "CommentInput", "DataDisclaimer" };

    internal static NoFeedbackMod Instance { get; private set; }

    public override void OnLoad()
    {
        Instance = this;
        GameEvents.SceneLoaded.Subscribe(this, _ => HideForm());
        HideForm();
    }

    public override void OnUnload() => Instance = null;

    private void HideForm()
    {
        var canvas = Feedback_Sender._ != null ? Feedback_Sender._.transform.Find("Canvas_Settings") : null;
        if (canvas == null)
            return;
        foreach (string part in FormParts)
            UiCloning.Hide(this, canvas.Find(part)?.gameObject);
    }
}
