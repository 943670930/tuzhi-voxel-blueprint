using System.Collections.Generic;
using UnityEngine;

namespace Vr3.VoxelBlueprintAuthoring.Editor
{
    static class VoxelBlueprintMeshSmoothUtil
    {
        public static void TaubinWithTipBias(Mesh mesh, int iterations, float tipBias = 1.8f)
        {
            if (mesh == null || iterations <= 0)
                return;

            Vector3[] vertices = mesh.vertices;
            int[] triangles = mesh.triangles;
            if (vertices.Length == 0 || triangles.Length == 0)
                return;

            List<int>[] neighbors = BuildNeighbors(vertices.Length, triangles);
            float minY = float.PositiveInfinity;
            float maxY = float.NegativeInfinity;
            for (int i = 0; i < vertices.Length; i++)
            {
                float y = vertices[i].y;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;
            }

            float ySpan = Mathf.Max(1e-5f, maxY - minY);
            const float lambda = 0.5f;
            const float mu = -0.53f;
            for (int iter = 0; iter < iterations; iter++)
            {
                vertices = LaplacianPass(vertices, neighbors, lambda, minY, ySpan, tipBias);
                vertices = LaplacianPass(vertices, neighbors, mu, minY, ySpan, tipBias);
            }

            mesh.vertices = vertices;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }

        static List<int>[] BuildNeighbors(int vertexCount, int[] triangles)
        {
            var neighbors = new List<int>[vertexCount];
            for (int i = 0; i < vertexCount; i++)
                neighbors[i] = new List<int>(8);

            for (int t = 0; t < triangles.Length; t += 3)
            {
                Link(neighbors, triangles[t], triangles[t + 1]);
                Link(neighbors, triangles[t + 1], triangles[t + 2]);
                Link(neighbors, triangles[t + 2], triangles[t]);
            }

            return neighbors;
        }

        static void Link(List<int>[] neighbors, int a, int b)
        {
            if (!neighbors[a].Contains(b))
                neighbors[a].Add(b);
            if (!neighbors[b].Contains(a))
                neighbors[b].Add(a);
        }

        static Vector3[] LaplacianPass(
            Vector3[] vertices,
            List<int>[] neighbors,
            float factor,
            float minY,
            float ySpan,
            float tipBias)
        {
            var next = new Vector3[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                List<int> adj = neighbors[i];
                if (adj.Count == 0)
                {
                    next[i] = vertices[i];
                    continue;
                }

                Vector3 sum = Vector3.zero;
                for (int n = 0; n < adj.Count; n++)
                    sum += vertices[adj[n]];
                Vector3 avg = sum / adj.Count;
                float y01 = Mathf.Clamp01((vertices[i].y - minY) / ySpan);
                float weight = Mathf.Lerp(1f, tipBias, y01 * y01);
                float k = factor * weight;
                next[i] = vertices[i] + (avg - vertices[i]) * k;
            }

            return next;
        }
    }
}
