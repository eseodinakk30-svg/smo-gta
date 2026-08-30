using UnityEngine;
using SanMonica.CameraRig;
using SanMonica.Characters;
using SanMonica.Core;
using SanMonica.Data;
using SanMonica.Vehicles;

namespace SanMonica.Players
{
    /// <summary>
    /// Dominic Vela on foot: walking, running, sprinting, crouching, jumping,
    /// vaulting, climbing, swimming, diving, melee, gunplay, interaction and
    /// getting in and out of anything with an engine.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Movement")]
        public float WalkSpeed = 1.9f;
        public float RunSpeed = 4.4f;
        public float SprintSpeed = 6.6f;
        public float CrouchSpeed = 1.25f;
        public float AimStrafeSpeed = 2.1f;
        public float SwimSpeed = 2.0f;
        public float SwimSprintSpeed = 3.4f;
        public float DiveSpeed = 2.4f;
        public float Acceleration = 16f;
        public float AirAcceleration = 5f;
        public float JumpHeight = 1.15f;
        public float RotationSpeed = 14f;

        [Header("Traversal")]
        public float VaultMaxHeight = 1.35f;
        public float ClimbMaxHeight = 2.25f;
        public float VaultReach = 1.0f;

        [Header("Damage")]
        public float SafeFallHeight = 5.5f;
        public float FallDamagePerMetre = 11f;

        public CharacterController Controller { get; private set; }
        public CharacterRig Rig { get; private set; }
        public ProceduralAnimator Animator { get; private set; }
        public CharacterHealth Health { get; private set; }
        public RagdollController Ragdoll { get; private set; }
        public SanMonica.Weapons.WeaponHolder Weapons { get; private set; }
        public Vehicle CurrentVehicle { get; private set; }
        public int CurrentSeat { get; private set; }

        public bool IsSwimming { get; private set; }
        public bool IsUnderwater { get; private set; }
        public bool IsSprinting { get; private set; }
        public bool IsCrouching { get; private set; }
        public bool IsAiming { get; private set; }
        public bool IsGrounded { get; private set; }

        /// <summary>
        /// Set while the world is still streaming in. The player exists so the
        /// streamer and the population have something to centre on, but gravity
        /// must not run: there are no colliders yet, and a few seconds of free
        /// fall puts them kilometres under the city before the ground appears.
        /// </summary>
        public bool Frozen;
        public bool InVehicle => CurrentVehicle != null;
        public float CurrentSpeed { get; private set; }
        public string NearbyPrompt { get; private set; }

        private Vector3 _velocity;
        private float _verticalVelocity;

        /// <summary>
        /// Falling is capped well below the speed at which a CharacterController
        /// starts stepping through thin colliders in a single frame.
        /// </summary>
        private const float TerminalVelocity = -55f;
        private float _fallStartY;
        private bool _wasGrounded = true;
        private float _vaultTimer;
        private Vector3 _vaultStart, _vaultEnd;
        private float _vaultDuration;
        private float _enterVehicleCooldown;
        private float _footstepTimer;
        private GameCamera _camera;
        private InputHub _input;
        private Vehicle _nearestVehicle;
        private SanMonica.World.ShopInstance _nearbyShop;
        private SanMonica.World.PropertyInstance _nearbyProperty;
        private float _interactScanTimer;
        private float _swimSurfaceY;

        public void Initialize(CharacterAppearance appearance)
        {
            Controller = GetComponent<CharacterController>();
            Controller.height = appearance.Height * 0.98f;
            Controller.radius = 0.30f;
            Controller.center = new Vector3(0f, Controller.height * 0.5f, 0f);
            Controller.slopeLimit = 52f;
            Controller.stepOffset = 0.45f;
            Controller.skinWidth = 0.035f;
            gameObject.layer = GameLayers.Player;

            Rig = CharacterRigBuilder.Build(gameObject, appearance);
            Animator = gameObject.GetComponent<ProceduralAnimator>();
            if (Animator == null) Animator = gameObject.AddComponent<ProceduralAnimator>();
            Animator.Bind(Rig);

            Health = gameObject.GetComponent<CharacterHealth>();
            if (Health == null) Health = gameObject.AddComponent<CharacterHealth>();
            Health.IsPlayer = true;
            Health.RegenerateHealth = true;
            Health.MaxHealth = 200f;
            Health.MaxArmour = 100f;

            Ragdoll = RagdollBuilder.Build(Rig, Health, Controller, null);
            Health.Bind(Ragdoll, Animator);
            Health.ResetVitals(200f, 0f);

            Weapons = gameObject.GetComponent<SanMonica.Weapons.WeaponHolder>();
            if (Weapons == null) Weapons = gameObject.AddComponent<SanMonica.Weapons.WeaponHolder>();
            Weapons.Initialize(Rig, Animator, Health, true);

            _camera = Services.Camera;
            _input = Services.Input;
            _fallStartY = transform.position.y;
        }

        public void Teleport(Vector3 position, float heading)
        {
            Controller.enabled = false;
            transform.position = position;
            transform.rotation = Quaternion.Euler(0f, heading, 0f);
            Controller.enabled = true;
            _velocity = Vector3.zero;
            _verticalVelocity = 0f;
            _fallStartY = position.y;
            if (_camera != null) { _camera.Yaw = heading; _camera.SetTarget(transform); }
        }

        private void Update()
        {
            if (_camera == null) _camera = Services.Camera;
            if (_input == null) _input = Services.Input;
            if (_input == null || Services.Game == null) return;

            float dt = Time.deltaTime;
            if (_enterVehicleCooldown > 0f) _enterVehicleCooldown -= dt;

            bool interactive = Services.Game.State == GameState.Playing;

            if (_camera != null && interactive)
                _camera.ApplyLook(_input.Look);

            if (!Health.IsAlive)
            {
                CurrentSpeed = 0f;
                return;
            }

            if (Frozen) { CurrentSpeed = 0f; _verticalVelocity = 0f; return; }

            if (InVehicle) { UpdateInVehicle(dt, interactive); return; }
            if (!interactive) { ApplyGravityOnly(dt); return; }

            if (_vaultTimer > 0f) { UpdateVault(dt); return; }

            UpdateWaterState(dt);
            UpdateInteractionScan(dt);
            UpdateCombat(dt);

            if (IsSwimming) UpdateSwimming(dt);
            else UpdateGroundMovement(dt);

            UpdateAnimatorParameters();
        }

        // ------------------------------------------------------------------
        private void ApplyGravityOnly(float dt)
        {
            _verticalVelocity = Mathf.Max(_verticalVelocity + Services.Config.gravity * dt, TerminalVelocity);
            Controller.Move(new Vector3(0f, _verticalVelocity * dt, 0f));
        }

        private void UpdateWaterState(float dt)
        {
            var water = Services.Water;
            if (water == null) { IsSwimming = false; IsUnderwater = false; return; }

            Vector3 chest = transform.position + Vector3.up * (Controller.height * 0.62f);
            Vector3 head = transform.position + Vector3.up * (Controller.height * 0.94f);
            float surface = water.SurfaceAt(transform.position);
            _swimSurfaceY = surface;

            bool wasSwimming = IsSwimming;
            IsSwimming = surface != float.MinValue && chest.y < surface;
            IsUnderwater = surface != float.MinValue && head.y < surface;

            Health.TickUnderwater(dt, IsUnderwater);
            _camera?.SetUnderwater(IsUnderwater, dt);

            if (IsSwimming && !wasSwimming)
            {
                _verticalVelocity = Mathf.Max(_verticalVelocity, -3f);
                _fallStartY = transform.position.y;
                Services.Audio?.PlayOneShot("splash", transform.position, 0.9f);
            }
        }

        private void UpdateGroundMovement(float dt)
        {
            IsGrounded = Controller.isGrounded;
            IsCrouching = _input.Crouch && IsGrounded;
            IsSprinting = _input.Sprint && !IsCrouching && _input.Move.sqrMagnitude > 0.25f && !IsAiming;

            Vector3 camForward = _camera != null ? Vector3.ProjectOnPlane(_camera.Cam.transform.forward, Vector3.up).normalized : transform.forward;
            if (camForward.sqrMagnitude < 0.001f) camForward = transform.forward;
            Vector3 camRight = Vector3.Cross(Vector3.up, camForward);

            Vector3 wish = camRight * _input.Move.x + camForward * _input.Move.y;
            float wishMag = Mathf.Clamp01(wish.magnitude);
            if (wishMag > 0.001f) wish /= wishMag;

            float targetSpeed =
                IsCrouching ? CrouchSpeed :
                IsAiming ? AimStrafeSpeed :
                IsSprinting ? SprintSpeed :
                (wishMag > 0.65f ? RunSpeed : WalkSpeed);
            targetSpeed *= wishMag;

            Vector3 horizontal = new Vector3(_velocity.x, 0f, _velocity.z);
            float accel = IsGrounded ? Acceleration : AirAcceleration;
            horizontal = Vector3.MoveTowards(horizontal, wish * targetSpeed, accel * dt);
            _velocity.x = horizontal.x;
            _velocity.z = horizontal.z;

            // Facing
            if (IsAiming && _camera != null)
            {
                float yaw = _camera.Yaw;
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(0f, yaw, 0f), 1f - Mathf.Exp(-RotationSpeed * 1.6f * dt));
            }
            else if (horizontal.sqrMagnitude > 0.05f)
            {
                var look = Quaternion.LookRotation(horizontal.normalized, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, look, 1f - Mathf.Exp(-RotationSpeed * dt));
            }

            // Vertical
            if (IsGrounded)
            {
                if (_verticalVelocity < 0f) _verticalVelocity = -2f;
                if (!_wasGrounded)
                {
                    float fall = _fallStartY - transform.position.y;
                    if (fall > SafeFallHeight)
                    {
                        float dmg = (fall - SafeFallHeight) * FallDamagePerMetre;
                        Health.ApplyDamage(DamageInfo.Simple(dmg, DamageKind.Fall, null, transform.position, Vector3.down));
                        _camera?.Shake(Mathf.Clamp01(dmg / 60f) * 0.6f);
                    }
                    Services.Audio?.PlayOneShot("land", transform.position, 0.6f);
                }
                _fallStartY = transform.position.y;

                if (_input.JumpPressed)
                {
                    if (!TryVaultOrClimb())
                    {
                        _verticalVelocity = Mathf.Sqrt(2f * -Services.Config.gravity * JumpHeight);
                        Services.Audio?.PlayOneShot("jump", transform.position, 0.4f);
                    }
                }
            }
            else
            {
                _verticalVelocity = Mathf.Max(_verticalVelocity + Services.Config.gravity * dt, TerminalVelocity);
                if (transform.position.y > _fallStartY) _fallStartY = transform.position.y;
                if (_input.JumpPressed) TryVaultOrClimb();
            }

            _wasGrounded = IsGrounded;
            Vector3 motion = new Vector3(_velocity.x, _verticalVelocity, _velocity.z) * dt;
            Controller.Move(motion);
            CurrentSpeed = new Vector2(_velocity.x, _velocity.z).magnitude;

            UpdateFootsteps(dt);
        }

        private void UpdateSwimming(float dt)
        {
            IsGrounded = false;
            IsCrouching = false;
            IsSprinting = _input.Sprint;

            Vector3 camForward = _camera != null ? _camera.Cam.transform.forward : transform.forward;
            Vector3 flatForward = Vector3.ProjectOnPlane(camForward, Vector3.up).normalized;
            Vector3 camRight = Vector3.Cross(Vector3.up, flatForward);

            Vector3 wish = camRight * _input.Move.x + flatForward * _input.Move.y;
            float speed = IsSprinting ? SwimSprintSpeed : SwimSpeed;

            // Diving: look down and hold forward, or hold crouch.
            float dive = 0f;
            if (_input.Crouch) dive = -DiveSpeed;
            else if (_input.JumpPressed || _input.Sprint && camForward.y > 0.35f) dive = DiveSpeed * 0.7f;
            else if (_input.Move.y > 0.2f) dive = camForward.y * DiveSpeed;

            Vector3 target = wish * speed + Vector3.up * dive;

            // Buoyancy pushes the body back to the surface when idle.
            float surface = _swimSurfaceY;
            if (surface != float.MinValue)
            {
                float submersion = surface - (transform.position.y + Controller.height * 0.62f);
                if (submersion > 0f && dive >= 0f)
                    target.y += Mathf.Clamp(submersion * 2.2f, 0f, 2.4f);
            }

            _velocity = Vector3.Lerp(_velocity, target, 1f - Mathf.Exp(-4.5f * dt));
            _verticalVelocity = _velocity.y;
            Controller.Move(_velocity * dt);
            CurrentSpeed = _velocity.magnitude;
            _fallStartY = transform.position.y;
            _wasGrounded = false;

            if (wish.sqrMagnitude > 0.05f)
            {
                var look = Quaternion.LookRotation(new Vector3(wish.x, 0f, wish.z).normalized, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, look, 1f - Mathf.Exp(-6f * dt));
            }
        }

        // ------------------------------------------------------------------
        private bool TryVaultOrClimb()
        {
            Vector3 origin = transform.position + Vector3.up * 0.45f;
            Vector3 dir = transform.forward;
            if (!Physics.Raycast(origin, dir, out var hit, VaultReach + Controller.radius, GameLayers.WorldMask, QueryTriggerInteraction.Ignore))
                return false;

            // Find the top edge of the obstacle.
            Vector3 probeTop = hit.point + dir * 0.35f + Vector3.up * (ClimbMaxHeight + 0.4f);
            if (!Physics.Raycast(probeTop, Vector3.down, out var topHit, ClimbMaxHeight + 0.6f, GameLayers.WorldMask, QueryTriggerInteraction.Ignore))
                return false;

            float ledgeHeight = topHit.point.y - transform.position.y;
            if (ledgeHeight < 0.35f || ledgeHeight > ClimbMaxHeight) return false;

            Vector3 landing = topHit.point + dir * 0.55f + Vector3.up * 0.1f;
            if (Physics.CheckCapsule(landing + Vector3.up * 0.4f, landing + Vector3.up * (Controller.height - 0.4f),
                    Controller.radius * 0.85f, GameLayers.WorldMask, QueryTriggerInteraction.Ignore))
                return false;

            _vaultStart = transform.position;
            _vaultEnd = landing;
            _vaultDuration = ledgeHeight <= VaultMaxHeight ? 0.42f : 0.78f;
            _vaultTimer = _vaultDuration;
            _verticalVelocity = 0f;
            _velocity = Vector3.zero;
            if (Animator != null) Animator.Climbing = ledgeHeight > VaultMaxHeight;
            Services.Audio?.PlayOneShot("vault", transform.position, 0.5f);
            return true;
        }

        private void UpdateVault(float dt)
        {
            _vaultTimer -= dt;
            float t = 1f - Mathf.Clamp01(_vaultTimer / _vaultDuration);
            Vector3 pos = Vector3.Lerp(_vaultStart, _vaultEnd, t);
            pos.y = Mathf.Lerp(_vaultStart.y, _vaultEnd.y, Mathf.Sqrt(t)) + Mathf.Sin(t * Mathf.PI) * 0.18f;
            Controller.enabled = false;
            transform.position = pos;
            Controller.enabled = true;

            if (_vaultTimer <= 0f)
            {
                _vaultTimer = 0f;
                _fallStartY = transform.position.y;
                if (Animator != null) Animator.Climbing = false;
            }
            UpdateAnimatorParameters();
        }

        // ------------------------------------------------------------------
        private void UpdateCombat(float dt)
        {
            IsAiming = _input.Aim && !IsSwimming && Weapons != null && Weapons.CanAim;
            if (_camera != null) _camera.Aiming = IsAiming;

            if (Weapons == null) return;
            Weapons.SetAiming(IsAiming);

            if (_input.NextWeaponPressed) Weapons.CycleWeapon(1);
            if (_input.PrevWeaponPressed) Weapons.CycleWeapon(-1);
            if (_input.ReloadPressed) Weapons.Reload();
            if (_input.MeleePressed) Weapons.Melee();

            if (_input.Fire && !IsSwimming)
            {
                Ray ray = _camera != null ? _camera.ScreenCenterRay() : new Ray(transform.position + Vector3.up * 1.5f, transform.forward);
                if (Weapons.TryFire(ray, IsAiming))
                    _camera?.Shake(Weapons.CurrentDefinition != null ? Weapons.CurrentDefinition.cameraShake : 0.1f, 0.18f);
            }
            else Weapons.ReleaseTrigger();   // semi-automatic weapons need the button to come back up

            if (_input.CameraTogglePressed) _camera?.ToggleFirstPerson();
        }

        private void UpdateInteractionScan(float dt)
        {
            _interactScanTimer -= dt;
            if (_interactScanTimer <= 0f)
            {
                _interactScanTimer = 0.2f;
                _nearestVehicle = FindNearestVehicle(4.2f);
                _nearbyShop = Services.Landmarks?.ShopAt(transform.position, 3.0f);
                _nearbyProperty = Services.Landmarks?.PropertyAt(transform.position, 3.0f);

                if (_nearestVehicle != null) NearbyPrompt = "Enter " + _nearestVehicle.DisplayName;
                else if (_nearbyShop != null) NearbyPrompt = "Enter " + _nearbyShop.Definition.displayName;
                else if (_nearbyProperty != null)
                    NearbyPrompt = _nearbyProperty.Owned
                        ? "Enter " + _nearbyProperty.Definition.displayName
                        : "Buy " + _nearbyProperty.Definition.displayName + " ($" + _nearbyProperty.Definition.price.ToString("N0") + ")";
                else NearbyPrompt = null;
            }

            if (_input.EnterVehiclePressed && _nearestVehicle != null && _enterVehicleCooldown <= 0f)
            {
                EnterVehicle(_nearestVehicle, _nearestVehicle.NearestFreeSeat(transform.position));
                return;
            }

            if (_input.InteractPressed)
            {
                if (_nearbyShop != null) Services.Game.EnterShop(_nearbyShop);
                else if (_nearbyProperty != null) Services.Game.InteractWithProperty(_nearbyProperty);
                else Services.Missions?.TryInteract(transform.position);
            }
        }

        private Vehicle FindNearestVehicle(float radius)
        {
            var hits = Physics.OverlapSphere(transform.position, radius, GameLayers.VehicleMask, QueryTriggerInteraction.Ignore);
            Vehicle best = null;
            float bestScore = float.MaxValue;
            foreach (var h in hits)
            {
                var v = h.GetComponentInParent<Vehicle>();
                if (v == null || v.IsDestroyed) continue;
                float d = Vector3.Distance(transform.position, v.transform.position);
                if (d < bestScore) { bestScore = d; best = v; }
            }
            return best;
        }

        // ------------------------------------------------------------------
        public void EnterVehicle(Vehicle vehicle, int seat)
        {
            if (vehicle == null || InVehicle) return;
            if (!vehicle.TryOccupySeat(seat, gameObject, true)) return;

            CurrentVehicle = vehicle;
            CurrentSeat = seat;
            _enterVehicleCooldown = 0.6f;

            Controller.enabled = false;
            var anchor = vehicle.GetSeatAnchor(seat);
            transform.SetParent(anchor, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;

            if (Animator != null) { Animator.Sitting = true; Animator.Driving = seat == 0; }
            Weapons?.SetHolstered(true);
            _camera?.SetVehicle(vehicle.transform);
            GameEvents.RaisePlayerVehicleChanged(vehicle.gameObject, true);

            if (seat == 0 && !vehicle.IsPlayerOwned && vehicle.HasOwner)
            {
                GameEvents.RaiseCrime(new CrimeEvent
                {
                    Type = CrimeType.VehicleTheft,
                    Position = transform.position,
                    Perpetrator = gameObject
                });
            }
        }

        public void ExitVehicle()
        {
            if (!InVehicle) return;
            var vehicle = CurrentVehicle;
            Vector3 exit = vehicle.GetExitPosition(CurrentSeat);
            vehicle.ReleaseSeat(CurrentSeat);

            transform.SetParent(null, true);
            Controller.enabled = false;
            transform.position = exit;
            transform.rotation = Quaternion.Euler(0f, vehicle.transform.eulerAngles.y, 0f);
            Controller.enabled = true;

            CurrentVehicle = null;
            _enterVehicleCooldown = 0.6f;
            _velocity = vehicle.Body != null ? Vector3.ClampMagnitude(vehicle.Body.velocity, 6f) : Vector3.zero;
            _verticalVelocity = 0f;
            _fallStartY = transform.position.y;

            if (Animator != null) { Animator.Sitting = false; Animator.Driving = false; }
            Weapons?.SetHolstered(false);
            _camera?.SetVehicle(null);
            GameEvents.RaisePlayerVehicleChanged(vehicle.gameObject, false);
        }

        private void UpdateInVehicle(float dt, bool interactive)
        {
            var vehicle = CurrentVehicle;
            if (vehicle == null || vehicle.IsDestroyed) { ExitVehicle(); return; }

            CurrentSpeed = vehicle.SpeedMs;
            _camera?.SetTargetSpeed(vehicle.SpeedMs);

            if (!interactive) { vehicle.SetInput(0f, 1f, 0f, true); return; }

            if (CurrentSeat == 0)
            {
                vehicle.SetInput(_input.Throttle, _input.Brake, _input.Steer, _input.Handbrake);
                vehicle.SetAirInput(_input.Pitch, _input.Roll, _input.Throttle - _input.Brake);
                if (_input.Horn) vehicle.SoundHorn();
            }

            if (Animator != null) Animator.Turn = _input.Steer;

            // Drive-by shooting from any seat.
            IsAiming = _input.Aim && Weapons != null && Weapons.CanAim;
            if (_camera != null) _camera.Aiming = IsAiming;
            Weapons?.SetAiming(IsAiming);
            if (Weapons != null && !_input.Fire) Weapons.ReleaseTrigger();
            if (_input.Fire && Weapons != null && vehicle.AllowsDriveBy)
            {
                Ray ray = _camera != null ? _camera.ScreenCenterRay() : new Ray(transform.position, transform.forward);
                if (Weapons.TryFire(ray, IsAiming)) _camera?.Shake(0.08f, 0.15f);
            }

            if (_input.EnterVehiclePressed && _enterVehicleCooldown <= 0f) ExitVehicle();
            if (_input.CameraTogglePressed) _camera?.ToggleFirstPerson();
            if (_input.RadioNextPressed) Services.Radio?.NextStation();
        }

        // ------------------------------------------------------------------
        private void UpdateFootsteps(float dt)
        {
            if (!IsGrounded || CurrentSpeed < 0.4f) { _footstepTimer = 0f; return; }
            _footstepTimer -= dt * CurrentSpeed;
            if (_footstepTimer <= 0f)
            {
                _footstepTimer = 2.0f;
                Services.Audio?.PlayFootstep(transform.position, IsSprinting);
                if (IsSprinting)
                    GameEvents.RaiseNoise(new NoiseEvent { Position = transform.position, Loudness = 9f, Source = gameObject });
            }
        }

        private void UpdateAnimatorParameters()
        {
            if (Animator == null) return;
            Animator.Speed = CurrentSpeed;
            Animator.Grounded = IsGrounded;
            Animator.Crouching = IsCrouching;
            Animator.Swimming = IsSwimming;
            Animator.Aiming = IsAiming;
            Animator.VerticalVelocity = _verticalVelocity;
            Animator.TwoHanded = Weapons != null && Weapons.IsTwoHanded;
            if (_camera != null) Animator.AimPitch = Mathf.DeltaAngle(0f, _camera.Pitch);

            Vector3 local = transform.InverseTransformDirection(new Vector3(_velocity.x, 0f, _velocity.z));
            Animator.Forward = local.z;
            Animator.Strafe = local.x;
            _camera?.SetTargetSpeed(CurrentSpeed);
        }

        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            var body = hit.collider.attachedRigidbody;
            if (body == null || body.isKinematic) return;
            if (hit.moveDirection.y < -0.3f) return;
            Vector3 push = new Vector3(hit.moveDirection.x, 0f, hit.moveDirection.z);
            body.AddForceAtPosition(push * 2.4f, hit.point, ForceMode.Impulse);
        }
    }
}
