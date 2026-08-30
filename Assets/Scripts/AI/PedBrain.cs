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
        private float _morale = 1f;
        private Vector3 _coverPoint;
        private bool _hasCover;
        private float _coverTimer;
        private float _threatLastSeen;
        private Vector3 _threatLastPosition;
        private bool _warned;
        private RoleProfile _profile;
        private Vector3 _post;
        private float _idleUntil;

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
            _morale = Mathf.Clamp01(0.55f + archetype.bravery * 0.45f);
            // A jogger, a guard, a vendor and a tourist now spend their day
            // differently instead of all walking to a random point and pausing.
            _profile = RoleProfile.For(archetype.role);
            _post = transform.position;
            _idleUntil = 0f;
            _threatLastSeen = 0f;
            _warned = false;
            ClearCover();
            Perception.ResetAwareness();
            Weapons.ResetFireState();

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
                case PedState.Surrender: TickSurrender(dt); break;
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
                SetThreat(player.transform);
                return;
            }

            // Gang, corporate and police factions attack their enemies and, when
            // the story says so, the player. Officers were left out of this scan
            // entirely, so the SMPD walked past cartel gunmen in the street.
            if ((Archetype != null && Archetype.aggression > 0.4f) || IsPolice)
            {
                if (FactionRelations.IsHostileToPlayer(Faction) && Perception.CanSeePlayer && DistanceToPlayer < 45f)
                {
                    SetThreat(player.transform);
                    return;
                }
                var hostile = Perception.FindHostile(Faction, IsPolice ? 42f : 32f);
                if (hostile != null)
                {
                    SetThreat(hostile.transform);
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

            // Idle behaviour driven by the role, the archetype and the clock.
            if (State == PedState.Wander && _stateTimer > _rng.Range(_profile.WanderMin, _profile.WanderMax))
            {
                if (_rng.Chance(_profile.WorkChance)) EnterState(PedState.Working);
                else if (_rng.Chance(_profile.IdleChance)) EnterState(PedState.Idle);
                else EnterState(PedState.Wander);
            }
            else if (State == PedState.Idle && _stateTimer > _idleUntil)
                EnterState(PedState.Wander);
        }

        /// <summary>
        /// Points this NPC at a specific target and starts a fight. When
        /// <paramref name="propagate"/> is set the NPC also shouts, and everyone
        /// of the same faction within earshot joins in - so a gang answers as a
        /// gang instead of feeding you one man at a time. Alerted allies never
        /// propagate further, which keeps one gunshot from waking the city.
        /// </summary>
        public void SetThreat(Transform target, bool engage = true, bool propagate = true)
        {
            if (target == null) return;
            bool fresh = _threat != target;
            _threat = target;
            _threatLastSeen = 0f;
            _threatLastPosition = target.position;
            Perception.Alertness = 1f;
            if (!engage) return;
            if (State != PedState.Combat) EnterState(PedState.Combat);
            if (propagate && fresh) AlertAllies(target);
        }

        /// <summary>Calls nearby allies of the same faction into the fight.</summary>
        private void AlertAllies(Transform target, float radius = 32f)
        {
            var peds = Services.Peds != null ? Services.Peds.ActivePeds : null;
            if (peds == null || target == null) return;
            float sqr = radius * radius;
            int called = 0;
            for (int i = 0; i < peds.Count && called < 6; i++)
            {
                var mate = peds[i];
                if (mate == null || mate == this) continue;
                if (mate.Faction != Faction) continue;
                if (mate.Health == null || !mate.Health.IsAlive) continue;
                if (mate.State == PedState.Combat || mate.InVehicle) continue;
                if ((mate.transform.position - transform.position).sqrMagnitude > sqr) continue;
                // Bystanders of the same faction do not become gunmen just
                // because someone shouted; only those with the stomach for it.
                if (!mate.IsPolice && (mate.Archetype == null || mate.Archetype.aggression < 0.3f)) continue;
                mate.SetThreat(target, true, false);
                called++;
            }
            if (called > 0) Services.Audio?.PlayOneShot("shout", transform.position, 0.55f);
        }

        private void ClearCover()
        {
            _hasCover = false;
            _coverTimer = 0f;
        }

        private void ClearThreat()
        {
            _threat = null;
            _warned = false;
            ClearCover();
        }

        public Transform CurrentThreat => _threat;

        public void EnterState(PedState next)
        {
            State = next;
            _stateTimer = 0f;
            if (Animator != null && next != PedState.Combat && next != PedState.Cower && next != PedState.Surrender)
                Animator.Crouching = false;

            switch (next)
            {
                case PedState.Wander:
                    PickWanderDestination();
                    break;
                case PedState.Idle:
                    _idleUntil = _rng.Range(_profile.IdleMin, _profile.IdleMax);
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
                    _warned = false;
                    _threatLastSeen = 0f;
                    ClearCover();
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
            if (FollowPath(dt, RoleSpeed())) PickWanderDestination();
        }

        private void TickGoTo(float dt)
        {
            if (FollowPath(dt, RoleSpeed())) EnterState(PedState.Idle);
        }

        private void TickWorking(float dt)
        {
            Move(Vector3.zero, 0f, dt);
            // Face the work rather than whatever direction they arrived from.
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

        /// <summary>
        /// Hands up. They stay down while the danger is standing over them and
        /// only get up and run once it has moved on.
        /// </summary>
        private void TickSurrender(float dt)
        {
            Move(Vector3.zero, 0f, dt);
            if (Animator != null) { Animator.Crouching = true; Animator.Aiming = false; }
            if (_threat != null)
            {
                Vector3 to = _threat.position - transform.position;
                to.y = 0f;
                if (to.sqrMagnitude > 0.01f)
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to.normalized), 1f - Mathf.Exp(-4f * dt));
            }
            bool clear = _threat == null || Vector3.Distance(transform.position, _threat.position) > 18f;
            if (_stateTimer > 4f && clear) EnterState(PedState.Flee);
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
            if (threatHealth != null && !threatHealth.IsAlive) { ClearThreat(); EnterState(PedState.Wander); return; }

            float distance = Vector3.Distance(transform.position, _threat.position);
            bool visible = Perception.CanSee(_threat, out _);
            if (visible) { _threatLastSeen = 0f; _threatLastPosition = _threat.position; }
            else _threatLastSeen += dt;

            // Lost them: go and look where they were last seen, rather than
            // standing in the road aiming at a wall.
            if (_threatLastSeen > 6f)
            {
                SetDestination(_threatLastPosition);
                ClearCover();
                EnterState(PedState.Investigate);
                return;
            }

            UpdateMorale(dt);
            if (ShouldBreakOff(distance))
            {
                bool hasGun = Weapons != null && Weapons.HasRangedWeapon;
                ClearCover();
                EnterState(!hasGun && distance < 7f ? PedState.Surrender : PedState.Flee);
                return;
            }

            Vector3 toThreat = _threat.position - transform.position;
            toThreat.y = 0f;
            if (toThreat.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(toThreat.normalized), 1f - Mathf.Exp(-9f * dt));

            bool armed = Weapons != null && Weapons.HasRangedWeapon;
            bool reloading = Weapons != null && Weapons.IsReloading;
            bool pinned = Perception.Suppression > 0.45f;

            // Cover. An armed NPC who is reloading or taking fire gets behind
            // something instead of standing in the open trading bullets.
            _coverTimer -= dt;
            if (armed && (reloading || pinned) && _coverTimer <= 0f)
            {
                _coverTimer = 1.5f;
                if (FindCover(_threat.position, out var spot)) { _coverPoint = spot; _hasCover = true; }
            }
            if (_hasCover && !reloading && !pinned && Vector3.Distance(transform.position, _coverPoint) < 1.4f)
                ClearCover();   // the pressure is off: lean back out and fight

            float speed = Archetype != null ? Archetype.runSpeed : 3.6f;
            float preferred = armed ? (IsPolice ? 13f : 10f) : 1.7f;

            Vector3 desired;
            if (_hasCover)
            {
                Vector3 toCover = _coverPoint - transform.position;
                toCover.y = 0f;
                bool arrived = toCover.sqrMagnitude < 1.6f;
                desired = arrived ? Vector3.zero : toCover.normalized;
                if (Animator != null) Animator.Crouching = arrived;
            }
            else
            {
                if (Animator != null) Animator.Crouching = false;
                if (distance > preferred * 1.3f) desired = toThreat.normalized;
                else if (distance < preferred * 0.6f) desired = -toThreat.normalized;
                else desired = Vector3.Cross(Vector3.up, toThreat.normalized) * (Mathf.Sin(Time.time * 0.8f + GetInstanceID()) > 0f ? 1f : -1f);
            }

            desired = KeepOnLand(desired);
            int avoidMask = (1 << GameLayers.Building) | (1 << GameLayers.Prop) | GameLayers.VehicleMask;
            Move(NavGraph.AvoidObstacles(transform.position, desired * speed, 0.45f, 2.2f, avoidMask), speed, dt);

            if (Animator != null) Animator.Aiming = armed && visible;
            if (!visible || Weapons == null) return;

            // The SMPD give one warning before opening fire on a suspect who is
            // not already shooting at them.
            if (IsPolice && !_warned)
            {
                _warned = true;
                int level = Services.Wanted != null ? Services.Wanted.Level : 0;
                bool atPlayer = Services.Player != null && _threat == Services.Player.transform;
                if (atPlayer && level <= 1)
                {
                    Services.Audio?.PlayOneShot("shout", transform.position, 0.7f);
                    if (DistanceToPlayer < 34f) GameEvents.Notify("SMPD: on the ground, now!", 2.2f);
                    _fireTimer = 1.5f;
                }
            }

            _fireTimer -= dt;
            if (armed)
            {
                // The weapon governs its own cadence now - bursts, reloads and
                // recoil all live in the holder - so this timer is a reaction
                // delay between engagements, not a metronome between rounds.
                if (_fireTimer <= 0f)
                {
                    float aggression = Archetype != null ? Archetype.aggression : 0.5f;
                    float accuracy = Mathf.Clamp01(Mathf.Lerp(0.3f, 0.92f, aggression)
                                                   - Perception.Suppression * 0.35f
                                                   + (distance < 12f ? 0.1f : 0f));
                    Vector3 aimPoint = _threat.position + Vector3.up * Mathf.Lerp(0.85f, 1.35f, accuracy);
                    Weapons.AiFire(aimPoint, accuracy);
                    if (!Weapons.InBurst) _fireTimer = _rng.Range(0.2f, 0.75f);
                }
            }
            else if (distance < 2.2f && _fireTimer <= 0f)
            {
                Weapons.Melee();
                _fireTimer = 0.9f;
            }
        }

        /// <summary>
        /// How much fight is left. Wounds, nerve and being shot at all feed into
        /// one number, so a cornered gangster with a rifle behaves differently
        /// from a shopkeeper who picked up a bat.
        /// </summary>
        private void UpdateMorale(float dt)
        {
            float health = Health != null && Health.MaxHealth > 0f ? Health.Health / Health.MaxHealth : 1f;
            float bravery = Archetype != null ? Archetype.bravery : 0.25f;
            float suppression = Perception != null ? Perception.Suppression : 0f;
            float target = Mathf.Clamp01(health * 0.7f + bravery * 0.45f - suppression * 0.4f);
            _morale = Mathf.MoveTowards(_morale, target, dt * 0.9f);
        }

        /// <summary>Police never break. Everyone else eventually does.</summary>
        private bool ShouldBreakOff(float distance)
        {
            if (IsPolice) return false;
            bool armed = Weapons != null && Weapons.HasRangedWeapon;
            float breakPoint = armed ? 0.28f : 0.55f;
            if (!armed && distance > 12f) breakPoint = 0.70f;
            return _morale < breakPoint;
        }

        /// <summary>
        /// Looks for a spot the threat cannot see into and that this NPC can
        /// actually reach. Eight samples on a one-and-a-half second cooldown is
        /// cheap enough to run on every gunman in a firefight.
        /// </summary>
        private bool FindCover(Vector3 threatPosition, out Vector3 cover)
        {
            cover = transform.position;
            Vector3 eye = threatPosition + Vector3.up * 1.5f;
            Vector3 here = transform.position + Vector3.up * 1.1f;
            float best = float.MaxValue;
            bool found = false;

            for (int i = 0; i < 8; i++)
            {
                float angle = (i / 8f + _rng.Value * 0.12f) * Mathf.PI * 2f;
                float radius = _rng.Range(3.5f, 12f);
                Vector3 candidate = transform.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                if (Services.Map != null && Services.Map.IsWater(candidate.x, candidate.z)) continue;
                if (Services.Nav != null) candidate = Services.Nav.SnapToWalkable(candidate, 6f);

                Vector3 chest = candidate + Vector3.up * 1.1f;
                if (!Physics.Linecast(eye, chest, GameLayers.VisionBlockMask, QueryTriggerInteraction.Ignore)) continue;
                if (Physics.Linecast(here, chest, GameLayers.VisionBlockMask, QueryTriggerInteraction.Ignore)) continue;

                float score = Vector3.Distance(transform.position, candidate);
                if (score < best) { best = score; cover = candidate; found = true; }
            }
            return found;
        }

        /// <summary>
        /// Combat steering is free-form, and free-form steering used to reverse
        /// gunmen off the end of piers. Anything that would step into the bay is
        /// turned along the shoreline instead.
        /// </summary>
        private Vector3 KeepOnLand(Vector3 desired)
        {
            var map = Services.Map;
            if (map == null || desired.sqrMagnitude < 0.001f) return desired;
            Vector3 here = transform.position;
            if (map.IsWater(here.x, here.z)) return desired;    // already wet: let them wade out

            Vector3 step = desired.normalized * 1.6f;
            if (!map.IsWater(here.x + step.x, here.z + step.z)) return desired;

            Vector3 side = Vector3.Cross(Vector3.up, desired.normalized) * 1.6f;
            if (!map.IsWater(here.x + side.x, here.z + side.z)) return side.normalized;
            if (!map.IsWater(here.x - side.x, here.z - side.z)) return -side.normalized;
            return Vector3.zero;
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
            float speed = RoleSpeed();
            if (delta.magnitude < 1.5f) { _pathIndex++; return; }
            Vector3 step = delta.normalized * speed * dt;
            Vector3 next = transform.position + step;
            // Follow the height the path itself carries rather than resampling the
            // terrain: the path runs along pavements and over bridge decks, and
            // the height field underneath is the bed of the bay.
            next.y = Mathf.Lerp(transform.position.y, target.y, Mathf.Clamp01(dt * 6f));
            transform.position = next;
            transform.rotation = Quaternion.LookRotation(delta.normalized);
        }

        // ------------------------------------------------------------------
        private void PickWanderDestination()
        {
            if (Services.Nav == null) return;
            float radius = _rng.Range(Mathf.Max(1f, _profile.RadiusMin), Mathf.Max(2f, _profile.RadiusMax));

            // Guards, vendors, dockworkers and corner crews have a post. They
            // move around it rather than drifting off across the district.
            Vector3 from = transform.position;
            if (_profile.PostRadius > 0.5f &&
                (transform.position - _post).sqrMagnitude > _profile.PostRadius * _profile.PostRadius)
            {
                SetDestination(Services.Nav.SnapToWalkable(_post, 30f));
                return;
            }

            Vector3 point = Services.Nav.RandomWalkPoint(from, radius, ref _rng);
            if (_profile.PostRadius > 0.5f)
            {
                Vector3 offset = point - _post;
                offset.y = 0f;
                if (offset.magnitude > _profile.PostRadius)
                    point = Services.Nav.SnapToWalkable(_post + offset.normalized * _profile.PostRadius, 20f);
            }
            SetDestination(point);
        }

        /// <summary>Walking pace for this role: a jogger runs, a drifter shuffles.</summary>
        private float RoleSpeed()
        {
            float baseSpeed = Archetype != null ? Archetype.walkSpeed : 1.3f;
            float speed = baseSpeed * Mathf.Max(0.2f, _profile.SpeedScale);
            float cap = Archetype != null ? Archetype.sprintSpeed : 5.4f;
            return Mathf.Min(speed, cap);
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
            _post = exit;                 // wherever they got out is their new beat
            if (Controller != null && Health != null && Health.IsAlive) Controller.enabled = true;
            if (Animator != null) { Animator.Sitting = false; Animator.Driving = false; }
            EnterState(Health != null && Health.IsAlive ? PedState.Flee : PedState.Dead);
        }

        // ------------------------------------------------------------------
        private void OnDamaged(DamageInfo info)
        {
            Perception?.Suppress(0.5f);
            if (info.Source == null) return;
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
            {
                SetThreat(info.Source.transform);
            }
            else
            {
                // Being shot repeatedly should not restart the panic every frame:
                // one hit sends them running, the rest keep them running.
                SetThreat(info.Source.transform, false, false);
                if (State != PedState.Flee && State != PedState.Cower && State != PedState.Surrender)
                    EnterState(_rng.Chance(0.7f) ? PedState.Flee : PedState.Cower);
            }
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
            _morale = 1f;
            _warned = false;
            _threatLastSeen = 0f;
            _hasCover = false;
            _coverTimer = 0f;
            _post = transform.position;
            _idleUntil = 0f;
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
