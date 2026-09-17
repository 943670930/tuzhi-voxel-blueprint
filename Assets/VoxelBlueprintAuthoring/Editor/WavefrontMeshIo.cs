using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;

namespace Vr3.VoxelBlueprintAuthoring.Editor
{
    static class WavefrontMeshIo
    {
        public static void ExportMesh(string path, Mesh mesh)
        {
            if (mesh == null)
                throw new System.ArgumentNullException(nameof(mesh));
            var sb = new StringBuilder(mesh.vertexCount * 24 + mesh.triangles.Length * 8);
            Vector3[] vertices = mesh.vertices;
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 v = vertices[i];
                sb.Append("v ").Append(v.x.ToString("G9", CultureInfo.InvariantCulture))
                    .Append(' ').Append(v.y.ToString("G9", CultureInfo.InvariantCulture))
                    .Append(' ').Append(v.z.ToString("G9", CultureInfo.InvariantCulture))
                    .Append('\n');
            }

            int[] triangles = mesh.triangles;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                sb.Append("f ").Append(triangles[i] + 1).Append(' ')
                    .Append(triangles[i + 1] + 1).Append(' ')
                    .Append(triangles[i + 2] + 1).Append('\n');
            }

            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(path, sb.ToString(), Encoding.ASCII);
        }

        public static bool TryImportMesh(string path, out Mesh mesh, out string error)
        {
            mesh = null;
            error = null;
            if (!File.Exists(path))
            {
                error = "OBJ not found: " + path;
                return false;
            }

            var vertices = new List<Vector3>(1024);
            var triangles = new List<int>(2048);
            foreach (string raw in File.ReadAllLines(path))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line[0] == '#')
                    continue;
                if (line.StartsWith("v "))
                {
                    string[] parts = line.Split((char[])null, System.StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length < 4)
                        continue;
                    vertices.Add(new Vector3(
                        float.Parse(parts[1], CultureInfo.InvariantCulture),
                        float.Parse(parts[2], CultureInfo.InvariantCulture),
                        float.Parse(parts[3], CultureInfo.InvariantCulture)));
                    continue;
                }

                if (!line.StartsWith("f "))
                    continue;
                string[] face = line.Split((char[])null, System.StringSplitOptions.RemoveEmptyEntries);
                if (face.Length < 4)
                    continue;
                var indices = new List<int>(face.Length - 1);
                for (int i = 1; i < face.Length; i++)
                {
                    string token = face[i];
                    int slash = token.IndexOf('/');
                    if (slash >= 0)
                        token = token.Substring(0, slash);
                    indices.Add(int.Parse(token, CultureInfo.InvariantCulture) - 1);
                }

                for (int i = 1; i < indices.Count - 1; i++)
                {
                    triangles.Add(indices[0]);
                    triangles.Add(indices[i]);
                    triangles.Add(indices[i + 1]);
                }
            }

            if (vertices.Count == 0 || triangles.Count == 0)
            {
                error = "OBJ has no geometry: " + path;
                return false;
            }

            mesh = new Mesh
            {
                name = Path.GetFileNameWithoutExtension(path),
                indexFormat = IndexFormat.UInt32,
                hideFlags = HideFlags.HideAndDontSave,
            };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0, true);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return true;
        }
    }
}
