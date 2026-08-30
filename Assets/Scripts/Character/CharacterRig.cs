using UnityEngine;

namespace SanMonica.Characters
{
    public enum HumanBone
    {
        Hips = 0, Spine, Chest, Neck, Head,
        LeftShoulder, LeftUpperArm, LeftLowerArm, LeftHand,
        RightShoulder, RightUpperArm, RightLowerArm, RightHand,
        LeftUpperLeg, LeftLowerLeg, LeftFoot,
        RightUpperLeg, RightLowerLeg, RightFoot,
        Count
    }

    /// <summary>
    /// A procedurally built humanoid: 19 bones, one skinned mesh, one material.
    /// Both the player and every pedestrian use this rig, which is what allows
    /// a crowded street to stay inside a mobile draw-call budget.
    /// </summary>
    public class CharacterRig : MonoBehaviour
    {
        public Transform[] Bones = new Transform[(int)HumanBone.Count];
        public SkinnedMeshRenderer Renderer;
        public Mesh HighMesh;
        public Mesh LowMesh;
        public float Height = 1.78f;
        public float Build = 1f;
        public Transform RightHandAttach;
        public Transform LeftHandAttach;
        public Transform HeadAttach;

        /// <summary>
        /// The appearance this body was built from. Kept so clothes and haircuts
        /// can change it and rebuild, instead of being a notification that lies.
        /// </summary>
        public CharacterAppearance Appearance;

        private readonly Quaternion[] _bindLocalRotations = new Quaternion[(int)HumanBone.Count];
        private readonly Vector3[] _bindLocalPositions = new Vector3[(int)HumanBone.Count];
        private int _currentLod = -1;

        public Transform Bone(HumanBone b) => Bones[(int)b];

        public void CacheBindPose()
        {
            for (int i = 0; i < Bones.Length; i++)
            {
                if (Bones[i] == null) continue;
                _bindLocalRotations[i] = Bones[i].localRotation;
                _bindLocalPositions[i] = Bones[i].localPosition;
            }
        }

        public Quaternion BindRotation(HumanBone b) => _bindLocalRotations[(int)b];
        public Vector3 BindPosition(HumanBone b) => _bindLocalPositions[(int)b];

        public void ResetToBindPose()
        {
            for (int i = 0; i < Bones.Length; i++)
            {
                if (Bones[i] == null) continue;
                Bones[i].localRotation = _bindLocalRotations[i];
                Bones[i].localPosition = _bindLocalPositions[i];
            }
        }

        /// <summary>Re-applies the current level of detail after the meshes change.</summary>
        public void RefreshMesh()
        {
            int lod = _currentLod < 0 ? 0 : _currentLod;
            _currentLod = -1;
            SetMeshLod(lod);
        }

        /// <summary>Swaps between the detailed and the simplified body mesh.</summary>
        public void SetMeshLod(int lod)
        {
            if (Renderer == null || _currentLod == lod) return;
            _currentLod = lod;
            Renderer.sharedMesh = lod <= 0 ? HighMesh : (LowMesh != null ? LowMesh : HighMesh);
            Renderer.quality = lod <= 0 ? SkinQuality.Bone1 : SkinQuality.Bone1;
            Renderer.updateWhenOffscreen = false;
        }

        private void OnDestroy()
        {
            if (HighMesh != null) Destroy(HighMesh);
            if (LowMesh != null) Destroy(LowMesh);
        }
    }
}
