using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Vr3.VoxelBlueprintAuthoring.Editor
{
    public sealed class VoxelBlueprintPartWearOffsetWindow : EditorWindow
    {
        const string ArmorPartsFolder = "Assets/VoxelBlueprints/Good/AigeiFantasyRpgArmor_VoxelV06/Part";
        const string ArmorPrefix = "PmUnitVoxelBodyPartStyle_AigeiFantasyRpgArmor_";
        const string ArmorSuffix = "_VoxelV06";
        const string OffsetAssetPath = ArmorPartsFolder + "/AigeiFantasyRpgArmor_VoxelV06_WearOffset.asset";
        const string FleshFolder = "Assets/VoxelBlueprints/Imported3DModels/AIGeneratedHumanoid_UnitParts_FromFullBody_V01";
        const string FleshPrefix = "PmUnitVoxelBodyPartStyle_AIGeneratedHumanoid_";
        const string FleshSuffix = "_V01";
        const string Vr3Res = "D:/SVNRoot/trunk/VR3/Assets/LightGunShooting/Resources/UnitVoxelArmor/AigeiFantasyRpgArmor";
        const string Vr3Temp = "D:/SVNRoot/trunk/VR3/Assets/Temp";

        VoxelBlueprintPartWearOffset _offset;
        int _selected;
        PreviewRenderUtility _preview;
        Mesh _fleshMesh;
        Mesh _armorMesh;
        Material _fleshMat;
        Material _armorMat;
        VoxelBlueprintAsset _fleshAsset;
        VoxelBlueprintAsset _armorAsset;
        int _fleshDrawn;
        int _armorDrawn;
        Vector3 _armorOcc;
        Vector3 _builtClipOffset;
        float _builtClipScale;
        bool _clipMeshReady;
        string _loadedLabel;
        float _yaw = 215f;
        float _pitch = 22f;
        float _distance;
        Vector2 _pan;
        bool _orbit;
        bool _panning;
        bool _dragging;

        [MenuItem("TuZhi/Unit/Armor Wear Offset")]
        public static void OpenMenu()
        {
            Open(null);
        }

        public static void Open(VoxelBlueprintAsset context)
        {
            VoxelBlueprintPartWearOffsetWindow window = GetWindow<VoxelBlueprintPartWearOffsetWindow>("穿戴偏移");
            window.EnsureOffset();
            if (context != null)
            {
                string name = context.name;
                for (int i = 0; i < VoxelBlueprintPartWearOffset.PartCount; i++)
                {
                    string label = VoxelBlueprintPartWearOffset.Labels[i];
                    if (name.IndexOf("_" + label + "_", System.StringComparison.Ordinal) >= 0
                        || name.EndsWith("_" + label + ArmorSuffix, System.StringComparison.Ordinal))
                    {
                        window._selected = i;
                        break;
                    }
                }
            }
            window.Show();
            window.Focus();
        }

        void OnEnable()
        {
            _preview = new PreviewRenderUtility();
            _fleshMesh = new Mesh { name = "WearOffsetFlesh", hideFlags = HideFlags.HideAndDontSave, indexFormat = IndexFormat.UInt32 };
            _armorMesh = new Mesh { name = "WearOffsetArmor", hideFlags = HideFlags.HideAndDontSave, indexFormat = IndexFormat.UInt32 };
            Shader shader = Shader.Find("Hidden/VoxelBlueprint/CubeUnlit") ?? Shader.Find("Sprites/Default");
            if (shader != null)
            {
                _fleshMat = new Material(shader) { hideFlags = HideFlags.HideAndDontSave, color = new Color(0.35f, 0.35f, 0.38f, 1f) };
                _armorMat = new Material(shader) { hideFlags = HideFlags.HideAndDontSave, color = new Color(0.75f, 0.82f, 0.9f, 1f) };
                if (shader.name == "Sprites/Default")
                {
                    _fleshMat.mainTexture = Texture2D.whiteTexture;
                    _armorMat.mainTexture = Texture2D.whiteTexture;
                }
            }
            EnsureOffset();
        }

        void OnDisable()
        {
            _preview?.Cleanup();
            _preview = null;
            DestroyImmediate(_fleshMesh);
            DestroyImmediate(_armorMesh);
            DestroyImmediate(_fleshMat);
            DestroyImmediate(_armorMat);
            _fleshMesh = null;
            _armorMesh = null;
            _fleshMat = null;
            _armorMat = null;
        }

        void OnGUI()
        {
            EnsureOffset();
            EditorGUILayout.HelpBox("深色=肉体 V01（盔甲挡住的已按游戏穿戴裁切隐藏），浅色=V06 装备。中键旋转，右键平移，滚轮缩放，左键拖装备。偏移单位是格子。缩放是相对该部位当前穿戴格尺寸，每个部位单独。", MessageType.Info);
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawSidebar();
                using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true)))
                {
                    string label = VoxelBlueprintPartWearOffset.Labels[_selected];
                    EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
                    LoadMeshesIfNeeded(label);
                    RebuildClippedFlesh(_offset.Get(label), _offset.GetScale(label));
                    Rect rect = GUILayoutUtility.GetRect(320f, 420f, GUILayout.ExpandWidth(true), GUILayout.MinHeight(360f));
                    if (DrawPreview(rect))
                        Repaint();
                    Vector3 off = _offset.Get(label);
                    float scale = _offset.GetScale(label);
                    EditorGUI.BeginChangeCheck();
                    off = EditorGUILayout.Vector3Field("偏移（格）", off);
                    scale = EditorGUILayout.FloatField("装备缩放", scale);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(_offset, "Wear Offset");
                        _offset.Set(label, off);
                        _offset.SetScale(label, scale);
                        EditorUtility.SetDirty(_offset);
                        _clipMeshReady = false;
                    }
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("保存", GUILayout.Height(28f)))
                            Save();
                        if (GUILayout.Button("清零当前部位", GUILayout.Height(28f)))
                        {
                            Undo.RecordObject(_offset, "Clear Wear Offset");
                            _offset.Set(label, Vector3.zero);
                            _offset.SetScale(label, 1f);
                            EditorUtility.SetDirty(_offset);
                            _clipMeshReady = false;
                        }
                    }
                }
            }
        }

        void DrawSidebar()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(140f)))
            {
                _selected = Mathf.Clamp(_selected, 0, VoxelBlueprintPartWearOffset.PartCount - 1);
                for (int i = 0; i < VoxelBlueprintPartWearOffset.PartCount; i++)
                {
                    string label = VoxelBlueprintPartWearOffset.Labels[i];
                    if (GUILayout.Toggle(_selected == i, label, "Button"))
                        _selected = i;
                }
            }
        }

        void LoadMeshesIfNeeded(string label)
        {
            if (_loadedLabel == label)
                return;
            _loadedLabel = label;
            _clipMeshReady = false;
            _fleshAsset = AssetDatabase.LoadAssetAtPath<VoxelBlueprintAsset>(
                FleshFolder + "/" + FleshPrefix + label + FleshSuffix + ".asset");
            _armorAsset = AssetDatabase.LoadAssetAtPath<VoxelBlueprintAsset>(
                ArmorPartsFolder + "/" + ArmorPrefix + label + ArmorSuffix + ".asset");
            _armorDrawn = _armorAsset != null ? VoxelBlueprintMeshBuilder.Fill(_armorAsset, _armorMesh, out _, out _) : 0;
            OccupiedCenter(_armorAsset, out _armorOcc);
            _distance = 0f;
        }

        void RebuildClippedFlesh(Vector3 offset, float scale)
        {
            if (_clipMeshReady && _builtClipOffset == offset && Mathf.Approximately(_builtClipScale, scale))
                return;
            _clipMeshReady = true;
            _builtClipOffset = offset;
            _builtClipScale = scale;
            if (_fleshAsset == null)
            {
                _fleshDrawn = 0;
                return;
            }

            float s = Mathf.Max(1e-4f, scale);
            Vector3 armorPosCells = -_armorOcc + offset;
            Vector3 fleshGridCenter = new Vector3(_fleshAsset.sizeX, _fleshAsset.sizeY, _fleshAsset.sizeZ) * 0.5f;
            Vector3 armorGridCenter = _armorAsset != null
                ? new Vector3(_armorAsset.sizeX, _armorAsset.sizeY, _armorAsset.sizeZ) * 0.5f
                : Vector3.zero;
            int ax = _armorAsset != null ? _armorAsset.sizeX : 0;
            int ay = _armorAsset != null ? _armorAsset.sizeY : 0;
            int az = _armorAsset != null ? _armorAsset.sizeZ : 0;
            VoxelBlueprintAsset armor = _armorAsset;
            _fleshDrawn = VoxelBlueprintMeshBuilder.Fill(_fleshAsset, _fleshMesh, out _, out _, (x, y, z) =>
            {
                if (armor == null || _armorDrawn <= 0)
                    return true;
                Vector3 fleshWorld = new Vector3(x + 0.5f, y + 0.5f, z + 0.5f) - fleshGridCenter;
                Vector3 p = fleshWorld / s - armorPosCells + armorGridCenter;
                Vector3 o = -armorPosCells + armorGridCenter;
                return VoxelBlueprintArmorFleshClip.ShouldShowFlesh(
                    (ox, oy, oz) => ArmorOccupied(armor, ox, oy, oz),
                    ax,
                    ay,
                    az,
                    o,
                    p);
            });
        }

        static bool ArmorOccupied(VoxelBlueprintAsset armor, int x, int y, int z)
        {
            return armor != null
                && armor.TryGetCell(x, y, z, out VoxelBlueprintAsset.Cell cell)
                && cell.enabled;
        }

        bool DrawPreview(Rect rect)
        {
            if (_preview == null || rect.width < 8f || rect.height < 8f)
                return false;
            if (_fleshDrawn <= 0 && _armorDrawn <= 0)
            {
                EditorGUI.DrawRect(rect, new Color(0.12f, 0.12f, 0.13f, 1f));
                GUI.Label(rect, "没有体素。", EditorStyles.centeredGreyMiniLabel);
                return false;
            }

            float radius = 12f;
            if (_distance <= 0f)
                _distance = radius * 3.2f;
            bool repaint = HandleOrbit(rect);
            string label = VoxelBlueprintPartWearOffset.Labels[_selected];
            Vector3 offset = _offset.Get(label);
            float scale = _offset.GetScale(label);
            Vector3 armorPos = (-_armorOcc + offset) * scale;
            if (HandleArmorDrag(rect, armorPos, label, scale))
            {
                offset = _offset.Get(label);
                scale = _offset.GetScale(label);
                armorPos = (-_armorOcc + offset) * scale;
                RebuildClippedFlesh(offset, scale);
                repaint = true;
            }

            Quaternion orbit = Quaternion.Euler(_pitch, _yaw, 0f);
            Vector3 panTarget = orbit * new Vector3(_pan.x, _pan.y, 0f);
            _preview.camera.transform.position = panTarget + orbit * (Vector3.back * _distance);
            _preview.camera.transform.LookAt(panTarget);
            _preview.camera.nearClipPlane = 0.01f;
            _preview.camera.farClipPlane = _distance + 80f;
            _preview.camera.fieldOfView = 35f;
            _preview.camera.aspect = rect.width / Mathf.Max(1f, rect.height);
            _preview.camera.backgroundColor = new Color(0.12f, 0.12f, 0.13f, 1f);
            if (_preview.lights != null && _preview.lights.Length > 0)
                _preview.lights[0].intensity = 0f;

            _preview.BeginPreview(rect, GUIStyle.none);
            if (_fleshDrawn > 0 && _fleshMat != null)
                _preview.DrawMesh(_fleshMesh, Vector3.zero, Quaternion.identity, _fleshMat, 0);
            if (_armorDrawn > 0 && _armorMat != null)
                _preview.DrawMesh(_armorMesh, Matrix4x4.TRS(armorPos, Quaternion.identity, Vector3.one * scale), _armorMat, 0);
            _preview.Render();
            _preview.EndAndDrawPreview(rect);
            GUI.Label(new Rect(rect.x + 6f, rect.y + 4f, rect.width - 12f, 18f), "中键旋转 | 右键平移 | 左键拖装备", EditorStyles.miniLabel);
            return repaint;
        }

        bool HandleOrbit(Rect rect)
        {
            Event e = Event.current;
            if (!rect.Contains(e.mousePosition) && !_orbit && !_panning && !_dragging)
                return false;
            if (e.rawType == EventType.ScrollWheel)
            {
                _distance += e.delta.y * 0.15f;
                _distance = Mathf.Clamp(_distance, 2f, 200f);
                e.Use();
                return true;
            }
            switch (e.type)
            {
                case EventType.MouseDown when e.button == 2:
                    _orbit = true;
                    e.Use();
                    return true;
                case EventType.MouseDown when e.button == 1:
                    _panning = true;
                    e.Use();
                    return true;
                case EventType.MouseDrag when _orbit && e.button == 2:
                    _yaw += e.delta.x * 0.4f;
                    _pitch = Mathf.Clamp(_pitch - e.delta.y * 0.4f, -89f, 89f);
                    e.Use();
                    return true;
                case EventType.MouseDrag when _panning && e.button == 1:
                    _pan += new Vector2(-e.delta.x, e.delta.y) * 0.02f * (_distance * 0.04f);
                    e.Use();
                    return true;
                case EventType.MouseUp:
                    _orbit = false;
                    _panning = false;
                    break;
            }
            return false;
        }

        bool HandleArmorDrag(Rect rect, Vector3 armorPos, string label, float scale)
        {
            Event e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0 && rect.Contains(e.mousePosition))
            {
                _dragging = true;
                e.Use();
                return true;
            }
            if (!_dragging)
                return false;
            if (e.type == EventType.MouseUp && e.button == 0)
            {
                _dragging = false;
                e.Use();
                return true;
            }
            if (e.type != EventType.MouseDrag || e.button != 0)
                return false;
            Camera cam = _preview.camera;
            Ray prev = VoxelBlueprintPreview3D.GuiRay(cam, rect, e.mousePosition - e.delta);
            Ray now = VoxelBlueprintPreview3D.GuiRay(cam, rect, e.mousePosition);
            Plane plane = new Plane(-cam.transform.forward, armorPos);
            if (!plane.Raycast(prev, out float t0) || !plane.Raycast(now, out float t1))
            {
                e.Use();
                return true;
            }
            Vector3 delta = now.GetPoint(t1) - prev.GetPoint(t0);
            Undo.RecordObject(_offset, "Wear Offset");
            _offset.Set(label, _offset.Get(label) + delta / Mathf.Max(1e-4f, scale));
            EditorUtility.SetDirty(_offset);
            _clipMeshReady = false;
            e.Use();
            return true;
        }

        void EnsureOffset()
        {
            if (_offset != null)
            {
                _offset.Ensure();
                return;
            }
            _offset = AssetDatabase.LoadAssetAtPath<VoxelBlueprintPartWearOffset>(OffsetAssetPath);
            if (_offset != null)
            {
                _offset.Ensure();
                return;
            }
            if (!AssetDatabase.IsValidFolder(ArmorPartsFolder))
                return;
            _offset = CreateInstance<VoxelBlueprintPartWearOffset>();
            _offset.Ensure();
            AssetDatabase.CreateAsset(_offset, OffsetAssetPath);
            AssetDatabase.SaveAssets();
        }

        void Save()
        {
            if (_offset == null)
                return;
            EditorUtility.SetDirty(_offset);
            AssetDatabase.SaveAssets();
            string src = Path.Combine(Directory.GetParent(Application.dataPath).FullName, OffsetAssetPath.Replace('/', Path.DirectorySeparatorChar));
            CopyOffset(src, Vr3Res);
            CopyOffset(src, Vr3Temp);
            Debug.Log("[WearOffset] saved " + OffsetAssetPath);
        }

        static void CopyOffset(string srcAsset, string destFolder)
        {
            if (!File.Exists(srcAsset) || string.IsNullOrEmpty(destFolder))
                return;
            if (!Directory.Exists(destFolder))
                Directory.CreateDirectory(destFolder);
            string name = Path.GetFileName(srcAsset);
            File.Copy(srcAsset, Path.Combine(destFolder, name), true);
            string meta = srcAsset + ".meta";
            if (File.Exists(meta))
                File.Copy(meta, Path.Combine(destFolder, name + ".meta"), true);
        }

        static void OccupiedCenter(VoxelBlueprintAsset blueprint, out Vector3 center)
        {
            center = Vector3.zero;
            if (blueprint == null)
                return;
            int minX = int.MaxValue, minY = int.MaxValue, minZ = int.MaxValue;
            int maxX = int.MinValue, maxY = int.MinValue, maxZ = int.MinValue;
            for (int z = 0; z < blueprint.sizeZ; z++)
            for (int y = 0; y < blueprint.sizeY; y++)
            for (int x = 0; x < blueprint.sizeX; x++)
            {
                if (!blueprint.TryGetCell(x, y, z, out VoxelBlueprintAsset.Cell cell) || !cell.enabled)
                    continue;
                if (x < minX) minX = x;
                if (y < minY) minY = y;
                if (z < minZ) minZ = z;
                if (x > maxX) maxX = x;
                if (y > maxY) maxY = y;
                if (z > maxZ) maxZ = z;
            }
            if (minX == int.MaxValue)
                return;
            Vector3 gridCenter = new Vector3(blueprint.sizeX, blueprint.sizeY, blueprint.sizeZ) * 0.5f;
            center = new Vector3(
                (minX + maxX + 1) * 0.5f,
                (minY + maxY + 1) * 0.5f,
                (minZ + maxZ + 1) * 0.5f) - gridCenter;
        }
    }
}
