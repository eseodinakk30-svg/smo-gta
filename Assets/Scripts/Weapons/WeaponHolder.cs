using System.Collections.Generic;
using UnityEngine;
using SanMonica.Characters;
using SanMonica.Core;
using SanMonica.Data;
using SanMonica.Utils;

namespace SanMonica.Weapons
{
    /// <summary>
    /// Carries, draws, fires and reloads weapons for the player and for NPCs.
    /// Hitscan ballistics with spread, recoil, pellets, penetration and per-body
    /// part damage; melee arcs; and thrown explosives.
    /// </summary>
    public class WeaponHolder : MonoBehaviour
    {
        [System.Serializable]
        public class Slot
        {
            public WeaponDefinition Definition;
            public int Magazine;
        }

        private readonly Dictionary<int, Slot> _slots = new Dictionary<int, Slot>(8);
        private readonly Dictionary<AmmoType, int> _reserve = new Dictionary<AmmoType, int>(8);

        private CharacterRig _rig;
        private ProceduralAnimator _animator;
        private CharacterHealth _health;
        private Transform _model;
        private MeshFilter _modelFilter;
        private MeshRenderer _modelRenderer;
        private AudioSource _audio;

        private int _currentSlot = 0;
        private float _nextFireTime;
        private float _reloadEndTime;
        private bool _holstered;
        private bool _isPlayer;
        private float _recoilAccumulated;
        private float _spreadPenalty;
        private bool _triggerHeld;
        private int _burstRemaining;
        private int _aiBurstRemaining;
        private CharacterController _mover;

        public WeaponDefinition CurrentDefinition => _slots.TryGetValue(_currentSlot, out var s) ? s.Definition : null;
        public int CurrentSlot => _currentSlot;
        public bool IsReloading => Time.time < _reloadEndTime;
        public bool IsWeaponDrawn => !_holstered && CurrentDefinition != null && CurrentDefinition.category != WeaponCategory.Unarmed;
        public bool CanAim => CurrentDefinition != null && CurrentDefinition.IsGun && !_holstered;
        public bool IsTwoHanded
        {
            get
            {
                var d = CurrentDefinition;
                return d != null && (d.category == WeaponCategory.Rifle || d.category == WeaponCategory.SMG
                    || d.category == WeaponCategory.Shotgun || d.category == WeaponCategory.Sniper || d.category == WeaponCategory.Heavy);
            }
        }
        public bool HasRangedWeapon => CurrentDefinition != null && CurrentDefinition.IsGun;
        public int MagazineAmmo => _slots.TryGetValue(_currentSlot, out var s) ? s.Magazine : 0;
        public int ReserveAmmo
        {
            get
            {
                var d = CurrentDefinition;
                if (d == null) return 0;
                return _reserve.TryGetValue(d.ammoType, out int n) ? n : 0;
            }
        }

        public IEnumerable<Slot> Slots => _slots.Values;
        public float RecoilOffset => _recoilAccumulated;

        // ------------------------------------------------------------------
        public void Initialize(CharacterRig rig, ProceduralAnimator animator, CharacterHealth health, bool isPlayer)
        {
            _rig = rig;
            _animator = animator;
            _health = health;
            _isPlayer = isPlayer;
            _mover = GetComponent<CharacterController>();

            if (_model == null && rig != null && rig.RightHandAttach != null)
            {
                var go = new GameObject("WeaponModel");
                go.transform.SetParent(rig.RightHandAttach, false);
                go.transform.localPosition = new Vector3(0.02f, -0.02f, 0.06f);
                go.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
                _modelFilter = go.AddComponent<MeshFilter>();
                _modelRenderer = go.AddComponent<MeshRenderer>();
                _modelRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _model = go.transform;
            }

            if (_audio == null)
            {
                var go = new GameObject("WeaponAudio");
                go.transform.SetParent(transform, false);
                _audio = go.AddComponent<AudioSource>();
                _audio.spatialBlend = isPlayer ? 0.25f : 1f;
                _audio.playOnAwake = false;
                _audio.rolloffMode = AudioRolloffMode.Linear;
                _audio.minDistance = 6f;
                _audio.maxDistance = 220f;
                Services.Audio?.Register(_audio, SanMonica.Audio.AudioBus.Sfx);
            }

            var fists = Services.Database?.Weapon("fists");
            if (fists != null && !_slots.ContainsKey(0)) _slots[0] = new Slot { Definition = fists, Magazine = 0 };
            RefreshModel();
        }

        public void GiveWeapon(WeaponDefinition def, int ammo, bool equip)
        {
            if (def == null) return;
            if (!_slots.TryGetValue(def.slot, out var slot) || slot.Definition != def)
            {
                slot = new Slot { Definition = def, Magazine = def.magazineSize };
                _slots[def.slot] = slot;
            }
            AddAmmo(def.ammoType, ammo);
            if (equip) EquipSlot(def.slot);
        }

        /// <summary>
        /// Tops up a reserve, honouring the per-weapon carry limit that until now
        /// was data nobody read. Returns how many rounds actually went in, so a
        /// shop can refuse the sale instead of taking money for nothing.
        /// </summary>
        public int AddAmmo(AmmoType type, int amount)
        {
            if (type == AmmoType.None || amount <= 0) return 0;
            _reserve.TryGetValue(type, out int current);
            int cap = MaxReserveFor(type);
            int next = Mathf.Min(current + amount, cap);
            _reserve[type] = next;
            return next - current;
        }

        /// <summary>Carry limit for an ammunition type, taken from the guns held.</summary>
        public int MaxReserveFor(AmmoType type)
        {
            int cap = 0;
            foreach (var kv in _slots)
            {
                var d = kv.Value.Definition;
                if (d != null && d.ammoType == type) cap = Mathf.Max(cap, d.maxReserve);
            }
            return cap > 0 ? cap : 999;
        }

        public bool IsAmmoFull(AmmoType type)
        {
            if (type == AmmoType.None) return true;
            _reserve.TryGetValue(type, out int current);
            return current >= MaxReserveFor(type);
        }

        public bool HasWeapon(string id)
        {
            foreach (var kv in _slots) if (kv.Value.Definition != null && kv.Value.Definition.id == id) return true;
            return false;
        }

        public void EquipSlot(int slot)
        {
            if (!_slots.ContainsKey(slot)) return;
            _currentSlot = slot;
            _reloadEndTime = 0f;
            _burstRemaining = 0;
            _aiBurstRemaining = 0;
            _nextFireTime = Mathf.Max(_nextFireTime, Time.time + 0.18f);   // draw time
            RefreshModel();
        }

        public void CycleWeapon(int direction)
        {
            if (_slots.Count == 0) return;
            var keys = new List<int>(_slots.Keys);
            keys.Sort();
            int index = keys.IndexOf(_currentSlot);
            if (index < 0) index = 0;
            index = (index + direction + keys.Count) % keys.Count;
            EquipSlot(keys[index]);
            Services.Audio?.PlayOneShot("weapon_switch", transform.position, 0.4f);
        }

        public void SetHolstered(bool holstered)
        {
            _holstered = holstered;
            if (_model != null) _model.gameObject.SetActive(!holstered && CurrentDefinition != null && CurrentDefinition.category != WeaponCategory.Unarmed);
        }

        public void SetAiming(bool aiming)
        {
            if (_animator != null) _animator.Aiming = aiming && CanAim;
        }

        private void RefreshModel()
        {
            if (_modelFilter == null) return;
            var def = CurrentDefinition;
            var catalog = Services.Weapons;
            if (def == null || catalog == null || def.category == WeaponCategory.Unarmed)
            {
                _model.gameObject.SetActive(false);
                return;
            }
            _modelFilter.sharedMesh = catalog.MeshFor(def);
            _modelRenderer.sharedMaterials = catalog.Materials;
            _model.gameObject.SetActive(!_holstered);
        }

        private void Update()
        {
            _recoilAccumulated = Mathf.MoveTowards(_recoilAccumulated, 0f,
                Time.deltaTime * (CurrentDefinition != null ? CurrentDefinition.recoilRecovery : 5f));
            _spreadPenalty = Mathf.MoveTowards(_spreadPenalty, 0f, Time.deltaTime * 2.5f);
        }

        // ------------------------------------------------------------------
        /// <summary>
        /// Pulls the trigger. Fire discipline is real now: a semi-automatic
        /// weapon needs a fresh press for every round, a burst weapon fires its
        /// burst and then pauses. The old code set the same cooldown twice and
        /// called it semi-automatic, so every pistol in the city emptied itself
        /// as fast as an SMG for as long as the button was held.
        /// </summary>
        public bool TryFire(Ray aimRay, bool aiming)
        {
            var def = CurrentDefinition;
            bool freshPress = !_triggerHeld;
            _triggerHeld = true;

            if (def == null || _holstered || IsReloading) return false;
            if (_health != null && !_health.IsAlive) return false;

            if (def.category == WeaponCategory.Unarmed || def.category == WeaponCategory.Melee)
            {
                if (Time.time < _nextFireTime) return false;
                Melee();
                return true;
            }

            if (Time.time < _nextFireTime) return false;

            if (def.category == WeaponCategory.Thrown)
            {
                if (!freshPress) return false;
                if (ReserveAmmo <= 0) return false;
                _reserve[def.ammoType] = ReserveAmmo - 1;
                _nextFireTime = Time.time + def.FireInterval;
                ThrowProjectile(def, aimRay);
                _animator?.TriggerMelee();
                return true;
            }

            var slot = _slots[_currentSlot];
            if (slot.Magazine <= 0)
            {
                _burstRemaining = 0;
                if (ReserveAmmo > 0) Reload();
                else Services.Audio?.PlayOneShot("weapon_empty", transform.position, 0.5f);
                return false;
            }

            if (def.IsBurst)
            {
                if (_burstRemaining <= 0)
                {
                    if (!freshPress) return false;
                    _burstRemaining = def.burstCount;
                }
            }
            else if (!def.automatic && !freshPress) return false;

            slot.Magazine--;
            _nextFireTime = Time.time + def.FireInterval;
            if (def.IsBurst)
            {
                _burstRemaining--;
                if (_burstRemaining <= 0)
                    _nextFireTime = Time.time + Mathf.Max(def.FireInterval, def.burstInterval);
            }

            FireHitscan(def, aimRay, aiming);
            return true;
        }

        /// <summary>
        /// Lets go of the trigger. Callers that fire from a held button must call
        /// this on the frames the button is up, or nothing is ever semi-automatic.
        /// </summary>
        public void ReleaseTrigger()
        {
            _triggerHeld = false;
            _burstRemaining = 0;
        }

        /// <summary>
        /// Clears fire and reload state. A pooled NPC that was reloading when it
        /// was despawned would otherwise come back still reloading, for ever.
        /// </summary>
        public void ResetFireState()
        {
            _nextFireTime = 0f;
            _reloadEndTime = 0f;
            _triggerHeld = false;
            _burstRemaining = 0;
            _aiBurstRemaining = 0;
            _recoilAccumulated = 0f;
            _spreadPenalty = 0f;
        }

        private void FireHitscan(WeaponDefinition def, Ray aimRay, bool aiming)
        {
            Vector3 muzzle = MuzzlePosition(def);

            // Firing on the move costs accuracy - the per-weapon penalty was in
            // the data all along and nothing read it.
            float moveSpread = 0f;
            if (_mover != null && _mover.enabled && def.moveSpreadPenalty > 0f)
            {
                Vector3 v = _mover.velocity;
                float planar = new Vector2(v.x, v.z).magnitude;
                moveSpread = Mathf.Min(def.moveSpreadPenalty, planar * def.moveSpreadPenalty * 0.22f);
            }

            float spread = def.spreadDegrees * (aiming ? def.aimSpreadMultiplier : 1f)
                           + _spreadPenalty + _recoilAccumulated * 0.35f + moveSpread;

            for (int p = 0; p < Mathf.Max(1, def.pelletsPerShot); p++)
            {
                Vector3 direction = ApplySpread(aimRay.direction, spread);
                ResolveShot(def, aimRay.origin, direction, muzzle);
            }

            // Recoil and feedback.
            _recoilAccumulated = Mathf.Min(6f, _recoilAccumulated + def.recoilVertical);
            _spreadPenalty = Mathf.Min(5f, _spreadPenalty + def.recoilHorizontal * 0.5f);
            _animator?.TriggerRecoil(Mathf.Clamp01(def.recoilVertical / 4f));

            Services.Effects?.SpawnMuzzleFlash(muzzle, aimRay.direction);
            var clip = Services.Audio?.GetShotClip(def);
            if (clip != null && _audio != null) _audio.PlayOneShot(clip, def.suppressed ? 0.35f : 0.85f);

            GameEvents.RaiseNoise(new NoiseEvent
            {
                Position = transform.position,
                Loudness = def.noiseRadius,
                Source = gameObject,
                IsGunshot = true
            });

            if (_isPlayer)
                GameEvents.RaiseCrime(new CrimeEvent
                {
                    Type = CrimeType.WeaponFired,
                    Position = transform.position,
                    Perpetrator = gameObject
                });
        }

        /// <summary>
        /// Walks a bullet through the world instead of stopping at whatever the
        /// first raycast happened to touch. It skips the shooter's own body - in
        /// third person the camera sits behind the player and an NPC's muzzle is
        /// inside its own hand, so both of them were shooting themselves in the
        /// back of the head - passes through foliage, loses energy with distance
        /// and punches through as many surfaces as the round has penetration for.
        /// </summary>
        private void ResolveShot(WeaponDefinition def, Vector3 origin, Vector3 direction, Vector3 muzzle)
        {
            float remaining = def.range;
            Vector3 point = origin;
            Vector3 endPoint = origin + direction * def.range;
            int penetrationsLeft = Mathf.Max(0, def.penetration);
            float damageScale = 1f;
            float travelled = 0f;
            CharacterHealth alreadyHit = null;

            for (int step = 0; step < 10 && remaining > 0.05f; step++)
            {
                if (!Physics.Raycast(point, direction, out var hit, remaining,
                        GameLayers.ShootableMask, QueryTriggerInteraction.Ignore))
                {
                    endPoint = point + direction * remaining;
                    break;
                }

                travelled += hit.distance;
                endPoint = hit.point;
                remaining -= hit.distance + 0.03f;
                point = hit.point + direction * 0.03f;   // step past the surface we just hit

                if (IsOwnBody(hit.collider)) continue;

                int layer = hit.collider.gameObject.layer;
                if (layer == GameLayers.Foliage)
                {
                    // Leaves and hedges are cover you can see through, not cover
                    // that stops a bullet.
                    Services.Effects?.SpawnImpact(hit.point, hit.normal, false);
                    continue;
                }

                var info = new DamageInfo
                {
                    Amount = def.DamageAtRange(travelled) * damageScale,
                    Point = hit.point,
                    Direction = direction,
                    Force = def.impactForce * damageScale,
                    Source = gameObject,
                    Kind = DamageKind.Bullet,
                    Part = BodyPart.Torso,
                    ArmourPiercing = def.armourPiercing,
                    HeadMultiplier = def.headshotMultiplier,
                    LimbMultiplier = def.limbMultiplier
                };

                var victim = ResolveVictim(hit.collider);
                if (victim != null)
                {
                    if (victim == alreadyHit) continue;   // one wound per body per round
                    alreadyHit = victim;
                    CharacterHealth.ApplyHit(hit.collider, in info, out _);
                    Services.Effects?.SpawnBlood(hit.point, -direction);
                    if (_isPlayer) Services.Missions?.NotifyKillOrHit(victim.gameObject, !victim.IsAlive);
                }
                else
                {
                    var vehicle = hit.collider.GetComponentInParent<SanMonica.Vehicles.Vehicle>();
                    if (vehicle != null) vehicle.ApplyDamage(in info);
                    bool metallic = vehicle != null || layer == GameLayers.Prop;
                    Services.Effects?.SpawnImpact(hit.point, hit.normal, metallic);
                    var body = hit.collider.attachedRigidbody;
                    if (body != null && !body.isKinematic)
                        body.AddForceAtPosition(direction * info.Force, hit.point, ForceMode.Impulse);
                }

                if (penetrationsLeft <= 0) break;
                penetrationsLeft--;
                damageScale *= Mathf.Clamp01(1f - def.penetrationLoss);
                if (damageScale < 0.12f) break;
            }

            Services.Effects?.SpawnTracer(muzzle, endPoint);
        }

        /// <summary>True when the collider is part of the shooter's own body.</summary>
        private bool IsOwnBody(Collider collider)
        {
            if (collider == null) return false;
            var t = collider.transform;
            return t != null && t.IsChildOf(transform);
        }

        private static CharacterHealth ResolveVictim(Collider collider)
        {
            if (collider == null) return null;
            var zone = collider.GetComponent<HitZone>();
            if (zone != null && zone.Owner != null) return zone.Owner;
            return collider.GetComponentInParent<CharacterHealth>();
        }

        private Vector3 ApplySpread(Vector3 direction, float degrees)
        {
            if (degrees <= 0.001f) return direction;
            float radius = Mathf.Tan(degrees * Mathf.Deg2Rad);
            Vector3 right = Vector3.Cross(Vector3.up, direction).normalized;
            if (right.sqrMagnitude < 0.001f) right = Vector3.right;
            Vector3 up = Vector3.Cross(direction, right);
            Vector2 offset = Random.insideUnitCircle * radius;
            return (direction + right * offset.x + up * offset.y).normalized;
        }

        private Vector3 MuzzlePosition(WeaponDefinition def)
        {
            if (_model != null) return _model.TransformPoint(new Vector3(0f, def.bodySize.y * 0.18f, def.bodySize.z + def.barrelLength));
            return transform.position + Vector3.up * 1.5f + transform.forward * 0.6f;
        }

        // ------------------------------------------------------------------
        public void Reload()
        {
            var def = CurrentDefinition;
            if (def == null || !def.IsGun || IsReloading) return;
            var slot = _slots[_currentSlot];
            if (slot.Magazine >= def.magazineSize) return;
            int available = ReserveAmmo;
            if (available <= 0) return;

            _reloadEndTime = Time.time + def.reloadTime;
            Services.Audio?.PlayOneShot("weapon_reload", transform.position, 0.6f);
            StartCoroutine(FinishReload(def, slot));
        }

        private System.Collections.IEnumerator FinishReload(WeaponDefinition def, Slot slot)
        {
            yield return new WaitForSeconds(def.reloadTime);
            if (slot.Definition != def) yield break;
            int needed = def.magazineSize - slot.Magazine;
            int available = ReserveAmmo;
            int taken = Mathf.Min(needed, available);
            slot.Magazine += taken;
            _reserve[def.ammoType] = available - taken;
        }

        public void Melee()
        {
            var def = CurrentDefinition;
            if (def == null) return;
            if (Time.time < _nextFireTime) return;
            _nextFireTime = Time.time + Mathf.Max(0.25f, def.meleeCooldown);
            _animator?.TriggerMelee();
            Services.Audio?.PlayOneShot("melee_swing", transform.position, 0.5f);
            GameEvents.RaiseNoise(new NoiseEvent
            {
                Position = transform.position,
                Loudness = def.noiseRadius,
                Source = gameObject,
                IsGunshot = false
            });

            // The hit boxes live on the ragdoll layer, so a swing that only looked
            // at body capsules could never land on a head or an arm.
            var hits = Physics.OverlapSphere(transform.position + transform.forward * def.meleeReach * 0.5f + Vector3.up,
                def.meleeReach * 0.75f,
                GameLayers.CharacterMask | GameLayers.VehicleMask | (1 << GameLayers.Prop) | (1 << GameLayers.Ragdoll),
                QueryTriggerInteraction.Ignore);

            foreach (var hit in hits)
            {
                if (hit.transform.IsChildOf(transform)) continue;
                Vector3 toTarget = hit.transform.position - transform.position;
                if (Vector3.Angle(transform.forward, toTarget) > def.meleeArc * 0.5f) continue;

                var info = new DamageInfo
                {
                    Amount = def.damage,
                    Point = hit.ClosestPoint(transform.position + Vector3.up),
                    Direction = transform.forward,
                    Force = def.impactForce,
                    Source = gameObject,
                    Kind = DamageKind.Melee,
                    Part = BodyPart.Torso,
                    HeadMultiplier = def.headshotMultiplier,
                    LimbMultiplier = def.limbMultiplier
                };

                if (CharacterHealth.ApplyHit(hit, in info, out var victim))
                {
                    Services.Effects?.SpawnBlood(info.Point, transform.forward);
                    Services.Audio?.PlayOneShot("melee_hit", info.Point, 0.7f);
                    if (_isPlayer)
                    {
                        GameEvents.RaiseCrime(new CrimeEvent { Type = CrimeType.Assault, Position = transform.position, Perpetrator = gameObject });
                        Services.Missions?.NotifyKillOrHit(victim.gameObject, !victim.IsAlive);
                    }
                    break;
                }

                var vehicle = hit.GetComponentInParent<SanMonica.Vehicles.Vehicle>();
                if (vehicle != null) { vehicle.ApplyDamage(in info); break; }
            }
        }

        // ------------------------------------------------------------------
        private void ThrowProjectile(WeaponDefinition def, Ray aimRay)
        {
            var go = new GameObject("Projectile_" + def.id);
            go.transform.position = MuzzlePosition(def) + aimRay.direction * 0.3f;
            go.layer = GameLayers.Projectile;

            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            mf.sharedMesh = Services.Weapons != null ? Services.Weapons.MeshFor(def) : null;
            mr.sharedMaterials = Services.Weapons != null ? Services.Weapons.Materials : null;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var col = go.AddComponent<SphereCollider>();
            col.radius = 0.12f;
            var body = go.AddComponent<Rigidbody>();
            body.mass = 0.6f;
            body.velocity = aimRay.direction * def.throwForce + Vector3.up * 2.2f;
            body.angularVelocity = Random.insideUnitSphere * 6f;

            var projectile = go.AddComponent<Explosive>();
            projectile.Setup(def, gameObject);
        }

        /// <summary>
        /// NPC firing. Bursts rather than a metronome, real reloads, its own
        /// recoil, and the same ballistics the player gets - so cover, distance
        /// and body armour matter as much when it is pointed at you.
        /// </summary>
        public void AiFire(Vector3 aimPoint, float accuracy)
        {
            var def = CurrentDefinition;
            if (def == null || !def.IsGun) return;
            if (_health != null && !_health.IsAlive) return;
            if (Time.time < _nextFireTime || IsReloading) return;

            var slot = _slots[_currentSlot];
            if (slot.Magazine <= 0)
            {
                _aiBurstRemaining = 0;
                // They reload like everyone else, and only find a fresh magazine
                // once their pockets are genuinely empty: a firefight should not
                // end because a gangster ran out of arithmetic.
                if (ReserveAmmo <= 0) AddAmmo(def.ammoType, def.magazineSize * 2);
                Reload();
                return;
            }

            if (_aiBurstRemaining <= 0)
            {
                if (def.IsBurst) _aiBurstRemaining = def.burstCount;
                else if (def.automatic) _aiBurstRemaining = Mathf.Clamp(Mathf.RoundToInt(2f + accuracy * 5f), 2, 8);
                else _aiBurstRemaining = 1;
            }

            Vector3 muzzle = MuzzlePosition(def);
            Vector3 direction = (aimPoint - muzzle).normalized;
            float inaccuracy = Mathf.Lerp(8f, 0.9f, Mathf.Clamp01(accuracy)) + _recoilAccumulated * 0.3f;
            direction = ApplySpread(direction, inaccuracy);

            slot.Magazine--;
            _aiBurstRemaining--;
            _nextFireTime = Time.time + def.FireInterval;
            if (_aiBurstRemaining <= 0)
                _nextFireTime += Mathf.Max(def.burstInterval, Mathf.Lerp(0.85f, 0.25f, Mathf.Clamp01(accuracy)));

            _recoilAccumulated = Mathf.Min(6f, _recoilAccumulated + def.recoilVertical * 0.5f);
            ResolveShot(def, muzzle, direction, muzzle);
            _animator?.TriggerRecoil(0.4f);
            Services.Effects?.SpawnMuzzleFlash(muzzle, direction);

            var clip = Services.Audio?.GetShotClip(def);
            if (clip != null && _audio != null) _audio.PlayOneShot(clip, def.suppressed ? 0.3f : 0.8f);
            GameEvents.RaiseNoise(new NoiseEvent
            {
                Position = transform.position,
                Loudness = def.noiseRadius,
                Source = gameObject,
                IsGunshot = true
            });
        }

        /// <summary>True while an NPC is part way through a burst.</summary>
        public bool InBurst => _aiBurstRemaining > 0;

        public void ClearAll()
        {
            _slots.Clear();
            _reserve.Clear();
            _burstRemaining = 0;
            _aiBurstRemaining = 0;
            _triggerHeld = false;
            var fists = Services.Database?.Weapon("fists");
            if (fists != null) _slots[0] = new Slot { Definition = fists, Magazine = 0 };
            _currentSlot = 0;
            RefreshModel();
        }

        public Dictionary<AmmoType, int> ReserveSnapshot() => new Dictionary<AmmoType, int>(_reserve);
    }
}
