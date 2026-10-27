using UnityEngine;

namespace WeatherExpansion.Windows;

/// <summary>
/// The material rain on glass is drawn with: a copy of the game's own refracting glass
/// (Glass/DiffuseBumped, the ferris wheel's windows), with the drops' normal map plugged in.
/// </summary>
internal static class GlassMaterial
{
    private const string ShaderName = "Glass/DiffuseBumped";

    public static Material Create(Texture normalMap)
    {
        var source = FindGameGlass();
        if (source == null)
            return null;
        var material = new Material(source) { name = "WeatherExpansion glass", hideFlags = HideFlags.HideAndDontSave };
        material.SetTexture("_Normal", normalMap);
        material.SetFloat("_NormalScale", 1f);
        material.SetColor("_Tint", new Color(0.7f, 0.78f, 0.9f, 1f));
        material.SetFloat("_Smoothness", 0.5f);
        material.SetFloat("_IOR", 1.12f);
        material.SetFloat("_ChromaticAberration", 0.02f);
        SetHaze(material, 0f);
        return material;
    }

    /// <summary>How fogged up the glass is (the shader's opacity): a bluish film that makes the drops show at night.</summary>
    public static void SetHaze(Material material, float haze) => material.SetFloat("_Opacity", haze);

    private static Material FindGameGlass()
    {
        foreach (var material in Resources.FindObjectsOfTypeAll<Material>())
        {
            if (material != null && material.shader != null && material.shader.name == ShaderName && material.name == "Glass")
                return material;
        }
        return null;
    }
}
