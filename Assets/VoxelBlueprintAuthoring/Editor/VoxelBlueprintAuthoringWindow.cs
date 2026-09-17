using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Vr3.VoxelBlueprintAuthoring.Editor
{
    enum VoxelBlueprintAuthoringSliceTool
    {
        Paint = 0,
        Select = 1,
        Move = 2
    }

    /// <summary>Standalone drawing, mesh-baking, and preview window for VoxelBlueprintAsset.</summary>
    public sealed class VoxelBlueprintAuthoringWindow : EditorWindow
    {
        VoxelBlueprintAsset _asset;
        GameObject _meshSource;
        int _sliceAxis = 2;
        int _sliceIndex;
        Color _paintColor = new Color(0.67f, 0.78f, 0.88f, 1f);
        byte _paintValue = 4;
        bool _erase;
        float _yaw = 215f;
        float _pitch = 22f;
        float _distance;
        Vector2 _orbitPan;
        bool _orbitDragging;
        bool _orbitPanning;
        PreviewRenderUtility _preview;
        Mesh _previewMesh;
        Mesh _selectionMesh;
        Material _selectionMaterial;
        Vector2 _scroll;
        bool _previewDirty = true;
        int _previewDrawn;
        int _selectionDrawn;
        Vector3 _previewBoundsCenter;
        float _previewBoundsRadius = 1f;
        readonly HashSet<Vector3Int> _selection = new HashSet<Vector3Int>();
        VoxelBlueprintAuthoringSliceTool _sliceTool = VoxelBlueprintAuthoringSliceTool.Paint;
        bool _pastePending;
        bool _sliceClickArmed;
        bool _sliceMarquee;
        Vector2 _sliceClickStart;
        Rect _sliceAreaRect;
        bool _sliceMoveDragging;
        Vector2Int _sliceMoveStartUv;
        Vector2Int _sliceMoveDeltaUv;
        string _editStatus = "";

        [MenuItem("Tools/Voxel Blueprint Authoring/Open")]
        static void Open() => ShowWindow(null);

        public static void ShowWindow(VoxelBlueprintAsset asset)
        {
            VoxelBlueprintAuthoringWindow window = GetWindow<VoxelBlueprintAuthoringWindow>("Voxel Blueprint Authoring");
            window._asset = asset;
            window._previewDirty = true;
            window._distance = 0f;
            window.ClampSlice();
            window.Show();
        }

        void OnEnable()
        {
            _preview = new PreviewRenderUtility();
            Undo.undoRedoPerformed += InvalidatePreview;
        }
        void OnDisable()
        {
            Undo.undoRedoPerformed -= InvalidatePreview;
            _preview?.Cleanup();
            if (_previewMesh != null) DestroyImmediate(_previewMesh);
            if (_selectionMesh != null) DestroyImmediate(_selectionMesh);
            if (_selectionMaterial != null) DestroyImmediate(_selectionMaterial);
        }

        void OnGUI()
        {
            EditorGUILayout.LabelField("Portable Voxel Blueprint Authoring", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("This window is self-contained: paint slices, bake readable meshes, and inspect a 3D preview. It does not alter scenes, prefabs, or gameplay objects.", MessageType.Info);
            EditorGUI.BeginChangeCheck();
            _asset = (VoxelBlueprintAsset)EditorGUILayout.ObjectField("Blueprint", _asset, typeof(VoxelBlueprintAsset), false);
            if (EditorGUI.EndChangeCheck()) { _selection.Clear(); _pastePending = false; ClampSlice(); InvalidatePreview(); }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Create Blueprint")) CreateAsset();
                using (new EditorGUI.DisabledScope(_asset == null))
                {
                    if (GUILayout.Button("Select")) Selection.activeObject = _asset;
                    if (GUILayout.Button("Clear")) Change("Clear Voxel Blueprint", () => _asset.Clear());
                }
            }
            if (_asset == null) return;

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Import .vox Into Blueprint")) ImportVox();
                if (GUILayout.Button("Export Blueprint As .vox")) ExportVox();
            }

            DrawSettings();
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(300f), GUILayout.MinWidth(260f)))
                    DrawPreview();
                using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true)))
                    DrawPainter();
            }
            EditorGUILayout.EndScrollView();
        }

        void DrawSettings()
        {
            EditorGUILayout.Space(4f);
            int x = EditorGUILayout.IntField("Size X", _asset.sizeX);
            int y = EditorGUILayout.IntField("Size Y", _asset.sizeY);
            int z = EditorGUILayout.IntField("Size Z", _asset.sizeZ);
            if (x != _asset.sizeX || y != _asset.sizeY || z != _asset.sizeZ)
                Change("Resize Voxel Blueprint", () => _asset.Resize(x, y, z));

            _meshSource = (GameObject)EditorGUILayout.ObjectField("Mesh Source", _meshSource, typeof(GameObject), true);
            using (new EditorGUI.DisabledScope(_meshSource == null))
                if (GUILayout.Button("Bake Mesh Surface Into Blueprint")) BakeMeshSurface();
            DrawOpenGameArtSword01PipelineButtons();
        }

        void DrawOpenGameArtSword01PipelineButtons()
        {
            if (_asset == null)
                return;
            string path = AssetDatabase.GetAssetPath(_asset);
            bool openGameArt = path == VoxelBlueprintFbxSurfaceImportUtil.BlueprintAssetPath;
            bool ironSword = VoxelBlueprintVr3IronSwordImportUtil.IsIronSwordBlueprintAsset(path);
            if (!openGameArt && !ironSword)
                return;

            EditorGUILayout.Space(4f);
            if (GUILayout.Button("从模型重新生成图纸"))
            {
                if (openGameArt)
                    ReimportOpenGameArtSword01FromModel();
                else
                    ReimportVr3IronSwordFromModel();
            }
        }

        void ReimportOpenGameArtSword01FromModel()
        {
            if (!EditorUtility.DisplayDialog(
                    "Voxel Blueprint",
                    "将用 Sword01.fbx 覆盖当前图纸上的全部体素，手改内容会丢失。继续？",
                    "继续",
                    "取消"))
                return;

            VoxelBlueprintFbxSurfaceImportUtil.ImportSword01Preview(true, false);
            _asset = AssetDatabase.LoadAssetAtPath<VoxelBlueprintAsset>(VoxelBlueprintFbxSurfaceImportUtil.BlueprintAssetPath);
            _selection.Clear();
            _pastePending = false;
            ClampSlice();
            InvalidatePreview();
        }

        void ReimportVr3IronSwordFromModel()
        {
            if (!EditorUtility.DisplayDialog(
                    "Voxel Blueprint",
                    "将用 Sword4_FBX.fbx 覆盖当前图纸上的全部体素，手改内容会丢失。继续？",
                    "继续",
                    "取消"))
                return;

            string blueprintPath = AssetDatabase.GetAssetPath(_asset);
            VoxelBlueprintVr3IronSwordImportUtil.ImportIronSwordToBlueprint(blueprintPath, true);
            _asset = AssetDatabase.LoadAssetAtPath<VoxelBlueprintAsset>(blueprintPath);
            _selection.Clear();
            _pastePending = false;
            ClampSlice();
            InvalidatePreview();
        }

        void DrawPainter()
        {
            if (HandleEditKeys())
                Repaint();
            EditorGUILayout.LabelField("Slice Painter", EditorStyles.boldLabel);
            _sliceTool = (VoxelBlueprintAuthoringSliceTool)GUILayout.Toolbar((int)_sliceTool, new[] { "铅笔", "选择", "移动" });
            _sliceAxis = GUILayout.Toolbar(_sliceAxis, new[] { "X", "Y", "Z" });
            ClampSlice();
            _sliceIndex = EditorGUILayout.IntSlider("Slice", _sliceIndex, 0, AxisSize() - 1);
            using (new EditorGUI.DisabledScope(_sliceTool != VoxelBlueprintAuthoringSliceTool.Paint))
            {
                _paintColor = EditorGUILayout.ColorField("Paint Color", _paintColor);
                _paintValue = (byte)Mathf.Clamp(EditorGUILayout.IntField("Voxel Value", _paintValue), 1, 255);
                _erase = EditorGUILayout.ToggleLeft("Erase (or right-click)", _erase);
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(_selection.Count == 0))
                {
                    if (GUILayout.Button("复制", GUILayout.Width(52f)))
                        CopySelection();
                }
                using (new EditorGUI.DisabledScope(VoxelBlueprintAuthoringSelectionUtil.Clipboard == null || VoxelBlueprintAuthoringSelectionUtil.Clipboard.Count == 0))
                {
                    if (GUILayout.Button(_pastePending ? "粘贴中…" : "粘贴", GUILayout.Width(64f)))
                    {
                        _pastePending = true;
                        _editStatus = "在切面上点击粘贴角点";
                    }
                }
                using (new EditorGUI.DisabledScope(_selection.Count == 0))
                {
                    if (GUILayout.Button("清空选区", GUILayout.Width(72f)))
                    {
                        _selection.Clear();
                        _editStatus = "";
                        RebuildSelectionMesh();
                        Repaint();
                    }
                }
            }
            if (!string.IsNullOrEmpty(_editStatus))
                EditorGUILayout.LabelField(_editStatus, EditorStyles.miniLabel);
            else if (_selection.Count > 0)
                EditorGUILayout.LabelField("已选 " + _selection.Count + " 格 · Ctrl+C/V/X · Del", EditorStyles.miniLabel);
            DrawSliceGrid();
        }

        void DrawSliceGrid()
        {
            GetSliceDimensions(out int uSize, out int vSize);
            Rect holder = GUILayoutUtility.GetRect(10f, 10f, GUILayout.ExpandWidth(true), GUILayout.MinHeight(360f));
            float cell = Mathf.Clamp(Mathf.Min(holder.width / uSize, holder.height / vSize), 10f, 48f);
            float gridW = uSize * cell;
            float gridH = vSize * cell;
            Rect area = new Rect(holder.x + (holder.width - gridW) * 0.5f, holder.y, gridW, gridH);
            _sliceAreaRect = area;
            EditorGUI.DrawRect(area, new Color(0.12f, 0.12f, 0.13f));
            for (int v = 0; v < vSize; v++)
            for (int u = 0; u < uSize; u++)
            {
                CellAt(u, v, out int x, out int y, out int z);
                _asset.TryGetCell(x, y, z, out VoxelBlueprintAsset.Cell voxel);
                Vector3Int key = new Vector3Int(x, y, z);
                bool selected = _selection.Contains(key);
                Rect r = new Rect(area.x + u * cell, area.y + (vSize - 1 - v) * cell, cell - 1f, cell - 1f);
                Color fill = voxel.enabled ? voxel.color : new Color(0.18f, 0.18f, 0.19f);
                if (selected)
                    fill = Color.Lerp(fill, new Color(1f, 0.85f, 0.2f, 1f), 0.45f);
                EditorGUI.DrawRect(r, fill);
                if (selected)
                    DrawSliceCellBorder(r, new Color(1f, 0.9f, 0.25f, 1f));
            }

            switch (_sliceTool)
            {
                case VoxelBlueprintAuthoringSliceTool.Paint:
                    HandleSlicePaintInput(area, cell, uSize, vSize);
                    EditorGUILayout.LabelField("左键：铅笔 · 右键：橡皮", EditorStyles.miniLabel);
                    break;
                case VoxelBlueprintAuthoringSliceTool.Select:
                    if (HandleSliceSelectInput(area, cell, uSize, vSize))
                        Repaint();
                    if (_sliceMarquee)
                        DrawSliceMarquee(_sliceClickStart, Event.current.mousePosition);
                    EditorGUILayout.LabelField("左键点选/框选 · Shift 加选 · 粘贴模式左键落点", EditorStyles.miniLabel);
                    break;
                case VoxelBlueprintAuthoringSliceTool.Move:
                    if (HandleSliceMoveInput(area, cell, uSize, vSize))
                        Repaint();
                    EditorGUILayout.LabelField("在已选格上左键拖动平移（仅当前切面 UV）", EditorStyles.miniLabel);
                    break;
            }
        }

        static void DrawSliceCellBorder(Rect r, Color color)
        {
            EditorGUI.DrawRect(new Rect(r.xMin, r.yMin, r.width, 1f), color);
            EditorGUI.DrawRect(new Rect(r.xMin, r.yMax - 1f, r.width, 1f), color);
            EditorGUI.DrawRect(new Rect(r.xMin, r.yMin, 1f, r.height), color);
            EditorGUI.DrawRect(new Rect(r.xMax - 1f, r.yMin, 1f, r.height), color);
        }

        static void DrawSliceMarquee(Vector2 a, Vector2 b)
        {
            float x = Mathf.Min(a.x, b.x);
            float y = Mathf.Min(a.y, b.y);
            Rect box = new Rect(x, y, Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));
            EditorGUI.DrawRect(box, new Color(0.25f, 0.55f, 1f, 0.18f));
            DrawSliceCellBorder(box, new Color(0.35f, 0.7f, 1f, 0.9f));
        }

        void HandleSlicePaintInput(Rect area, float cell, int uSize, int vSize)
        {
            Event e = Event.current;
            for (int v = 0; v < vSize; v++)
            for (int u = 0; u < uSize; u++)
            {
                Rect r = SliceCellRect(area, cell, uSize, vSize, u, v);
                if (!r.Contains(e.mousePosition))
                    continue;
                if ((e.type != EventType.MouseDown && e.type != EventType.MouseDrag) || (e.button != 0 && e.button != 1))
                    continue;
                CellAt(u, v, out int x, out int y, out int z);
                bool erase = _erase || e.button == 1;
                Change("Paint Voxel Blueprint", () => _asset.SetCell(x, y, z, !erase, _paintColor, _paintValue));
                e.Use();
                return;
            }
        }

        bool HandleSliceSelectInput(Rect area, float cell, int uSize, int vSize)
        {
            Event e = Event.current;
            const float clickSlop = 4f;
            bool repaint = false;
            bool inside = area.Contains(e.mousePosition);
            if (!inside && !_sliceClickArmed && !_sliceMarquee)
                return false;
            switch (e.type)
            {
                case EventType.MouseDown when e.button == 0:
                    _sliceClickArmed = true;
                    _sliceMarquee = false;
                    _sliceClickStart = e.mousePosition;
                    e.Use();
                    return true;
                case EventType.MouseDrag when e.button == 0 && _sliceClickArmed:
                    if ((e.mousePosition - _sliceClickStart).sqrMagnitude > clickSlop * clickSlop)
                    {
                        _sliceClickArmed = false;
                        _sliceMarquee = true;
                    }
                    e.Use();
                    return true;
                case EventType.MouseUp when e.button == 0:
                    if (_sliceMarquee)
                    {
                        Rect box = SliceGuiMarquee(_sliceClickStart, e.mousePosition);
                        bool add = e.shift;
                        Change("Select Voxels", () => MarqueeSelectSlice(area, cell, uSize, vSize, box, add));
                        _editStatus = "已选 " + _selection.Count + " 格";
                    }
                    else if (_sliceClickArmed)
                    {
                        if (TryPickSliceUv(area, cell, uSize, vSize, e.mousePosition, out int u, out int v))
                        {
                            CellAt(u, v, out int x, out int y, out int z);
                            Vector3Int key = new Vector3Int(x, y, z);
                            if (_pastePending)
                            {
                                Change("Paste Voxel Selection", () =>
                                {
                                    int placed = VoxelBlueprintAuthoringSelectionUtil.PasteClipboard(_asset, key, true);
                                    _editStatus = placed > 0 ? "已粘贴 " + placed + " 格" : "粘贴失败（越界）";
                                });
                                _pastePending = false;
                            }
                            else if (_asset.TryGetCell(x, y, z, out VoxelBlueprintAsset.Cell voxel) && voxel.enabled)
                            {
                                bool add = e.shift;
                                if (add)
                                {
                                    if (_selection.Contains(key))
                                        _selection.Remove(key);
                                    else
                                        _selection.Add(key);
                                }
                                else
                                {
                                    _selection.Clear();
                                    _selection.Add(key);
                                }
                                _editStatus = "已选 " + _selection.Count + " 格";
                                RebuildSelectionMesh();
                            }
                            else if (!e.shift)
                            {
                                _selection.Clear();
                                _editStatus = "";
                                RebuildSelectionMesh();
                            }
                        }
                    }
                    _sliceClickArmed = false;
                    _sliceMarquee = false;
                    e.Use();
                    repaint = true;
                    break;
            }
            return repaint;
        }

        bool HandleSliceMoveInput(Rect area, float cell, int uSize, int vSize)
        {
            if (_selection.Count == 0)
                return false;
            Event e = Event.current;
            if (!area.Contains(e.mousePosition) && e.type != EventType.MouseUp)
                return false;
            switch (e.type)
            {
                case EventType.MouseDown when e.button == 0:
                    if (!TryPickSliceUv(area, cell, uSize, vSize, e.mousePosition, out int u, out int v))
                        return false;
                    CellAt(u, v, out int x, out int y, out int z);
                    if (!_selection.Contains(new Vector3Int(x, y, z)))
                        return false;
                    _sliceMoveDragging = true;
                    _sliceMoveStartUv = new Vector2Int(u, v);
                    _sliceMoveDeltaUv = Vector2Int.zero;
                    e.Use();
                    return true;
                case EventType.MouseDrag when e.button == 0 && _sliceMoveDragging:
                    if (TryPickSliceUv(area, cell, uSize, vSize, e.mousePosition, out int cu, out int cv))
                        _sliceMoveDeltaUv = new Vector2Int(cu - _sliceMoveStartUv.x, cv - _sliceMoveStartUv.y);
                    Vector3Int delta = SliceUvDeltaToGrid(_sliceMoveDeltaUv);
                    _editStatus = delta == Vector3Int.zero
                        ? "拖动目标格以移动选区"
                        : "移动 Δ " + delta.x + "," + delta.y + "," + delta.z;
                    e.Use();
                    return true;
                case EventType.MouseUp when e.button == 0 && _sliceMoveDragging:
                    _sliceMoveDragging = false;
                    Vector3Int move = SliceUvDeltaToGrid(_sliceMoveDeltaUv);
                    if (move != Vector3Int.zero)
                    {
                        Change("Move Voxel Selection", () =>
                        {
                            if (!VoxelBlueprintAuthoringSelectionUtil.TryMoveSelection(_asset, _selection, move))
                                _editStatus = "无法移动（越界或目标被占）";
                            else
                                _editStatus = "已移动选区";
                        });
                        RebuildSelectionMesh();
                    }
                    e.Use();
                    return true;
            }
            return false;
        }

        void MarqueeSelectSlice(Rect area, float cell, int uSize, int vSize, Rect guiSelect, bool addToSelection)
        {
            if (!addToSelection)
                _selection.Clear();
            if (guiSelect.width < 2f && guiSelect.height < 2f)
                return;
            for (int v = 0; v < vSize; v++)
            for (int u = 0; u < uSize; u++)
            {
                CellAt(u, v, out int x, out int y, out int z);
                if (!_asset.TryGetCell(x, y, z, out VoxelBlueprintAsset.Cell voxel) || !voxel.enabled)
                    continue;
                Rect r = SliceCellRect(area, cell, uSize, vSize, u, v);
                if (r.xMax < guiSelect.xMin || r.xMin > guiSelect.xMax || r.yMax < guiSelect.yMin || r.yMin > guiSelect.yMax)
                    continue;
                _selection.Add(new Vector3Int(x, y, z));
            }
            RebuildSelectionMesh();
        }

        Vector3Int SliceUvDeltaToGrid(Vector2Int deltaUv)
        {
            if (_sliceAxis == 0)
                return new Vector3Int(0, deltaUv.x, deltaUv.y);
            if (_sliceAxis == 1)
                return new Vector3Int(deltaUv.x, 0, deltaUv.y);
            return new Vector3Int(deltaUv.x, deltaUv.y, 0);
        }

        static Rect SliceCellRect(Rect area, float cell, int uSize, int vSize, int u, int v) =>
            new Rect(area.x + u * cell, area.y + (vSize - 1 - v) * cell, cell - 1f, cell - 1f);

        static bool TryPickSliceUv(Rect area, float cell, int uSize, int vSize, Vector2 mouse, out int u, out int v)
        {
            u = 0;
            v = 0;
            if (!area.Contains(mouse))
                return false;
            u = Mathf.Clamp(Mathf.FloorToInt((mouse.x - area.x) / cell), 0, uSize - 1);
            int row = Mathf.Clamp(Mathf.FloorToInt((mouse.y - area.y) / cell), 0, vSize - 1);
            v = vSize - 1 - row;
            return true;
        }

        static Rect SliceGuiMarquee(Vector2 a, Vector2 b)
        {
            float x = Mathf.Min(a.x, b.x);
            float y = Mathf.Min(a.y, b.y);
            return new Rect(x, y, Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));
        }

        void DrawPreview()
        {
            EditorGUILayout.LabelField("3D Preview", EditorStyles.boldLabel);
            Rect rect = GUILayoutUtility.GetRect(280f, 420f, GUILayout.ExpandWidth(true));
            EnsurePreviewMesh();
            if (VoxelBlueprintPreview3D.DrawOrbitPreview(
                    rect, _preview, _asset,
                    ref _yaw, ref _pitch, ref _distance,
                    ref _orbitPan, ref _orbitDragging, ref _orbitPanning,
                    DrawSelectionOverlay))
                Repaint();
        }

        void DrawSelectionOverlay(PreviewRenderUtility preview)
        {
            if (_selectionMesh == null || _selectionDrawn <= 0)
                return;
            preview.DrawMesh(_selectionMesh, Vector3.zero, Quaternion.identity, SelectionMaterial(), 0);
        }

        bool HandleEditKeys()
        {
            Event e = Event.current;
            if (e.type != EventType.KeyDown || _asset == null)
                return false;
            bool mod = e.control || e.command;
            if (e.keyCode == KeyCode.Escape)
            {
                _pastePending = false;
                _selection.Clear();
                _editStatus = "";
                RebuildSelectionMesh();
                e.Use();
                return true;
            }
            if (mod && e.keyCode == KeyCode.C)
            {
                CopySelection();
                e.Use();
                return true;
            }
            if (mod && e.keyCode == KeyCode.V)
            {
                _pastePending = VoxelBlueprintAuthoringSelectionUtil.Clipboard != null
                    && VoxelBlueprintAuthoringSelectionUtil.Clipboard.Count > 0;
                _editStatus = _pastePending ? "在切面上点击粘贴角点" : "剪贴板为空";
                e.Use();
                return true;
            }
            if (mod && e.keyCode == KeyCode.X)
            {
                Change("Cut Voxel Selection", () => VoxelBlueprintAuthoringSelectionUtil.CutSelection(_selection, _asset));
                _editStatus = "已剪切 " + (VoxelBlueprintAuthoringSelectionUtil.Clipboard?.Count ?? 0) + " 格";
                RebuildSelectionMesh();
                e.Use();
                return true;
            }
            if ((e.keyCode == KeyCode.Delete || e.keyCode == KeyCode.Backspace) && _selection.Count > 0)
            {
                Change("Delete Voxel Selection", () => VoxelBlueprintAuthoringSelectionUtil.EraseSelection(_selection, _asset));
                _editStatus = "已删除选区";
                RebuildSelectionMesh();
                e.Use();
                return true;
            }
            if (_selection.Count > 0 && !_pastePending
                && (_sliceTool == VoxelBlueprintAuthoringSliceTool.Select || _sliceTool == VoxelBlueprintAuthoringSliceTool.Move))
            {
                Vector3Int nudge = Vector3Int.zero;
                if (e.keyCode == KeyCode.LeftArrow) nudge.x = -1;
                else if (e.keyCode == KeyCode.RightArrow) nudge.x = 1;
                else if (e.keyCode == KeyCode.UpArrow) nudge.y = 1;
                else if (e.keyCode == KeyCode.DownArrow) nudge.y = -1;
                else if (e.keyCode == KeyCode.PageUp) nudge.z = 1;
                else if (e.keyCode == KeyCode.PageDown) nudge.z = -1;
                if (nudge != Vector3Int.zero)
                {
                    Change("Nudge Voxel Selection", () =>
                    {
                        if (!VoxelBlueprintAuthoringSelectionUtil.TryMoveSelection(_asset, _selection, nudge))
                            _editStatus = "无法移动（越界或目标被占）";
                    });
                    RebuildSelectionMesh();
                    e.Use();
                    return true;
                }
            }
            return false;
        }

        void CopySelection()
        {
            VoxelBlueprintAuthoringSelectionUtil.CopySelection(_selection, _asset);
            _editStatus = "已复制 " + (VoxelBlueprintAuthoringSelectionUtil.Clipboard?.Count ?? 0) + " 格";
        }

        void EnsurePreviewMesh()
        {
            if (!_previewDirty && _previewMesh != null)
                return;
            if (_previewMesh == null)
            {
                _previewMesh = new Mesh
                {
                    name = "VoxelBlueprintPreview",
                    hideFlags = HideFlags.HideAndDontSave,
                    indexFormat = IndexFormat.UInt32
                };
                _previewMesh.MarkDynamic();
            }
            if (_selectionMesh == null)
            {
                _selectionMesh = new Mesh
                {
                    name = "VoxelBlueprintSelectionPreview",
                    hideFlags = HideFlags.HideAndDontSave,
                    indexFormat = IndexFormat.UInt32
                };
                _selectionMesh.MarkDynamic();
            }
            _previewDrawn = VoxelBlueprintMeshBuilder.Fill(_asset, _previewMesh, out _previewBoundsCenter, out _previewBoundsRadius);
            RebuildSelectionMesh();
            _previewDirty = false;
        }

        void RebuildSelectionMesh()
        {
            if (_selectionMesh == null || _asset == null)
                return;
            _selectionDrawn = VoxelBlueprintAuthoringSelectionUtil.FillSelectionMesh(_asset, _selection, _selectionMesh, out _, out _);
        }

        Material SelectionMaterial()
        {
            if (_selectionMaterial == null)
            {
                Shader shader = Shader.Find("Hidden/VoxelBlueprint/CubeUnlitTransparent");
                if (shader == null)
                    shader = Shader.Find("Sprites/Default");
                _selectionMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                _selectionMaterial.color = new Color(1f, 0.85f, 0.2f, 0.45f);
            }
            return _selectionMaterial;
        }

        void BakeMeshSurface()
        {
            MeshFilter[] filters = _meshSource.GetComponentsInChildren<MeshFilter>(true);
            Bounds bounds = new Bounds(); bool hasBounds = false;
            foreach (MeshFilter f in filters) if (f.sharedMesh != null) foreach (Vector3 point in f.sharedMesh.vertices) { Vector3 p = _meshSource.transform.worldToLocalMatrix.MultiplyPoint3x4(f.transform.localToWorldMatrix.MultiplyPoint3x4(point)); if (!hasBounds) { bounds = new Bounds(p, Vector3.zero); hasBounds = true; } else bounds.Encapsulate(p); }
            if (!hasBounds) { EditorUtility.DisplayDialog("Voxel Blueprint", "No readable MeshFilter geometry was found.", "OK"); return; }
            Change("Bake Mesh Into Voxel Blueprint", () => { _asset.Clear(); foreach (MeshFilter f in filters) BakeFilter(f, bounds); });
        }

        void BakeFilter(MeshFilter filter, Bounds bounds)
        {
            Mesh mesh = filter.sharedMesh; if (mesh == null || !mesh.isReadable) return;
            Matrix4x4 toRoot = _meshSource.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
            Vector3[] points = mesh.vertices; int[] indices = mesh.triangles;
            for (int i = 0; i + 2 < indices.Length; i += 3) StampTriangle(Remap(toRoot.MultiplyPoint3x4(points[indices[i]]), bounds), Remap(toRoot.MultiplyPoint3x4(points[indices[i+1]]), bounds), Remap(toRoot.MultiplyPoint3x4(points[indices[i+2]]), bounds));
        }
        Vector3 Remap(Vector3 p, Bounds b) => new Vector3((p.x-b.min.x)/Mathf.Max(.0001f,b.size.x)*(_asset.sizeX-2)+1, (p.y-b.min.y)/Mathf.Max(.0001f,b.size.y)*(_asset.sizeY-2)+1, (p.z-b.min.z)/Mathf.Max(.0001f,b.size.z)*(_asset.sizeZ-2)+1);
        void StampTriangle(Vector3 a, Vector3 b, Vector3 c) { int n = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(Vector3.Distance(a,b),Vector3.Distance(b,c),Vector3.Distance(c,a))*2.5f),1,64); for(int row=0;row<=n;row++) for(int col=0;col<=n-row;col++) { float v=row/(float)n,w=col/(float)n; Vector3 p=a*(1-v-w)+b*v+c*w; _asset.SetCell(Mathf.RoundToInt(p.x),Mathf.RoundToInt(p.y),Mathf.RoundToInt(p.z),true,_paintColor,_paintValue); } }

        void CreateAsset()
        {
            string path = EditorUtility.SaveFilePanelInProject("Create Voxel Blueprint", "VoxelBlueprint", "asset", "Choose the blueprint asset path.");
            if (string.IsNullOrEmpty(path)) return;
            _asset = CreateInstance<VoxelBlueprintAsset>(); AssetDatabase.CreateAsset(_asset, path); AssetDatabase.SaveAssets(); Selection.activeObject = _asset;
        }

        void ImportVox()
        {
            string path = EditorUtility.OpenFilePanel("Import MagicaVoxel .vox", Application.dataPath, "vox");
            if (string.IsNullOrEmpty(path)) return;
            try
            {
                Change("Import MagicaVoxel Blueprint", () => MagicaVoxelVoxCodec.Import(path, _asset));
                ClampSlice();
            }
            catch (Exception exception)
            {
                EditorUtility.DisplayDialog("Import .vox", exception.Message, "OK");
            }
        }

        void ExportVox()
        {
            string defaultName = string.IsNullOrWhiteSpace(_asset.name) ? "VoxelBlueprint" : _asset.name;
            string path = EditorUtility.SaveFilePanel("Export MagicaVoxel .vox", Application.dataPath, defaultName, "vox");
            if (string.IsNullOrEmpty(path)) return;
            try { MagicaVoxelVoxCodec.Export(path, _asset); }
            catch (Exception exception) { EditorUtility.DisplayDialog("Export .vox", exception.Message, "OK"); }
        }
        void Change(string label, System.Action action)
        {
            Undo.RecordObject(_asset, label);
            action();
            EditorUtility.SetDirty(_asset);
            VoxelBlueprintPreview3D.Invalidate(_asset);
            InvalidatePreview();
        }

        void InvalidatePreview()
        {
            _previewDirty = true;
            Repaint();
        }
        void ClampSlice() { if (_asset != null) _sliceIndex = Mathf.Clamp(_sliceIndex, 0, AxisSize() - 1); }
        int AxisSize() => _sliceAxis == 0 ? _asset.sizeX : _sliceAxis == 1 ? _asset.sizeY : _asset.sizeZ;
        void GetSliceDimensions(out int u, out int v) { if (_sliceAxis == 0) { u=_asset.sizeY; v=_asset.sizeZ; } else if (_sliceAxis == 1) { u=_asset.sizeX; v=_asset.sizeZ; } else { u=_asset.sizeX; v=_asset.sizeY; } }
        void CellAt(int u, int v, out int x, out int y, out int z) { if (_sliceAxis==0) { x=_sliceIndex;y=u;z=v; } else if (_sliceAxis==1) { x=u;y=_sliceIndex;z=v; } else { x=u;y=v;z=_sliceIndex; } }
    }
}
