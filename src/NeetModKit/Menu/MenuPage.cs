namespace NeetModKit.Menu;

/// <summary>
/// One tab in the mod menu. Draw with GUILayout (the <see cref="Widgets"/> helpers cover most
/// needs). Pages are drawn only while the menu is open and the tab is selected.
/// </summary>
public abstract class MenuPage
{
    public abstract string Title { get; }

    /// <summary>Lower comes first. Built-in pages use 0-100.</summary>
    public virtual int Order => 500;

    /// <summary>
    /// True if the page lays itself out to the full height (with its own scroll areas).
    /// Otherwise the window wraps it in one scroll view.
    /// </summary>
    public virtual bool FillsHeight => false;

    /// <summary>Called when the tab becomes visible.</summary>
    public virtual void OnShow() { }

    /// <summary>Called when the tab is left or the menu closes.</summary>
    public virtual void OnHide() { }

    public abstract void Draw();
}
