using UnityEngine;

namespace NeetModKit.Items;

/// <summary>Builds the template object for an <see cref="ItemDefinition"/> from the item it copies.</summary>
internal static class ItemTemplateBuilder
{
    private static readonly int MainTex = Shader.PropertyToID("_MainTex");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    public static GameObject Build(ItemDefinition def, GameObject basePrefab)
    {
        var template = Object.Instantiate(basePrefab, PrefabStash.Root);
        template.name = def.Name;

        // The save system recreates items from Usable.prefabName, so it must match the registry key.
        var usable = template.GetComponent<Usable>() ?? template.GetComponentInChildren<Usable>(true);
        if (usable != null)
        {
            usable.prefabName = def.Name;
            if (def.Description != null)
                usable.description = def.Description;
            if (def.Price.HasValue)
                usable.price = def.Price.Value;
            if (def.Store.HasValue)
                usable.purchaseableAt = def.Store.Value;
            if (def.StorePicture != null)
                usable.storePicture = def.StorePicture;
        }

        if (def.Texture != null || def.Tint.HasValue)
            Recolour(template, def);

        if (!Mathf.Approximately(def.Scale, 1f))
            template.transform.localScale *= def.Scale;

        def.Setup?.Invoke(template);
        return template;
    }

    private static void Recolour(GameObject template, ItemDefinition def)
    {
        foreach (var renderer in template.GetComponentsInChildren<Renderer>(true))
        {
            // .materials makes per-renderer copies on the template; spawned items share those copies.
            var materials = renderer.materials;
            foreach (var material in materials)
            {
                if (def.Texture != null && material.HasProperty(MainTex))
                    material.SetTexture(MainTex, def.Texture);
                if (def.Tint.HasValue && material.HasProperty(ColorId))
                    material.SetColor(ColorId, material.GetColor(ColorId) * def.Tint.Value);
            }
            renderer.materials = materials;
        }
    }
}
