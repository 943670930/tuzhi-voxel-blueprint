using System.Collections.Generic;
using UnityEngine;

namespace Vr3.VoxelBlueprintAuthoring.Editor
{
    static class VoxelBlueprintMeshWeldUtil
    {
        public static void Weld(Mesh mesh, float epsilon)
        {
            if (mesh == null || epsilon <= 0f)
                return;

            Vector3[] vertices = mesh.vertices;
            int[] triangles = mesh.triangles;
            Color[] colors = mesh.colors;
            if (vertices.Length == 0 || triangles.Length == 0)
                return;

            float inv = 1f / epsilon;
            var indexByQuant = new Dictionary<Vector3Int, int>(vertices.Length);
            var weldedVerts = new List<Vector3>(vertices.Length);
            var weldedColors = colors != null && colors.Length == vertices.Length
                ? new List<Color>(vertices.Length)
                : null;
            var remap = new int[vertices.Length];

            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3Int key = Quantize(vertices[i], inv);
                if (!indexByQuant.TryGetValue(key, out int weldedIndex))
                {
                    weldedIndex = weldedVerts.Count;
                    indexByQuant.Add(key, weldedIndex);
                    weldedVerts.Add(vertices[i]);
                    if (weldedColors != null)
                        weldedColors.Add(colors[i]);
                }

                remap[i] = weldedIndex;
            }

            var weldedTris = new List<int>(triangles.Length);
            for (int t = 0; t < triangles.Length; t += 3)
            {
                int a = remap[triangles[t]];
                int b = remap[triangles[t + 1]];
                int c = remap[triangles[t + 2]];
                if (a == b || b == c || a == c)
                    continue;
                weldedTris.Add(a);
                weldedTris.Add(b);
                weldedTris.Add(c);
            }

            mesh.Clear(false);
            mesh.SetVertices(weldedVerts);
            if (weldedColors != null)
                mesh.SetColors(weldedColors);
            mesh.SetTriangles(weldedTris, 0, true);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }

        static Vector3Int Quantize(Vector3 v, float invEpsilon)
        {
            return new Vector3Int(
                Mathf.RoundToInt(v.x * invEpsilon),
                Mathf.RoundToInt(v.y * invEpsilon),
                Mathf.RoundToInt(v.z * invEpsilon));
        }
    }
}
