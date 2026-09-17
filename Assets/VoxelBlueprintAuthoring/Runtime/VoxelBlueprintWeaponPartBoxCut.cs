using System;
using System.Collections.Generic;
using UnityEngine;

namespace Vr3.VoxelBlueprintAuthoring
{
    [CreateAssetMenu(fileName = "VoxelBlueprintWeaponPartBoxCut", menuName = "Voxel Blueprint Authoring/Weapon Part Box Cut")]
    public sealed class VoxelBlueprintWeaponPartBoxCut : ScriptableObject
    {
        public const string DefaultBladeLabel = "\u5203";
        public const string DefaultHandleLabel = "\u67c4";

        [Serializable]
        public struct Box
        {
            public string label;
            public Vector3Int min;
            public Vector3Int max;
            public Vector3 center;
            public Vector3 size;
            public Vector3 euler;

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

        [Tooltip("VR3 weapon prefab ItemState.ItemId (Props_Weapon.csv id). Used when copying part blueprints to VR3.")]
        public string weaponItemId;

        public List<string> partLabels = new List<string> { DefaultBladeLabel, DefaultHandleLabel };
        public List<Box> boxes = new List<Box>();
        public List<RejectedCells> rejected = new List<RejectedCells>();
        public List<RejectedCells> claimed = new List<RejectedCells>();

        public int PartCount => partLabels != null ? partLabels.Count : 0;

        public string PartLabel(int index)
        {
            if (partLabels == null || index < 0 || index >= partLabels.Count)
                return "";
            return partLabels[index];
        }

        public void EnsureDefaults()
        {
            if (partLabels == null || partLabels.Count == 0)
                partLabels = new List<string> { DefaultBladeLabel, DefaultHandleLabel };
            EnsureBoxes();
            EnsureRejects();
            EnsureClaims();
        }

        public void EnsureBoxes()
        {
            if (partLabels == null)
                partLabels = new List<string>();
            if (boxes == null)
                boxes = new List<Box>();
            while (boxes.Count < partLabels.Count)
            {
                string label = partLabels[boxes.Count];
                boxes.Add(new Box { label = label, min = Vector3Int.zero, max = Vector3Int.zero });
            }
            while (boxes.Count > partLabels.Count)
                boxes.RemoveAt(boxes.Count - 1);
            for (int i = 0; i < partLabels.Count; i++)
            {
                Box box = boxes[i];
                box.label = partLabels[i];
                box.EnsureOriented();
                boxes[i] = box;
            }
        }

        public bool TryAddPart(string label, out string assignedLabel)
        {
            EnsureDefaults();
            assignedLabel = UniqueLabel(string.IsNullOrWhiteSpace(label) ? "Part" : label.Trim());
            partLabels.Add(assignedLabel);
            boxes.Add(new Box { label = assignedLabel, min = Vector3Int.zero, max = Vector3Int.zero });
            EnsureRejects();
            EnsureClaims();
            return true;
        }

        public bool TryRemovePart(int index)
        {
            EnsureDefaults();
            if (index < 0 || index >= partLabels.Count || partLabels.Count <= 1)
                return false;
            string label = partLabels[index];
            partLabels.RemoveAt(index);
            boxes.RemoveAt(index);
            RemoveLabelMarks(rejected, label);
            RemoveLabelMarks(claimed, label);
            return true;
        }

        public bool TryRenamePart(int index, string newLabel)
        {
            EnsureDefaults();
            if (index < 0 || index >= partLabels.Count || string.IsNullOrWhiteSpace(newLabel))
                return false;
            newLabel = newLabel.Trim();
            for (int i = 0; i < partLabels.Count; i++)
            {
                if (i != index && partLabels[i] == newLabel)
                    return false;
            }
            string old = partLabels[index];
            partLabels[index] = newLabel;
            Box box = boxes[index];
            box.label = newLabel;
            boxes[index] = box;
            RenameLabelMarks(rejected, old, newLabel);
            RenameLabelMarks(claimed, old, newLabel);
            return true;
        }

        string UniqueLabel(string baseLabel)
        {
            if (!partLabels.Contains(baseLabel))
                return baseLabel;
            for (int n = 2; n < 1000; n++)
            {
                string candidate = baseLabel + n;
                if (!partLabels.Contains(candidate))
                    return candidate;
            }
            return baseLabel + Guid.NewGuid().ToString("N").Substring(0, 4);
        }

        static void RemoveLabelMarks(List<RejectedCells> list, string label)
        {
            if (list == null)
                return;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (list[i] != null && list[i].label == label)
                    list.RemoveAt(i);
            }
        }

        static void RenameLabelMarks(List<RejectedCells> list, string oldLabel, string newLabel)
        {
            if (list == null)
                return;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] != null && list[i].label == oldLabel)
                    list[i].label = newLabel;
            }
        }

        public void EnsureRejects()
        {
            EnsureLabeledCells(ref rejected);
        }

        public void EnsureClaims()
        {
            EnsureLabeledCells(ref claimed);
        }

        void EnsureLabeledCells(ref List<RejectedCells> list)
        {
            if (list == null)
                list = new List<RejectedCells>();
            for (int i = 0; i < PartCount; i++)
            {
                string label = PartLabel(i);
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

        public bool IsRejected(string label, int x, int y, int z) => HasCell(RejectList(label), x, y, z);

        public bool IsClaimed(string label, int x, int y, int z) => HasCell(ClaimList(label), x, y, z);

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

        public void SetReject(string label, Vector3Int cell, bool reject) => SetCellMark(RejectList(label), cell, reject);

        public void SetClaim(string label, Vector3Int cell, bool claim) => SetCellMark(ClaimList(label), cell, claim);

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

        public int RejectStamp() => CellMarkStamp(rejected);

        public int ClaimStamp() => CellMarkStamp(claimed);

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
            h = h * 31 + PartCount;
            for (int i = 0; i < boxes.Count; i++)
            {
                Box b = boxes[i];
                h = h * 31 + b.center.GetHashCode();
                h = h * 31 + b.size.GetHashCode();
                h = h * 31 + b.euler.GetHashCode();
            }
            return h;
        }

        List<Vector3Int> RejectList(string label) => CellsOf(rejected, label);

        List<Vector3Int> ClaimList(string label) => CellsOf(claimed, label);

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
            if (partLabels == null)
                return -1;
            for (int i = 0; i < partLabels.Count; i++)
            {
                if (partLabels[i] == label)
                    return i;
            }
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
            for (int i = 0; i < PartCount; i++)
            {
                string label = PartLabel(i);
                if (!boxes[i].Contains(x, y, z))
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
