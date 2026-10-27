using UnityEngine;

namespace WeatherExpansion.Windows;

/// <summary>
/// A pane of one of the house's windows: where the glass is, how big it is, which way is into the
/// house, and whether it's still whole. Each window is two invisible paintable meshes
/// (<c>..._Pane_1</c> outside, <c>..._Pane_2</c> inside) in a wall with a tinted
/// <c>WindowHighlight</c> quad between them; breaking one is what the game calls a broken window.
/// </summary>
internal sealed class WindowPane
{
    public readonly Transform Window;
    public readonly Transform Inner;
    public readonly Transform Outer;
    public readonly Vector3 Centre;
    public readonly Vector3 Inwards;
    public readonly Vector2 Size;
    private readonly BreakOnImpact _innerBreak;
    private readonly BreakOnImpact _outerBreak;

    public WindowPane(Transform window, Transform pane1, Transform pane2, InteriorRooms rooms)
    {
        Window = window;
        var b1 = pane1.GetComponent<Renderer>().bounds;
        var b2 = pane2.GetComponent<Renderer>().bounds;
        Vector3 across = b2.center - b1.center;
        across.y = 0f;
        across = across.sqrMagnitude > 1e-6f ? across.normalized : -window.forward;

        // Panes are named outer and inner but not for every window, so ask the rooms which side is inside.
        Vector3 mid = (b1.center + b2.center) * 0.5f;
        bool towards2 = rooms.Contains(mid + across * 0.5f);
        bool towards1 = rooms.Contains(mid - across * 0.5f);
        if (towards1 && !towards2)
            across = -across;
        else if (!towards1 && !towards2)
            across = pane2.name.EndsWith("_Pane_2") ? across : -across;
        Inwards = across;
        bool secondIsInner = Vector3.Dot(b2.center - b1.center, across) > 0f;
        Inner = secondIsInner ? pane2 : pane1;
        Outer = secondIsInner ? pane1 : pane2;
        var innerBounds = secondIsInner ? b2 : b1;
        Centre = innerBounds.center;
        Size = new Vector2(Mathf.Max(innerBounds.size.x, innerBounds.size.z), innerBounds.size.y);
        _innerBreak = Inner.GetComponent<BreakOnImpact>();
        _outerBreak = Outer.GetComponent<BreakOnImpact>();
    }

    /// <summary>False once either pane broke or went away.</summary>
    public bool Whole =>
        Window != null && Inner != null && Outer != null
        && Inner.gameObject.activeInHierarchy && Outer.gameObject.activeInHierarchy
        && !(_innerBreak != null && _innerBreak.broken)
        && !(_outerBreak != null && _outerBreak.broken);
}
