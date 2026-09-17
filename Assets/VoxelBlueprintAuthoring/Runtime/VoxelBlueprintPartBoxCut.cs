using System;
using System.Collections.Generic;
using UnityEngine;

namespace Vr3.VoxelBlueprintAuthoring
{
    [CreateAssetMenu(fileName = "VoxelBlueprintPartBoxCut", menuName = "Voxel Blueprint Authoring/Part Box Cut")]
    public sealed class VoxelBlueprintPartBoxCut : ScriptableObject
    {
        public const int PartCount = 15;

        public static readonly string[] Labels =
        {
            "Head", "Torso", "Hips",
            "UpperArm_L", "Forearm_L", "Hand_L",
            "UpperArm_R", "Forearm_R", "Hand_R",
            "UpperLeg_L", "LowerLeg_L", "Foot_L",
            "UpperLeg_R", "LowerLeg_R", "Foot_R",
        };

        public static readonly string[] AssignOrder =
        {
            "Hand_L", "Hand_R", "Foot_L", "Foot_R",
            "Forearm_L", "Forearm_R", "LowerLeg_L", "LowerLeg_R",
            "UpperArm_L", "UpperArm_R", "UpperLeg_L", "UpperLeg_R",
            "Head", "Hips", "Torso",
        };

        [Serializable]
        public struct Box
        {
            public string label;
            public Vector3Int min;
            public Vector3Int max;
            public Vector3 center;
            public Vector3 size;
            public Vector3 euler;
            public bool mirror;

            public Quaternion Rotation => Quaternion.Euler(euler);

            public void Sort()
            {
                Vector3Int a = Vector3Int.Min(min, max);
                Vector3Int b = Vector3Int.Max(min, max);
                min = a;
                max = b;
            }

            public void EnsureOriented()
            {
                if (size.x >= 0.5f && size.y >= 0.5f && size.z >= 0.5f)
                    return;
                Sort();
                size = new Vector3(max.x - min.x + 1, max.y - min.y + 1, max.z - min.z + 1);
                center = new Vector3(min.x, min.y, min.z) + size * 0.5f;
                euler = Vector3.zero;
            }

            public static Box FromAabb(string label, Vector3Int min, Vector3Int max)
            {
                Vector3Int a = Vector3Int.Min(min, max);
                Vector3Int b = Vector3Int.Max(min, max);
                Vector3 size = new Vector3(b.x - a.x + 1, b.y - a.y + 1, b.z - a.z + 1);
                return new Box
                {
                    label = label,
                    min = a,
                    max = b,
                    size = size,
                    center = new Vector3(a.x, a.y, a.z) + size * 0.5f,
                    euler = Vector3.zero,
                };
            }

            public bool Contains(int x, int y, int z)
            {
                EnsureOriented();
                Vector3 local = Quaternion.Inverse(Rotation) * (new Vector3(x + 0.5f, y + 0.5f, z + 0.5f) - center);
                Vector3 h = size * 0.5f;
                return Mathf.Abs(local.x) <= h.x + 0.001f
                    && Mathf.Abs(local.y) <= h.y + 0.001f
                    && Mathf.Abs(local.z) <= h.z + 0.001f;
            }
        }

        [Serializable]
        public class RejectedCells
        {
            public string label;
            public List<Vector3Int> cells = new List<Vector3Int>();
        }

        public VoxelBlueprintAsset source;
        public Box[] boxes = Array.Empty<Box>();
        public ArmMirror armMirror;
        public bool limbViewerLrSwapDone;
        public List<RejectedCells> rejected = new List<RejectedCells>();
        public List<RejectedCells> claimed = new List<RejectedCells>();

        public enum ArmMirror
        {
            None = 0,
            LeftFromRight = 1,
            RightFromLeft = 2,
        }

        public bool IsArmMirrorDest(string label)
        {
            EnsureBoxes();
            if (ArmMate(label) == null)
                return false;
            int index = IndexOf(label);
            if (index < 0 || !boxes[index].mirror)
                return false;
            int mate = IndexOf(ArmMate(label));
            if (mate >= 0 && boxes[mate].mirror)
                return false;
            return true;
        }

        public bool IsArmMirrorSource(string label)
        {
            string mate = ArmMate(label);
            return mate != null && IsArmMirrorDest(mate);
        }

        public static string ArmMate(string label)
        {
            if (label.EndsWith("_L", StringComparison.Ordinal))
                return label.Substring(0, label.Length - 2) + "_R";
            if (label.EndsWith("_R", StringComparison.Ordinal))
                return label.Substring(0, label.Length - 2) + "_L";
            return null;
        }

        public void EnsureBoxes()
        {
            if (boxes != null && boxes.Length == PartCount)
            {
                for (int i = 0; i < PartCount; i++)
                {
                    if (string.IsNullOrEmpty(boxes[i].label))
                        boxes[i].label = Labels[i];
                    boxes[i].EnsureOriented();
                }
                MigrateLegacyArmMirror();
                EnsureRejects();
                EnsureClaims();
                TryMigrateLimbViewerLr();
                return;
            }
            boxes = new Box[PartCount];
            for (int i = 0; i < PartCount; i++)
                boxes[i] = new Box { label = Labels[i], min = Vector3Int.zero, max = Vector3Int.zero };
            MigrateLegacyArmMirror();
            EnsureRejects();
            EnsureClaims();
            TryMigrateLimbViewerLr();
        }

        public bool TryMigrateLimbViewerLr()
        {
            if (limbViewerLrSwapDone)
                return false;
            bool any = false;
            for (int i = 0; i < boxes.Length; i++)
            {
                boxes[i].EnsureOriented();
                if (boxes[i].size.sqrMagnitude > 0.25f)
                    any = true;
            }
            if (!any)
                return false;
            limbViewerLrSwapDone = true;
            for (int i = 0; i < Labels.Length; i++)
            {
                string mate = ArmMate(Labels[i]);
                if (mate == null)
                    continue;
                int j = IndexOf(mate);
                if (j <= i)
                    continue;
                Box a = boxes[i];
                Box b = boxes[j];
                Vector3 ac = a.center, asz = a.size, ae = a.euler;
                Vector3Int amin = a.min, amax = a.max;
                a.center = b.center;
                a.size = b.size;
                a.euler = b.euler;
                a.min = b.min;
                a.max = b.max;
                b.center = ac;
                b.size = asz;
                b.euler = ae;
                b.min = amin;
                b.max = amax;
                boxes[i] = a;
                boxes[j] = b;
                SwapMarkLists(rejected, Labels[i], mate);
                SwapMarkLists(claimed, Labels[i], mate);
            }
            return true;
        }

        static void SwapMarkLists(List<RejectedCells> list, string a, string b)
        {
            if (list == null)
                return;
            RejectedCells ea = null;
            RejectedCells eb = null;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == null)
                    continue;
                if (list[i].label == a)
                    ea = list[i];
                else if (list[i].label == b)
                    eb = list[i];
            }
            if (ea == null || eb == null)
                return;
            List<Vector3Int> tmp = ea.cells;
            ea.cells = eb.cells;
            eb.cells = tmp;
        }

        void MigrateLegacyArmMirror()
        {
            if (armMirror == ArmMirror.None)
                return;
            bool leftDest = armMirror == ArmMirror.LeftFromRight;
            for (int i = 0; i < boxes.Length; i++)
            {
                string label = boxes[i].label;
                bool arm = label.StartsWith("UpperArm", StringComparison.Ordinal)
                    || label.StartsWith("Forearm", StringComparison.Ordinal)
                    || label.StartsWith("Hand", StringComparison.Ordinal);
                Box box = boxes[i];
                box.mirror = arm && ((leftDest && label.EndsWith("_L", StringComparison.Ordinal))
                    || (!leftDest && label.EndsWith("_R", StringComparison.Ordinal)));
                boxes[i] = box;
            }
            armMirror = ArmMirror.None;
        }

        public void EnsureRejects()
        {
            EnsureLabeledCells(ref rejected);
        }

        public void EnsureClaims()
        {
            EnsureLabeledCells(ref claimed);
        }

        static void EnsureLabeledCells(ref List<RejectedCells> list)
        {
            if (list == null)
                list = new List<RejectedCells>();
            for (int i = 0; i < PartCount; i++)
            {
                string label = Labels[i];
                bool found = false;
                for (int r = 0; r < list.Count; r++)
                {
                    if (list[r] != null && list[r].label == label)
                    {
                        if (list[r].cells == null)
                            list[r].cells = new List<Vector3Int>();
                        found = true;
                        break;
                    }
                }
                if (!found)
                    list.Add(new RejectedCells { label = label, cells = new List<Vector3Int>() });
            }
        }

        public bool IsRejected(string label, int x, int y, int z)
        {
            return HasCell(RejectList(label), x, y, z);
        }

        public bool IsClaimed(string label, int x, int y, int z)
        {
            return HasCell(ClaimList(label), x, y, z);
        }

        static bool HasCell(List<Vector3Int> cells, int x, int y, int z)
        {
            if (cells == null)
                return false;
            Vector3Int p = new Vector3Int(x, y, z);
            for (int i = 0; i < cells.Count; i++)
            {
                if (cells[i] == p)
                    return true;
            }
            return false;
        }

        public bool ToggleReject(string label, Vector3Int cell)
        {
            bool next = !IsRejected(label, cell.x, cell.y, cell.z);
            SetReject(label, cell, next);
            return next;
        }

        public void SetReject(string label, Vector3Int cell, bool reject)
        {
            SetCellMark(RejectList(label), cell, reject);
        }

        public void SetClaim(string label, Vector3Int cell, bool claim)
        {
            SetCellMark(ClaimList(label), cell, claim);
        }

        static void SetCellMark(List<Vector3Int> cells, Vector3Int cell, bool on)
        {
            if (cells == null)
                return;
            for (int i = 0; i < cells.Count; i++)
            {
                if (cells[i] != cell)
                    continue;
                if (!on)
                    cells.RemoveAt(i);
                return;
            }
            if (on)
                cells.Add(cell);
        }

        public int RejectStamp()
        {
            EnsureRejects();
            return CellMarkStamp(rejected);
        }

        public int ClaimStamp()
        {
            EnsureClaims();
            return CellMarkStamp(claimed);
        }

        static int CellMarkStamp(List<RejectedCells> list)
        {
            int h = 17;
            if (list == null)
                return h;
            for (int r = 0; r < list.Count; r++)
            {
                RejectedCells entry = list[r];
                if (entry == null || entry.cells == null)
                    continue;
                h = h * 31 + (entry.label != null ? entry.label.GetHashCode() : 0);
                h = h * 31 + entry.cells.Count;
                for (int i = 0; i < entry.cells.Count; i++)
                    h = h * 31 + entry.cells[i].GetHashCode();
            }
            return h;
        }

        public int AssignmentStamp()
        {
            EnsureBoxes();
            int h = GetInstanceID();
            h = h * 31 + (source != null ? source.GetInstanceID() : 0);
            h = h * 31 + RejectStamp();
            h = h * 31 + ClaimStamp();
            for (int i = 0; i < boxes.Length; i++)
            {
                Box b = boxes[i];
                h = h * 31 + b.center.GetHashCode();
                h = h * 31 + b.size.GetHashCode();
                h = h * 31 + b.euler.GetHashCode();
                h = h * 31 + (b.mirror ? 1 : 0);
            }
            return h;
        }

        List<Vector3Int> RejectList(string label)
        {
            EnsureRejects();
            return CellsOf(rejected, label);
        }

        List<Vector3Int> ClaimList(string label)
        {
            EnsureClaims();
            return CellsOf(claimed, label);
        }

        static List<Vector3Int> CellsOf(List<RejectedCells> list, string label)
        {
            if (list == null)
                return null;
            for (int r = 0; r < list.Count; r++)
            {
                if (list[r] != null && list[r].label == label)
                    return list[r].cells;
            }
            return null;
        }

        public int IndexOf(string label)
        {
            for (int i = 0; i < Labels.Length; i++)
                if (Labels[i] == label)
                    return i;
            return -1;
        }

        public string OwnerOf(int x, int y, int z)
        {
            EnsureBoxes();
            string hit = FirstAssignOwner(x, y, z, true);
            if (hit != null)
                return hit;
            return FirstAssignOwner(x, y, z, false);
        }

        string FirstAssignOwner(int x, int y, int z, bool requireClaim)
        {
            for (int i = 0; i < AssignOrder.Length; i++)
            {
                string label = AssignOrder[i];
                int index = IndexOf(label);
                if (index < 0 || IsArmMirrorDest(label) || !boxes[index].Contains(x, y, z))
                    continue;
                if (IsRejected(label, x, y, z))
                    continue;
                if (requireClaim && !IsClaimed(label, x, y, z))
                    continue;
                return label;
            }
            return null;
        }
    }
}
