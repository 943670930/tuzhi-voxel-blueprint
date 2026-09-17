using UnityEngine;
using Vr3.VoxelBlueprintAuthoring;

namespace Vr3.VoxelBlueprintAuthoring
{
    [ExecuteAlways]
    public sealed class VoxelBlueprintSceneDisplay : MonoBehaviour
    {
        public VoxelBlueprintAsset blueprint;
        [Min(0.001f)] public float cellWorldSize = 0.05f;

        MeshFilter _filter;
        MeshRenderer _renderer;
        Mesh _mesh;

        void OnEnable() => Rebuild();
        void OnValidate() => Rebuild();

        public void Rebuild()
        {
            if (blueprint == null)
                return;

            EnsureComponents();
            if (_mesh == null)
            {
                _mesh = new Mesh { name = "VoxelBlueprintSceneMesh" };
                _mesh.MarkDynamic();
                _filter.sharedMesh = _mesh;
            }

            VoxelBlueprintMeshBuilder.Fill(blueprint, _mesh, out _, out _);
            transform.localScale = Vector3.one * cellWorldSize;
        }

        void EnsureComponents()
        {
            if (_filter == null)
                _filter = GetComponent<MeshFilter>() ?? gameObject.AddComponent<MeshFilter>();
            if (_renderer == null)
                _renderer = GetComponent<MeshRenderer>() ?? gameObject.AddComponent<MeshRenderer>();
            if (_renderer.sharedMaterial == null)
            {
                Shader shader = Shader.Find("Hidden/VoxelBlueprint/CubeUnlit");
                if (shader != null)
                    _renderer.sharedMaterial = new Material(shader);
            }
        }
    }
}
