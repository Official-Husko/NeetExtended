namespace NeetModKit.Ui.Toolkit;

/// <summary>
/// One tab of a <see cref="UiTabbedWindow"/>. <see cref="Build"/> lays the page out when it is
/// shown; <see cref="Refresh"/> runs a few times a second while it's visible to update values.
/// Call <see cref="Rebuild"/> when the layout itself has to change.
/// </summary>
public abstract class UiPage
{
    public abstract string Title { get; }

    /// <summary>Lower comes first.</summary>
    public virtual int Order => 500;

    public abstract void Build(UiBuilder ui);

    public virtual void Refresh() { }

    /// <summary>Called when the tab is left or the window closes.</summary>
    public virtual void OnHide() { }

    /// <summary>Asks the window to build this page again on the next frame.</summary>
    public void Rebuild() => RebuildRequested = true;

    internal bool RebuildRequested { get; set; }
}
