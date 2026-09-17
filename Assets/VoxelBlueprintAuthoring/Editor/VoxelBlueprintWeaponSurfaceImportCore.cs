using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Vr3.VoxelBlueprintAuthoring;

namespace Vr3.VoxelBlueprintAuthoring.Editor
{
    internal static class VoxelBlueprintWeaponSurfaceImportCore
    {
        internal static bool ImportWeaponSurface(
            string fbxPath,
            string texturePath,
            string outputFolder,
            string blueprintAssetPath,
            string sourceMarkdownTitle,
            string sourceMarkdownBody,
            int targetCellsOnLongestAxis,
            bool logDialog)
            => ImportWeaponSurface(
                fbxPath,
                texturePath,
                outputFolder,
                blueprintAssetPath,
                sourceMarkdownTitle,
                sourceMarkdownBody,
                targetCellsOnLongestAxis,
                logDialog,
                meshNameFilter: null);

        internal static bool ImportWeaponSurface(
            string fbxPath,
            string texturePath,
            string outputFolder,
            string blueprintAssetPath,
            string sourceMarkdownTitle,
            string sourceMarkdownBody,
            int targetCellsOnLongestAxis,
            bool logDialog,
            string meshNameFilter)
        {
            EnsureImportSettings(fbxPath, texturePath);
            Mesh mesh = LoadReadableMesh(fbxPath, meshNameFilter, combineHierarchyMeshes: string.IsNullOrEmpty(meshNameFilter));
            Texture2D texture = LoadReadableTexture(texturePath);
            if (mesh == null)
            {
                if (logDialog)
                {
                    string suffix = string.IsNullOrEmpty(meshNameFilter) ? "" : "\nMesh filter: " + meshNameFilter;
                    EditorUtility.DisplayDialog("Voxel Blueprint", "Could not load a readable mesh from:\n" + fbxPath + suffix, "OK");
                }
                return false;
            }

            Dictionary<Vector3Int, Color32> cells = SurfaceVoxelize(mesh, texture, targetCellsOnLongestAxis, out float cellWorldSize);
            Vector3 boundsSize = mesh.bounds.size;
            if (mesh.hideFlags == HideFlags.HideAndDontSave)
                Object.DestroyImmediate(mesh);

            if (cells.Count == 0)
            {
                if (logDialog)
                    EditorUtility.DisplayDialog("Voxel Blueprint", "Surface voxelization produced zero cells.", "OK");
                return false;
            }

            Vector3Int min = new Vector3Int(int.MaxValue, int.MaxValue, int.MaxValue);
            Vector3Int max = new Vector3Int(int.MinValue, int.MinValue, int.MinValue);
            foreach (Vector3Int key in cells.Keys)
            {
                min = Vector3Int.Min(min, key);
                max = Vector3Int.Max(max, key);
            }

            Vector3Int size = max - min + Vector3Int.one;
            VoxelBlueprintAsset asset = AssetDatabase.LoadAssetAtPath<VoxelBlueprintAsset>(blueprintAssetPath);
            if (asset == null)
            {
                Directory.CreateDirectory(outputFolder);
                asset = ScriptableObject.CreateInstance<VoxelBlueprintAsset>();
                AssetDatabase.CreateAsset(asset, blueprintAssetPath);
            }

            asset.Resize(size.x, size.y, size.z, false);
            asset.Clear();
            foreach (KeyValuePair<Vector3Int, Color32> entry in cells)
            {
                Vector3Int local = entry.Key - min;
                asset.SetCell(local.x, local.y, local.z, true, entry.Value, 4);
            }

            asset.interiorMode = VoxelBlueprintInteriorMode.SurfaceShell;
            EditorUtility.SetDirty(asset);
            WriteSourceMarkdown(outputFolder, sourceMarkdownTitle, sourceMarkdownBody, boundsSize, size, cells.Count, cellWorldSize);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (logDialog)
                EditorUtility.DisplayDialog(
                    "Voxel Blueprint",
                    $"Voxelized: {cells.Count} cells, grid {size.x}×{size.y}×{size.z}, cell size {cellWorldSize:F4} m.",
                    "OK");
            else
                Debug.Log($"[VoxelBlueprint] Weapon voxelized: {cells.Count} cells at {blueprintAssetPath}");
            return true;
        }

        static void EnsureImportSettings(string fbxPath, string texturePath)
        {
            var modelImporter = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
            if (modelImporter != null && !modelImporter.isReadable)
            {
                modelImporter.isReadable = true;
                modelImporter.SaveAndReimport();
            }

            if (string.IsNullOrEmpty(texturePath))
                return;
            var textureImporter = AssetImporter.GetAtPath(texturePath) as TextureImporter;
            if (textureImporter != null && !textureImporter.isReadable)
            {
                textureImporter.isReadable = true;
                textureImporter.SaveAndReimport();
            }
        }

        static Mesh LoadReadableMesh(string path, string meshNameFilter = null, bool combineHierarchyMeshes = false)
        {
            if (combineHierarchyMeshes)
            {
                Mesh combined = TryBuildCombinedHierarchyMesh(path, meshNameFilter);
                if (combined != null)
                    return combined;
            }

            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
            Mesh named = null;
            Mesh largest = null;
            int largestVerts = 0;
            for (int i = 0; i < assets.Length; i++)
            {
                if (assets[i] is not Mesh mesh || mesh.vertexCount <= 0)
                    continue;
                if (!string.IsNullOrEmpty(meshNameFilter)
                    && string.Equals(mesh.name, meshNameFilter, System.StringComparison.Ordinal))
                    named = mesh;
                if (mesh.vertexCount > largestVerts)
                {
                    largestVerts = mesh.vertexCount;
                    largest = mesh;
                }
            }

            if (!string.IsNullOrEmpty(meshNameFilter))
                return named;
            return largest;
        }

        static Mesh TryBuildCombinedHierarchyMesh(string path, string meshNameFilter)
        {
            GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (root == null)
                return null;

            MeshFilter[] filters = root.GetComponentsInChildren<MeshFilter>(true);
            if (filters == null || filters.Length == 0)
                return null;

            var combines = new List<CombineInstance>(filters.Length);
            Matrix4x4 rootToLocal = root.transform.worldToLocalMatrix;
            for (int i = 0; i < filters.Length; i++)
            {
                MeshFilter filter = filters[i];
                Mesh shared = filter.sharedMesh;
                if (shared == null || shared.vertexCount <= 0 || !shared.isReadable)
                    continue;
                if (!string.IsNullOrEmpty(meshNameFilter)
                    && !string.Equals(shared.name, meshNameFilter, System.StringComparison.Ordinal))
                    continue;

                combines.Add(new CombineInstance
                {
                    mesh = shared,
                    transform = rootToLocal * filter.transform.localToWorldMatrix,
                });
            }

            if (combines.Count == 0)
                return null;

            var combined = new Mesh
            {
                name = Path.GetFileNameWithoutExtension(path) + "_Combined",
                hideFlags = HideFlags.HideAndDontSave,
            };
            combined.CombineMeshes(combines.ToArray(), mergeSubMeshes: true, useMatrices: true);
            combined.RecalculateBounds();
            return combined;
        }

        static Texture2D LoadReadableTexture(string path) =>
            string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(path);

        static Dictionary<Vector3Int, Color32> SurfaceVoxelize(Mesh mesh, Texture2D texture, int targetCells, out float cellWorldSize)
        {
            var result = new Dictionary<Vector3Int, Color32>();
            Vector3[] vertices = mesh.vertices;
            Vector2[] uvs = mesh.uv;
            int[] triangles = mesh.triangles;
            Bounds bounds = mesh.bounds;
            float longest = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
            cellWorldSize = longest / Mathf.Max(8, targetCells);
            Vector3 origin = bounds.min;

            for (int t = 0; t < triangles.Length; t += 3)
            {
                Vector3 a = vertices[triangles[t]];
                Vector3 b = vertices[triangles[t + 1]];
                Vector3 c = vertices[triangles[t + 2]];
                int steps = Mathf.Clamp(
                    Mathf.CeilToInt(Mathf.Max(Vector3.Distance(a, b), Vector3.Distance(b, c), Vector3.Distance(c, a)) / (cellWorldSize * 0.5f)),
                    1,
                    96);
                for (int i = 0; i <= steps; i++)
                {
                    float u = i / (float)steps;
                    for (int j = 0; j <= steps - i; j++)
                    {
                        float v = j / (float)steps;
                        float w = 1f - u - v;
                        Vector3 p = a * u + b * v + c * w;
                        Vector3Int cell = Vector3Int.RoundToInt((p - origin) / cellWorldSize);
                        Color32 color = SampleColor(texture, uvs, triangles, t, u, v, w);
                        result[cell] = color;
                    }
                }
            }

            return result;
        }

        static Color32 SampleColor(Texture2D texture, Vector2[] uvs, int[] triangles, int triStart, float u, float v, float w)
        {
            if (texture == null || uvs == null || uvs.Length == 0)
                return new Color32(180, 180, 180, 255);

            int i0 = triangles[triStart];
            int i1 = triangles[triStart + 1];
            int i2 = triangles[triStart + 2];
            Vector2 uv = uvs[i0] * u + uvs[i1] * v + uvs[i2] * w;
            Color sampled = texture.GetPixelBilinear(Mathf.Repeat(uv.x, 1f), Mathf.Repeat(uv.y, 1f));
            return (Color32)sampled;
        }

        static void WriteSourceMarkdown(
            string outputFolder,
            string title,
            string body,
            Vector3 boundsSize,
            Vector3Int gridSize,
            int enabledCells,
            float pitch)
        {
            Directory.CreateDirectory(outputFolder);
            var sb = new StringBuilder();
            sb.AppendLine("# " + title);
            sb.AppendLine();
            sb.AppendLine(body);
            sb.AppendLine("- **Voxel processing:** surface voxelize only, no fill, no split.");
            sb.AppendLine($"- **Pitch:** `{pitch:F6}` m; enabled `{enabledCells}`; grid `{gridSize.x} x {gridSize.y} x {gridSize.z}`; mesh bounds `{boundsSize.x:F3} x {boundsSize.y:F3} x {boundsSize.z:F3}` m.");
            File.WriteAllText(Path.Combine(outputFolder, "SOURCE.md"), sb.ToString(), Encoding.UTF8);
        }
    }
}
