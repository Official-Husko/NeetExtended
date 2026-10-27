using System.Collections.Generic;
using NeetModKit.Events;
using NeetModKit.Features;
using UnityEngine;
using UnityEngine.SceneManagement;
using WeatherExpansion.Storm;

namespace WeatherExpansion.Windows;

/// <summary>Drops sitting on the house's windows and running down them, seen from inside.</summary>
internal sealed class RainOnGlassFeature : ModFeature
{
    public const string FeatureId = "RainOnGlass";

    private readonly List<GameObject> _overlays = new List<GameObject>();
    private readonly List<Renderer> _renderers = new List<Renderer>();
    private readonly List<WindowPane> _panes = new List<WindowPane>();
    private DropField _field;
    private Material _material;
    private Mesh _mesh;
    private float _nextCheck;

    public override string Id => FeatureId;
    public override string Name => "Rain on the windows";
    public override string Description =>
        "Experimental. Drops sit on the windows and run down them, bending what you see through the glass. " +
        "Panes you break lose theirs.";

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
        if (_field == null)
            return;
        if (AnyVisible())
        {
            _field.Wetness = Mathf.Clamp01(WeatherExpansionMod.Config.Value.WindowRain * StormInputs.Wetness);
            _field.Advance(Time.deltaTime);
        }
        if (Time.unscaledTime >= _nextCheck)
        {
            _nextCheck = Time.unscaledTime + 1f;
            for (int i = 0; i < _overlays.Count; i++)
            {
                if (_overlays[i] != null)
                    _overlays[i].SetActive(_panes[i].Whole);
            }
        }
    }

    // The water only moves while a window is on screen.
    private bool AnyVisible()
    {
        for (int i = 0; i < _renderers.Count; i++)
        {
            if (_renderers[i] != null && _renderers[i].isVisible)
                return true;
        }
        return false;
    }

    private void OnSettingsChanged(WeatherExpansionConfig config)
    {
        if (_material != null)
            GlassMaterial.SetHaze(_material, config.WindowHaze);
    }

    private void OnSceneUnloaded(Scene scene) => Clear();

    private void Build()
    {
        Clear();
        _field = new DropField();
        _material = GlassMaterial.Create(_field.Texture);
        if (_material == null)
        {
            WeatherExpansionMod.Instance.Log.Warn("Rain on the windows: the game's glass material wasn't found.");
            return;
        }
        GlassMaterial.SetHaze(_material, WeatherExpansionMod.Config.Value.WindowHaze);
        _mesh = QuadMesh.Create();
        foreach (var pane in WindowFinder.Find())
        {
            var go = new GameObject("WeatherExpansion_GlassRain");
            go.layer = pane.Inner.gameObject.layer;
            var t = go.transform;
            t.SetParent(pane.Window, true);
            t.position = pane.Centre + pane.Inwards * 0.012f;
            t.rotation = Quaternion.LookRotation(-pane.Inwards, Vector3.up);
            t.localScale = new Vector3(pane.Size.x * 0.97f / t.parent.lossyScale.x, pane.Size.y * 0.97f / t.parent.lossyScale.y, 1f);
            go.AddComponent<MeshFilter>().sharedMesh = _mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            _overlays.Add(go);
            _renderers.Add(renderer);
            _panes.Add(pane);
        }
        WeatherExpansionMod.Instance.Log.Info($"Rain on the windows: {_overlays.Count} panes.");
    }

    private void Clear()
    {
        foreach (var go in _overlays)
        {
            if (go != null)
                Object.Destroy(go);
        }
        _overlays.Clear();
        _renderers.Clear();
        _panes.Clear();
        _field?.Destroy();
        _field = null;
        if (_material != null)
            Object.Destroy(_material);
        _material = null;
        if (_mesh != null)
            Object.Destroy(_mesh);
        _mesh = null;
    }
}
