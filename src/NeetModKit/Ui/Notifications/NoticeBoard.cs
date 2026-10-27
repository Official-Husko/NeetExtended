using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace NeetModKit.Ui.Notifications;

/// <summary>
/// The corner the notices stack in: newest at the bottom, each fading in, and out once closed.
/// Only runs while there's something to show.
/// </summary>
internal sealed class NoticeBoard : MonoBehaviour
{
    private const float Width = 400f;
    private const float Margin = 18f;
    private const float Gap = 8f;
    private const float FadeIn = 0.2f;
    private const float FadeOut = 0.4f;

    private readonly List<NoticeView> _views = new List<NoticeView>();
    private RectTransform _stack;

    public void Add(Notice notice)
    {
        if (_stack == null)
            BuildStack();
        _views.Add(new NoticeView(notice, _stack, Width));
        enabled = true;
    }

    private void BuildStack()
    {
        var go = new GameObject("Stack", typeof(RectTransform));
        _stack = (RectTransform)go.transform;
        _stack.SetParent(transform, false);
        _stack.anchorMin = _stack.anchorMax = _stack.pivot = new Vector2(1f, 0f);
        _stack.anchoredPosition = new Vector2(-Margin, Margin);
        _stack.sizeDelta = new Vector2(Width, 0f);

        var layout = go.AddComponent<VerticalLayoutGroup>();
        layout.spacing = Gap;
        layout.childAlignment = TextAnchor.LowerRight;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        go.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    private void Update()
    {
        float now = Time.unscaledTime;
        for (int i = _views.Count - 1; i >= 0; i--)
        {
            var view = _views[i];
            var notice = view.Notice;
            if (notice.Changed)
            {
                notice.Changed = false;
                view.Refresh();
            }

            float alpha = Mathf.Clamp01((now - view.ShownAt) / FadeIn);
            if (notice.CloseAt >= 0f && now >= notice.CloseAt)
                alpha = Mathf.Min(alpha, 1f - (now - notice.CloseAt) / FadeOut);
            if (alpha <= 0f && notice.CloseAt >= 0f && now >= notice.CloseAt)
            {
                view.Destroy();
                _views.RemoveAt(i);
                continue;
            }
            view.Alpha = alpha;
        }
        if (_views.Count == 0)
            enabled = false;
    }

    private void OnDestroy()
    {
        _views.Clear();
    }
}
