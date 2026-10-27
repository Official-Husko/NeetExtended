using UnityEngine;

namespace Coop.Assets.Faces;

/// <summary>
/// Another player's face on their body here. The game puts the face picture on the player model's
/// skin material (slot 3, which bare body parts use too); a body gets its own copy of that material
/// with the face, and it goes with the body.
/// </summary>
internal sealed class PuppetFace : MonoBehaviour
{
    private const int SkinSlot = 3;
    private const string BodyName = "Neet";

    private SkinnedMeshRenderer[] _renderers;
    private Material _original;
    private Material _face;

    /// <summary>Shows <paramref name="face"/> on the body, or its default face for null.</summary>
    public static void Show(Component body, Texture face)
    {
        if (body == null)
            return;
        if (!body.TryGetComponent(out PuppetFace shown))
        {
            if (face == null)
                return;
            shown = body.gameObject.AddComponent<PuppetFace>();
        }
        shown.Set(face);
    }

    private void Set(Texture face)
    {
        _renderers ??= GetComponentsInChildren<SkinnedMeshRenderer>(true);
        if (_original == null)
            _original = FindSkin();
        if (_original == null)
            return;
        if (face != null && _face == null)
            _face = new Material(_original) { name = _original.name + " (co-op face)" };
        if (_face != null)
            _face.mainTexture = face;

        // The body's skin slot, the bare parts that copy it, and the hands' skin.
        var from = face != null ? _original : _face;
        var to = face != null ? _face : _original;
        if (from == null || from == to)
            return;
        foreach (var renderer in _renderers)
        {
            if (renderer == null)
                continue;
            var materials = renderer.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < materials.Length; i++)
            {
                if (materials[i] == from)
                {
                    materials[i] = to;
                    changed = true;
                }
            }
            if (changed)
                renderer.sharedMaterials = materials;
        }
    }

    private Material FindSkin()
    {
        foreach (var renderer in _renderers)
        {
            if (renderer != null && renderer.name == BodyName && renderer.sharedMaterials.Length > SkinSlot)
                return renderer.sharedMaterials[SkinSlot];
        }
        return null;
    }

    private void OnDestroy()
    {
        if (_face != null)
            Destroy(_face);
    }
}
