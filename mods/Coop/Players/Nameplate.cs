using UnityEngine;

namespace Coop.Players;

/// <summary>A player's name floating over their head, always turned to this game's camera.</summary>
internal sealed class Nameplate : MonoBehaviour
{
    private static Font _font;

    public static Nameplate Attach(Transform parent, string text, float height)
    {
        var go = new GameObject("Nameplate");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(0f, height, 0f);

        var mesh = go.AddComponent<TextMesh>();
        _font ??= Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        mesh.font = _font;
        mesh.text = text;
        mesh.fontSize = 48;
        mesh.characterSize = 0.012f;
        mesh.anchor = TextAnchor.LowerCenter;
        mesh.alignment = TextAlignment.Center;
        mesh.color = new Color(1f, 1f, 1f, 0.9f);
        go.GetComponent<MeshRenderer>().sharedMaterial = _font.material;
        return go.AddComponent<Nameplate>();
    }

    private void LateUpdate()
    {
        var camera = HouseManager._ != null ? HouseManager._.mainCamera : null;
        if (camera != null)
            transform.rotation = Quaternion.LookRotation(transform.position - camera.transform.position);
    }
}
