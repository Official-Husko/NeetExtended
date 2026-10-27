using UnityEngine;
using UnityEngine.Rendering;

namespace WeatherExpansion.Fog;

/// <summary>
/// A bank of mist over one patch of ground: a few dozen big soft particles that drift slowly with
/// the wind, one particle system (one draw call). The game's alpha-blended particle shader draws
/// them and fades them out with distance in the fog.
/// </summary>
internal sealed class MistSystem
{
    private readonly GameObject _object;
    private readonly ParticleSystem _particles;

    public MistSystem(string name, Vector3 centre, Material material)
    {
        _object = new GameObject(name);
        _object.transform.position = centre;
        _particles = _object.AddComponent<ParticleSystem>();
        _particles.Stop();

        var main = _particles.main;
        main.loop = true;
        main.prewarm = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = 18f;
        main.startSize = new ParticleSystem.MinMaxCurve(4f, 7f);
        main.startSpeed = 0f;
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.28f);
        main.maxParticles = 70;

        var emission = _particles.emission;
        emission.rateOverTime = 3.5f;

        var shape = _particles.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(18f, 2f, 18f);

        // Wind: everything slides the same way, a little faster than the puffs spin.
        var velocity = _particles.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = 0.55f;
        velocity.y = 0f;
        velocity.z = 0.12f;

        var rotation = _particles.rotationOverLifetime;
        rotation.enabled = true;
        rotation.z = new ParticleSystem.MinMaxCurve(-0.08f, 0.08f);

        // Fade in and out so puffs never pop.
        var colour = _particles.colorOverLifetime;
        colour.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(1f, 0.75f), new GradientAlphaKey(0f, 1f) });
        colour.color = gradient;

        var renderer = _object.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

        _particles.Play();
    }

    /// <summary>1 is the normal amount.</summary>
    public void SetAmount(float amount)
    {
        var emission = _particles.emission;
        emission.rateOverTime = 3.5f * amount;
    }

    public void Destroy()
    {
        if (_object != null)
            Object.Destroy(_object);
    }
}
