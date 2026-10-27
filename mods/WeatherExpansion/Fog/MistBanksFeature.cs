using System.Collections.Generic;
using NeetModKit.Events;
using NeetModKit.Features;
using UnityEngine;
using UnityEngine.SceneManagement;
using WeatherExpansion.Storm;

namespace WeatherExpansion.Fog;

/// <summary>
/// Banks of mist drifting over the front and the back yard, lit blue-grey and brighter in a lightning
/// flash. They sit where the game's own yard rain systems are (<c>Particle_Rain_Frontyard</c> and
/// <c>Particle_Rain_Backyard</c> hang over the middle of each yard).
/// </summary>
internal sealed class MistBanksFeature : ModFeature
{
    public const string FeatureId = "MistBanks";
    private const string ShaderName = "Legacy Shaders/Particles/Alpha Blended";
    private static readonly string[] YardMarkers = { "Particle_Rain_Frontyard", "Particle_Rain_Backyard" };

    // Particle colour at night and in a flash. The shader doubles its tint, so these look half as bright.
    private static readonly Color NightMist = new Color(0.10f, 0.13f, 0.20f);
    private static readonly Color FlashMist = new Color(0.32f, 0.38f, 0.52f);
    private const float BaseAlpha = 0.16f;

    private readonly List<MistSystem> _banks = new List<MistSystem>();
    private Material _material;
    private Texture2D _texture;
    private Light _moon;

    public override string Id => FeatureId;
    public override string Name => "Mist in the yards";
    public override string Description =>
        "Experimental. Banks of mist drift over the front and back yard and catch the lightning. " +
        "How much is in Settings.";

    protected override void OnEnable()
    {
        GameEvents.WhenHouseReady(this, Build);
        WeatherExpansionMod.Config.Changed += OnSettingsChanged;
        SceneManager.sceneUnloaded += OnSceneUnloaded;
    }

    protected override void OnDisable()
    {
        WeatherExpansionMod.Config.Changed -= OnSettingsChanged;
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
        Clear();
    }

    public override void OnUpdate()
    {
        if (_material == null)
            return;
        float flash = FlashLevel.Of(_moon);
        var colour = Color.Lerp(NightMist, FlashMist, flash);
        colour.a = BaseAlpha * Mathf.Min(Amount(), 1.5f);
        _material.SetColor("_TintColor", colour);
    }

    private static float Amount() => WeatherExpansionMod.Config.Value.Mist * StormInputs.Wetness;

    private void OnSettingsChanged(WeatherExpansionConfig config)
    {
        foreach (var bank in _banks)
            bank.SetAmount(Amount());
    }

    private void OnSceneUnloaded(Scene scene) => Clear();

    private void Build()
    {
        Clear();
        var shader = Shader.Find(ShaderName);
        if (shader == null)
        {
            WeatherExpansionMod.Instance.Log.Warn("Mist: the game's particle shader wasn't found.");
            return;
        }
        _texture = SoftTexture.Create();
        _material = new Material(shader) { name = "WeatherExpansion mist", mainTexture = _texture, hideFlags = HideFlags.HideAndDontSave };
        _moon = RenderSettings.sun;
        foreach (var marker in YardMarkers)
        {
            var found = GameObject.Find(marker);
            if (found == null)
                continue;
            var at = found.transform.position;
            // Puffs are 4 to 7 m wide, so their middle sits about 2.5 m up to keep them off the ground.
            var bank = new MistSystem("WeatherExpansion_Mist", new Vector3(at.x, 2.5f, at.z), _material);
            bank.SetAmount(Amount());
            _banks.Add(bank);
        }
    }

    private void Clear()
    {
        foreach (var bank in _banks)
            bank.Destroy();
        _banks.Clear();
        if (_material != null)
            Object.Destroy(_material);
        _material = null;
        if (_texture != null)
            Object.Destroy(_texture);
        _texture = null;
        _moon = null;
    }
}
