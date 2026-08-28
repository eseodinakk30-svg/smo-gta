using System.Collections.Generic;
using UnityEngine;
using SanMonica.Core;
using SanMonica.Utils;

namespace SanMonica.Characters
{
    /// <summary>Everything that makes one pedestrian look different from the next.</summary>
    public struct CharacterAppearance
    {
        public float Height;
        public float Build;
        public Color Skin;
        public Color Hair;
        public Color Shirt;
        public Color Trousers;
        public Color Shoes;
        public Color Accent;
        public bool Hat;
        public bool Vest;
        public bool Backpack;
        public bool ShortHair;

        public static CharacterAppearance Random(ref Rng rng, SanMonica.Data.PedArchetype a)
        {
            var app = new CharacterAppearance
            {
                Height = rng.Range(a.minHeight, a.maxHeight),
                Build = rng.Range(a.minBuild, a.maxBuild),
                Skin = a.skinTones != null && a.skinTones.Length > 0 ? rng.Pick(a.skinTones) : new Color(0.85f, 0.7f, 0.6f),
                Hair = a.hairColors != null && a.hairColors.Length > 0 ? rng.Pick(a.hairColors) : Color.black,
                Shirt = a.shirtColors != null && a.shirtColors.Length > 0 ? rng.Pick(a.shirtColors) : Color.grey,
                Trousers = a.trouserColors != null && a.trouserColors.Length > 0 ? rng.Pick(a.trouserColors) : new Color(0.2f, 0.2f, 0.25f),
                Shoes = new Color(0.10f, 0.10f, 0.12f),
                Accent = new Color(rng.Range(0.2f, 0.9f), rng.Range(0.2f, 0.9f), rng.Range(0.2f, 0.9f)),
                Hat = a.wearsHat && rng.Chance(0.8f),
                Vest = a.wearsVest,
                Backpack = a.wearsBackpack && rng.Chance(0.7f),
                ShortHair = rng.Chance(0.6f)
            };
            return app;
        }
    }

    /// <summary>
    /// Builds the humanoid skeleton and its skinned mesh entirely in code.
    /// Rigid (one bone per vertex) skinning keeps the mesh cheap while still
    /// giving fully articulated arms, legs, spine and head.
    /// </summary>
    public static class CharacterRigBuilder
    {
        private class PartRange
        {
            public int Start, End, Bone;
            public Vector2 Uv;
        }

        public static CharacterRig Build(GameObject go, in CharacterAppearance app)
        {
            var rig = go.GetComponent<CharacterRig>();
            if (rig == null) rig = go.AddComponent<CharacterRig>();
            rig.Height = app.Height;
            rig.Build = app.Build;

            float h = app.Height;
            float w = app.Build;

            // ---- Skeleton ----
            var bones = new Transform[(int)HumanBone.Count];
            Transform Make(HumanBone b, Transform parent, Vector3 localPos)
            {
                var t = new GameObject(b.ToString()).transform;
                t.SetParent(parent, false);
                t.localPosition = localPos;
                t.localRotation = Quaternion.identity;
                bones[(int)b] = t;
                return t;
            }

            var hips = Make(HumanBone.Hips, go.transform, new Vector3(0f, 0.530f * h, 0f));
            var spine = Make(HumanBone.Spine, hips, new Vector3(0f, 0.075f * h, 0f));
            var chest = Make(HumanBone.Chest, spine, new Vector3(0f, 0.100f * h, 0f));
            var neck = Make(HumanBone.Neck, chest, new Vector3(0f, 0.095f * h, 0f));
            Make(HumanBone.Head, neck, new Vector3(0f, 0.045f * h, 0f));

            float shoulderX = 0.098f * h * w;
            var lsh = Make(HumanBone.LeftShoulder, chest, new Vector3(-shoulderX * 0.55f, 0.070f * h, 0f));
            var lua = Make(HumanBone.LeftUpperArm, lsh, new Vector3(-shoulderX * 0.55f, 0f, 0f));
            var lla = Make(HumanBone.LeftLowerArm, lua, new Vector3(0f, -0.155f * h, 0f));
            Make(HumanBone.LeftHand, lla, new Vector3(0f, -0.145f * h, 0f));

            var rsh = Make(HumanBone.RightShoulder, chest, new Vector3(shoulderX * 0.55f, 0.070f * h, 0f));
            var rua = Make(HumanBone.RightUpperArm, rsh, new Vector3(shoulderX * 0.55f, 0f, 0f));
            var rla = Make(HumanBone.RightLowerArm, rua, new Vector3(0f, -0.155f * h, 0f));
            Make(HumanBone.RightHand, rla, new Vector3(0f, -0.145f * h, 0f));

            float hipX = 0.052f * h * w;
            var lul = Make(HumanBone.LeftUpperLeg, hips, new Vector3(-hipX, -0.030f * h, 0f));
            var lll = Make(HumanBone.LeftLowerLeg, lul, new Vector3(0f, -0.235f * h, 0f));
            Make(HumanBone.LeftFoot, lll, new Vector3(0f, -0.235f * h, 0f));

            var rul = Make(HumanBone.RightUpperLeg, hips, new Vector3(hipX, -0.030f * h, 0f));
            var rll = Make(HumanBone.RightLowerLeg, rul, new Vector3(0f, -0.235f * h, 0f));
            Make(HumanBone.RightFoot, rll, new Vector3(0f, -0.235f * h, 0f));

            rig.Bones = bones;

            // Attachment points for weapons and props.
            rig.RightHandAttach = new GameObject("RightHandAttach").transform;
            rig.RightHandAttach.SetParent(bones[(int)HumanBone.RightHand], false);
            rig.RightHandAttach.localPosition = new Vector3(0f, -0.03f, 0.04f);
            rig.LeftHandAttach = new GameObject("LeftHandAttach").transform;
            rig.LeftHandAttach.SetParent(bones[(int)HumanBone.LeftHand], false);
            rig.LeftHandAttach.localPosition = new Vector3(0f, -0.03f, 0.04f);
            rig.HeadAttach = new GameObject("HeadAttach").transform;
            rig.HeadAttach.SetParent(bones[(int)HumanBone.Head], false);
            rig.HeadAttach.localPosition = new Vector3(0f, 0.10f * h, 0f);

            rig.CacheBindPose();

            // ---- Meshes ----
            rig.HighMesh = BuildBody(go.transform, bones, app, false);
            rig.LowMesh = BuildBody(go.transform, bones, app, true);

            var smr = go.GetComponent<SkinnedMeshRenderer>();
            if (smr == null) smr = go.AddComponent<SkinnedMeshRenderer>();
            smr.bones = bones;
            smr.rootBone = hips;
            smr.sharedMesh = rig.HighMesh;
            smr.sharedMaterial = PaletteAtlas.Matte;
            smr.quality = SkinQuality.Bone1;
            smr.updateWhenOffscreen = false;
            smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            smr.localBounds = new Bounds(new Vector3(0f, h * 0.5f, 0f), new Vector3(h * 0.7f, h * 1.1f, h * 0.7f));
            rig.Renderer = smr;
            rig.SetMeshLod(0);
            return rig;
        }

        private static Mesh BuildBody(Transform root, Transform[] bones, in CharacterAppearance app, bool simplified)
        {
            float h = app.Height;
            float w = app.Build;
            var mb = new MeshBuilder(1);
            var parts = new List<PartRange>(24);

            int skinUv = PaletteAtlas.Register(app.Skin);
            int hairUv = PaletteAtlas.Register(app.Hair);
            int shirtUv = PaletteAtlas.Register(app.Shirt);
            int trouserUv = PaletteAtlas.Register(app.Trousers);
            int shoeUv = PaletteAtlas.Register(app.Shoes);
            int accentUv = PaletteAtlas.Register(app.Accent);

            void Part(HumanBone bone, int paletteIndex, System.Action add)
            {
                int start = mb.VertexCount;
                add();
                parts.Add(new PartRange { Start = start, End = mb.VertexCount, Bone = (int)bone, Uv = PaletteAtlas.UV(paletteIndex) });
            }

            Vector3 BonePos(HumanBone b) => root.InverseTransformPoint(bones[(int)b].position);

            // --- Torso ---
            Vector3 hipsP = BonePos(HumanBone.Hips);
            Vector3 chestP = BonePos(HumanBone.Chest);
            Vector3 neckP = BonePos(HumanBone.Neck);
            Vector3 headP = BonePos(HumanBone.Head);

            Part(HumanBone.Hips, trouserUv, () =>
                mb.AddTaperedBox(hipsP + Vector3.up * 0.02f * h, new Vector3(0.20f * h * w, 0.11f * h, 0.13f * h * w), 0.95f, 0.95f, Quaternion.identity, 0, 0f));

            if (!simplified)
                Part(HumanBone.Spine, shirtUv, () =>
                    mb.AddTaperedBox(BonePos(HumanBone.Spine) + Vector3.up * 0.04f * h, new Vector3(0.205f * h * w, 0.11f * h, 0.135f * h * w), 1.05f, 1.02f, Quaternion.identity, 0, 0f));

            Part(HumanBone.Chest, shirtUv, () =>
                mb.AddTaperedBox(chestP + Vector3.up * 0.045f * h,
                    new Vector3(0.225f * h * w, simplified ? 0.20f * h : 0.115f * h, 0.145f * h * w), 0.92f, 0.92f, Quaternion.identity, 0, 0f));

            if (app.Vest && !simplified)
                Part(HumanBone.Chest, accentUv, () =>
                    mb.AddTaperedBox(chestP + Vector3.up * 0.045f * h, new Vector3(0.245f * h * w, 0.125f * h, 0.165f * h * w), 0.94f, 0.94f, Quaternion.identity, 0, 0f));

            if (app.Backpack && !simplified)
                Part(HumanBone.Chest, accentUv, () =>
                    mb.AddTaperedBox(chestP + new Vector3(0f, 0.03f * h, -0.115f * h * w), new Vector3(0.17f * h, 0.20f * h, 0.10f * h), 0.9f, 0.9f, Quaternion.identity, 0, 0f));

            if (!simplified)
                Part(HumanBone.Neck, skinUv, () =>
                    mb.AddTaperedBox(neckP + Vector3.up * 0.02f * h, new Vector3(0.055f * h, 0.05f * h, 0.055f * h), 1f, 1f, Quaternion.identity, 0, 0f));

            // --- Head ---
            Part(HumanBone.Head, skinUv, () =>
                mb.AddTaperedBox(headP + Vector3.up * 0.055f * h, new Vector3(0.115f * h, 0.135f * h, 0.125f * h), 0.86f, 0.86f, Quaternion.identity, 0, 0f));

            if (!simplified)
            {
                Part(HumanBone.Head, hairUv, () =>
                    mb.AddTaperedBox(headP + Vector3.up * (app.ShortHair ? 0.108f : 0.100f) * h,
                        new Vector3(0.122f * h, (app.ShortHair ? 0.035f : 0.065f) * h, 0.132f * h), 0.90f, 0.90f, Quaternion.identity, 0, 0f));

                if (app.Hat)
                    Part(HumanBone.Head, accentUv, () =>
                    {
                        mb.AddTaperedBox(headP + Vector3.up * 0.135f * h, new Vector3(0.128f * h, 0.055f * h, 0.138f * h), 0.92f, 0.92f, Quaternion.identity, 0, 0f);
                        mb.AddBox(headP + new Vector3(0f, 0.112f * h, 0.075f * h), new Vector3(0.13f * h, 0.012f * h, 0.09f * h), Quaternion.identity, 0f, 0);
                    });
            }

            // --- Arms ---
            void Arm(HumanBone upper, HumanBone lower, HumanBone hand, float side)
            {
                Vector3 up = BonePos(upper), lo = BonePos(lower), ha = BonePos(hand);
                float upLen = Vector3.Distance(up, lo);
                float loLen = Vector3.Distance(lo, ha);
                float thick = 0.052f * h * w;

                if (simplified)
                {
                    Part(upper, shirtUv, () =>
                        mb.AddTaperedBox((up + ha) * 0.5f, new Vector3(thick * 1.9f, upLen + loLen, thick * 1.9f), 0.8f, 0.8f, Quaternion.identity, 0, 0f));
                    return;
                }
                Part(upper, shirtUv, () =>
                    mb.AddTaperedBox(up + Vector3.down * (upLen * 0.5f), new Vector3(thick * 2f, upLen * 1.04f, thick * 2f), 0.86f, 0.86f, Quaternion.identity, 0, 0f));
                Part(lower, shirtUv, () =>
                    mb.AddTaperedBox(lo + Vector3.down * (loLen * 0.45f), new Vector3(thick * 1.75f, loLen * 0.92f, thick * 1.75f), 0.9f, 0.9f, Quaternion.identity, 0, 0f));
                Part(hand, skinUv, () =>
                    mb.AddTaperedBox(ha + Vector3.down * (0.028f * h), new Vector3(thick * 1.7f, 0.055f * h, thick * 2.1f), 0.85f, 0.85f, Quaternion.identity, 0, 0f));
            }

            Arm(HumanBone.LeftUpperArm, HumanBone.LeftLowerArm, HumanBone.LeftHand, -1f);
            Arm(HumanBone.RightUpperArm, HumanBone.RightLowerArm, HumanBone.RightHand, 1f);

            // --- Legs ---
            void Leg(HumanBone upper, HumanBone lower, HumanBone foot)
            {
                Vector3 up = BonePos(upper), lo = BonePos(lower), fo = BonePos(foot);
                float upLen = Vector3.Distance(up, lo);
                float loLen = Vector3.Distance(lo, fo);
                float thick = 0.066f * h * w;

                if (simplified)
                {
                    Part(upper, trouserUv, () =>
                        mb.AddTaperedBox((up + fo) * 0.5f, new Vector3(thick * 2f, upLen + loLen, thick * 2f), 0.8f, 0.8f, Quaternion.identity, 0, 0f));
                    return;
                }
                Part(upper, trouserUv, () =>
                    mb.AddTaperedBox(up + Vector3.down * (upLen * 0.5f), new Vector3(thick * 2.1f, upLen * 1.02f, thick * 2.1f), 0.88f, 0.88f, Quaternion.identity, 0, 0f));
                Part(lower, trouserUv, () =>
                    mb.AddTaperedBox(lo + Vector3.down * (loLen * 0.48f), new Vector3(thick * 1.8f, loLen * 0.96f, thick * 1.8f), 0.92f, 0.92f, Quaternion.identity, 0, 0f));
                Part(foot, shoeUv, () =>
                    mb.AddTaperedBox(fo + new Vector3(0f, -0.018f * h, 0.028f * h), new Vector3(thick * 1.9f, 0.045f * h, 0.115f * h), 0.9f, 0.9f, Quaternion.identity, 0, 0f));
            }

            Leg(HumanBone.LeftUpperLeg, HumanBone.LeftLowerLeg, HumanBone.LeftFoot);
            Leg(HumanBone.RightUpperLeg, HumanBone.RightLowerLeg, HumanBone.RightFoot);

            // ---- Assign palette UVs and bone weights ----
            var weights = new BoneWeight[mb.VertexCount];
            foreach (var part in parts)
            {
                mb.SetUVRange(part.Start, part.End, part.Uv);
                for (int i = part.Start; i < part.End && i < weights.Length; i++)
                {
                    weights[i].boneIndex0 = part.Bone;
                    weights[i].weight0 = 1f;
                }
            }

            var mesh = mb.ToMesh(simplified ? "CharacterLow" : "CharacterHigh");
            mesh.boneWeights = weights;

            var binds = new Matrix4x4[bones.Length];
            for (int i = 0; i < bones.Length; i++)
                binds[i] = bones[i] != null ? bones[i].worldToLocalMatrix * root.localToWorldMatrix : Matrix4x4.identity;
            mesh.bindposes = binds;
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
