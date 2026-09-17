using UnityEngine;

namespace Vr3.VoxelBlueprintAuthoring
{
    [CreateAssetMenu(fileName = "VoxelBlueprintPartWearOffset", menuName = "Voxel Blueprint Authoring/Part Wear Offset")]
    public sealed class VoxelBlueprintPartWearOffset : ScriptableObject
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

        public Vector3[] offsets = new Vector3[PartCount];
        public float[] scales = new float[PartCount];

        public void Ensure()
        {
            if (offsets == null || offsets.Length != PartCount)
                offsets = new Vector3[PartCount];
            if (scales == null || scales.Length != PartCount)
            {
                float[] next = new float[PartCount];
                for (int i = 0; i < PartCount; i++)
                    next[i] = 1f;
                if (scales != null)
                {
                    int n = Mathf.Min(scales.Length, PartCount);
                    for (int i = 0; i < n; i++)
                        next[i] = scales[i] > 1e-4f ? scales[i] : 1f;
                }
                scales = next;
                return;
            }
            for (int i = 0; i < PartCount; i++)
            {
                if (scales[i] <= 1e-4f)
                    scales[i] = 1f;
            }
        }

        public Vector3 Get(string label)
        {
            Ensure();
            int i = IndexOf(label);
            return i >= 0 ? offsets[i] : Vector3.zero;
        }

        public void Set(string label, Vector3 value)
        {
            Ensure();
            int i = IndexOf(label);
            if (i >= 0)
                offsets[i] = value;
        }

        public float GetScale(string label)
        {
            Ensure();
            int i = IndexOf(label);
            if (i < 0)
                return 1f;
            float s = scales[i];
            return s > 1e-4f ? s : 1f;
        }

        public void SetScale(string label, float value)
        {
            Ensure();
            int i = IndexOf(label);
            if (i >= 0)
                scales[i] = Mathf.Clamp(value, 0.05f, 8f);
        }

        public static int IndexOf(string label)
        {
            for (int i = 0; i < Labels.Length; i++)
            {
                if (Labels[i] == label)
                    return i;
            }
            return -1;
        }
    }
}
