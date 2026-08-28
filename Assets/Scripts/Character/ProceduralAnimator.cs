using UnityEngine;

namespace SanMonica.Characters
{
    /// <summary>
    /// Code-driven animation for the humanoid rig. Parameters (speed, strafe,
    /// crouch, aim, airborne, swim, sit) are blended continuously the same way a
    /// blend tree would, but evaluated procedurally - no animation clips needed
    /// and the whole thing costs a few dozen quaternion operations per character.
    /// </summary>
    public class ProceduralAnimator : MonoBehaviour
    {
        [Header("Parameters")]
        public float Speed;              // horizontal speed in m/s
        public float Forward = 1f;       // -1..1 local forward component
        public float Strafe;             // -1..1 local right component
        public float Turn;               // yaw rate, drives lean
        public bool Grounded = true;
        public bool Crouching;
        public bool Swimming;
        public bool Aiming;
        public bool Sitting;
        public bool Driving;
        public bool Climbing;
        public bool Dead;
        public bool TwoHanded;
        public float AimPitch;
        public float VerticalVelocity;

        [Header("Tuning")]
        public float WalkSpeedReference = 1.5f;
        public float RunSpeedReference = 5.6f;
        public float StrideFrequency = 1.05f;
        public int UpdateInterval = 1;   // raised by the AI LOD manager for distant peds

        private CharacterRig _rig;
        private float _phase;
        private float _blendRun;
        private float _blendCrouch;
        private float _blendAim;
        private float _blendSwim;
        private float _blendSit;
        private float _recoil;
        private float _meleeTimer;
        private float _hitReact;
        private float _lean;
        private int _frameCounter;
        private float _accumulatedDelta;
        private float _idleTimer;
        private float _breath;

        public float StridePhase => _phase;

        public void Bind(CharacterRig rig)
        {
            _rig = rig;
            _phase = Random.value * Mathf.PI * 2f;
            _idleTimer = Random.value * 10f;
        }

        public void TriggerRecoil(float amount) => _recoil = Mathf.Clamp01(_recoil + amount);
        public void TriggerMelee() => _meleeTimer = 0.55f;
        public void TriggerHitReaction(float amount) => _hitReact = Mathf.Clamp01(_hitReact + amount);

        private void LateUpdate()
        {
            if (_rig == null || Dead) return;
            _accumulatedDelta += Time.deltaTime;
            _frameCounter++;
            if (UpdateInterval > 1 && (_frameCounter % UpdateInterval) != 0) return;

            float dt = _accumulatedDelta;
            _accumulatedDelta = 0f;
            Evaluate(dt);
        }

        private void Evaluate(float dt)
        {
            // ---- Blend weights ----
            float runTarget = Mathf.InverseLerp(WalkSpeedReference, RunSpeedReference, Speed);
            _blendRun = Mathf.MoveTowards(_blendRun, runTarget, dt * 4f);
            _blendCrouch = Mathf.MoveTowards(_blendCrouch, Crouching ? 1f : 0f, dt * 6f);
            _blendAim = Mathf.MoveTowards(_blendAim, Aiming ? 1f : 0f, dt * 8f);
            _blendSwim = Mathf.MoveTowards(_blendSwim, Swimming ? 1f : 0f, dt * 4f);
            _blendSit = Mathf.MoveTowards(_blendSit, (Sitting || Driving) ? 1f : 0f, dt * 8f);
            _recoil = Mathf.MoveTowards(_recoil, 0f, dt * 4.5f);
            _hitReact = Mathf.MoveTowards(_hitReact, 0f, dt * 2.5f);
            _meleeTimer = Mathf.Max(0f, _meleeTimer - dt);
            _breath += dt * 1.6f;
            _idleTimer += dt;

            float moveAmount = Mathf.Clamp01(Speed / Mathf.Max(0.1f, RunSpeedReference));
            float freq = StrideFrequency * Mathf.Lerp(2.4f, 4.6f, _blendRun) * Mathf.Lerp(1f, 0.75f, _blendCrouch);
            if (Speed > 0.08f) _phase += dt * freq * Mathf.Lerp(0.6f, 1f, moveAmount) * Mathf.PI;
            else _phase = Mathf.Lerp(_phase, 0f, dt * 6f);

            _lean = Mathf.Lerp(_lean, Mathf.Clamp(Turn, -1f, 1f), dt * 5f);

            _rig.ResetToBindPose();

            if (_blendSit > 0.01f) ApplySitting(_blendSit);
            if (_blendSwim > 0.01f) ApplySwimming(_blendSwim, dt);
            if (_blendSit < 0.99f && _blendSwim < 0.99f) ApplyLocomotion(dt, moveAmount);

            ApplyUpperBody(dt);
        }

        // ------------------------------------------------------------------
        private void ApplyLocomotion(float dt, float moveAmount)
        {
            float w = (1f - _blendSit) * (1f - _blendSwim);
            float sin = Mathf.Sin(_phase);
            float cos = Mathf.Cos(_phase);

            float legSwing = Mathf.Lerp(24f, 46f, _blendRun) * moveAmount * w;
            float kneeBend = Mathf.Lerp(18f, 52f, _blendRun) * moveAmount * w;
            float armSwing = Mathf.Lerp(20f, 42f, _blendRun) * moveAmount * w * (Aiming ? 0.25f : 1f);

            // Airborne pose overrides the stride.
            if (!Grounded)
            {
                float up = Mathf.Clamp(VerticalVelocity / 6f, -1f, 1f);
                Rotate(HumanBone.LeftUpperLeg, new Vector3(-28f + up * 18f, 0f, 0f) * w);
                Rotate(HumanBone.RightUpperLeg, new Vector3(-8f - up * 12f, 0f, 0f) * w);
                Rotate(HumanBone.LeftLowerLeg, new Vector3(38f, 0f, 0f) * w);
                Rotate(HumanBone.RightLowerLeg, new Vector3(14f, 0f, 0f) * w);
                Rotate(HumanBone.LeftUpperArm, new Vector3(-25f, 0f, -34f) * w);
                Rotate(HumanBone.RightUpperArm, new Vector3(-25f, 0f, 34f) * w);
                Rotate(HumanBone.Spine, new Vector3(6f, 0f, 0f) * w);
                return;
            }

            // Legs
            Rotate(HumanBone.LeftUpperLeg, new Vector3(sin * legSwing, 0f, 0f));
            Rotate(HumanBone.RightUpperLeg, new Vector3(-sin * legSwing, 0f, 0f));
            Rotate(HumanBone.LeftLowerLeg, new Vector3(Mathf.Max(0f, -sin) * kneeBend, 0f, 0f));
            Rotate(HumanBone.RightLowerLeg, new Vector3(Mathf.Max(0f, sin) * kneeBend, 0f, 0f));
            Rotate(HumanBone.LeftFoot, new Vector3(-sin * legSwing * 0.35f, 0f, 0f));
            Rotate(HumanBone.RightFoot, new Vector3(sin * legSwing * 0.35f, 0f, 0f));

            // Arms counter-swing
            Rotate(HumanBone.LeftUpperArm, new Vector3(-sin * armSwing, 0f, -6f * w));
            Rotate(HumanBone.RightUpperArm, new Vector3(sin * armSwing, 0f, 6f * w));
            Rotate(HumanBone.LeftLowerArm, new Vector3(-Mathf.Abs(sin) * armSwing * 0.4f - 8f * w, 0f, 0f));
            Rotate(HumanBone.RightLowerArm, new Vector3(-Mathf.Abs(sin) * armSwing * 0.4f - 8f * w, 0f, 0f));

            // Torso bob, lean into the turn and forward pitch when running
            float bob = Mathf.Abs(cos) * 0.022f * moveAmount * Mathf.Lerp(0.6f, 1.4f, _blendRun);
            var hips = _rig.Bone(HumanBone.Hips);
            if (hips != null)
            {
                Vector3 p = _rig.BindPosition(HumanBone.Hips);
                p.y += bob - _blendCrouch * _rig.Height * 0.16f;
                hips.localPosition = p;
            }
            Rotate(HumanBone.Hips, new Vector3(0f, 0f, -_lean * 7f));
            Rotate(HumanBone.Spine, new Vector3(_blendRun * 9f * moveAmount + _blendCrouch * 16f, sin * 3f * moveAmount, -_lean * 5f));
            Rotate(HumanBone.Chest, new Vector3(_blendCrouch * 8f, -sin * 4f * moveAmount, 0f));

            // Idle micro-motion so standing characters are never statues.
            if (Speed < 0.08f)
            {
                float b = Mathf.Sin(_breath) * 1.6f;
                Rotate(HumanBone.Chest, new Vector3(b * 0.5f, 0f, 0f));
                Rotate(HumanBone.LeftUpperArm, new Vector3(0f, 0f, -3f - b * 0.4f));
                Rotate(HumanBone.RightUpperArm, new Vector3(0f, 0f, 3f + b * 0.4f));
                if (_idleTimer > 7f)
                {
                    float t = Mathf.Clamp01((_idleTimer - 7f) / 1.2f);
                    Rotate(HumanBone.Neck, new Vector3(0f, Mathf.Sin(t * Mathf.PI) * 22f, 0f));
                    if (_idleTimer > 8.5f) _idleTimer = Random.Range(0f, 4f);
                }
            }

            if (Climbing)
            {
                Rotate(HumanBone.LeftUpperArm, new Vector3(-120f, 0f, -20f));
                Rotate(HumanBone.RightUpperArm, new Vector3(-120f + Mathf.Sin(_phase) * 30f, 0f, 20f));
                Rotate(HumanBone.LeftUpperLeg, new Vector3(35f, 0f, 0f));
                Rotate(HumanBone.RightUpperLeg, new Vector3(10f, 0f, 0f));
            }
        }

        private void ApplySwimming(float w, float dt)
        {
            _phase += dt * 3.2f;
            float sin = Mathf.Sin(_phase);
            Rotate(HumanBone.Hips, new Vector3(72f * w, 0f, 0f));
            Rotate(HumanBone.Spine, new Vector3(-14f * w, 0f, 0f));
            Rotate(HumanBone.Chest, new Vector3(-10f * w, 0f, 0f));
            Rotate(HumanBone.Neck, new Vector3(-30f * w, 0f, 0f));
            Rotate(HumanBone.LeftUpperArm, new Vector3((-70f + sin * 60f) * w, 0f, -22f * w));
            Rotate(HumanBone.RightUpperArm, new Vector3((-70f - sin * 60f) * w, 0f, 22f * w));
            Rotate(HumanBone.LeftUpperLeg, new Vector3(sin * 22f * w, 0f, 0f));
            Rotate(HumanBone.RightUpperLeg, new Vector3(-sin * 22f * w, 0f, 0f));
        }

        private void ApplySitting(float w)
        {
            Rotate(HumanBone.LeftUpperLeg, new Vector3(-78f * w, 0f, 4f * w));
            Rotate(HumanBone.RightUpperLeg, new Vector3(-78f * w, 0f, -4f * w));
            Rotate(HumanBone.LeftLowerLeg, new Vector3(72f * w, 0f, 0f));
            Rotate(HumanBone.RightLowerLeg, new Vector3(72f * w, 0f, 0f));
            Rotate(HumanBone.Spine, new Vector3(6f * w, 0f, 0f));

            if (Driving)
            {
                float steer = Mathf.Clamp(Turn, -1f, 1f);
                Rotate(HumanBone.LeftUpperArm, new Vector3(-62f * w, 0f, -26f * w + steer * 12f));
                Rotate(HumanBone.RightUpperArm, new Vector3(-62f * w, 0f, 26f * w + steer * 12f));
                Rotate(HumanBone.LeftLowerArm, new Vector3(-42f * w, 0f, 0f));
                Rotate(HumanBone.RightLowerArm, new Vector3(-42f * w, 0f, 0f));
            }
            else
            {
                Rotate(HumanBone.LeftUpperArm, new Vector3(-24f * w, 0f, -10f * w));
                Rotate(HumanBone.RightUpperArm, new Vector3(-24f * w, 0f, 10f * w));
                Rotate(HumanBone.LeftLowerArm, new Vector3(-52f * w, 0f, 0f));
                Rotate(HumanBone.RightLowerArm, new Vector3(-52f * w, 0f, 0f));
            }
        }

        private void ApplyUpperBody(float dt)
        {
            // Aiming raises the arms toward the look direction.
            if (_blendAim > 0.01f)
            {
                float pitch = Mathf.Clamp(AimPitch, -60f, 60f);
                float a = _blendAim;
                Rotate(HumanBone.RightUpperArm, new Vector3((-78f - pitch) * a, 0f, 16f * a));
                Rotate(HumanBone.RightLowerArm, new Vector3(-12f * a, 0f, 0f));
                if (TwoHanded)
                {
                    Rotate(HumanBone.LeftUpperArm, new Vector3((-72f - pitch) * a, 0f, -34f * a));
                    Rotate(HumanBone.LeftLowerArm, new Vector3(-46f * a, 0f, 0f));
                }
                else
                {
                    Rotate(HumanBone.LeftUpperArm, new Vector3(-24f * a, 0f, -12f * a));
                    Rotate(HumanBone.LeftLowerArm, new Vector3(-38f * a, 0f, 0f));
                }
                Rotate(HumanBone.Chest, new Vector3(0f, -14f * a, 0f));
                Rotate(HumanBone.Neck, new Vector3(-pitch * 0.35f * a, 0f, 0f));
            }

            if (_recoil > 0.001f)
            {
                float r = _recoil;
                Rotate(HumanBone.RightUpperArm, new Vector3(18f * r, 0f, 0f));
                Rotate(HumanBone.RightLowerArm, new Vector3(14f * r, 0f, 0f));
                Rotate(HumanBone.Chest, new Vector3(-6f * r, 0f, 0f));
                Rotate(HumanBone.Neck, new Vector3(-5f * r, 0f, 0f));
            }

            if (_meleeTimer > 0f)
            {
                float t = 1f - _meleeTimer / 0.55f;
                float swing = Mathf.Sin(t * Mathf.PI);
                Rotate(HumanBone.RightUpperArm, new Vector3(-140f * swing, 0f, 40f * swing));
                Rotate(HumanBone.RightLowerArm, new Vector3(-60f * swing, 0f, 0f));
                Rotate(HumanBone.Chest, new Vector3(0f, -45f * swing, 0f));
                Rotate(HumanBone.Hips, new Vector3(0f, -18f * swing, 0f));
            }

            if (_hitReact > 0.001f)
            {
                float r = _hitReact;
                Rotate(HumanBone.Spine, new Vector3(-10f * r, 0f, 6f * r));
                Rotate(HumanBone.Neck, new Vector3(-14f * r, 0f, 0f));
            }
        }

        // ------------------------------------------------------------------
        private void Rotate(HumanBone bone, Vector3 euler)
        {
            var t = _rig.Bone(bone);
            if (t == null) return;
            t.localRotation *= Quaternion.Euler(euler);
        }
    }
}
