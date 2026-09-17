using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Vr3.VoxelBlueprintAuthoring;

namespace Vr3.VoxelBlueprintAuthoring.Editor
{
    public sealed class VoxelBlueprintWeaponLowPolyPreviewWindow : EditorWindow
    {
        const string PrefsExe = "TuZhi.WeaponLowPoly.InstantMeshesExe";
        const string PrefsTargetFaces = "TuZhi.WeaponLowPoly.TargetFaces";
        const string PrefsCrease = "TuZhi.WeaponLowPoly.CreaseAngle";
        const string PrefsPureQuad = "TuZhi.WeaponLowPoly.PureQuad";
        const string PrefsInputSmooth = "TuZhi.WeaponLowPoly.InputSmooth";
        const string PrefsImSmooth = "TuZhi.WeaponLowPoly.ImSmooth";
        const string PrefsRoute = "TuZhi.WeaponLowPoly.Route";
        const string ExportFolder = "Assets/3DModel/Generated/GiantSword_LowPolyFromVoxel";

        VoxelBlueprintWeaponPartBoxCut _cut;
        PreviewRenderUtility _preview;
        Mesh _previewMesh;
        Mesh _blockMesh;
        int _drawn;
        Vector3 _boundsCenter;
        float _boundsRadius = 1f;
        int _targetFaces = 500;
        float _creaseAngle = 35f;
        bool _pureQuad;
        int _inputSmoothIterations = 1;
        int _imSmoothIterations = 3;
        VoxelBlueprintWeaponLowPolyRoute _route = VoxelBlueprintWeaponLowPolyRoute.InstantMeshes;
        string _instantMeshesExe;
        string _status = "";
        string _error = "";
        int _lastMs;
        int _lastFaces;
        int _blockFaces;
        bool _generating;
        bool _pendingGenerate;
        double _generateAt;
        float _orbitYaw = 215f;
        float _orbitPitch = 22f;
        float _orbitDistance;
        Vector2 _orbitPan;
        bool _orbitDragging;
        bool _orbitPanning;
        bool _clickArmed;
        Vector2 _clickStart;
        bool _marquee;

        [MenuItem("Tools/Voxel Blueprint Authoring/Weapon Low-Poly Preview")]
        static void OpenMenu()
        {
            string last = EditorPrefs.GetString("TuZhi.WeaponPartBoxCut.LastSource", "");
            VoxelBlueprintAsset source = string.IsNullOrEmpty(last)
                ? null
                : AssetDatabase.LoadAssetAtPath<VoxelBlueprintAsset>(last);
            OpenForWeapon(source);
        }

        public static void OpenForWeapon(VoxelBlueprintAsset source)
        {
            string title = source != null ? "武器 Low-Poly: " + source.name : "武器 Low-Poly";
            VoxelBlueprintWeaponLowPolyPreviewWindow window = GetWindow<VoxelBlueprintWeaponLowPolyPreviewWindow>(title);
            if (source != null)
            {
                window._cut = VoxelBlueprintWeaponPartBoxCutUtil.LoadOrCreateCutFor(source);
                window._pendingGenerate = true;
                window._generateAt = EditorApplication.timeSinceStartup + 0.15f;
            }
            window.Show();
            window.Focus();
        }

        void OnEnable()
        {
            _preview = new PreviewRenderUtility();
            _targetFaces = EditorPrefs.GetInt(PrefsTargetFaces, 500);
            _creaseAngle = EditorPrefs.GetFloat(PrefsCrease, 35f);
            _pureQuad = EditorPrefs.GetBool(PrefsPureQuad, false);
            _inputSmoothIterations = EditorPrefs.GetInt(PrefsInputSmooth, 1);
            _imSmoothIterations = EditorPrefs.GetInt(PrefsImSmooth, 3);
            _route = (VoxelBlueprintWeaponLowPolyRoute)EditorPrefs.GetInt(
                PrefsRoute, (int)VoxelBlueprintWeaponLowPolyRoute.InstantMeshes);
            _instantMeshesExe = EditorPrefs.GetString(PrefsExe, VoxelBlueprintWeaponLowPolyGenUtil.DefaultInstantMeshesExe);
            if (_cut == null)
            {
                string last = EditorPrefs.GetString("TuZhi.WeaponPartBoxCut.LastSource", "");
                if (!string.IsNullOrEmpty(last))
                {
                    VoxelBlueprintAsset source = AssetDatabase.LoadAssetAtPath<VoxelBlueprintAsset>(last);
                    if (source != null)
                        _cut = VoxelBlueprintWeaponPartBoxCutUtil.LoadOrCreateCutFor(source);
                }
            }

            EditorApplication.update += Tick;
            _pendingGenerate = _cut != null && _cut.source != null;
            _generateAt = EditorApplication.timeSinceStartup + 0.2f;
        }

        void OnDisable()
        {
            EditorApplication.update -= Tick;
            DestroyPreviewMeshes();
            _preview?.Cleanup();
            _preview = null;
        }

        void Tick()
        {
            if (!_pendingGenerate || _generating || EditorApplication.timeSinceStartup < _generateAt)
                return;
            _pendingGenerate = false;
            GenerateNow();
        }

        void OnGUI()
        {
            EditorGUILayout.LabelField("武器 Low-Poly 预览", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Instant Meshes：游戏 low-poly，需焊点后再平滑（已自动焊点）。若出现碎片/炸面，先切到「体素 Block（稳定）」或把输入平滑降到 0～1。",
                MessageType.Info);

            EditorGUI.BeginChangeCheck();
            _cut = (VoxelBlueprintWeaponPartBoxCut)EditorGUILayout.ObjectField("Cut", _cut, typeof(VoxelBlueprintWeaponPartBoxCut), false);
            if (_cut != null)
            {
                _cut.EnsureDefaults();
                _cut.source = (VoxelBlueprintAsset)EditorGUILayout.ObjectField("Weapon blueprint", _cut.source, typeof(VoxelBlueprintAsset), false);
            }

            _instantMeshesExe = EditorGUILayout.TextField("Instant Meshes.exe", _instantMeshesExe);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("默认路径", GUILayout.Width(72f)))
                    _instantMeshesExe = VoxelBlueprintWeaponLowPolyGenUtil.DefaultInstantMeshesExe;
                if (GUILayout.Button("打开切割窗口", GUILayout.Width(96f)) && _cut?.source != null)
                    VoxelBlueprintWeaponPartBoxCutWindow.OpenForWeapon(_cut.source);
            }
            if (EditorGUI.EndChangeCheck())
            {
                if (_cut != null)
                    VoxelBlueprintWeaponPartBoxCutUtil.Persist(_cut);
                ScheduleGenerate();
            }

            EditorGUI.BeginChangeCheck();
            _route = (VoxelBlueprintWeaponLowPolyRoute)EditorGUILayout.EnumPopup("生成路线", _route);
            _targetFaces = EditorGUILayout.IntSlider("目标面数 (IM -f)", _targetFaces, 80, 2500);
            using (new EditorGUI.DisabledScope(_route != VoxelBlueprintWeaponLowPolyRoute.InstantMeshes))
            {
                _creaseAngle = EditorGUILayout.Slider("折痕角 (IM -c)", _creaseAngle, 10f, 80f);
                _inputSmoothIterations = EditorGUILayout.IntSlider("输入平滑（尖部加重）", _inputSmoothIterations, 0, 8);
                _imSmoothIterations = EditorGUILayout.IntSlider("IM 重投影平滑 (-S)", _imSmoothIterations, 0, 8);
                _pureQuad = EditorGUILayout.Toggle("Pure quad（面数更多）", _pureQuad);
            }
            if (EditorGUI.EndChangeCheck())
            {
                EditorPrefs.SetInt(PrefsTargetFaces, _targetFaces);
                EditorPrefs.SetInt(PrefsRoute, (int)_route);
                EditorPrefs.SetFloat(PrefsCrease, _creaseAngle);
                EditorPrefs.SetBool(PrefsPureQuad, _pureQuad);
                EditorPrefs.SetInt(PrefsInputSmooth, _inputSmoothIterations);
                EditorPrefs.SetInt(PrefsImSmooth, _imSmoothIterations);
                if (_cut != null)
                    VoxelBlueprintWeaponPartBoxCutUtil.Persist(_cut);
                ScheduleGenerate();
            }

            using (new EditorGUI.DisabledScope(_generating || _cut == null || _cut.source == null))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(_generating ? "生成中..." : "立即生成", GUILayout.Height(28f)))
                        GenerateNow();
                    if (GUILayout.Button("导出 Low-Poly 资源", GUILayout.Height(28f)))
                        ExportAssets();
                }
            }

            if (!string.IsNullOrEmpty(_status))
                EditorGUILayout.LabelField(_status, EditorStyles.miniLabel);
            if (!string.IsNullOrEmpty(_error))
                EditorGUILayout.HelpBox(_error, MessageType.Error);

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawSidebarStats();
                using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true)))
                {
                    EditorGUILayout.LabelField("3D Preview", EditorStyles.boldLabel);
                    Rect rect = GUILayoutUtility.GetRect(260f, 520f, GUILayout.ExpandWidth(true), GUILayout.MinHeight(420f));
                    if (VoxelBlueprintPreview3D.DrawOrbitMesh(
                            rect,
                            _preview,
                            _previewMesh,
                            _drawn,
                            _boundsCenter,
                            _boundsRadius,
                            ref _orbitYaw,
                            ref _orbitPitch,
                            ref _orbitDistance,
                            ref _orbitPan,
                            ref _orbitDragging,
                            ref _orbitPanning,
                            null,
                            ref _clickArmed,
                            ref _clickStart,
                            null,
                            ref _marquee,
                            null,
                            "中键旋转 | 右键平移 | 滚轮缩放"))
                        Repaint();
                }
            }
        }

        void DrawSidebarStats()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(220f)))
            {
                EditorGUILayout.LabelField("统计", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Block 三角", _blockFaces.ToString());
                EditorGUILayout.LabelField("Low-Poly 三角", _lastFaces.ToString());
                EditorGUILayout.LabelField("上次耗时", _lastMs + " ms");
                EditorGUILayout.Space(8f);
                EditorGUILayout.LabelField("说明", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("部件图纸在 Part/ 下；改柄后若未切割，请先点「打开切割窗口」→ 开始切割。", EditorStyles.wordWrappedMiniLabel);
            }
        }

        void ScheduleGenerate()
        {
            _pendingGenerate = true;
            _generateAt = EditorApplication.timeSinceStartup + 0.45f;
        }

        void GenerateNow()
        {
            if (_generating || _cut == null || _cut.source == null)
                return;
            _generating = true;
            _error = "";
            EditorPrefs.SetString(PrefsExe, _instantMeshesExe ?? "");
            try
            {
                EditorUtility.DisplayProgressBar("Weapon Low-Poly", "Instant Meshes 重拓扑中...", 0.35f);
                if (!VoxelBlueprintWeaponLowPolyGenUtil.TryGenerate(
                        _cut,
                        _targetFaces,
                        _creaseAngle,
                        _pureQuad,
                        _instantMeshesExe,
                        _inputSmoothIterations,
                        _imSmoothIterations,
                        _route,
                        out VoxelBlueprintWeaponLowPolyGenUtil.LowPolyResult result,
                        out string error))
                {
                    _error = error;
                    _status = "";
                    return;
                }

                SetPreviewMesh(result.mesh, result.blockMesh);
                _blockFaces = result.blockTriangles;
                _lastFaces = result.mesh.triangles.Length / 3;
                _lastMs = result.elapsedMs;
                _status = "OK: target=" + result.targetFaces + ", actual=" + _lastFaces + " tris, " + _lastMs + " ms";
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                _generating = false;
                Repaint();
            }
        }

        void SetPreviewMesh(Mesh lowMesh, Mesh blockMesh)
        {
            DestroyPreviewMeshes();
            _previewMesh = lowMesh;
            _blockMesh = blockMesh;
            _drawn = _previewMesh != null ? _previewMesh.triangles.Length / 3 : 0;
            if (_previewMesh != null)
            {
                _boundsCenter = _previewMesh.bounds.center;
                _boundsRadius = Mathf.Max(0.05f, _previewMesh.bounds.extents.magnitude);
            }
        }

        void ExportAssets()
        {
            if (_previewMesh == null)
            {
                _error = "请先生成预览网格。";
                return;
            }

            string folder = ExportFolder;
            string assetPath = folder + "/GiantSword_FromVoxel_LowPoly_Mesh.asset";
            string objPath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, folder, "GiantSword_FromVoxel_LowPoly_Unity.obj");
            if (!VoxelBlueprintWeaponLowPolyGenUtil.TryExportMeshAsset(_previewMesh, assetPath, out string error))
            {
                _error = error;
                return;
            }

            WavefrontMeshIo.ExportMesh(objPath, _previewMesh);
            AssetDatabase.Refresh();
            _status = "已导出: " + assetPath;
            _error = "";
            EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<Mesh>(assetPath));
        }

        void DestroyPreviewMeshes()
        {
            if (_previewMesh != null)
                DestroyImmediate(_previewMesh);
            if (_blockMesh != null)
                DestroyImmediate(_blockMesh);
            _previewMesh = null;
            _blockMesh = null;
            _drawn = 0;
        }
    }
}
