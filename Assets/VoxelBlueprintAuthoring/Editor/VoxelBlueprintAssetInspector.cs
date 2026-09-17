using UnityEditor;
using UnityEngine;

namespace Vr3.VoxelBlueprintAuthoring.Editor
{
    [CustomEditor(typeof(VoxelBlueprintAsset))]
    public sealed class VoxelBlueprintAssetInspector : UnityEditor.Editor
    {
        const float FrontPreviewYaw = 215f;
        const float FrontPreviewPitch = 22f;

        PreviewRenderUtility _preview;
        float _orbitYaw = FrontPreviewYaw;
        float _orbitPitch = FrontPreviewPitch;
        float _orbitDistance;
        Vector2 _orbitPan;
        bool _orbitDragging;
        bool _orbitPanning;

        void OnEnable()
        {
            _preview = new PreviewRenderUtility();
            _orbitYaw = FrontPreviewYaw;
            _orbitPitch = FrontPreviewPitch;
            _orbitDistance = 0f;
            _orbitPan = Vector2.zero;
            VoxelBlueprintPreview3D.Invalidate((VoxelBlueprintAsset)target);
        }

        void OnDisable()
        {
            _preview?.Cleanup();
            _preview = null;
        }

        public override void OnInspectorGUI()
        {
            VoxelBlueprintAsset asset = (VoxelBlueprintAsset)target;
            EditorGUI.BeginChangeCheck();
            asset.interiorMode = (VoxelBlueprintInteriorMode)EditorGUILayout.EnumPopup("Interior Mode", asset.interiorMode);
            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(asset);
                VoxelBlueprintPreview3D.Invalidate(asset);
            }
            EditorGUILayout.LabelField("Grid", asset.sizeX + " × " + asset.sizeY + " × " + asset.sizeZ);
            EditorGUILayout.LabelField("Cell Slots", asset.CellCount.ToString());
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("3D Preview", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                Rect rect = GUILayoutUtility.GetRect(280f, 320f, GUILayout.ExpandWidth(true), GUILayout.MinHeight(280f));
                if (VoxelBlueprintPreview3D.DrawOrbitPreview(
                    rect,
                    _preview,
                    asset,
                    ref _orbitYaw,
                    ref _orbitPitch,
                    ref _orbitDistance,
                    ref _orbitPan,
                    ref _orbitDragging,
                    ref _orbitPanning))
                    Repaint();
                if (VoxelBlueprintWeaponPartBoxCutUtil.IsWeaponBlueprint(asset))
                {
                    using (new EditorGUILayout.VerticalScope(GUILayout.Width(56f)))
                    {
                        if (GUILayout.Button("武器切割", GUILayout.Height(150f)))
                            VoxelBlueprintWeaponPartBoxCutWindow.OpenForWeapon(asset);
                        if (GUILayout.Button("Low-Poly", GUILayout.Height(150f)))
                            VoxelBlueprintWeaponLowPolyPreviewWindow.OpenForWeapon(asset);
                    }
                }
                else
                {
                    if (GUILayout.Button("切割", GUILayout.Width(56f), GUILayout.Height(320f)))
                        VoxelBlueprintPartBoxCutWindow.OpenForFullBody(asset);
                    if (GUILayout.Button("偏移", GUILayout.Width(56f), GUILayout.Height(320f)))
                        VoxelBlueprintPartWearOffsetWindow.Open(asset);
                }
            }
            EditorGUILayout.Space(6f);
            if (GUILayout.Button("Open In Voxel Blueprint Authoring"))
                VoxelBlueprintAuthoringWindow.ShowWindow(asset);
        }

        public override bool HasPreviewGUI() => false;
    }
}
