using System.Collections.Generic;
using UnityEngine;
using SanMonica.Core;

namespace SanMonica.Characters
{
    /// <summary>
    /// Builds a physical ragdoll on top of the procedural rig and doubles as the
    /// hit-box hierarchy, so headshots and limb hits are resolved by real
    /// colliders rather than by guesswork.
    /// </summary>
    public class RagdollController : MonoBehaviour
    {
        public readonly List<Rigidbody> Bodies = new List<Rigidbody>(12);
        public readonly List<Collider> Colliders = new List<Collider>(12);
        public bool Active { get; private set; }

        private CharacterRig _rig;
        private Collider _mainCollider;
        private Rigidbody _mainBody;

        public void Setup(CharacterRig rig, Collider mainCollider, Rigidbody mainBody)
        {
            _rig = rig;
            _mainCollider = mainCollider;
            _mainBody = mainBody;
            SetKinematic(true);
            SetLayer(GameLayers.Ragdoll);
        }

        public void SetKinematic(bool kinematic)
        {
            for (int i = 0; i < Bodies.Count; i++)
            {
                if (Bodies[i] == null) continue;
                Bodies[i].isKinematic = kinematic;
                Bodies[i].detectCollisions = !kinematic;
                if (!kinematic) Bodies[i].WakeUp();
            }
        }

        public void SetLayer(int layer)
        {
            for (int i = 0; i < Colliders.Count; i++)
                if (Colliders[i] != null) Colliders[i].gameObject.layer = layer;
        }

        public void Enable(Vector3 impulse, Vector3 impulsePoint)
        {
            if (Active) return;
            Active = true;
            if (_mainCollider != null) _mainCollider.enabled = false;
            if (_mainBody != null) { _mainBody.isKinematic = true; _mainBody.detectCollisions = false; }

            SetLayer(GameLayers.Prop);
            SetKinematic(false);

            if (impulse.sqrMagnitude > 0.0001f)
            {
                Rigidbody closest = null;
                float best = float.MaxValue;
                for (int i = 0; i < Bodies.Count; i++)
                {
                    if (Bodies[i] == null) continue;
                    float d = (Bodies[i].position - impulsePoint).sqrMagnitude;
                    if (d < best) { best = d; closest = Bodies[i]; }
                }
                if (closest != null) closest.AddForceAtPosition(impulse, impulsePoint, ForceMode.Impulse);
                for (int i = 0; i < Bodies.Count; i++)
                    if (Bodies[i] != null && Bodies[i] != closest)
                        Bodies[i].AddForce(impulse * 0.12f, ForceMode.Impulse);
            }
        }

        public void Disable()
        {
            if (!Active) return;
            Active = false;
            SetKinematic(true);
            SetLayer(GameLayers.Ragdoll);
            if (_mainCollider != null) _mainCollider.enabled = true;
            if (_mainBody != null) { _mainBody.isKinematic = false; _mainBody.detectCollisions = true; }
            if (_rig != null) _rig.ResetToBindPose();
        }

        public Vector3 HipsPosition => Bodies.Count > 0 && Bodies[0] != null ? Bodies[0].position : transform.position;
    }

    public static class RagdollBuilder
    {
        private struct BoneSetup
        {
            public HumanBone Bone;
            public HumanBone ChildForLength;
            public float Radius;
            public float Mass;
            public BodyPart Part;
            public HumanBone Parent;
            public bool HasJoint;
            public float SwingLimit;
            public float TwistLimit;
        }

        public static RagdollController Build(CharacterRig rig, CharacterHealth health, Collider mainCollider, Rigidbody mainBody)
        {
            var ctrl = rig.gameObject.GetComponent<RagdollController>();
            if (ctrl == null) ctrl = rig.gameObject.AddComponent<RagdollController>();

            float h = rig.Height;
            float w = rig.Build;

            var setups = new[]
            {
                new BoneSetup { Bone = HumanBone.Hips, ChildForLength = HumanBone.Chest, Radius = 0.115f * w, Mass = 12f, Part = BodyPart.Torso, HasJoint = false },
                new BoneSetup { Bone = HumanBone.Chest, ChildForLength = HumanBone.Neck, Radius = 0.125f * w, Mass = 14f, Part = BodyPart.Torso, Parent = HumanBone.Hips, HasJoint = true, SwingLimit = 25f, TwistLimit = 18f },
                new BoneSetup { Bone = HumanBone.Head, ChildForLength = HumanBone.Head, Radius = 0.095f, Mass = 4.5f, Part = BodyPart.Head, Parent = HumanBone.Chest, HasJoint = true, SwingLimit = 35f, TwistLimit = 25f },
                new BoneSetup { Bone = HumanBone.LeftUpperArm, ChildForLength = HumanBone.LeftLowerArm, Radius = 0.052f * w, Mass = 2.2f, Part = BodyPart.LeftArm, Parent = HumanBone.Chest, HasJoint = true, SwingLimit = 70f, TwistLimit = 25f },
                new BoneSetup { Bone = HumanBone.LeftLowerArm, ChildForLength = HumanBone.LeftHand, Radius = 0.045f * w, Mass = 1.6f, Part = BodyPart.LeftArm, Parent = HumanBone.LeftUpperArm, HasJoint = true, SwingLimit = 80f, TwistLimit = 12f },
                new BoneSetup { Bone = HumanBone.RightUpperArm, ChildForLength = HumanBone.RightLowerArm, Radius = 0.052f * w, Mass = 2.2f, Part = BodyPart.RightArm, Parent = HumanBone.Chest, HasJoint = true, SwingLimit = 70f, TwistLimit = 25f },
                new BoneSetup { Bone = HumanBone.RightLowerArm, ChildForLength = HumanBone.RightHand, Radius = 0.045f * w, Mass = 1.6f, Part = BodyPart.RightArm, Parent = HumanBone.RightUpperArm, HasJoint = true, SwingLimit = 80f, TwistLimit = 12f },
                new BoneSetup { Bone = HumanBone.LeftUpperLeg, ChildForLength = HumanBone.LeftLowerLeg, Radius = 0.070f * w, Mass = 5.5f, Part = BodyPart.LeftLeg, Parent = HumanBone.Hips, HasJoint = true, SwingLimit = 55f, TwistLimit = 15f },
                new BoneSetup { Bone = HumanBone.LeftLowerLeg, ChildForLength = HumanBone.LeftFoot, Radius = 0.058f * w, Mass = 3.5f, Part = BodyPart.LeftLeg, Parent = HumanBone.LeftUpperLeg, HasJoint = true, SwingLimit = 70f, TwistLimit = 10f },
                new BoneSetup { Bone = HumanBone.RightUpperLeg, ChildForLength = HumanBone.RightLowerLeg, Radius = 0.070f * w, Mass = 5.5f, Part = BodyPart.RightLeg, Parent = HumanBone.Hips, HasJoint = true, SwingLimit = 55f, TwistLimit = 15f },
                new BoneSetup { Bone = HumanBone.RightLowerLeg, ChildForLength = HumanBone.RightFoot, Radius = 0.058f * w, Mass = 3.5f, Part = BodyPart.RightLeg, Parent = HumanBone.RightUpperLeg, HasJoint = true, SwingLimit = 70f, TwistLimit = 10f },
            };

            var bodies = new Dictionary<HumanBone, Rigidbody>();

            foreach (var s in setups)
            {
                var t = rig.Bone(s.Bone);
                if (t == null) continue;

                float length;
                if (s.Bone == HumanBone.Head) length = 0.20f * h / 1.78f + 0.06f;
                else
                {
                    var child = rig.Bone(s.ChildForLength);
                    length = child != null ? Vector3.Distance(t.position, child.position) : 0.15f;
                }
                length = Mathf.Max(length, s.Radius * 2.1f);

                var cap = t.gameObject.AddComponent<CapsuleCollider>();
                cap.direction = 1; // along local Y
                cap.radius = s.Radius;
                cap.height = length + s.Radius * 0.5f;
                cap.center = s.Bone == HumanBone.Head
                    ? new Vector3(0f, length * 0.35f, 0f)
                    : new Vector3(0f, -length * 0.5f, 0f);
                if (s.Bone == HumanBone.Hips || s.Bone == HumanBone.Chest)
                    cap.center = new Vector3(0f, length * 0.5f, 0f);

                var rb = t.gameObject.AddComponent<Rigidbody>();
                rb.mass = s.Mass;
                rb.linearDamping = 0.15f;
                rb.angularDamping = 0.9f;
                rb.interpolation = RigidbodyInterpolation.None;
                rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
                rb.isKinematic = true;

                var zone = t.gameObject.AddComponent<HitZone>();
                zone.Part = s.Part;
                zone.Owner = health;

                bodies[s.Bone] = rb;
                ctrl.Bodies.Add(rb);
                ctrl.Colliders.Add(cap);

                if (s.HasJoint && bodies.TryGetValue(s.Parent, out var parentBody))
                {
                    var joint = t.gameObject.AddComponent<CharacterJoint>();
                    joint.connectedBody = parentBody;
                    joint.enablePreprocessing = false;
                    joint.enableCollision = false;
                    var low = joint.lowTwistLimit; low.limit = -s.TwistLimit; joint.lowTwistLimit = low;
                    var high = joint.highTwistLimit; high.limit = s.TwistLimit; joint.highTwistLimit = high;
                    var sw1 = joint.swing1Limit; sw1.limit = s.SwingLimit; joint.swing1Limit = sw1;
                    var sw2 = joint.swing2Limit; sw2.limit = s.SwingLimit * 0.5f; joint.swing2Limit = sw2;
                }
            }

            // Bones of one body never collide with each other.
            for (int i = 0; i < ctrl.Colliders.Count; i++)
            for (int k = i + 1; k < ctrl.Colliders.Count; k++)
                Physics.IgnoreCollision(ctrl.Colliders[i], ctrl.Colliders[k], true);

            if (mainCollider != null)
                for (int i = 0; i < ctrl.Colliders.Count; i++)
                    Physics.IgnoreCollision(ctrl.Colliders[i], mainCollider, true);

            ctrl.Setup(rig, mainCollider, mainBody);
            return ctrl;
        }
    }
}
