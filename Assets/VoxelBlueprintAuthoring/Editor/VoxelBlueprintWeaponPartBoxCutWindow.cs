using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Vr3.VoxelBlueprintAuthoring.Editor
{
    public sealed class VoxelBlueprintWeaponPartBoxCutWindow : EditorWindow
    {
        internal static readonly Color[] PartColors =
        {
            new Color(1f, 0.85f, 0.2f),
            new Color(0.95f, 0.35f, 0.3f),
            new Color(0.4f, 0.75f, 1f),
            new Color(1f, 0.5f, 0.2f),
            new Color(1f, 0.65f, 0.15f),
            new Color(1f, 0.9f, 0.35f),
            new Color(0.85f, 0.4f, 1f),
            new Color(0.7f, 0.45f, 1f),
            new Color(0.55f, 0.7f, 1f),
            new Color(0.35f, 0.9f, 0.45f),
            new Color(0.25f, 0.75f, 0.55f),
            new Color(0.45f, 0.95f, 0.7f),
            new Color(0.2f, 0.85f, 0.35f),
            new Color(0.15f, 0.65f, 0.45f),
            new Color(0.4f, 0.9f, 0.6f),
        };

        const int LeftoverSelect = -1;
        VoxelBlueprintWeaponPartBoxCut _cut;
        int _selected;
        Vector2 _scroll;
        PreviewRenderUtility _preview;
        PreviewRenderUtility _cropPreview;
        Mesh _cube;
        Mesh _cropMesh;
        Mesh _cropConflictMesh;
        VoxelBlueprintAsset _cropAsset;
        VoxelBlueprintAsset _cropConflictAsset;
        int _cropDrawn;
        int _cropConflictDrawn;
        Vector3Int _cropPackMin;
        Vector3Int _cropPackSize;
        Vector3 _cropCenter;
        float _cropRadius = 1f;
        int _cropPart = -1;
        int _cropStamp;
        int _cropBuiltSliceAxis;
        int _cropBuiltSliceKeep = int.MaxValue;
        bool _pickPaint = true;
        sealed class PaintSnap
        {
            public List<VoxelBlueprintWeaponPartBoxCut.RejectedCells> rejected;
            public List<VoxelBlueprintWeaponPartBoxCut.RejectedCells> claimed;
        }
        readonly List<PaintSnap> _paintUndo = new List<PaintSnap>();
        readonly List<PaintSnap> _paintRedo = new List<PaintSnap>();
        int _sliceAxis = 1;
        int _sliceKeep = int.MaxValue;
        float _cropYaw = 215f;
        float _cropPitch = 22f;
        float _cropDistance;
        Vector2 _cropPan;
        bool _cropDragging;
        bool _cropPanning;
        bool _cropClickArmed;
        Vector2 _cropClickStart;
        bool _cropMarquee;
        Rect _cropRect;
        Material _gizmoMaterial;
        Material _conflictMaterial;
        Material _axisMatX;
        Material _axisMatY;
        Material _axisMatZ;
        int _vecPasteSlot;
        Vector3 _vecPasteValue;
        Vector3 _vecPasteCenter;
        Vector3 _vecPasteSize;
        Vector3 _vecPasteEuler;
        string _status = "";
        int[] _counts = System.Array.Empty<int>();
        string _newPartLabel = VoxelBlueprintWeaponPartBoxCut.DefaultBladeLabel;
        int _ownerStamp;
        bool _ownerCountsReady;
        int _leftover;
        float _orbitYaw = 215f;
        float _orbitPitch = 22f;
        float _orbitDistance;
        Vector2 _orbitPan;
        bool _orbitDragging;
        bool _orbitPanning;
        VoxelBlueprintPartBoxTool _tool = VoxelBlueprintPartBoxTool.Move;
        int _dragId;
        Vector3 _dragAnchor;
        bool _nudgeRedrawPending;
        double _nudgeRedrawAt;
        bool _cropBuilding;
        bool _cropFillPending;
        int _buildStamp;
        int _scanX;
        int _scanY;
        int _scanZ;
        VoxelBlueprintWeaponPartBoxCut.Box _buildBox;
        bool _buildDest;
        bool _buildLeftover;
        bool _buildPartChanged;
        string _buildLabel;
        string _buildMate;
        readonly List<Vector3Int> _buildOwnedPoints = new List<Vector3Int>(512);
        readonly List<Color32> _buildOwnedColors = new List<Color32>(512);
        readonly List<byte> _buildOwnedValues = new List<byte>(512);
        readonly List<Vector3Int> _buildErasePoints = new List<Vector3Int>(128);
        readonly List<Color32> _buildEraseColors = new List<Color32>(128);

        internal static Color ColorOf(int i) => PartColors[Mathf.Abs(i) % PartColors.Length];

        public static void OpenForWeapon(VoxelBlueprintAsset source)
        {
            string title = source != null ? "武器切割: " + source.name : "武器切割";
            VoxelBlueprintWeaponPartBoxCutWindow window = GetWindow<VoxelBlueprintWeaponPartBoxCutWindow>(title);
            window._cut = VoxelBlueprintWeaponPartBoxCutUtil.LoadOrCreateCutFor(source);
            window._selected = 0;
            window._ownerCountsReady = false;
            window.Show();
            window.Focus();
        }

        void OnEnable()
        {
            _preview = new PreviewRenderUtility();
            _cropPreview = new PreviewRenderUtility();
            _cropMesh = new Mesh { name = "BoxCutCrop", hideFlags = HideFlags.HideAndDontSave, indexFormat = IndexFormat.UInt32 };
            _cropConflictMesh = new Mesh { name = "BoxCutCropConflict", hideFlags = HideFlags.HideAndDontSave, indexFormat = IndexFormat.UInt32 };
            _cropAsset = CreateInstance<VoxelBlueprintAsset>();
            _cropAsset.hideFlags = HideFlags.HideAndDontSave;
            _cropConflictAsset = CreateInstance<VoxelBlueprintAsset>();
            _cropConflictAsset.hideFlags = HideFlags.HideAndDontSave;
            _cube = CreateCube();
            Shader shader = Shader.Find("Hidden/Internal-Colored");
            if (shader == null)
                shader = Shader.Find("Sprites/Default");
            _gizmoMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            _gizmoMaterial.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
            _axisMatX = MakeAxisMaterial(shader, new Color(1f, 0.12f, 0.08f));
            _axisMatY = MakeAxisMaterial(shader, new Color(0.15f, 0.95f, 0.2f));
            _axisMatZ = MakeAxisMaterial(shader, new Color(0.2f, 0.4f, 1f));
            Shader conflictShader = Shader.Find("Hidden/VoxelBlueprint/CubeUnlitTransparent");
            if (conflictShader == null)
                conflictShader = Shader.Find("Hidden/VoxelBlueprint/CubeUnlit");
            if (conflictShader == null)
                conflictShader = shader;
            _conflictMaterial = new Material(conflictShader) { hideFlags = HideFlags.HideAndDontSave };
            if (_cut == null)
                _cut = VoxelBlueprintWeaponPartBoxCutUtil.LoadOrCreateCutFor(
                    AssetDatabase.LoadAssetAtPath<VoxelBlueprintAsset>(
                        EditorPrefs.GetString("TuZhi.WeaponPartBoxCut.LastSource", "")));
            if (_cut != null)
                _cut.EnsureDefaults();
            EditorApplication.update += TickNudgeRedraw;
        }

        void OnDisable()
        {
            EditorApplication.update -= TickNudgeRedraw;
            if (_cut != null)
                VoxelBlueprintWeaponPartBoxCutUtil.Persist(_cut);
            _preview?.Cleanup();
            _preview = null;
            _cropPreview?.Cleanup();
            _cropPreview = null;
            if (_cropMesh != null)
                DestroyImmediate(_cropMesh);
            _cropMesh = null;
            if (_cropConflictMesh != null)
                DestroyImmediate(_cropConflictMesh);
            _cropConflictMesh = null;
            if (_cropAsset != null)
                DestroyImmediate(_cropAsset);
            _cropAsset = null;
            if (_cropConflictAsset != null)
                DestroyImmediate(_cropConflictAsset);
            _cropConflictAsset = null;
            if (_gizmoMaterial != null)
                DestroyImmediate(_gizmoMaterial);
            _gizmoMaterial = null;
            if (_axisMatX != null)
                DestroyImmediate(_axisMatX);
            _axisMatX = null;
            if (_axisMatY != null)
                DestroyImmediate(_axisMatY);
            _axisMatY = null;
            if (_axisMatZ != null)
                DestroyImmediate(_axisMatZ);
            _axisMatZ = null;
            if (_conflictMaterial != null)
                DestroyImmediate(_conflictMaterial);
            _conflictMaterial = null;
            if (_cube != null)
                DestroyImmediate(_cube);
            _cube = null;
        }

        void OnGUI()
        {
            if (_cut != null && _cut.source != null)
            {
                _cut.EnsureDefaults();
                RefreshOwnerCounts();
            }
            using (new EditorGUI.DisabledScope(_cut == null || _cut.source == null))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("开始切割", GUILayout.Height(28f)))
                        RunSlice();
                    if (GUILayout.Button("切割并复制到 VR3", GUILayout.Height(28f)))
                        RunSliceAndCopyToVr3();
                }
            }
            if (_cut != null && _leftover > 0)
                EditorGUILayout.HelpBox("还有 " + _leftover + " 格没进任何盒，截取时不会写入这些格。", MessageType.Warning);
            EditorGUILayout.HelpBox(
                "移动：WASD 平移，R 上 F 下。预览中键旋转、右键平移。右侧剥层再画切盒。左键点/框选：铅笔=收回归属，橡皮=放弃归属。列表靠前的部位在盒重叠时优先归属。可自由添加/重命名部位（默认刃、柄）。",
                MessageType.Info);
            EditorGUI.BeginChangeCheck();
            _cut = (VoxelBlueprintWeaponPartBoxCut)EditorGUILayout.ObjectField("Cut", _cut, typeof(VoxelBlueprintWeaponPartBoxCut), false);
            if (_cut == null)
                return;
            _cut.EnsureDefaults();
            _cut.source = (VoxelBlueprintAsset)EditorGUILayout.ObjectField("Weapon blueprint", _cut.source, typeof(VoxelBlueprintAsset), false);
            _cut.weaponItemId = EditorGUILayout.TextField("Weapon Item Id", _cut.weaponItemId ?? "");
            EditorGUILayout.HelpBox(
                "对应 VR3 武器预制体 ItemState.ItemId（Props_Weapon.csv 的 id，例如 giant_sword）。"
                + "「切割并复制到 VR3」会写入 _ItemBindings/{id}.json，并带上 sourcePackMin / assemblyLocalOffsetMeters，拼装时不走样。",
                MessageType.None);
            if (EditorGUI.EndChangeCheck())
                VoxelBlueprintWeaponPartBoxCutUtil.Persist(_cut);
            if (HandlePaintUndoKeys())
                Repaint();
            _tool = (VoxelBlueprintPartBoxTool)GUILayout.Toolbar((int)_tool, new[] { "移动", "旋转", "缩放" });
            HandleWasd();
            RefreshOwnerCounts();

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawPartSidebar();
                using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true)))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true)))
                        {
                            EditorGUILayout.LabelField("3D Preview", EditorStyles.boldLabel);
                            Rect rect = GUILayoutUtility.GetRect(220f, 420f, GUILayout.ExpandWidth(true), GUILayout.MinHeight(360f));
                            bool gizmo = HandlePreview(rect);
                            if (_cut.source != null && VoxelBlueprintPreview3D.DrawOrbitPreview(
                                    rect, _preview, _cut.source,
                                    ref _orbitYaw, ref _orbitPitch, ref _orbitDistance,
                                    ref _orbitPan, ref _orbitDragging, ref _orbitPanning,
                                    DrawBoxes, gizmo || _dragId != 0))
                                Repaint();
                            if (_cut.source != null && _preview != null)
                                DrawWasdHud(rect, _preview.camera);
                            if (gizmo)
                            {
                                _nudgeRedrawPending = false;
                                Repaint();
                            }
                        }
                        using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true)))
                        {
                            EditorGUILayout.LabelField(
                                "当前裁剪  " + CropTitle(), EditorStyles.boldLabel);
                            RebuildCropMesh();
                            DrawSliceBar();
                            Rect cropRect = GUILayoutUtility.GetRect(220f, 420f, GUILayout.ExpandWidth(true), GUILayout.MinHeight(360f));
                            _cropRect = cropRect;
                            if (VoxelBlueprintPreview3D.DrawOrbitMesh(
                                    cropRect, _cropPreview, _cropMesh, _cropDrawn + _cropConflictDrawn,
                                    _cropCenter, _cropRadius,
                                    ref _cropYaw, ref _cropPitch, ref _cropDistance,
                                    ref _cropPan, ref _cropDragging, ref _cropPanning,
                                    DrawCropOverlay,
                                    ref _cropClickArmed, ref _cropClickStart, OnCropClick,
                                    ref _cropMarquee, OnCropMarquee))
                                Repaint();
                            if (_cropPreview != null)
                                DrawWasdHud(cropRect, _cropPreview.camera);
                            if (!ViewingLeftover)
                            {
                            DrawCropColorLegend();
                            using (new EditorGUILayout.HorizontalScope())
                            {
                                _pickPaint = GUILayout.Toggle(_pickPaint, "铅笔");
                                using (new EditorGUI.DisabledScope(_paintUndo.Count == 0))
                                {
                                    if (GUILayout.Button("回滚", GUILayout.Width(48f)))
                                    {
                                        StepPaintUndo();
                                        Repaint();
                                    }
                                }
                                using (new EditorGUI.DisabledScope(_paintRedo.Count == 0))
                                {
                                    if (GUILayout.Button("重做", GUILayout.Width(48f)))
                                    {
                                        StepPaintRedo();
                                        Repaint();
                                    }
                                }
                                using (new EditorGUI.DisabledScope(!HasPaintRejects()))
                                {
                                    if (GUILayout.Button("清空", GUILayout.Width(48f)))
                                    {
                                        ClearPaintRejects();
                                        Repaint();
                                    }
                                }
                            }
                            }
                        }
                    }

                    if (ViewingLeftover)
                    {
                        EditorGUILayout.HelpBox("正在看未进任何盒的格子。点左侧部位或左预览里的盒子再改切盒。", MessageType.Info);
                    }
                    else
                    {
                    int edit = _selected;
                    VoxelBlueprintWeaponPartBoxCut.Box box = _cut.boxes[edit];
                    box.EnsureOriented();
                    EditorGUI.BeginChangeCheck();
                    if (_vecPasteSlot == 4)
                    {
                        box.center = _vecPasteCenter;
                        box.size = _vecPasteSize;
                        box.euler = _vecPasteEuler;
                        _vecPasteSlot = 0;
                        GUI.changed = true;
                    }
                    Vector3 center = BoxVectorField("位置", box.center, 1, box.center, box.size, box.euler);
                    Vector3 size = BoxVectorField("大小", box.size, 2, center, box.size, box.euler);
                    Vector3 euler = BoxVectorField("角度", box.euler, 3, center, size, box.euler);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("复制三项", GUILayout.Width(88f)))
                            EditorGUIUtility.systemCopyBuffer = FormatBoxTriple(center, size, euler);
                        bool canPasteAll = TryParseBoxTriple(EditorGUIUtility.systemCopyBuffer, out _, out _, out _);
                        using (new EditorGUI.DisabledScope(!canPasteAll))
                        {
                            if (GUILayout.Button("粘贴三项", GUILayout.Width(88f)))
                            {
                                if (TryParseBoxTriple(EditorGUIUtility.systemCopyBuffer, out Vector3 pc, out Vector3 ps, out Vector3 pe))
                                {
                                    center = pc;
                                    size = ps;
                                    euler = pe;
                                    GUI.changed = true;
                                }
                            }
                        }
                    }
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(_cut, "Edit Part Box");
                        box.center = center;
                        box.size = Vector3.Max(size, Vector3.one);
                        box.euler = euler;
                        _cut.boxes[edit] = box;
                        EditorUtility.SetDirty(_cut);
                        _nudgeRedrawPending = false;
                    }
                    }

                    if (!string.IsNullOrEmpty(_status))
                        EditorGUILayout.HelpBox(_status, _status.StartsWith("FAIL") ? MessageType.Error : MessageType.Info);
                }
            }
        }

        void RunSliceAndCopyToVr3()
        {
            if (_cut == null || _cut.source == null)
                return;
            _status = VoxelBlueprintWeaponPartBoxCutUtil.SliceAndCopyToVr3(_cut);
            if (_status.StartsWith("FAIL"))
                Debug.LogError(_status);
            else
                Debug.Log(_status);
        }

        void RunSlice()
        {
            if (_cut == null || _cut.source == null)
                return;
            _status = VoxelBlueprintWeaponPartBoxCutUtil.Slice(_cut);
            if (_status.StartsWith("FAIL"))
                Debug.LogError(_status);
            else
                Debug.Log(_status);
        }

        void DrawPartSidebar()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(248f), GUILayout.ExpandHeight(true)))
            {
                _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.ExpandHeight(true));
                Color leftoverPrev = GUI.backgroundColor;
                GUI.backgroundColor = new Color(1f, 0.45f, 0.15f);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Toggle(ViewingLeftover, "未进任何盒", "Button", GUILayout.Width(140f)))
                        _selected = LeftoverSelect;
                    EditorGUILayout.LabelField(_leftover.ToString(), GUILayout.Width(40f));
                }
                GUI.backgroundColor = leftoverPrev;

                int partCount = _cut != null ? _cut.PartCount : 0;
                for (int i = 0; i < partCount; i++)
                {
                    Color prev = GUI.backgroundColor;
                    GUI.backgroundColor = ColorOf(i);
                    string currentLabel = _cut.PartLabel(i);
                    string nextLabel = currentLabel;
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Toggle(_selected == i, currentLabel, "Button", GUILayout.Width(56f)))
                            _selected = i;
                        nextLabel = EditorGUILayout.TextField(currentLabel, GUILayout.Width(72f));
                        int count = i < _counts.Length ? _counts[i] : 0;
                        EditorGUILayout.LabelField(count.ToString(), GUILayout.Width(28f));
                        using (new EditorGUI.DisabledScope(partCount <= 1))
                        {
                            if (GUILayout.Button("删", GUILayout.Width(28f)))
                            {
                                Undo.RecordObject(_cut, "Remove weapon part");
                                if (_cut.TryRemovePart(i))
                                {
                                    EditorUtility.SetDirty(_cut);
                                    _ownerCountsReady = false;
                                    if (_selected >= _cut.PartCount)
                                        _selected = Mathf.Max(0, _cut.PartCount - 1);
                                }
                            }
                        }
                    }
                    if (nextLabel != currentLabel)
                    {
                        Undo.RecordObject(_cut, "Rename weapon part");
                        if (_cut.TryRenamePart(i, nextLabel))
                        {
                            EditorUtility.SetDirty(_cut);
                            _ownerCountsReady = false;
                        }
                    }
                    GUI.backgroundColor = prev;
                }

                EditorGUILayout.Space(6f);
                using (new EditorGUILayout.HorizontalScope())
                {
                    _newPartLabel = EditorGUILayout.TextField(_newPartLabel);
                    if (GUILayout.Button("添加部位", GUILayout.Width(72f)))
                    {
                        Undo.RecordObject(_cut, "Add weapon part");
                        if (_cut.TryAddPart(_newPartLabel, out string assigned))
                        {
                            EditorUtility.SetDirty(_cut);
                            _ownerCountsReady = false;
                            _selected = _cut.PartCount - 1;
                            _newPartLabel = assigned;
                        }
                    }
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("+" + VoxelBlueprintWeaponPartBoxCut.DefaultBladeLabel, GUILayout.Width(40f)))
                        _newPartLabel = VoxelBlueprintWeaponPartBoxCut.DefaultBladeLabel;
                    if (GUILayout.Button("+" + VoxelBlueprintWeaponPartBoxCut.DefaultHandleLabel, GUILayout.Width(40f)))
                        _newPartLabel = VoxelBlueprintWeaponPartBoxCut.DefaultHandleLabel;
                }
                EditorGUILayout.EndScrollView();
            }
        }

        void RefreshOwnerCounts()
        {
            if (NudgeRedrawDeferred() || _cropBuilding || _cut == null)
                return;
            _cut.EnsureDefaults();
            int stamp = _cut.AssignmentStamp();
            if (_ownerCountsReady && stamp == _ownerStamp)
                return;
            _ownerStamp = stamp;
            _ownerCountsReady = true;
            if (_counts == null || _counts.Length != _cut.PartCount)
                _counts = new int[_cut.PartCount];
            VoxelBlueprintWeaponPartBoxCutUtil.CountOwners(_cut, _counts, out _leftover);
            if (_selected >= _cut.PartCount && _selected != LeftoverSelect)
                _selected = Mathf.Max(0, _cut.PartCount - 1);
        }

        bool ViewingLeftover => _selected == LeftoverSelect;

        string CropTitle()
        {
            if (ViewingLeftover)
                return "未进任何盒";
            return PartButtonLabel(_selected);
        }

        Vector3 BoxVectorField(string label, Vector3 value, int slot, Vector3 allCenter, Vector3 allSize, Vector3 allEuler)
        {
            if (_vecPasteSlot == slot)
            {
                value = _vecPasteValue;
                _vecPasteSlot = 0;
                GUI.changed = true;
            }

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, GUILayout.Width(28f));
            value.x = EditorGUILayout.FloatField(value.x, GUILayout.Width(54f));
            value.y = EditorGUILayout.FloatField(value.y, GUILayout.Width(54f));
            value.z = EditorGUILayout.FloatField(value.z, GUILayout.Width(54f));
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            Rect rect = GUILayoutUtility.GetLastRect();
            Event e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 1 && rect.Contains(e.mousePosition))
            {
                e.Use();
                Vector3 copy = value;
                Vector3 c = slot == 1 ? copy : allCenter;
                Vector3 s = slot == 2 ? copy : allSize;
                Vector3 eu = slot == 3 ? copy : allEuler;
                var menu = new GenericMenu();
                menu.AddItem(new GUIContent("复制"), false, () =>
                {
                    EditorGUIUtility.systemCopyBuffer = FormatBoxVector(copy);
                });
                menu.AddItem(new GUIContent("复制三项"), false, () =>
                {
                    EditorGUIUtility.systemCopyBuffer = FormatBoxTriple(c, s, eu);
                });
                if (TryParseBoxVector(EditorGUIUtility.systemCopyBuffer, out Vector3 clip))
                {
                    menu.AddItem(new GUIContent("粘贴"), false, () =>
                    {
                        _vecPasteSlot = slot;
                        _vecPasteValue = clip;
                        Repaint();
                    });
                }
                else
                    menu.AddDisabledItem(new GUIContent("粘贴"));
                if (TryParseBoxTriple(EditorGUIUtility.systemCopyBuffer, out Vector3 pc, out Vector3 ps, out Vector3 pe))
                {
                    menu.AddItem(new GUIContent("粘贴三项"), false, () =>
                    {
                        _vecPasteSlot = 4;
                        _vecPasteCenter = pc;
                        _vecPasteSize = ps;
                        _vecPasteEuler = pe;
                        Repaint();
                    });
                }
                else
                    menu.AddDisabledItem(new GUIContent("粘贴三项"));
                menu.ShowAsContext();
            }
            return value;
        }

        static string FormatBoxVector(Vector3 v)
        {
            return v.x.ToString("G9") + ", " + v.y.ToString("G9") + ", " + v.z.ToString("G9");
        }

        static string FormatBoxTriple(Vector3 center, Vector3 size, Vector3 euler)
        {
            return FormatBoxVector(center) + " | " + FormatBoxVector(size) + " | " + FormatBoxVector(euler);
        }

        static bool TryParseBoxTriple(string text, out Vector3 center, out Vector3 size, out Vector3 euler)
        {
            center = default;
            size = default;
            euler = default;
            if (string.IsNullOrWhiteSpace(text))
                return false;
            string[] groups = text.Split(new[] { '|' }, System.StringSplitOptions.None);
            if (groups.Length != 3)
                return false;
            return TryParseBoxVector(groups[0], out center)
                && TryParseBoxVector(groups[1], out size)
                && TryParseBoxVector(groups[2], out euler);
        }

        static bool TryParseBoxVector(string text, out Vector3 v)
        {
            v = default;
            if (string.IsNullOrWhiteSpace(text))
                return false;
            text = text.Trim();
            if (text.StartsWith("(") && text.EndsWith(")"))
                text = text.Substring(1, text.Length - 2);
            string[] parts = text.Split(new[] { ',', ' ', '\t' }, System.StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 3)
                return false;
            if (!float.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float x)
                && !float.TryParse(parts[0], out x))
                return false;
            if (!float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float y)
                && !float.TryParse(parts[1], out y))
                return false;
            if (!float.TryParse(parts[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float z)
                && !float.TryParse(parts[2], out z))
                return false;
            v = new Vector3(x, y, z);
            return true;
        }

        int EditPartIndex() => ViewingLeftover ? 0 : _selected;

        string PartButtonLabel(int i) => _cut != null ? _cut.PartLabel(i) : "";

        void DrawBoxes(PreviewRenderUtility preview)
        {
            if (_cut == null || _cut.source == null)
                return;
            Vector3 gridCenter = new Vector3(_cut.source.sizeX, _cut.source.sizeY, _cut.source.sizeZ) * 0.5f;
            VoxelBlueprintWeaponPartBoxCutGizmo.DrawBoxes(
                preview, _cut, ViewingLeftover ? LeftoverSelect : EditPartIndex(),
                !ViewingLeftover, gridCenter, _cube, _gizmoMaterial, _tool);
            DrawConflictMesh(preview, ConflictToFullBodyOffset(gridCenter));
        }

        void DrawCropOverlay(PreviewRenderUtility preview)
        {
            DrawConflictMesh(preview, Vector3.zero);
            DrawCropAxes(preview);
        }

        Vector3 ConflictToFullBodyOffset(Vector3 bodyCenter)
        {
            Vector3 packedCenter = new Vector3(_cropPackSize.x, _cropPackSize.y, _cropPackSize.z) * 0.5f;
            return new Vector3(_cropPackMin.x, _cropPackMin.y, _cropPackMin.z) + packedCenter - bodyCenter;
        }

        void DrawConflictMesh(PreviewRenderUtility preview, Vector3 offset)
        {
            if (_cropConflictDrawn <= 0 || _cropConflictMesh == null || _conflictMaterial == null)
                return;
            preview.DrawMesh(_cropConflictMesh, Matrix4x4.TRS(offset, Quaternion.identity, Vector3.one), _conflictMaterial, 0);
        }

        void DrawCropAxes(PreviewRenderUtility preview)
        {
            if (ViewingLeftover)
                return;
            if (_cut == null || _cube == null || _gizmoMaterial == null)
                return;
            VoxelBlueprintWeaponPartBoxCut.Box box = _cut.boxes[EditPartIndex()];
            box.EnsureOriented();
            Quaternion rot = box.Rotation;
            Vector3 origin = _cropCenter;
            float len = Mathf.Max(3f, _cropRadius * 1.35f);
            float thick = Mathf.Max(0.12f, len * 0.035f);
            DrawAxisBar(preview, origin, rot, Vector3.right, len, thick, _axisMatX);
            DrawAxisBar(preview, origin, rot, Vector3.up, len, thick, _axisMatY);
            DrawAxisBar(preview, origin, rot, Vector3.forward, len, thick, _axisMatZ);
        }

        static Material MakeAxisMaterial(Shader shader, Color color)
        {
            Material mat = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            mat.SetInt("_ZTest", (int)CompareFunction.Always);
            mat.color = color;
            mat.SetColor("_Color", color);
            return mat;
        }

        void DrawAxisBar(PreviewRenderUtility preview, Vector3 origin, Quaternion rot, Vector3 localAxis, float len, float thick, Material mat)
        {
            if (mat == null)
                return;
            Vector3 axis = rot * localAxis;
            Vector3 scale = new Vector3(
                Mathf.Abs(localAxis.x) > 0.5f ? len : thick,
                Mathf.Abs(localAxis.y) > 0.5f ? len : thick,
                Mathf.Abs(localAxis.z) > 0.5f ? len : thick);
            preview.DrawMesh(_cube, Matrix4x4.TRS(origin + axis * (len * 0.5f), rot, scale), mat, 0);
        }

        bool HandlePreview(Rect rect)
        {
            if (_cut == null || _cut.source == null || _preview == null || _preview.camera == null)
                return false;
            Vector3 gridCenter = new Vector3(_cut.source.sizeX, _cut.source.sizeY, _cut.source.sizeZ) * 0.5f;
            if (!ViewingLeftover)
            {
                VoxelBlueprintWeaponPartBoxCut.Box box = _cut.boxes[EditPartIndex()];
                bool changed = VoxelBlueprintWeaponPartBoxCutGizmo.TryHandle(
                    rect, _preview.camera, ref box, gridCenter, _tool, ref _dragId, ref _dragAnchor);
                if (changed)
                {
                    Undo.RecordObject(_cut, "Drag Part Box");
                    _cut.boxes[EditPartIndex()] = box;
                    EditorUtility.SetDirty(_cut);
                    return true;
                }
            }
            int other = VoxelBlueprintWeaponPartBoxCutGizmo.HitOtherBox(rect, _preview.camera, _cut, _selected, gridCenter);
            Event e = Event.current;
            if (other >= 0 && other != _selected && _dragId == 0)
            {
                _selected = other;
                e.Use();
                return true;
            }
            return false;
        }

        bool HandleWasd()
        {
            Event e = Event.current;
            if (e.type != EventType.KeyDown || _cut == null)
                return false;
            if (!TryResolveWasdStep(e.keyCode, out Vector3 step))
                return false;
            if (ViewingLeftover)
                return false;
            VoxelBlueprintWeaponPartBoxCut.Box box = _cut.boxes[_selected];
            Undo.RecordObject(_cut, "Nudge Part Box");
            int edit = EditPartIndex();
            box = _cut.boxes[edit];
            if (_tool == VoxelBlueprintPartBoxTool.Scale)
                VoxelBlueprintWeaponPartBoxCutGizmo.NudgeScale(ref box, step, e.shift ? -1f : 1f);
            else
                VoxelBlueprintWeaponPartBoxCutGizmo.NudgeMove(ref box, step.normalized);
            _cut.boxes[edit] = box;
            EditorUtility.SetDirty(_cut);
            ScheduleNudgeRedraw();
            e.Use();
            return true;
        }

        void ScheduleNudgeRedraw()
        {
            _nudgeRedrawPending = true;
            _nudgeRedrawAt = EditorApplication.timeSinceStartup + 2.0;
            AbortCropBuild();
        }

        bool NudgeRedrawDeferred()
        {
            return _nudgeRedrawPending && EditorApplication.timeSinceStartup < _nudgeRedrawAt;
        }

        void AbortCropBuild()
        {
            _cropBuilding = false;
            _cropFillPending = false;
            _scanX = 0;
            _scanY = 0;
            _scanZ = 0;
            _buildOwnedPoints.Clear();
            _buildOwnedColors.Clear();
            _buildOwnedValues.Clear();
            _buildErasePoints.Clear();
            _buildEraseColors.Clear();
        }

        void TickNudgeRedraw()
        {
            if (_nudgeRedrawPending)
            {
                if (EditorApplication.timeSinceStartup < _nudgeRedrawAt)
                    return;
                _nudgeRedrawPending = false;
                BeginCropBuild();
            }
            if (!_cropBuilding)
                return;
            PumpCropBuild();
            Repaint();
        }

        static void DrawCropColorLegend()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            DrawLegendSwatch(new Color(0.7f, 0.7f, 0.7f, 1f), "原色：本部位自然归属（图纸上的盔甲色）");
            DrawLegendSwatch(new Color(160f / 255f, 0f, 1f, 1f), "纯紫：在本盒里，但归了别的部位");
            DrawLegendSwatch(new Color(0.7f, 0.7f, 0.7f, 77f / 255f), "半透明：橡皮放弃，截取时不要这格");
            DrawLegendSwatch(new Color(0.35f, 0.35f, 0.35f, 1f), "变暗：铅笔从别的部位收回归本图");
            EditorGUILayout.EndVertical();
        }

        static void DrawLegendSwatch(Color color, string text)
        {
            EditorGUILayout.BeginHorizontal();
            Rect r = GUILayoutUtility.GetRect(14f, 14f, GUILayout.Width(14f), GUILayout.Height(14f));
            r.y += 2f;
            EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, r.height), new Color(0.15f, 0.15f, 0.15f, 1f));
            EditorGUI.DrawRect(new Rect(r.x + 1f, r.y + 1f, r.width - 2f, r.height - 2f), color);
            EditorGUILayout.LabelField(text, EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
        }

        static readonly (string key, Vector3 dir)[] WasdWorld =
        {
            ("W", Vector3.back),
            ("S", Vector3.forward),
            ("D", Vector3.left),
            ("A", Vector3.right),
            ("R", Vector3.up),
            ("F", Vector3.down),
        };

        static bool TryResolveWasdStep(KeyCode key, out Vector3 step)
        {
            string label = null;
            switch (key)
            {
                case KeyCode.W: label = "W"; break;
                case KeyCode.S: label = "S"; break;
                case KeyCode.A: label = "A"; break;
                case KeyCode.D: label = "D"; break;
                case KeyCode.R: label = "R"; break;
                case KeyCode.F: label = "F"; break;
            }

            if (label == null)
            {
                step = default;
                return false;
            }

            for (int i = 0; i < WasdWorld.Length; i++)
            {
                if (WasdWorld[i].key != label)
                    continue;
                step = WasdWorld[i].dir;
                return true;
            }

            step = default;
            return false;
        }

        static void DrawWasdHud(Rect rect, Camera camera)
        {
            if (camera == null || rect.width < 24f || rect.height < 24f)
                return;
            GUIStyle style = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 14,
            };
            style.normal.textColor = Color.white;
            const float pad = 10f;
            float reach = Mathf.Min(rect.width, rect.height) * 0.5f - 20f;
            if (reach < 12f)
                reach = 12f;
            for (int i = 0; i < WasdWorld.Length; i++)
            {
                Vector3 local = camera.transform.InverseTransformDirection(WasdWorld[i].dir);
                Vector2 screen = new Vector2(local.x, -local.y);
                float xy = screen.magnitude;
                Vector2 pos;
                if (xy < 0.22f)
                {
                    if (local.z >= 0f)
                        continue;
                    pos = rect.center;
                }
                else
                {
                    screen /= xy;
                    pos = rect.center + screen * reach;
                    pos.x = Mathf.Clamp(pos.x, rect.xMin + pad, rect.xMax - pad - 18f);
                    pos.y = Mathf.Clamp(pos.y, rect.yMin + pad, rect.yMax - pad - 18f);
                }
                Rect box = new Rect(pos.x, pos.y, 18f, 18f);
                EditorGUI.DrawRect(box, new Color(0f, 0f, 0f, 0.55f));
                GUI.Label(box, WasdWorld[i].key, style);
            }
        }

        void RebuildCropMesh()
        {
            if (_cropAsset == null || _cropMesh == null || _cropConflictAsset == null || _cropConflictMesh == null
                || _cut == null || _cut.source == null)
                return;
            int stamp = _selected * 17 + _cut.AssignmentStamp();
            stamp = stamp * 31 + _sliceAxis;
            stamp = stamp * 31 + _sliceKeep;
            if (_cropBuilding && stamp == _buildStamp && _cropPart == _selected)
                return;
            if (NudgeRedrawDeferred() && _cropPart == _selected
                && _sliceAxis == _cropBuiltSliceAxis && _sliceKeep == _cropBuiltSliceKeep)
                return;
            if (stamp == _cropStamp && _cropPart == _selected && !_cropBuilding)
                return;
            BeginCropBuild();
        }

        void BeginCropBuild()
        {
            if (_cut == null || _cut.source == null || _cropAsset == null)
                return;
            AbortCropBuild();
            _buildLeftover = ViewingLeftover;
            VoxelBlueprintWeaponPartBoxCut.Box box = default;
            if (!_buildLeftover)
            {
                box = _cut.boxes[EditPartIndex()];
                box.EnsureOriented();
            }
            _buildPartChanged = _cropPart != _selected;
            if (_buildPartChanged)
                _sliceKeep = int.MaxValue;
            int stamp = _selected * 17 + _cut.AssignmentStamp();
            stamp = stamp * 31 + _sliceAxis;
            stamp = stamp * 31 + _sliceKeep;
            _buildStamp = stamp;
            _cropPart = _selected;
            _cropBuiltSliceAxis = _sliceAxis;
            _cropBuiltSliceKeep = _sliceKeep;
            _buildBox = box;
            _buildLabel = _buildLeftover ? "" : _cut.boxes[_selected].label;
            _buildDest = false;
            _buildMate = null;
            _cropBuilding = true;
            _scanX = 0;
            _scanY = 0;
            _scanZ = 0;
        }

        void PumpCropBuild()
        {
            if (!_cropBuilding || _cut == null || _cut.source == null)
                return;
            if (_cropFillPending)
            {
                _cropFillPending = false;
                FinishCropBuild();
                return;
            }
            VoxelBlueprintAsset source = _cut.source;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            while (sw.Elapsed.TotalMilliseconds < 6.0 && _scanZ < source.sizeZ)
            {
                if (source.TryGetCell(_scanX, _scanY, _scanZ, out VoxelBlueprintAsset.Cell cell) && cell.enabled)
                    AccumulateCropCell(source, cell, _scanX, _scanY, _scanZ);
                _scanX++;
                if (_scanX >= source.sizeX)
                {
                    _scanX = 0;
                    _scanY++;
                    if (_scanY >= source.sizeY)
                    {
                        _scanY = 0;
                        _scanZ++;
                    }
                }
            }
            if (_scanZ >= source.sizeZ)
                _cropFillPending = true;
        }

        void AccumulateCropCell(VoxelBlueprintAsset source, VoxelBlueprintAsset.Cell cell, int x, int y, int z)
        {
            Color32 stolenPurple = new Color32(160, 0, 255, 255);
            if (_buildLeftover)
            {
                if (!VoxelBlueprintWeaponPartBoxCutUtil.CountsAsSliceLeftover(_cut, x, y, z))
                    return;
                Color32 color = cell.color;
                color.a = 255;
                _buildOwnedPoints.Add(new Vector3Int(x, y, z));
                _buildOwnedColors.Add(color);
                _buildOwnedValues.Add(cell.value);
                return;
            }
            if (_buildDest)
            {
                if (string.IsNullOrEmpty(_buildMate) || _cut.OwnerOf(x, y, z) != _buildMate)
                    return;
                Vector3Int p = new Vector3Int(source.sizeX - 1 - x, y, z);
                _buildOwnedPoints.Add(p);
                Color32 color = cell.color;
                color.a = 255;
                _buildOwnedColors.Add(color);
                _buildOwnedValues.Add(cell.value);
                return;
            }
            if (!_buildBox.Contains(x, y, z))
                return;
            Vector3Int srcP = new Vector3Int(x, y, z);
            if (_cut.IsRejected(_buildLabel, x, y, z))
            {
                Color32 ghost = cell.color;
                ghost.a = 77;
                _buildErasePoints.Add(srcP);
                _buildEraseColors.Add(ghost);
                return;
            }
            if (CropCellBelongsToSelected(x, y, z, _buildLabel, false))
            {
                Color32 color = cell.color;
                color.a = 255;
                if (_cut.IsClaimed(_buildLabel, x, y, z))
                {
                    color.r = (byte)(color.r / 2);
                    color.g = (byte)(color.g / 2);
                    color.b = (byte)(color.b / 2);
                }
                _buildOwnedPoints.Add(srcP);
                _buildOwnedColors.Add(color);
                _buildOwnedValues.Add(cell.value);
            }
            else
            {
                _buildOwnedPoints.Add(srcP);
                _buildOwnedColors.Add(stolenPurple);
                _buildOwnedValues.Add(1);
            }
        }

        void FinishCropBuild()
        {
            _cropBuilding = false;
            _cropStamp = _buildStamp;
            if (_buildOwnedPoints.Count == 0 && _buildErasePoints.Count == 0)
            {
                _cropMesh.Clear();
                _cropConflictMesh.Clear();
                _cropDrawn = 0;
                _cropConflictDrawn = 0;
                _cropCenter = Vector3.zero;
                _cropRadius = 1f;
                RefreshOwnerCountsNow();
                return;
            }
            Vector3Int min = _buildOwnedPoints.Count > 0 ? _buildOwnedPoints[0] : _buildErasePoints[0];
            Vector3Int max = min;
            for (int i = 0; i < _buildOwnedPoints.Count; i++)
            {
                min = Vector3Int.Min(min, _buildOwnedPoints[i]);
                max = Vector3Int.Max(max, _buildOwnedPoints[i]);
            }
            for (int i = 0; i < _buildErasePoints.Count; i++)
            {
                min = Vector3Int.Min(min, _buildErasePoints[i]);
                max = Vector3Int.Max(max, _buildErasePoints[i]);
            }
            Vector3Int size = max - min + Vector3Int.one;
            _cropPackMin = min;
            _cropPackSize = size;
            _cropAsset.Resize(size.x, size.y, size.z, preserveCells: false);
            _cropAsset.Clear();
            _cropConflictAsset.Resize(size.x, size.y, size.z, preserveCells: false);
            _cropConflictAsset.Clear();
            int keep = SliceKeepLocal(size);
            for (int i = 0; i < _buildOwnedPoints.Count; i++)
            {
                Vector3Int local = _buildOwnedPoints[i] - min;
                if (PackedAxisCoord(local) > keep)
                    continue;
                _cropAsset.SetCell(local.x, local.y, local.z, true, _buildOwnedColors[i], _buildOwnedValues[i]);
            }
            for (int i = 0; i < _buildErasePoints.Count; i++)
            {
                Vector3Int local = _buildErasePoints[i] - min;
                if (PackedAxisCoord(local) > keep)
                    continue;
                _cropConflictAsset.SetCell(local.x, local.y, local.z, true, _buildEraseColors[i], 1);
            }
            _cropDrawn = VoxelBlueprintMeshBuilder.Fill(_cropAsset, _cropMesh, out _cropCenter, out _cropRadius);
            Vector3 conflictCenter;
            float conflictRadius;
            _cropConflictDrawn = VoxelBlueprintMeshBuilder.Fill(_cropConflictAsset, _cropConflictMesh, out conflictCenter, out conflictRadius);
            if (_cropDrawn <= 0)
            {
                _cropCenter = conflictCenter;
                _cropRadius = conflictRadius;
            }
            if (_buildPartChanged)
                _cropDistance = 0f;
            RefreshOwnerCountsNow();
        }

        void RefreshOwnerCountsNow()
        {
            if (_cut == null)
                return;
            int stamp = _cut.AssignmentStamp();
            _ownerStamp = stamp;
            _ownerCountsReady = true;
            VoxelBlueprintWeaponPartBoxCutUtil.CountOwners(_cut, _counts, out _leftover);
        }

        void DrawSliceBar()
        {
            int len = PackedAxisLen();
            using (new EditorGUILayout.HorizontalScope())
            {
                int axis = GUILayout.Toolbar(_sliceAxis, new[] { "剥X", "剥Y", "剥Z" }, GUILayout.Width(180f));
                if (axis != _sliceAxis)
                {
                    _sliceAxis = axis;
                    _sliceKeep = int.MaxValue;
                    _cropStamp = 0;
                }
            }
            if (len < 1)
                return;
            int keep = Mathf.Clamp(_sliceKeep, 0, len - 1);
            if (_sliceKeep == int.MaxValue)
                keep = len - 1;
            EditorGUI.BeginChangeCheck();
            keep = EditorGUILayout.IntSlider("显示到层", keep, 0, len - 1);
            if (EditorGUI.EndChangeCheck())
            {
                _sliceKeep = keep;
                _cropStamp = 0;
            }
        }

        int PackedAxisLen()
        {
            if (_sliceAxis == 0)
                return _cropPackSize.x;
            if (_sliceAxis == 1)
                return _cropPackSize.y;
            return _cropPackSize.z;
        }

        int PackedAxisCoord(Vector3Int local)
        {
            if (_sliceAxis == 0)
                return local.x;
            if (_sliceAxis == 1)
                return local.y;
            return local.z;
        }

        int SliceKeepLocal(Vector3Int size)
        {
            int len = _sliceAxis == 0 ? size.x : (_sliceAxis == 1 ? size.y : size.z);
            if (len < 1)
                return 0;
            if (_sliceKeep == int.MaxValue)
                return len - 1;
            return Mathf.Clamp(_sliceKeep, 0, len - 1);
        }

        bool CropCellBelongsToSelected(int x, int y, int z, string selectedLabel, bool dest)
        {
            _ = dest;
            if (_cut.IsRejected(selectedLabel, x, y, z))
                return false;
            string owner = _cut.OwnerOf(x, y, z);
            return owner == selectedLabel || string.IsNullOrEmpty(owner);
        }

        void OnCropClick(Camera camera, Vector2 guiPos)
        {
            if (ViewingLeftover)
                return;
            if (_cut == null || camera == null || _cropPackSize.x < 1 || _cropRect.width < 2f)
                return;
            if (!TryPickCropCell(camera, guiPos, out Vector3Int cell))
                return;
            PaintCropCell(cell);
        }

        void OnCropMarquee(Camera camera, Rect guiSelect)
        {
            if (ViewingLeftover)
                return;
            if (_cut == null || camera == null || _cropPackSize.x < 1 || _cropRect.width < 2f)
                return;
            if (guiSelect.width < 2f && guiSelect.height < 2f)
                return;
            Vector3 packedCenter = new Vector3(_cropPackSize.x, _cropPackSize.y, _cropPackSize.z) * 0.5f;
            var hits = new List<Vector3Int>();
            for (int z = 0; z < _cropPackSize.z; z++)
            for (int y = 0; y < _cropPackSize.y; y++)
            for (int x = 0; x < _cropPackSize.x; x++)
            {
                if (!PackedEnabled(_cropAsset, x, y, z) && !PackedEnabled(_cropConflictAsset, x, y, z))
                    continue;
                Vector3 world = new Vector3(x + 0.5f, y + 0.5f, z + 0.5f) - packedCenter;
                if (!VoxelBlueprintPreview3D.WorldToGui(camera, _cropRect, world, out Vector2 gui))
                    continue;
                if (gui.x < guiSelect.xMin || gui.x > guiSelect.xMax || gui.y < guiSelect.yMin || gui.y > guiSelect.yMax)
                    continue;
                Vector3Int cell = _cropPackMin + new Vector3Int(x, y, z);
                if (!PaintWouldChange(cell))
                    continue;
                hits.Add(cell);
            }
            if (hits.Count == 0)
                return;
            PushPaintUndo();
            for (int i = 0; i < hits.Count; i++)
                ApplyPaint(hits[i]);
            AfterPaintChange();
        }

        void PaintCropCell(Vector3Int cell)
        {
            if (!PaintWouldChange(cell))
                return;
            PushPaintUndo();
            ApplyPaint(cell);
            AfterPaintChange();
        }

        bool PaintWouldChange(Vector3Int cell)
        {
            string label = _cut.boxes[_selected].label;
            if (_pickPaint)
                return !CropCellBelongsToSelected(cell.x, cell.y, cell.z, label, false);
            return !_cut.IsRejected(label, cell.x, cell.y, cell.z);
        }

        void ApplyPaint(Vector3Int cell)
        {
            string label = _cut.boxes[_selected].label;
            if (!_pickPaint)
            {
                _cut.SetReject(label, cell, true);
                _cut.SetClaim(label, cell, false);
                return;
            }
            _cut.SetReject(label, cell, false);
            _cut.SetClaim(label, cell, true);
        }

        bool HandlePaintUndoKeys()
        {
            Event e = Event.current;
            if (e.type != EventType.KeyDown || _cut == null)
                return false;
            bool mod = e.control || e.command;
            if (!mod)
                return false;
            if (e.keyCode == KeyCode.Z && !e.shift)
            {
                if (_paintUndo.Count == 0)
                    return false;
                StepPaintUndo();
                e.Use();
                return true;
            }
            if (e.keyCode == KeyCode.Y || (e.keyCode == KeyCode.Z && e.shift))
            {
                if (_paintRedo.Count == 0)
                    return false;
                StepPaintRedo();
                e.Use();
                return true;
            }
            return false;
        }

        void PushPaintUndo()
        {
            _paintUndo.Add(ClonePaint(_cut));
            if (_paintUndo.Count > 64)
                _paintUndo.RemoveAt(0);
            _paintRedo.Clear();
        }

        void StepPaintUndo()
        {
            if (_cut == null || _paintUndo.Count == 0)
                return;
            _paintRedo.Add(ClonePaint(_cut));
            RestorePaint(_cut, _paintUndo[_paintUndo.Count - 1]);
            _paintUndo.RemoveAt(_paintUndo.Count - 1);
            AfterPaintChange();
        }

        void StepPaintRedo()
        {
            if (_cut == null || _paintRedo.Count == 0)
                return;
            _paintUndo.Add(ClonePaint(_cut));
            RestorePaint(_cut, _paintRedo[_paintRedo.Count - 1]);
            _paintRedo.RemoveAt(_paintRedo.Count - 1);
            AfterPaintChange();
        }

        void AfterPaintChange()
        {
            EditorUtility.SetDirty(_cut);
            _ownerCountsReady = false;
            _cropStamp = 0;
            _nudgeRedrawPending = false;
        }

        bool HasPaintRejects()
        {
            if (_cut == null)
                return false;
            return HasMarks(_cut.rejected) || HasMarks(_cut.claimed);
        }

        static bool HasMarks(List<VoxelBlueprintWeaponPartBoxCut.RejectedCells> list)
        {
            if (list == null)
                return false;
            for (int i = 0; i < list.Count; i++)
            {
                VoxelBlueprintWeaponPartBoxCut.RejectedCells entry = list[i];
                if (entry != null && entry.cells != null && entry.cells.Count > 0)
                    return true;
            }
            return false;
        }

        void ClearPaintRejects()
        {
            if (_cut == null || !HasPaintRejects())
                return;
            PushPaintUndo();
            _cut.EnsureRejects();
            _cut.EnsureClaims();
            ClearMarks(_cut.rejected);
            ClearMarks(_cut.claimed);
            AfterPaintChange();
        }

        static void ClearMarks(List<VoxelBlueprintWeaponPartBoxCut.RejectedCells> list)
        {
            if (list == null)
                return;
            for (int i = 0; i < list.Count; i++)
            {
                VoxelBlueprintWeaponPartBoxCut.RejectedCells entry = list[i];
                if (entry != null && entry.cells != null)
                    entry.cells.Clear();
            }
        }

        static PaintSnap ClonePaint(VoxelBlueprintWeaponPartBoxCut cut)
        {
            cut.EnsureRejects();
            cut.EnsureClaims();
            return new PaintSnap
            {
                rejected = CloneMarks(cut.rejected),
                claimed = CloneMarks(cut.claimed),
            };
        }

        static List<VoxelBlueprintWeaponPartBoxCut.RejectedCells> CloneMarks(List<VoxelBlueprintWeaponPartBoxCut.RejectedCells> srcList)
        {
            var copy = new List<VoxelBlueprintWeaponPartBoxCut.RejectedCells>(srcList.Count);
            for (int i = 0; i < srcList.Count; i++)
            {
                VoxelBlueprintWeaponPartBoxCut.RejectedCells src = srcList[i];
                var cells = new List<Vector3Int>();
                if (src != null && src.cells != null)
                    cells.AddRange(src.cells);
                copy.Add(new VoxelBlueprintWeaponPartBoxCut.RejectedCells
                {
                    label = src != null ? src.label : null,
                    cells = cells,
                });
            }
            return copy;
        }

        static void RestorePaint(VoxelBlueprintWeaponPartBoxCut cut, PaintSnap snapshot)
        {
            cut.EnsureRejects();
            cut.EnsureClaims();
            if (snapshot == null)
                return;
            RestoreMarks(cut.rejected, snapshot.rejected);
            RestoreMarks(cut.claimed, snapshot.claimed);
        }

        static void RestoreMarks(
            List<VoxelBlueprintWeaponPartBoxCut.RejectedCells> dstList,
            List<VoxelBlueprintWeaponPartBoxCut.RejectedCells> snapshot)
        {
            if (snapshot == null)
                return;
            for (int s = 0; s < snapshot.Count; s++)
            {
                VoxelBlueprintWeaponPartBoxCut.RejectedCells src = snapshot[s];
                if (src == null || string.IsNullOrEmpty(src.label))
                    continue;
                for (int r = 0; r < dstList.Count; r++)
                {
                    VoxelBlueprintWeaponPartBoxCut.RejectedCells dst = dstList[r];
                    if (dst == null || dst.label != src.label)
                        continue;
                    dst.cells = src.cells != null ? new List<Vector3Int>(src.cells) : new List<Vector3Int>();
                    break;
                }
            }
        }

        bool TryPickCropCell(Camera camera, Vector2 guiPos, out Vector3Int sourceCell)
        {
            sourceCell = default;
            Vector2 local = guiPos - _cropRect.min;
            float u = local.x / _cropRect.width;
            float v = 1f - local.y / _cropRect.height;
            if (u < 0f || u > 1f || v < 0f || v > 1f)
                return false;
            Ray ray = VoxelBlueprintPreview3D.GuiRay(camera, _cropRect, guiPos);
            Vector3 packedCenter = new Vector3(_cropPackSize.x, _cropPackSize.y, _cropPackSize.z) * 0.5f;
            Vector3 o = ray.origin + packedCenter;
            Vector3 d = ray.direction;
            if (d.sqrMagnitude < 1e-12f)
                return false;
            d.Normalize();
            float tmin = 0f;
            float tmax = 1e6f;
            if (!ClipRayAabb(o, d, _cropPackSize, ref tmin, ref tmax))
                return false;
            Vector3 p = o + d * (tmin + 1e-4f);
            int x = Mathf.Clamp(Mathf.FloorToInt(p.x), 0, _cropPackSize.x - 1);
            int y = Mathf.Clamp(Mathf.FloorToInt(p.y), 0, _cropPackSize.y - 1);
            int z = Mathf.Clamp(Mathf.FloorToInt(p.z), 0, _cropPackSize.z - 1);
            int stepX = d.x >= 0f ? 1 : -1;
            int stepY = d.y >= 0f ? 1 : -1;
            int stepZ = d.z >= 0f ? 1 : -1;
            float tDeltaX = Mathf.Abs(d.x) < 1e-8f ? 1e30f : Mathf.Abs(1f / d.x);
            float tDeltaY = Mathf.Abs(d.y) < 1e-8f ? 1e30f : Mathf.Abs(1f / d.y);
            float tDeltaZ = Mathf.Abs(d.z) < 1e-8f ? 1e30f : Mathf.Abs(1f / d.z);
            float tMaxX = NextVoxelT(x, stepX, o.x, d.x);
            float tMaxY = NextVoxelT(y, stepY, o.y, d.y);
            float tMaxZ = NextVoxelT(z, stepZ, o.z, d.z);
            int guard = _cropPackSize.x + _cropPackSize.y + _cropPackSize.z + 8;
            for (int i = 0; i < guard; i++)
            {
                if (x >= 0 && x < _cropPackSize.x && y >= 0 && y < _cropPackSize.y && z >= 0 && z < _cropPackSize.z
                    && (PackedEnabled(_cropAsset, x, y, z) || PackedEnabled(_cropConflictAsset, x, y, z)))
                {
                    sourceCell = _cropPackMin + new Vector3Int(x, y, z);
                    return true;
                }
                if (tMaxX < tMaxY)
                {
                    if (tMaxX > tmax + 1e-4f)
                        return false;
                    x += stepX;
                    tMaxX += tDeltaX;
                }
                else if (tMaxY < tMaxZ)
                {
                    if (tMaxY > tmax + 1e-4f)
                        return false;
                    y += stepY;
                    tMaxY += tDeltaY;
                }
                else
                {
                    if (tMaxZ > tmax + 1e-4f)
                        return false;
                    z += stepZ;
                    tMaxZ += tDeltaZ;
                }
            }
            return false;
        }

        static bool PackedEnabled(VoxelBlueprintAsset asset, int x, int y, int z)
        {
            return asset != null && asset.TryGetCell(x, y, z, out VoxelBlueprintAsset.Cell cell) && cell.enabled;
        }

        static float NextVoxelT(int cell, int step, float origin, float dir)
        {
            if (Mathf.Abs(dir) < 1e-8f)
                return 1e30f;
            float boundary = step > 0 ? cell + 1 : cell;
            return (boundary - origin) / dir;
        }

        static bool ClipRayAabb(Vector3 o, Vector3 d, Vector3Int size, ref float tmin, ref float tmax)
        {
            if (!ClipAxis(o.x, d.x, size.x, ref tmin, ref tmax))
                return false;
            if (!ClipAxis(o.y, d.y, size.y, ref tmin, ref tmax))
                return false;
            if (!ClipAxis(o.z, d.z, size.z, ref tmin, ref tmax))
                return false;
            return tmin <= tmax;
        }

        static bool ClipAxis(float origin, float dir, int size, ref float tmin, ref float tmax)
        {
            if (Mathf.Abs(dir) < 1e-8f)
                return origin >= 0f && origin <= size;
            float t1 = (0f - origin) / dir;
            float t2 = (size - origin) / dir;
            if (t1 > t2)
            {
                float tmp = t1;
                t1 = t2;
                t2 = tmp;
            }
            tmin = Mathf.Max(tmin, t1);
            tmax = Mathf.Min(tmax, t2);
            return tmin <= tmax;
        }

        static Mesh CreateCube()
        {
            GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Mesh src = temp.GetComponent<MeshFilter>().sharedMesh;
            Mesh cube = Object.Instantiate(src);
            cube.hideFlags = HideFlags.HideAndDontSave;
            DestroyImmediate(temp);
            return cube;
        }
    }
}
