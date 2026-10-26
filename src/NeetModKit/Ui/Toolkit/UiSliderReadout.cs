using UnityEngine;
using UnityEngine.UI;

namespace NeetModKit.Ui.Toolkit;

/// <summary>
/// Keeps a slider's value label in step with the slider, also when code changes the value with
/// SetValueWithoutNotify (which skips the slider's listeners).
/// </summary>
internal sealed class UiSliderReadout : MonoBehaviour
{
    public Slider Slider;
    public Text Label;

    private float _shown = float.NaN;

    private void LateUpdate()
    {
        if (Slider == null || Label == null || Slider.value == _shown)
            return;
        _shown = Slider.value;
        Label.text = Slider.wholeNumbers ? ((int)_shown).ToString() : _shown.ToString("0.##");
    }
}
