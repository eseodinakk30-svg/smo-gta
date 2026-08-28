using System.Collections.Generic;
using UnityEngine;
using SanMonica.Characters;
using SanMonica.Core;
using SanMonica.Data;
using SanMonica.Vehicles;

namespace SanMonica.AI
{
    public enum PedState
    {
        Idle, Wander, GoTo, Talking, Working, Flee, Cower, Combat,
        Investigate, EnterVehicle, Driving, CallPolice, Dead, Surrender
    }

    /// <summary>
    /// The mind of every citizen, worker, gangster, guard and officer in San
    /// Monica: a state machine fed by perception, faction relations, the clock
    /// and whatever the player just did.
    /// </summary>
    public class PedBrain : MonoBehaviour, IPoolable
    {
        [Header("Identity")]
        public PedArchetype Archetype;
        public Faction Faction = Faction.Civilian;
        public bool IsPolice;
        public bool IsWitness;

        [Header("Runtime")]
        public PedState State = PedState.Idle;
        public int Lod;

        public CharacterController Controller { get; private set; }
        public CharacterRig Rig { get; private set; }
        public ProceduralAnimator Animator { get; private set; }
        public CharacterHealth Health { get; private set; }
        public AIPerception Perception { get; private set; }
        public SanMonica.Weapons.WeaponHolder Weapons { get; private set; }
        public Vehicle CurrentVehicle { get; private set; }
        public int Cash { get; private set; }

        private readonly List<Vector3> _path = new List<Vector3>(24);
        private int _pathIndex;
        private Vector3 _destination;
        private Vector3 _velocity;
        private float _verticalVelocity;
        private float _stateTimer;
        private float _thinkTimer;
        private float _repathTimer;
        private float _reportTimer;
        private float _fireTimer;
        private Rng _rng;
        private Transform _threat;
        private float _updateAccumulator;
        private float _footstepTimer;
        private int _seatIndex;
        private bool _initialised;
        private float _spawnTime;

        public float DistanceToPlayer { get; private set; }
        public bool InVehicle => CurrentVehicle != null;

        // ------------------------------------------------------------------
        public void Setup(PedArchetype archetype, ref Rng rng)
        {
            Archetype = archetype;
            Faction = archetype.faction;
            IsPolice = archetype.faction == Faction.SMPD;
            _rng = new Rng((int)rng.NextUInt());
            Cash = rng.Range(archetype.minCash, archetype.maxCash + 1);

            Controller = GetComponent<CharacterController>();
            Rig = GetComponent<CharacterRig>();
            Animator = GetComponent<ProceduralAnimator>();
            Health = GetComponent<CharacterHealth>();
            Perception = GetComponent<AIPerception>();
            Weapons = GetComponent<SanMonica.Weapons.WeaponHolder>();

            Perception.Alertness = archetype.alertness;
            Perception.ViewDistance = Mathf.Lerp(22f, 48f, archetype.alertness);
            Health.ResetVitals(archetype.maxHealth, archetype.armour);
            Health.Died -= OnDied;
            Health.Died += OnDied;
            Health.Damaged -= OnDamaged;
            Health.Damaged += OnDamaged;

            State = PedState.Wander;
            _stateTimer = 0f;
            _spawnTime = Time.time;
            _initialised = true;

            if (archetype.possibleWeapons != null && archetype.possibleWeapons.Length > 0 && _rng.Chance(archetype.armedChance))
            {
                string id = _rng.Pick(archetype.possibleWeapons);
                var def = Services.Database?.Weapon(id);
                if (def != null) Weapons.GiveWeapon(def, def.magazineSize * 3, true);
            }
        }

        public void SetLod(int lod)
        {
            Lod = lod;
            if (Perception != null) Perception.SetLod(lod);
            if (Animator != null) Animator.UpdateInterval = lod == 0 ? 1 : (lod == 1 ? 2 : 6);
            if (Rig != null) Rig.SetMeshLod(lod >= 1 ? 1 : 0);
            if (Controller != null) Controller.enabled = lod < 2 && Health != null && Health.IsAlive && !InVehicle;
        }

        // ------------------------------------------------------------------
        private void Update()
        {
            if (!_initialised || Health == null) return;
            float dt = Time.deltaTime;

            if (!Health.IsAlive)
            {
                if (State != PedState.Dead) EnterState(PedState.Dead);
                return;
            }

            var playerPos = Services.PlayerPosition;
            DistanceToPlayer = Vector3.Distance(transform.position, playerPos);

            // Distant NPCs think rarely and move on a coarse simulation.
            if (Lod == 2)
            {
                _updateAccumulator += dt;
                if (_updateAccumulator < 0.5f) return;
                dt = _updateAccumulator;
                _updateAccumulator = 0f;
                SimulateCoarse(dt);
                return;
            }

            _stateTimer += dt;
            _thinkTimer -= dt;
            if (_thinkTimer <= 0f)
            {
                _thinkTimer = Lod == 0 ? 0.35f : 0.9f;
                Think();
            }

            switch (State)
            {
                case PedState.Idle: TickIdle(dt); break;
                case PedState.Wander: TickWander(dt); break;
                case PedState.GoTo: TickGoTo(dt); break;
                case PedState.Working: TickWorking(dt); break;
                case PedState.Talking: TickIdle(dt); break;
                case PedState.Flee: TickFlee(dt); break;
                case PedState.Cower: TickCower(dt); break;
                case PedState.Combat: TickCombat(dt); break;
                case PedState.Investigate: TickInvestigate(dt); break;
                case PedState.EnterVehicle: TickEnterVehicle(dt); break;
                case PedState.Driving: TickDriving(dt); break;
                case PedState.CallPolice: TickCallPolice(dt); break;
                case PedState.Surrender: TickCower(dt); break;
            }

            UpdateAnimator();
        }

        // ------------------------------------------------------------------
        private void Think()
        {
            if (InVehicle) return;

            var player = Services.Player;
            bool playerHostile = player != null && Services.Wanted != null && Services.Wanted.Level > 0;

            // Police engage a wanted player.
            if (IsPolice && playerHostile && Perception.CanSeePlayer)
            {
                _threat = player.transform;
                if (State != PedState.Combat) EnterState(PedState.Combat);
                return;
            }

            // Gang and corporate factions attack their enemies and, when the
            // story says so, the player.
            if (Archetype != null && Archetype.aggression > 0.4f)
            {
                if (FactionRelations.IsHostileToPlayer(Faction) && Perception.CanSeePlayer && DistanceToPlayer < 45f)
                {
                    _threat = player.transform;
                    if (State != PedState.Combat) EnterState(PedState.Combat);
                    return;
                }
                var hostile = Perception.FindHostile(Faction, 32f);
                if (hostile != null)
                {
                    _threat = hostile.transform;
                    if (State != PedState.Combat) EnterState(PedState.Combat);
                    return;
                }
            }

            // Civilians run from danger.
            if (State != PedState.Combat && State != PedState.Flee && State != PedState.Cower)
            {
                bool dangerNearby = Perception.TimeSinceHeard < 2.5f &&
                                    Vector3.Distance(transform.position, Perception.LastHeardPosition) < 26f;
                bool playerThreatening = player != null && Perception.CanSeePlayer && DistanceToPlayer < 14f &&
                                         player.Weapons != null && player.Weapons.IsWeaponDrawn && Services.Wanted != null && Services.Wanted.Level > 0;

                if (dangerNearby || playerThreatening)
                {
                    if (Archetype != null && _rng.Value > Archetype.bravery)
                    {
                        EnterState(_rng.Chance(0.25f) ? PedState.Cower : PedState.Flee);
                        return;
                    }
                    if (Archetype != null && _rng.Chance(Archetype.reportChance) && _reportTimer <= 0f)
                    {
                        EnterState(PedState.CallPolice);
                        return;
                    }
                }
            }

            if (_reportTimer > 0f) _reportTimer -= _thinkTimer;

            // Idle behaviour driven by the archetype and the clock.
            if (State == PedState.Wander && _stateTimer > _rng.Range(18f, 45f))
                EnterState(_rng.Chance(0.4f) ? PedState.Idle : PedState.Wander);
            else if (State == PedState.Idle && _stateTimer > _rng.Range(3f, 9f))
                EnterState(PedState.Wander);
        }

        public void EnterState(PedState next)
        {
            State = next;
            _stateTimer = 0f;

            switch (next)
            {
                case PedState.Wander:
                    PickWanderDestination();
                    break;
                case PedState.Flee:
                {
                    Vector3 away = transform.position - Perception.LastHeardPosition;
                    if (away.sqrMagnitude < 1f) away = -transform.forward;
                    SetDestination(transform.position + away.normalized * _rng.Range(35f, 80f));
                    if (Services.Audio != null && DistanceToPlayer < 30f) Services.Audio.PlayOneShot("scream", transform.position, 0.6f);
                    break;
                }
                case PedState.CallPolice:
                    _reportTimer = 30f;
                    break;
                case PedState.Combat:
                    Weapons?.SetHolstered(false);
                    break;
                case PedState.Dead:
                    if (Controller != null) Controller.enabled = false;
                    if (Cash > 0 && Services.Economy != null) { /* dropped cash is picked up on looting */ }
                    break;
            }
        }

        // ------------------------------------------------------------------
        private void TickIdle(float dt)
        {
            Move(Vector3.zero, 0f, dt);
        }

        private void TickWander(float dt)
        {
            if (FollowPath(dt, Archetype != null ? Archetype.walkSpeed : 1.3f)) PickWanderDestination();
        }

        private void TickGoTo(float dt)
        {
            if (FollowPath(dt, Archetype != null ? Archetype.walkSpeed : 1.3f)) EnterState(PedState.Idle);
        }

        private void TickWorking(float dt)
        {
            Move(Vector3.zero, 0f, dt);
            if (_stateTimer > 20f) EnterState(PedState.Wander);
        }

        private void TickFlee(float dt)
        {
            float speed = Archetype != null ? Archetype.sprintSpeed : 5f;
            if (FollowPath(dt, speed) || _stateTimer > 14f)
            {
                if (Perception.TimeSinceHeard > 6f) EnterState(PedState.Wander);
                else EnterState(PedState.Flee);
            }
        }

        private void TickCower(float dt)
        {
            Move(Vector3.zero, 0f, dt);
            if (Animator != null) Animator.Crouching = true;
            if (_stateTimer > 8f && Perception.TimeSinceHeard > 5f)
            {
                if (Animator != null) Animator.Crouching = false;
                EnterState(PedState.Flee);
            }
        }

        private void TickCallPolice(float dt)
        {
            Move(Vector3.zero, 0f, dt);
            if (_stateTimer > 1.6f)
            {
                Services.Wanted?.ReportCrimeByWitness(transform.position);
                GameEvents.Notify("A witness called the police", 2.2f);
                EnterState(PedState.Flee);
            }
        }

        private void TickInvestigate(float dt)
        {
            if (FollowPath(dt, Archetype != null ? Archetype.runSpeed * 0.7f : 2.4f) || _stateTimer > 20f)
                EnterState(PedState.Wander);
        }

        private void TickCombat(float dt)
        {
            if (_threat == null) { EnterState(PedState.Wander); return; }
            var threatHealth = _threat.GetComponent<CharacterHealth>();
            if (threatHealth != null && !threatHealth.IsAlive) { _threat = null; EnterState(PedState.Wander); return; }

            float distance = Vector3.Distance(transform.position, _threat.position);
            bool visible = Perception.CanSee(_threat, out _);

            if (!visible && _stateTimer > 6f)
            {
                SetDestination(Perception.LastKnownPlayerPosition);
                EnterState(PedState.Investigate);
                return;
            }

            // Face the threat and shoot, closing the distance if unarmed.
            Vector3 toThreat = _threat.position - transform.position;
            toThreat.y = 0f;
            if (toThreat.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(toThreat.normalized), 1f - Mathf.Exp(-9f * dt));

            bool armed = Weapons != null && Weapons.HasRangedWeapon;
            float preferred = armed ? (IsPolice ? 12f : 9f) : 1.6f;

            Vector3 desired = Vector3.zero;
            if (distance > preferred * 1.3f) desired = toThreat.normalized;
            else if (distance < preferred * 0.6f) desired = -toThreat.normalized;
            else desired = Vector3.Cross(Vector3.up, toThreat.normalized) * (Mathf.Sin(Time.time * 0.8f + GetInstanceID()) > 0f ? 1f : -1f);

            float speed = Archetype != null ? Archetype.runSpeed : 3.6f;
            Move(desired * speed, speed, dt);

            if (Animator != null) Animator.Aiming = armed && visible;

            if (visible && Weapons != null)
            {
                _fireTimer -= dt;
                if (armed)
                {
                    if (_fireTimer <= 0f)
                    {
                        Vector3 aimPoint = _threat.position + Vector3.up * 1.0f;
                        float accuracy = Mathf.Lerp(0.35f, 0.95f, Archetype != null ? Archetype.aggression : 0.5f);
                        Weapons.AiFire(aimPoint, accuracy);
                        _fireTimer = _rng.Range(0.25f, 1.1f) / Mathf.Max(0.3f, Archetype != null ? Archetype.aggression : 0.5f);
                    }
                }
                else if (distance < 2.2f && _fireTimer <= 0f)
                {
                    Weapons.Melee();
                    _fireTimer = 0.9f;
                }
            }
        }

        private void TickEnterVehicle(float dt)
        {
            if (CurrentVehicle == null) { EnterState(PedState.Wander); return; }
            Vector3 door = CurrentVehicle.GetExitPosition(_seatIndex);
            if (Vector3.Distance(transform.position, door) < 1.4f)
            {
                SeatInVehicle(CurrentVehicle, _seatIndex);
                return;
            }
            SetDestination(door);
            if (FollowPath(dt, Archetype != null ? Archetype.runSpeed : 3.5f) || _stateTimer > 12f)
                SeatInVehicle(CurrentVehicle, _seatIndex);
        }

        private void TickDriving(float dt)
        {
            if (CurrentVehicle == null || CurrentVehicle.IsDestroyed) { ForceExitVehicle(); return; }
            if (Animator != null) { Animator.Sitting = true; Animator.Driving = _seatIndex == 0; }
        }

        // ------------------------------------------------------------------
        private void SimulateCoarse(float dt)
        {
            // Far from the player NPCs are simulated as simple drifting points -
            // they keep existing and moving, they just stop paying for physics.
            if (InVehicle) return;
            if (_path.Count == 0 || _pathIndex >= _path.Count) { PickWanderDestination(); return; }
            Vector3 target = _path[_pathIndex];
            Vector3 delta = target - transform.position;
            delta.y = 0f;
            float speed = Archetype != null ? Archetype.walkSpeed : 1.3f;
            if (delta.magnitude < 1.5f) { _pathIndex++; return; }
            Vector3 step = delta.normalized * speed * dt;
            Vector3 next = transform.position + step;
            next.y = Services.Map != null ? Services.Map.SampleHeight(next.x, next.z) : next.y;
            transform.position = next;
            transform.rotation = Quaternion.LookRotation(delta.normalized);
        }

        // ------------------------------------------------------------------
        private void PickWanderDestination()
        {
            if (Services.Nav == null) return;
            Vector3 point = Services.Nav.RandomWalkPoint(transform.position, _rng.Range(20f, 70f), ref _rng);
            SetDestination(point);
        }

        public void SetDestination(Vector3 destination)
        {
            _destination = destination;
            _path.Clear();
            _pathIndex = 0;
            if (Services.Nav != null) Services.Nav.FindPath(transform.position, destination, _path);
            else _path.Add(destination);
        }

        private bool FollowPath(float dt, float speed)
        {
            if (_path.Count == 0) return true;
            if (_pathIndex >= _path.Count) return true;

            Vector3 target = _path[_pathIndex];
            Vector3 delta = target - transform.position;
            delta.y = 0f;
            float distance = delta.magnitude;
            if (distance < 1.4f)
            {
                _pathIndex++;
                if (_pathIndex >= _path.Count) return true;
                return false;
            }

            Vector3 desired = delta.normalized * speed;
            int mask = (1 << GameLayers.Building) | (1 << GameLayers.Prop) | GameLayers.VehicleMask;
            desired = NavGraph.AvoidObstacles(transform.position, desired, 0.45f, 2.4f, mask);
            Move(desired, speed, dt);

            if (desired.sqrMagnitude > 0.05f)
            {
                var look = Quaternion.LookRotation(new Vector3(desired.x, 0f, desired.z).normalized);
                transform.rotation = Quaternion.Slerp(transform.rotation, look, 1f - Mathf.Exp(-8f * dt));
            }
            return false;
        }

        private void Move(Vector3 desiredVelocity, float speed, float dt)
        {
            if (Controller == null || !Controller.enabled) return;
            _velocity = Vector3.Lerp(_velocity, desiredVelocity, 1f - Mathf.Exp(-9f * dt));

            if (Controller.isGrounded) _verticalVelocity = -2f;
            else _verticalVelocity += (Services.Config != null ? Services.Config.gravity : -19.6f) * dt;

            Vector3 motion = _velocity;
            motion.y = _verticalVelocity;
            Controller.Move(motion * dt);

            float horizontal = new Vector2(_velocity.x, _velocity.z).magnitude;
            if (horizontal > 0.4f && Lod == 0)
            {
                _footstepTimer -= dt * horizontal;
                if (_footstepTimer <= 0f)
                {
                    _footstepTimer = 2.2f;
                    if (DistanceToPlayer < 22f) Services.Audio?.PlayFootstep(transform.position, horizontal > 3.5f);
                }
            }
        }

        private void UpdateAnimator()
        {
            if (Animator == null) return;
            Animator.Speed = new Vector2(_velocity.x, _velocity.z).magnitude;
            Animator.Grounded = Controller == null || Controller.isGrounded;
            Animator.TwoHanded = Weapons != null && Weapons.IsTwoHanded;
            Vector3 local = transform.InverseTransformDirection(_velocity);
            Animator.Forward = local.z;
            Animator.Strafe = local.x;
        }

        // ------------------------------------------------------------------
        public void SeatInVehicle(Vehicle vehicle, int seat)
        {
            if (vehicle == null) return;
            if (!vehicle.TryOccupySeat(seat, gameObject, false)) { EnterState(PedState.Wander); return; }
            CurrentVehicle = vehicle;
            _seatIndex = seat;
            if (Controller != null) Controller.enabled = false;
            var anchor = vehicle.GetSeatAnchor(seat);
            transform.SetParent(anchor, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            if (Animator != null) { Animator.Sitting = true; Animator.Driving = seat == 0; }
            EnterState(PedState.Driving);
        }

        public void ForceExitVehicle()
        {
            if (CurrentVehicle == null)
            {
                if (Controller != null && Health != null && Health.IsAlive) Controller.enabled = true;
                return;
            }
            var vehicle = CurrentVehicle;
            Vector3 exit = vehicle.GetExitPosition(_seatIndex);
            vehicle.ReleaseSeat(_seatIndex);
            CurrentVehicle = null;

            transform.SetParent(null, true);
            transform.position = exit;
            if (Controller != null && Health != null && Health.IsAlive) Controller.enabled = true;
            if (Animator != null) { Animator.Sitting = false; Animator.Driving = false; }
            EnterState(Health != null && Health.IsAlive ? PedState.Flee : PedState.Dead);
        }

        // ------------------------------------------------------------------
        private void OnDamaged(DamageInfo info)
        {
            if (info.Source == null) return;
            _threat = info.Source.transform;
            Perception.Alertness = 1f;

            bool fromPlayer = Services.Player != null && info.Source == Services.Player.gameObject;
            if (fromPlayer)
            {
                GameEvents.RaiseCrime(new CrimeEvent
                {
                    Type = CrimeType.Assault,
                    Position = transform.position,
                    Perpetrator = info.Source,
                    WitnessedByPolice = IsPolice
                });
            }

            if (Archetype != null && (Archetype.aggression > 0.35f || IsPolice) && _rng.Value < Archetype.bravery)
                EnterState(PedState.Combat);
            else
                EnterState(_rng.Chance(0.7f) ? PedState.Flee : PedState.Cower);
        }

        private void OnDied(DamageInfo info)
        {
            EnterState(PedState.Dead);
            if (Controller != null) Controller.enabled = false;
            if (InVehicle) ForceExitVehicle();

            bool byPlayer = Services.Player != null && info.Source == Services.Player.gameObject;
            if (byPlayer)
            {
                GameEvents.RaiseCrime(new CrimeEvent
                {
                    Type = IsPolice ? CrimeType.PoliceMurder : CrimeType.Murder,
                    Position = transform.position,
                    Perpetrator = info.Source,
                    WitnessedByPolice = IsPolice
                });
                if (Cash > 0) Services.Economy?.AddMoney(Cash, "Picked up cash");
            }
            Services.Population?.NotifyDeath(this);
        }

        // ------------------------------------------------------------------
        public void OnSpawned()
        {
            _path.Clear();
            _pathIndex = 0;
            _velocity = Vector3.zero;
            _verticalVelocity = 0f;
            _threat = null;
            State = PedState.Wander;
            _initialised = false;
        }

        public void OnDespawned()
        {
            if (InVehicle) ForceExitVehicle();
            _initialised = false;
            if (Health != null) { Health.Died -= OnDied; Health.Damaged -= OnDamaged; }
        }

        public float Age => Time.time - _spawnTime;
    }
}
