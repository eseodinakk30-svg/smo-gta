using UnityEngine;
using SanMonica.Core;

namespace SanMonica.CameraRig
{
    public enum CameraMode { ThirdPerson, FirstPerson, Vehicle, VehicleFirstPerson, Cinematic, Fixed }

    /// <summary>
    /// Player camera rig: smoothed third person orbit with obstacle avoidance,
    /// a real first person mode, aim framing, dynamic vehicle distance and
    /// shake. Everything is driven from <see cref="InputHub"/> look deltas so it
    /// behaves identically on touch, gamepad and mouse.
    /// </summary>
    public class GameCamera : MonoBehaviour
    {
        [Header("References")]
        public Camera Cam;
        public Transform Target;

        [Header("Orientation")]
        public float Yaw;
        public float Pitch = 12f;
        public float MinPitch = -45f;
        public float MaxPitch = 72f;

        [Header("Framing")]
        public float WalkDistance = 4.2f;
        public float AimDistance = 1.9f;
        public float VehicleDistanceBase = 6.5f;
        public float Height = 1.55f;
        public float ShoulderOffset = 0.55f;
        public float FollowSharpness = 12f;
        public float RotationSharpness = 18f;

        [Header("Field of view")]
        public float BaseFov = 62f;
        public float AimFov = 42f;
        public float SpeedFovBoost = 16f;

        [Header("Collision")]
        public float CollisionRadius = 0.28f;
        public float MinCollisionDistance = 0.8f;

        public CameraMode Mode = CameraMode.ThirdPerson;
        public bool Aiming;
        public bool AutoAlignToVehicle = true;

        private Vector3 _smoothTargetPos;
        private float _currentDistance;
        private float _shakeAmount;
        private float _shakeDecay;
        private Vector3 _shakeOffset;
        private float _targetSpeed;
        private Transform _vehicleTransform;
        private float _fovVelocity;
        private float _currentFov;
        private Vector3 _cinematicPosition;
        private Vector3 _cinematicLookAt;
        private float _underwaterBlend;

        public bool IsFirstPerson => Mode == CameraMode.FirstPerson || Mode == CameraMode.VehicleFirstPerson;
        public Vector3 AimOrigin => Cam != null ? Cam.transform.position : transform.position;
        public Vector3 AimDirection => Cam != null ? Cam.transform.forward : transform.forward;

        public void Initialize()
        {
            if (Cam == null)
            {
                var go = new GameObject("MainCamera");
                go.transform.SetParent(transform, false);
                go.tag = "MainCamera";
                Cam = go.AddComponent<Camera>();
                Cam.nearClipPlane = 0.12f;
                Cam.farClipPlane = 1800f;
                go.AddComponent<AudioListener>();
            }
            _currentDistance = WalkDistance;
            _currentFov = BaseFov;
            Cam.fieldOfView = BaseFov;
        }

        public void SetTarget(Transform target)
        {
            Target = target;
            if (target != null) _smoothTargetPos = target.position + Vector3.up * Height;
        }

        public void SetVehicle(Transform vehicle)
        {
            _vehicleTransform = vehicle;
            Mode = vehicle != null ? CameraMode.Vehicle : CameraMode.ThirdPerson;
        }

        public void ToggleFirstPerson()
        {
            if (_vehicleTransform != null)
                Mode = Mode == CameraMode.Vehicle ? CameraMode.VehicleFirstPerson : CameraMode.Vehicle;
            else
                Mode = Mode == CameraMode.ThirdPerson ? CameraMode.FirstPerson : CameraMode.ThirdPerson;
        }

        public void Shake(float amount, float duration = 0.35f)
        {
            _shakeAmount = Mathf.Max(_shakeAmount, amount);
            _shakeDecay = Mathf.Max(0.05f, duration);
        }

        public void SetCinematic(Vector3 position, Vector3 lookAt)
        {
            Mode = CameraMode.Cinematic;
            _cinematicPosition = position;
            _cinematicLookAt = lookAt;
        }

        public void EndCinematic()
        {
            Mode = _vehicleTransform != null ? CameraMode.Vehicle : CameraMode.ThirdPerson;
        }

        public void SetTargetSpeed(float speed) => _targetSpeed = speed;

        public void ApplyLook(Vector2 delta)
        {
            Yaw += delta.x;
            Pitch = Mathf.Clamp(Pitch - delta.y, MinPitch, MaxPitch);
        }

        public void SnapBehind(Transform t)
        {
            if (t == null) return;
            Yaw = t.eulerAngles.y;
        }

        private void LateUpdate()
        {
            if (Cam == null) return;
            float dt = Time.deltaTime > 0f ? Time.deltaTime : Time.unscaledDeltaTime;

            if (_shakeAmount > 0f)
            {
                _shakeAmount = Mathf.MoveTowards(_shakeAmount, 0f, dt / _shakeDecay);
                _shakeOffset = Random.insideUnitSphere * _shakeAmount * 0.35f;
            }
            else _shakeOffset = Vector3.zero;

            if (Mode == CameraMode.Cinematic)
            {
                Cam.transform.position = Vector3.Lerp(Cam.transform.position, _cinematicPosition + _shakeOffset, 1f - Mathf.Exp(-6f * dt));
                var look = _cinematicLookAt - Cam.transform.position;
                if (look.sqrMagnitude > 0.001f)
                    Cam.transform.rotation = Quaternion.Slerp(Cam.transform.rotation, Quaternion.LookRotation(look), 1f - Mathf.Exp(-8f * dt));
                UpdateFov(dt, BaseFov);
                return;
            }

            if (Target == null) return;

            bool inVehicle = _vehicleTransform != null;
            Vector3 focus = inVehicle
                ? _vehicleTransform.position + Vector3.up * (Height * 0.85f)
                : Target.position + Vector3.up * Height;

            _smoothTargetPos = Vector3.Lerp(_smoothTargetPos, focus, 1f - Mathf.Exp(-FollowSharpness * dt));

            // Vehicles gently re-centre behind the direction of travel.
            if (inVehicle && AutoAlignToVehicle && _targetSpeed > 3f)
            {
                float targetYaw = _vehicleTransform.eulerAngles.y;
                float align = Mathf.Clamp01((_targetSpeed - 3f) / 18f) * 2.4f;
                Yaw = Mathf.LerpAngle(Yaw, targetYaw, 1f - Mathf.Exp(-align * dt));
            }

            var rotation = Quaternion.Euler(Pitch, Yaw, 0f);

            if (IsFirstPerson)
            {
                Vector3 eye = inVehicle
                    ? _vehicleTransform.TransformPoint(new Vector3(-0.35f, 0.55f, 0.15f))
                    : Target.position + Vector3.up * (Height + 0.12f) + rotation * Vector3.forward * 0.12f;
                Cam.transform.position = eye + _shakeOffset;
                Cam.transform.rotation = rotation;
                UpdateFov(dt, Aiming ? AimFov * 0.9f : BaseFov + Mathf.Clamp01(_targetSpeed / 45f) * SpeedFovBoost);
                return;
            }

            float desired = inVehicle
                ? VehicleDistanceBase + Mathf.Clamp01(_targetSpeed / 40f) * 2.4f
                : (Aiming ? AimDistance : WalkDistance);

            _currentDistance = Mathf.Lerp(_currentDistance, desired, 1f - Mathf.Exp(-8f * dt));

            Vector3 shoulder = rotation * new Vector3(Aiming && !inVehicle ? ShoulderOffset : 0f, 0f, 0f);
            Vector3 origin = _smoothTargetPos + shoulder;
            Vector3 back = rotation * Vector3.back;
            float distance = _currentDistance;

            if (Physics.SphereCast(origin, CollisionRadius, back, out var hit, distance + 0.35f,
                    GameLayers.CameraCollisionMask, QueryTriggerInteraction.Ignore))
                distance = Mathf.Max(MinCollisionDistance, hit.distance - 0.25f);

            Vector3 wanted = origin + back * distance;
            Cam.transform.position = wanted + _shakeOffset;
            Cam.transform.rotation = Quaternion.Slerp(Cam.transform.rotation, rotation, 1f - Mathf.Exp(-RotationSharpness * dt));

            float speedFov = Mathf.Clamp01(_targetSpeed / 45f) * SpeedFovBoost;
            UpdateFov(dt, Aiming ? AimFov : BaseFov + speedFov);
        }

        private void UpdateFov(float dt, float target)
        {
            _currentFov = Mathf.SmoothDamp(_currentFov, target, ref _fovVelocity, 0.14f, Mathf.Infinity, dt);
            Cam.fieldOfView = _currentFov;
        }

        /// <summary>Called by the player controller so the camera can tint and fog underwater.</summary>
        public void SetUnderwater(bool underwater, float dt)
        {
            _underwaterBlend = Mathf.MoveTowards(_underwaterBlend, underwater ? 1f : 0f, dt * 3f);
            if (Cam == null) return;
            if (_underwaterBlend > 0.001f)
            {
                RenderSettings.fogColor = Color.Lerp(RenderSettings.fogColor, new Color(0.06f, 0.24f, 0.33f), _underwaterBlend * 0.6f);
                Cam.backgroundColor = Color.Lerp(Cam.backgroundColor, new Color(0.05f, 0.20f, 0.30f), _underwaterBlend);
            }
        }

        /// <summary>World ray used for aiming and interaction.</summary>
        public Ray ScreenCenterRay()
        {
            return Cam != null ? Cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f)) : new Ray(transform.position, transform.forward);
        }
    }
}
