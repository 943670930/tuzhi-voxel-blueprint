using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Vr3.VoxelBlueprintAuthoring.Editor
{
    internal static class VoxelBlueprintPreview3D
    {
        static Mesh _voxelMesh;
        static Material _voxelMaterial;
        static VoxelBlueprintAsset _cachedAsset;
        static Hash128 _cachedHash;
        static bool _meshDirty = true;
        static int _drawn;
        static Vector3 _boundsCenter;
        static float _boundsRadius = 1f;

        internal static void Invalidate(VoxelBlueprintAsset asset)
        {
            _meshDirty = true;
        }

        internal static bool DrawOrbitPreview(
            Rect rect, PreviewRenderUtility preview, VoxelBlueprintAsset asset,
            ref float orbitYaw, ref float orbitPitch, ref float orbitDistance,
            ref Vector2 orbitPan, ref bool isDragging, ref bool isPanning,
            Action<PreviewRenderUtility> extraDraw = null, bool suppressLmbOrbit = false)
        {
            if (asset == null || preview == null || rect.width < 8f || rect.height < 8f)
                return false;
            int drawn = EnsurePreviewMesh(asset);
            if (drawn <= 0)
            {
                EditorGUI.DrawRect(rect, new Color(.12f, .12f, .13f, 1f));
                GUI.Label(rect, "No voxels in this blueprint.", EditorStyles.centeredGreyMiniLabel);
                return false;
            }

            if (orbitDistance <= 0f) orbitDistance = _boundsRadius * 3.2f;
            bool needsRepaint = HandleOrbitInput(rect, orbitDistance, ref orbitYaw, ref orbitPitch, ref orbitDistance, ref orbitPan, ref isDragging, ref isPanning, suppressLmbOrbit);
            orbitDistance = Mathf.Clamp(orbitDistance, _boundsRadius * 0.35f, _boundsRadius * 80f);

            Quaternion orbit = Quaternion.Euler(orbitPitch, orbitYaw, 0f);
            Vector3 panTarget = _boundsCenter + orbit * new Vector3(orbitPan.x, orbitPan.y, 0f);
            preview.camera.transform.position = panTarget + orbit * (Vector3.back * orbitDistance);
            preview.camera.transform.LookAt(panTarget);
            preview.camera.nearClipPlane = .01f;
            preview.camera.farClipPlane = orbitDistance + _boundsRadius * 4f;
            preview.camera.fieldOfView = 35f;
            preview.camera.backgroundColor = new Color(.12f, .12f, .13f, 1f);
            if (preview.lights != null && preview.lights.Length > 0)
                preview.lights[0].intensity = 0f;

            preview.BeginPreview(rect, GUIStyle.none);
            if (drawn > 0 && TryGetPreviewMaterial(out Material material))
            {
                preview.DrawMesh(_voxelMesh, Vector3.zero, Quaternion.identity, material, 0);
                extraDraw?.Invoke(preview);
                preview.Render();
            }
            preview.EndAndDrawPreview(rect);
            GUI.Label(new Rect(rect.x + 6f, rect.y + 4f, rect.width - 12f, 18f), "中键拖：旋转 | 右键拖：平移 | 滚轮：缩放", EditorStyles.miniLabel);
            return needsRepaint;
        }

        internal static bool DrawOrbitMesh(
            Rect rect, PreviewRenderUtility preview, Mesh mesh, int drawn,
            Vector3 boundsCenter, float boundsRadius,
            ref float orbitYaw, ref float orbitPitch, ref float orbitDistance,
            ref Vector2 orbitPan, ref bool isDragging, ref bool isPanning,
            Action<PreviewRenderUtility> extraDraw,
            ref bool clickArmed, ref Vector2 clickStart, Action<Camera, Vector2> onClick,
            ref bool marqueeActive, Action<Camera, Rect> onMarquee, string hintText = null)
        {
            if (preview == null || rect.width < 8f || rect.height < 8f)
                return false;
            bool empty = mesh == null || drawn <= 0;
            if (empty && extraDraw == null)
            {
                EditorGUI.DrawRect(rect, new Color(.12f, .12f, .13f, 1f));
                GUI.Label(rect, "当前盒子里没有体素。", EditorStyles.centeredGreyMiniLabel);
                return false;
            }
            if (boundsRadius < 0.01f)
                boundsRadius = 1f;
            if (orbitDistance <= 0f) orbitDistance = boundsRadius * 3.2f;
            bool needsRepaint = HandleOrbitInput(
                rect, orbitDistance, ref orbitYaw, ref orbitPitch, ref orbitDistance, ref orbitPan,
                ref isDragging, ref isPanning, ref clickArmed, ref clickStart, out bool clicked,
                false, true, ref marqueeActive, out bool marqueeDone);
            orbitDistance = Mathf.Clamp(orbitDistance, boundsRadius * 0.35f, boundsRadius * 80f);
            Quaternion orbit = Quaternion.Euler(orbitPitch, orbitYaw, 0f);
            Vector3 panTarget = boundsCenter + orbit * new Vector3(orbitPan.x, orbitPan.y, 0f);
            preview.camera.transform.position = panTarget + orbit * (Vector3.back * orbitDistance);
            preview.camera.transform.LookAt(panTarget);
            preview.camera.nearClipPlane = .01f;
            preview.camera.farClipPlane = orbitDistance + boundsRadius * 4f;
            preview.camera.fieldOfView = 35f;
            preview.camera.aspect = rect.width / Mathf.Max(1f, rect.height);
            preview.camera.backgroundColor = new Color(.12f, .12f, .13f, 1f);
            if (preview.lights != null && preview.lights.Length > 0)
                preview.lights[0].intensity = 0f;
            if (clicked && onClick != null)
                onClick(preview.camera, Event.current.mousePosition);
            if (marqueeDone && onMarquee != null)
                onMarquee(preview.camera, GuiMarquee(clickStart, Event.current.mousePosition));
            preview.BeginPreview(rect, GUIStyle.none);
            if (!empty && TryGetPreviewMaterial(out Material material))
                preview.DrawMesh(mesh, Vector3.zero, Quaternion.identity, material, 0);
            extraDraw?.Invoke(preview);
            preview.Render();
            preview.EndAndDrawPreview(rect);
            if (empty)
                GUI.Label(rect, "当前盒子里没有体素。", EditorStyles.centeredGreyMiniLabel);
            if (marqueeActive)
            {
                Rect box = GuiMarquee(clickStart, Event.current.mousePosition);
                Color fill = new Color(0.25f, 0.55f, 1f, 0.18f);
                Color edge = new Color(0.35f, 0.7f, 1f, 0.9f);
                EditorGUI.DrawRect(box, fill);
                DrawMarqueeBorder(box, edge);
            }
            GUI.Label(new Rect(rect.x + 6f, rect.y + 4f, rect.width - 12f, 18f),
                hintText ?? "当前裁剪  左键点/框选画归属  中键旋转  右键平移  +X红 +Y绿 +Z蓝", EditorStyles.miniLabel);
            return needsRepaint || clicked || marqueeDone;
        }

        internal static Ray GuiRay(Camera camera, Rect rect, Vector2 gui)
        {
            float w = Mathf.Max(1f, rect.width);
            float h = Mathf.Max(1f, rect.height);
            float u = (gui.x - rect.x) / w;
            float v = 1f - (gui.y - rect.y) / h;
            float aspect = w / h;
            float tan = Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            Vector3 local = new Vector3((u * 2f - 1f) * tan * aspect, (v * 2f - 1f) * tan, 1f);
            Vector3 dir = camera.transform.TransformDirection(local.normalized);
            return new Ray(camera.transform.position, dir);
        }

        internal static bool WorldToGui(Camera camera, Rect rect, Vector3 world, out Vector2 gui)
        {
            gui = default;
            Vector3 cam = camera.transform.InverseTransformPoint(world);
            if (cam.z <= 1e-4f)
                return false;
            float w = Mathf.Max(1f, rect.width);
            float h = Mathf.Max(1f, rect.height);
            float tan = Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float u = (cam.x / (cam.z * tan * (w / h)) + 1f) * 0.5f;
            float v = (cam.y / (cam.z * tan) + 1f) * 0.5f;
            gui = new Vector2(rect.x + u * w, rect.y + (1f - v) * h);
            return true;
        }

        static Rect GuiMarquee(Vector2 a, Vector2 b)
        {
            float x = Mathf.Min(a.x, b.x);
            float y = Mathf.Min(a.y, b.y);
            return new Rect(x, y, Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));
        }

        static void DrawMarqueeBorder(Rect box, Color color)
        {
            EditorGUI.DrawRect(new Rect(box.xMin, box.yMin, box.width, 1f), color);
            EditorGUI.DrawRect(new Rect(box.xMin, box.yMax - 1f, box.width, 1f), color);
            EditorGUI.DrawRect(new Rect(box.xMin, box.yMin, 1f, box.height), color);
            EditorGUI.DrawRect(new Rect(box.xMax - 1f, box.yMin, 1f, box.height), color);
        }

        internal static Texture2D RenderStill(VoxelBlueprintAsset asset, int width, int height, float orbitYaw, float orbitPitch)
        {
            if (asset == null || width < 8 || height < 8 || EnsurePreviewMesh(asset) <= 0) return null;
            PreviewRenderUtility preview = new PreviewRenderUtility();
            try
            {
                float distance = _boundsRadius * 3.2f;
                Quaternion orbit = Quaternion.Euler(orbitPitch, orbitYaw, 0f);
                preview.camera.transform.position = _boundsCenter + orbit * (Vector3.back * distance);
                preview.camera.transform.LookAt(_boundsCenter);
                preview.camera.nearClipPlane = .01f;
                preview.camera.farClipPlane = distance + _boundsRadius * 4f;
                preview.camera.fieldOfView = 35f;
                preview.camera.backgroundColor = new Color(.12f, .12f, .13f, 1f);
                preview.camera.clearFlags = CameraClearFlags.SolidColor;
                if (preview.lights != null && preview.lights.Length > 0)
                    preview.lights[0].intensity = 0f;
                preview.BeginStaticPreview(new Rect(0, 0, width, height));
                if (TryGetPreviewMaterial(out Material material)) { preview.DrawMesh(_voxelMesh, Vector3.zero, Quaternion.identity, material, 0); preview.Render(); }
                return preview.EndStaticPreview();
            }
            finally { preview.Cleanup(); }
        }

        static int EnsurePreviewMesh(VoxelBlueprintAsset asset)
        {
            Hash128 hash = default;
            string path = AssetDatabase.GetAssetPath(asset);
            if (!string.IsNullOrEmpty(path))
                hash = AssetDatabase.GetAssetDependencyHash(path);
            if (_cachedAsset == asset && !_meshDirty && _cachedHash.Equals(hash))
                return _drawn;
            _cachedAsset = asset;
            _cachedHash = hash;
            if (_voxelMesh == null)
            {
                _voxelMesh = new Mesh { name = "VoxelBlueprintPreviewMesh", hideFlags = HideFlags.HideAndDontSave, indexFormat = IndexFormat.UInt32 };
                _voxelMesh.MarkDynamic();
            }
            _drawn = VoxelBlueprintMeshBuilder.Fill(asset, _voxelMesh, out _boundsCenter, out _boundsRadius);
            _meshDirty = false;
            return _drawn;
        }

        static bool TryGetPreviewMaterial(out Material material)
        {
            if (_voxelMaterial == null)
            {
                Shader shader = Shader.Find("Hidden/VoxelBlueprint/CubeUnlit");
                if (shader == null) shader = Shader.Find("Sprites/Default");
                if (shader == null) { material = null; return false; }
                _voxelMaterial = new Material(shader) { name = "VoxelBlueprintPreviewMaterial", hideFlags = HideFlags.HideAndDontSave };
                if (shader.name == "Sprites/Default")
                    _voxelMaterial.mainTexture = Texture2D.whiteTexture;
            }
            material = _voxelMaterial; return true;
        }

        static bool HandleOrbitInput(Rect rect, float panDistance, ref float yaw, ref float pitch, ref float distance, ref Vector2 pan, ref bool isDragging, ref bool isPanning, bool suppressLmbOrbit = false)
        {
            bool clickArmed = false;
            Vector2 clickStart = Vector2.zero;
            bool marqueeActive = false;
            return HandleOrbitInput(rect, panDistance, ref yaw, ref pitch, ref distance, ref pan, ref isDragging, ref isPanning, ref clickArmed, ref clickStart, out _, suppressLmbOrbit, false, ref marqueeActive, out _);
        }

        static bool HandleOrbitInput(
            Rect rect, float panDistance, ref float yaw, ref float pitch, ref float distance, ref Vector2 pan,
            ref bool isDragging, ref bool isPanning, ref bool clickArmed, ref Vector2 clickStart, out bool clicked,
            bool suppressLmbOrbit, bool delayLmbForClick, ref bool marqueeActive, out bool marqueeDone)
        {
            clicked = false;
            marqueeDone = false;
            bool repaint = false;
            Event e = Event.current;
            bool lmbHeld = delayLmbForClick && (clickArmed || marqueeActive);
            bool outside = !rect.Contains(e.mousePosition);
            if (outside && !(lmbHeld && e.type == EventType.MouseUp && e.button == 0) && !isDragging && !isPanning)
                return false;
            if (e.rawType == EventType.ScrollWheel)
            {
                float wheel = Mathf.Abs(e.delta.y) >= Mathf.Abs(e.delta.x) ? e.delta.y : e.delta.x;
                bool fast = (e.modifiers & EventModifiers.Shift) != 0 || e.shift;
                distance += wheel * 0.15f * (fast ? 10f : 1f);
                e.Use();
                return true;
            }
            const float clickSlop = 4f;
            switch (e.type)
            {
                case EventType.MouseDown when e.button == 0 && delayLmbForClick:
                    clickArmed = true;
                    marqueeActive = false;
                    clickStart = e.mousePosition;
                    e.Use();
                    repaint = true;
                    break;
                case EventType.MouseDown when e.button == 2:
                    isDragging = true;
                    e.Use();
                    repaint = true;
                    break;
                case EventType.MouseDown when e.button == 1:
                    isPanning = true;
                    e.Use();
                    repaint = true;
                    break;
                case EventType.MouseDrag when e.button == 0 && delayLmbForClick:
                    if (clickArmed && (e.mousePosition - clickStart).sqrMagnitude > clickSlop * clickSlop)
                    {
                        clickArmed = false;
                        marqueeActive = true;
                    }
                    if (marqueeActive)
                    {
                        e.Use();
                        repaint = true;
                    }
                    break;
                case EventType.MouseDrag when e.button == 2 && isDragging:
                    yaw += e.delta.x * .6f;
                    pitch = Mathf.Clamp(pitch - e.delta.y * .6f, -85f, 85f);
                    e.Use();
                    repaint = true;
                    break;
                case EventType.MouseDrag when e.button == 1 && isPanning:
                    pan += new Vector2(-e.delta.x, e.delta.y) * (panDistance * .63f / Mathf.Max(1f, rect.height));
                    e.Use();
                    repaint = true;
                    break;
                case EventType.MouseUp when e.button == 0:
                    if (delayLmbForClick && marqueeActive)
                        marqueeDone = true;
                    else if (delayLmbForClick && clickArmed)
                        clicked = true;
                    clickArmed = false;
                    marqueeActive = false;
                    e.Use();
                    repaint = true;
                    break;
                case EventType.MouseUp when e.button == 2:
                    isDragging = false;
                    e.Use();
                    repaint = true;
                    break;
                case EventType.MouseUp when e.button == 1:
                    isPanning = false;
                    e.Use();
                    repaint = true;
                    break;
            }
            return repaint;
        }
    }
}
