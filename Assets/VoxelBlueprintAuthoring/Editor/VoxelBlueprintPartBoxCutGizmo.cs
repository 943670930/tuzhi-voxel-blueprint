using UnityEditor;
using UnityEngine;

namespace Vr3.VoxelBlueprintAuthoring.Editor
{
    enum VoxelBlueprintPartBoxTool
    {
        Move,
        Rotate,
        Scale,
    }

    static class VoxelBlueprintPartBoxCutGizmo
    {
        static readonly Vector3[] FaceAxis =
        {
            Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back,
        };

        public static Vector3 GridToMesh(Vector3 grid, Vector3 gridCenter) => grid - gridCenter;

        public static Ray GuiRay(Camera camera, Rect rect, Vector2 gui)
        {
            Vector2 local = gui - rect.position;
            local.y = rect.height - local.y;
            return camera.ScreenPointToRay(local);
        }

        public static void DrawBoxes(
            PreviewRenderUtility preview, VoxelBlueprintPartBoxCut cut, int selected,
            bool onlySelected, Vector3 gridCenter, Mesh cube, Material material, VoxelBlueprintPartBoxTool tool)
        {
            if (cut == null || cube == null || material == null)
                return;
            cut.EnsureBoxes();
            bool prev = GL.wireframe;
            for (int i = 0; i < VoxelBlueprintPartBoxCut.PartCount; i++)
            {
                if (onlySelected && i != selected)
                    continue;
                VoxelBlueprintPartBoxCut.Box box = cut.boxes[i];
                box.EnsureOriented();
                Color color = VoxelBlueprintPartBoxCutWindow.ColorOf(i);
                if (i != selected)
                    color.a = 0.35f;
                material.color = color;
                Matrix4x4 matrix = Matrix4x4.TRS(
                    GridToMesh(box.center, gridCenter), box.Rotation, box.size);
                GL.wireframe = true;
                preview.DrawMesh(cube, matrix, material, 0);
                GL.wireframe = false;
                if (i != selected)
                    continue;
                DrawSelectedHandles(preview, box, gridCenter, cube, material, color, tool);
            }
            GL.wireframe = prev;
        }

        static Mesh _circle;

        static Mesh Circle()
        {
            if (_circle != null)
                return _circle;
            const int n = 48;
            var vertices = new Vector3[n];
            var indices = new int[n * 2];
            for (int i = 0; i < n; i++)
            {
                float a = i / (float)n * Mathf.PI * 2f;
                vertices[i] = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                indices[i * 2] = i;
                indices[i * 2 + 1] = (i + 1) % n;
            }
            _circle = new Mesh { hideFlags = HideFlags.HideAndDontSave };
            _circle.SetVertices(vertices);
            _circle.SetIndices(indices, MeshTopology.Lines, 0);
            return _circle;
        }

        static void DrawSelectedHandles(
            PreviewRenderUtility preview, VoxelBlueprintPartBoxCut.Box box, Vector3 gridCenter,
            Mesh cube, Material material, Color color, VoxelBlueprintPartBoxTool tool)
        {
            Vector3 world = GridToMesh(box.center, gridCenter);
            Quaternion rot = box.Rotation;
            float handle = Mathf.Max(0.45f, box.size.magnitude * 0.04f);
            material.color = color;
            if (tool == VoxelBlueprintPartBoxTool.Scale)
            {
                for (int f = 0; f < 6; f++)
                {
                    Vector3 axis = rot * FaceAxis[f];
                    float ext = Vector3.Dot(box.size, Abs(FaceAxis[f])) * 0.5f;
                    Matrix4x4 m = Matrix4x4.TRS(world + axis * ext, rot, Vector3.one * handle);
                    preview.DrawMesh(cube, m, material, 0);
                }
            }
            else if (tool == VoxelBlueprintPartBoxTool.Move)
            {
                preview.DrawMesh(cube, Matrix4x4.TRS(world, rot, Vector3.one * handle * 1.2f), material, 0);
            }
            else
            {
                float radius = Mathf.Max(box.size.x, box.size.y, box.size.z) * 0.55f;
                Mesh circle = Circle();
                preview.DrawMesh(circle, Matrix4x4.TRS(world, rot, Vector3.one * radius), material, 0);
                preview.DrawMesh(circle, Matrix4x4.TRS(world, rot * Quaternion.Euler(90f, 0f, 0f), Vector3.one * radius), material, 0);
                preview.DrawMesh(circle, Matrix4x4.TRS(world, rot * Quaternion.Euler(0f, 90f, 0f), Vector3.one * radius), material, 0);
            }
        }

        public static bool TryHandle(
            Rect rect, Camera camera, ref VoxelBlueprintPartBoxCut.Box box, Vector3 gridCenter,
            VoxelBlueprintPartBoxTool tool, ref int dragId, ref Vector3 dragAnchor)
        {
            Event e = Event.current;
            if (camera == null || !rect.Contains(e.mousePosition))
                return false;
            box.EnsureOriented();
            Ray ray = GuiRay(camera, rect, e.mousePosition);
            Vector3 world = GridToMesh(box.center, gridCenter);
            Quaternion rot = box.Rotation;
            float pick = HandlePick(camera, box.size.magnitude);
            if (e.type == EventType.MouseDown && e.button == 0)
            {
                int id = Hit(tool, ray, world, rot, box.size, pick);
                if (id == 0)
                    return false;
                dragId = id;
                dragAnchor = ray.origin + ray.direction * 2f;
                if (tool == VoxelBlueprintPartBoxTool.Move)
                    dragAnchor = ProjectOnPlane(ray, world, -camera.transform.forward);
                else if (tool == VoxelBlueprintPartBoxTool.Scale)
                    dragAnchor = ProjectOnAxis(ray, FaceWorld(world, rot, box.size, id - 10), rot * FaceAxis[id - 10]);
                else
                    dragAnchor = ProjectOnPlane(ray, world, rot * FaceAxis[(id - 20) * 2]);
                e.Use();
                return true;
            }
            if (dragId == 0 || e.button != 0)
                return false;
            if (e.type == EventType.MouseUp)
            {
                dragId = 0;
                e.Use();
                return true;
            }
            if (e.type != EventType.MouseDrag)
                return false;
            if (tool == VoxelBlueprintPartBoxTool.Move)
            {
                Vector3 now = ProjectOnPlane(ray, world, -camera.transform.forward);
                box.center += now - dragAnchor;
                dragAnchor = now;
            }
            else if (tool == VoxelBlueprintPartBoxTool.Scale)
            {
                int face = dragId - 10;
                Vector3 axis = rot * FaceAxis[face];
                Vector3 now = ProjectOnAxis(ray, world, axis);
                float delta = Vector3.Dot(now - dragAnchor, axis);
                ApplyFaceScale(ref box, face, delta);
                dragAnchor = now;
            }
            else
            {
                int axisIndex = dragId - 20;
                Vector3 axis = rot * new Vector3(axisIndex == 0 ? 1 : 0, axisIndex == 1 ? 1 : 0, axisIndex == 2 ? 1 : 0);
                Vector3 a = Vector3.ProjectOnPlane(dragAnchor - world, axis);
                Vector3 b = Vector3.ProjectOnPlane(ProjectOnPlane(ray, world, axis) - world, axis);
                if (a.sqrMagnitude > 1e-6f && b.sqrMagnitude > 1e-6f)
                {
                    float angle = Vector3.SignedAngle(a, b, axis);
                    box.euler = (Quaternion.AngleAxis(angle, axis) * box.Rotation).eulerAngles;
                    dragAnchor = ProjectOnPlane(ray, GridToMesh(box.center, gridCenter), axis);
                    world = GridToMesh(box.center, gridCenter);
                }
            }
            e.Use();
            return true;
        }

        public static int HitOtherBox(
            Rect rect, Camera camera, VoxelBlueprintPartBoxCut cut, int selected, Vector3 gridCenter)
        {
            Event e = Event.current;
            if (camera == null || e.type != EventType.MouseDown || e.button != 0 || !rect.Contains(e.mousePosition))
                return -1;
            Ray ray = GuiRay(camera, rect, e.mousePosition);
            float best = 1e9f;
            int hit = -1;
            for (int i = 0; i < VoxelBlueprintPartBoxCut.PartCount; i++)
            {
                VoxelBlueprintPartBoxCut.Box box = cut.boxes[i];
                box.EnsureOriented();
                Vector3 world = GridToMesh(box.center, gridCenter);
                if (!RayObb(ray, world, box.Rotation, box.size, out float t) || t >= best)
                    continue;
                best = t;
                hit = i;
            }
            return hit;
        }

        static int Hit(VoxelBlueprintPartBoxTool tool, Ray ray, Vector3 world, Quaternion rot, Vector3 size, float pick)
        {
            if (tool == VoxelBlueprintPartBoxTool.Scale)
            {
                int best = 0;
                float bestT = 1e9f;
                for (int f = 0; f < 6; f++)
                {
                    Vector3 p = FaceWorld(world, rot, size, f);
                    if (!RaySphere(ray, p, pick, out float t) || t >= bestT)
                        continue;
                    bestT = t;
                    best = 10 + f;
                }
                return best;
            }
            if (tool == VoxelBlueprintPartBoxTool.Rotate)
            {
                int best = 0;
                float bestD = pick * 1.8f;
                float radius = Mathf.Max(size.x, size.y, size.z) * 0.55f;
                Vector3[] axes = { rot * Vector3.right, rot * Vector3.up, rot * Vector3.forward };
                for (int a = 0; a < 3; a++)
                {
                    if (!RayCircle(ray, world, axes[a], radius, out float d) || d >= bestD)
                        continue;
                    bestD = d;
                    best = 20 + a;
                }
                return best;
            }
            return RayObb(ray, world, rot, size, out _) ? 1 : 0;
        }

        public static void NudgeMove(ref VoxelBlueprintPartBoxCut.Box box, Vector3 worldStep)
        {
            box.EnsureOriented();
            box.center += worldStep;
        }

        public static void NudgeScale(ref VoxelBlueprintPartBoxCut.Box box, Vector3 worldDir, float delta)
        {
            box.EnsureOriented();
            if (worldDir.sqrMagnitude < 1e-8f)
                return;
            worldDir.Normalize();
            int face = 0;
            float best = -1e9f;
            Quaternion rot = box.Rotation;
            for (int f = 0; f < 6; f++)
            {
                float d = Vector3.Dot(rot * FaceAxis[f], worldDir);
                if (d <= best)
                    continue;
                best = d;
                face = f;
            }
            ApplyFaceScale(ref box, face, delta);
        }

        static void ApplyFaceScale(ref VoxelBlueprintPartBoxCut.Box box, int face, float delta)
        {
            Vector3 local = FaceAxis[face];
            Vector3 abs = Abs(local);
            float component = Vector3.Dot(box.size, abs);
            float next = Mathf.Max(1f, component + delta);
            float used = next - component;
            box.size += abs * used;
            box.center += box.Rotation * local * (used * 0.5f);
        }

        static Vector3 FaceWorld(Vector3 world, Quaternion rot, Vector3 size, int face)
        {
            Vector3 local = FaceAxis[face];
            float ext = Vector3.Dot(size, Abs(local)) * 0.5f;
            return world + rot * local * ext;
        }

        static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

        static float HandlePick(Camera camera, float size) =>
            Mathf.Max(0.35f, camera != null ? (camera.transform.position.magnitude * 0.012f + size * 0.03f) : 0.5f);

        static Vector3 ProjectOnPlane(Ray ray, Vector3 point, Vector3 normal)
        {
            Plane plane = new Plane(normal.sqrMagnitude < 1e-8f ? Vector3.up : normal.normalized, point);
            if (!plane.Raycast(ray, out float t))
                t = Vector3.Dot(point - ray.origin, ray.direction);
            return ray.origin + ray.direction * t;
        }

        static Vector3 ProjectOnAxis(Ray ray, Vector3 point, Vector3 axis)
        {
            axis = axis.normalized;
            Vector3 to = point - ray.origin;
            float a = Vector3.Dot(ray.direction, axis);
            float b = Vector3.Dot(to, axis);
            float c = Vector3.Dot(ray.direction, ray.direction);
            float d = Vector3.Dot(to, ray.direction);
            float denom = 1f - a * a;
            float t = denom < 1e-6f ? d : (d - b * a) / denom;
            return point + axis * Vector3.Dot(ray.origin + ray.direction * t - point, axis);
        }

        static bool RaySphere(Ray ray, Vector3 center, float radius, out float t)
        {
            Vector3 oc = ray.origin - center;
            float b = Vector3.Dot(oc, ray.direction);
            float c = Vector3.Dot(oc, oc) - radius * radius;
            float disc = b * b - c;
            t = 0f;
            if (disc < 0f)
                return false;
            t = -b - Mathf.Sqrt(disc);
            if (t < 0f)
                t = -b + Mathf.Sqrt(disc);
            return t >= 0f;
        }

        static bool RayCircle(Ray ray, Vector3 center, Vector3 axis, float radius, out float dist)
        {
            Plane plane = new Plane(axis.normalized, center);
            dist = 1e9f;
            if (!plane.Raycast(ray, out float t))
                return false;
            Vector3 p = ray.origin + ray.direction * t;
            dist = Mathf.Abs((p - center).magnitude - radius);
            return dist < radius * 0.18f + 0.4f;
        }

        static bool RayObb(Ray ray, Vector3 center, Quaternion rot, Vector3 size, out float t)
        {
            Quaternion inv = Quaternion.Inverse(rot);
            Vector3 o = inv * (ray.origin - center);
            Vector3 d = inv * ray.direction;
            Vector3 h = size * 0.5f;
            t = 0f;
            float tmin = -1e9f;
            float tmax = 1e9f;
            if (!Slab(o.x, d.x, h.x, ref tmin, ref tmax)) return false;
            if (!Slab(o.y, d.y, h.y, ref tmin, ref tmax)) return false;
            if (!Slab(o.z, d.z, h.z, ref tmin, ref tmax)) return false;
            t = tmin >= 0f ? tmin : tmax;
            return t >= 0f;
        }

        static bool Slab(float o, float d, float h, ref float tmin, ref float tmax)
        {
            if (Mathf.Abs(d) < 1e-8f)
                return Mathf.Abs(o) <= h;
            float t1 = (-h - o) / d;
            float t2 = (h - o) / d;
            if (t1 > t2)
            {
                float s = t1;
                t1 = t2;
                t2 = s;
            }
            tmin = Mathf.Max(tmin, t1);
            tmax = Mathf.Min(tmax, t2);
            return tmin <= tmax;
        }
    }
}
