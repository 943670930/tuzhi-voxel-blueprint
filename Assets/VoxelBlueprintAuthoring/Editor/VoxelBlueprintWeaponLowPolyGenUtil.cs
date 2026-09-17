using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;
using Vr3.VoxelBlueprintAuthoring;

namespace Vr3.VoxelBlueprintAuthoring.Editor
{
    public enum VoxelBlueprintWeaponLowPolyRoute
    {
        InstantMeshes = 0,
        VoxelBlockStable = 1,
    }

    public static class VoxelBlueprintWeaponLowPolyGenUtil
    {
        const string DefaultRelativeExe = "Tools/instant-meshes/Instant Meshes.exe";

        public struct LowPolyResult
        {
            public Mesh mesh;
            public Mesh blockMesh;
            public int targetFaces;
            public float creaseAngle;
            public bool pureQuad;
            public int inputSmoothIterations;
            public int imSmoothIterations;
            public int blockTriangles;
            public int elapsedMs;
        }

        public static string DefaultInstantMeshesExe =>
            Path.Combine(Directory.GetParent(Application.dataPath).FullName, DefaultRelativeExe);

        public static bool TryGenerate(
            VoxelBlueprintWeaponPartBoxCut cut,
            int targetFaces,
            float creaseAngle,
            bool pureQuad,
            string instantMeshesExe,
            int inputSmoothIterations,
            int imSmoothIterations,
            VoxelBlueprintWeaponLowPolyRoute route,
            out LowPolyResult result,
            out string error)
        {
            result = default;
            error = null;
            targetFaces = Mathf.Clamp(targetFaces, 50, 5000);
            creaseAngle = Mathf.Clamp(creaseAngle, 5f, 89f);
            inputSmoothIterations = Mathf.Clamp(inputSmoothIterations, 0, 10);
            imSmoothIterations = Mathf.Clamp(imSmoothIterations, 0, 10);
            if (string.IsNullOrEmpty(instantMeshesExe) || !File.Exists(instantMeshesExe))
            {
                error = "Instant Meshes 未找到:\n" + instantMeshesExe
                    + "\n请确认已解压到 Tools/instant-meshes/Instant Meshes.exe";
                return false;
            }

            if (!VoxelBlueprintWeaponPartBoxCutUtil.TryBuildAssembledWeaponBlockMesh(
                    cut, out Mesh blockMesh, out float cellWorldSize, out error))
                return false;

            if (route == VoxelBlueprintWeaponLowPolyRoute.VoxelBlockStable)
            {
                Mesh stable = UnityEngine.Object.Instantiate(blockMesh);
                stable.hideFlags = HideFlags.HideAndDontSave;
                stable.name = cut.source.name + "_BlockStable";
                result = new LowPolyResult
                {
                    mesh = stable,
                    blockMesh = blockMesh,
                    targetFaces = targetFaces,
                    creaseAngle = creaseAngle,
                    pureQuad = pureQuad,
                    inputSmoothIterations = 0,
                    imSmoothIterations = 0,
                    blockTriangles = blockMesh.triangles.Length / 3,
                    elapsedMs = 0,
                };
                return true;
            }

            string tempDir = Path.Combine(Path.GetTempPath(), "TuZhiLowPolyPreview");
            Directory.CreateDirectory(tempDir);
            string blockObj = Path.Combine(tempDir, "block.obj");
            string imObj = Path.Combine(tempDir, "lowpoly_im.obj");
            Mesh imInputMesh = UnityEngine.Object.Instantiate(blockMesh);
            imInputMesh.hideFlags = HideFlags.HideAndDontSave;
            if (inputSmoothIterations > 0)
                VoxelBlueprintMeshSmoothUtil.TaubinWithTipBias(imInputMesh, inputSmoothIterations);
            WavefrontMeshIo.ExportMesh(blockObj, imInputMesh);
            UnityEngine.Object.DestroyImmediate(imInputMesh);

            var sw = Stopwatch.StartNew();
            try
            {
                string args = BuildInstantMeshesArgs(imObj, blockObj, targetFaces, creaseAngle, pureQuad, imSmoothIterations);
                if (!RunInstantMeshes(instantMeshesExe, args, out string imLog, out string imError))
                {
                    error = string.IsNullOrEmpty(imError) ? imLog : imError;
                    return false;
                }

                if (!WavefrontMeshIo.TryImportMesh(imObj, out Mesh lowMesh, out error))
                    return false;

                TransferVertexColorsNearest(blockMesh, lowMesh);
                lowMesh.name = cut.source.name + "_LowPoly";
                result = new LowPolyResult
                {
                    mesh = lowMesh,
                    blockMesh = blockMesh,
                    targetFaces = targetFaces,
                    creaseAngle = creaseAngle,
                    pureQuad = pureQuad,
                    inputSmoothIterations = inputSmoothIterations,
                    imSmoothIterations = imSmoothIterations,
                    blockTriangles = blockMesh.triangles.Length / 3,
                    elapsedMs = (int)sw.ElapsedMilliseconds,
                };
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                if (blockMesh != null)
                    UnityEngine.Object.DestroyImmediate(blockMesh);
                return false;
            }
        }

        public static bool TryExportMeshAsset(Mesh mesh, string assetPath, out string error)
        {
            error = null;
            if (mesh == null)
            {
                error = "Mesh is null.";
                return false;
            }

            string folder = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(folder) && !AssetDatabase.IsValidFolder(folder))
            {
                string[] parts = folder.Split('/');
                string current = parts[0];
                for (int i = 1; i < parts.Length; i++)
                {
                    string next = current + "/" + parts[i];
                    if (!AssetDatabase.IsValidFolder(next))
                        AssetDatabase.CreateFolder(current, parts[i]);
                    current = next;
                }
            }

            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
            if (existing == null)
            {
                Mesh copy = UnityEngine.Object.Instantiate(mesh);
                copy.hideFlags = HideFlags.None;
                AssetDatabase.CreateAsset(copy, assetPath);
            }
            else
            {
                existing.Clear(false);
                EditorUtility.CopySerialized(mesh, existing);
                existing.name = mesh.name;
                EditorUtility.SetDirty(existing);
            }

            AssetDatabase.SaveAssets();
            return true;
        }

        static string BuildInstantMeshesArgs(
            string outputObj,
            string inputObj,
            int targetFaces,
            float creaseAngle,
            bool pureQuad,
            int imSmoothIterations)
        {
            string args = "-o \"" + outputObj + "\" -f " + targetFaces
                + " -c " + creaseAngle.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)
                + " -S " + Mathf.Max(0, imSmoothIterations)
                + " -b ";
            if (!pureQuad)
                args += "-D ";
            args += "\"" + inputObj + "\"";
            return args;
        }

        static bool RunInstantMeshes(string exe, string arguments, out string stdout, out string stderr)
        {
            var psi = new ProcessStartInfo
            {
                FileName = exe,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            using Process process = Process.Start(psi);
            if (process == null)
            {
                stdout = "";
                stderr = "Failed to start Instant Meshes.";
                return false;
            }

            stdout = process.StandardOutput.ReadToEnd();
            stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();
            return process.ExitCode == 0;
        }

        static void TransferVertexColorsNearest(Mesh source, Mesh target)
        {
            Color[] srcColors = source.colors;
            if (srcColors == null || srcColors.Length == 0)
                return;
            Vector3[] srcVerts = source.vertices;
            Vector3[] tgtVerts = target.vertices;
            var colors = new Color[tgtVerts.Length];
            for (int i = 0; i < tgtVerts.Length; i++)
            {
                int best = 0;
                float bestD = float.MaxValue;
                Vector3 p = tgtVerts[i];
                for (int j = 0; j < srcVerts.Length; j++)
                {
                    float d = (p - srcVerts[j]).sqrMagnitude;
                    if (d < bestD)
                    {
                        bestD = d;
                        best = j;
                    }
                }

                colors[i] = srcColors[best];
            }

            target.colors = colors;
        }
    }
}
