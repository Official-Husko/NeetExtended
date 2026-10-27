using UnityEngine;

namespace WeatherExpansion.Windows;

/// <summary>A one by one quad facing -Z like Unity's own, with UVs 0..1, for drawing on a window.</summary>
internal static class QuadMesh
{
    public static Mesh Create()
    {
        var mesh = new Mesh { name = "WeatherExpansion quad", hideFlags = HideFlags.HideAndDontSave };
        mesh.vertices = new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f) };
        mesh.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
        mesh.normals = new[] { -Vector3.forward, -Vector3.forward, -Vector3.forward, -Vector3.forward };
        mesh.tangents = new[] { new Vector4(1, 0, 0, -1), new Vector4(1, 0, 0, -1), new Vector4(1, 0, 0, -1), new Vector4(1, 0, 0, -1) };
        mesh.triangles = new[] { 0, 3, 1, 3, 0, 2 };
        mesh.UploadMeshData(true);
        return mesh;
    }
}
