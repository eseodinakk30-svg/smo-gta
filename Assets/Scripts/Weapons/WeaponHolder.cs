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

        public void AddAmmo(AmmoType type, int amount)
        {
            if (type == AmmoType.None || amount <= 0) return;
            _reserve.TryGetValue(type, out int current);
            _reserve[type] = current + amount;
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
        public bool TryFire(Ray aimRay, bool aiming)
        {
            var def = CurrentDefinition;
            if (def == null || _holstered || IsReloading) return false;
            if (Time.time < _nextFireTime) return false;
            if (_health != null && !_health.IsAlive) return false;

            if (def.category == WeaponCategory.Unarmed || def.category == WeaponCategory.Melee)
            {
                Melee();
                return true;
            }

            if (def.category == WeaponCategory.Thrown)
            {
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
                if (ReserveAmmo > 0) Reload();
                else Services.Audio?.PlayOneShot("weapon_empty", transform.position, 0.5f);
                return false;
            }

            slot.Magazine--;
            _nextFireTime = Time.time + def.FireInterval;
            if (!def.automatic) _nextFireTime = Mathf.Max(_nextFireTime, Time.time + def.FireInterval);

            FireHitscan(def, aimRay, aiming);
            return true;
        }

        private void FireHitscan(WeaponDefinition def, Ray aimRay, bool aiming)
        {
            Vector3 muzzle = MuzzlePosition(def);
            float spread = def.spreadDegrees * (aiming ? def.aimSpreadMultiplier : 1f) + _spreadPenalty + _recoilAccumulated * 0.35f;

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
            if (clip != null && _audio != null) _audio.PlayOneShot(clip, 0.85f);

            GameEvents.RaiseNoise(new NoiseEvent
            {
                Position = transform.position,
                Loudness = Mathf.Lerp(45f, 140f, Mathf.Clamp01(def.damage / 80f)),
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

        private void ResolveShot(WeaponDefinition def, Vector3 origin, Vector3 direction, Vector3 muzzle)
        {
            float range = def.range;
            Vector3 endPoint = origin + direction * range;

            if (Physics.Raycast(origin, direction, out var hit, range, GameLayers.ShootableMask, QueryTriggerInteraction.Ignore))
            {
                endPoint = hit.point;
                var info = new DamageInfo
                {
                    Amount = def.damage,
                    Point = hit.point,
                    Direction = direction,
                    Force = def.impactForce,
                    Source = gameObject,
                    Kind = DamageKind.Bullet,
                    Part = BodyPart.Torso,
                    ArmourPiercing = def.armourPiercing
                };

                if (CharacterHealth.ApplyHit(hit.collider, in info, out var victim))
                {
                    Services.Effects?.SpawnBlood(hit.point, -direction);
                    if (_isPlayer && victim != null) Services.Missions?.NotifyKillOrHit(victim.gameObject, !victim.IsAlive);
                }
                else
                {
                    var vehicle = hit.collider.GetComponentInParent<SanMonica.Vehicles.Vehicle>();
                    if (vehicle != null) vehicle.ApplyDamage(in info);
                    bool metallic = vehicle != null || hit.collider.gameObject.layer == GameLayers.Prop;
                    Services.Effects?.SpawnImpact(hit.point, hit.normal, metallic);
                    var body = hit.collider.attachedRigidbody;
                    if (body != null && !body.isKinematic) body.AddForceAtPosition(direction * def.impactForce, hit.point, ForceMode.Impulse);
                }
            }

            Services.Effects?.SpawnTracer(muzzle, endPoint);
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

            var hits = Physics.OverlapSphere(transform.position + transform.forward * def.meleeReach * 0.5f + Vector3.up,
                def.meleeReach * 0.75f, GameLayers.CharacterMask | GameLayers.VehicleMask | (1 << GameLayers.Prop), QueryTriggerInteraction.Ignore);

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
                    Part = BodyPart.Torso
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

        /// <summary>NPC firing: less accurate, no camera involved.</summary>
        public void AiFire(Vector3 aimPoint, float accuracy)
        {
            var def = CurrentDefinition;
            if (def == null || !def.IsGun) return;
            if (Time.time < _nextFireTime || IsReloading) return;

            var slot = _slots[_currentSlot];
            if (slot.Magazine <= 0)
            {
                if (ReserveAmmo > 0) Reload();
                else AddAmmo(def.ammoType, def.magazineSize * 2);
                return;
            }

            Vector3 muzzle = MuzzlePosition(def);
            Vector3 direction = (aimPoint - muzzle).normalized;
            float inaccuracy = Mathf.Lerp(7f, 0.9f, Mathf.Clamp01(accuracy));
            direction = ApplySpread(direction, inaccuracy);

            slot.Magazine--;
            _nextFireTime = Time.time + def.FireInterval;
            ResolveShot(def, muzzle, direction, muzzle);
            _animator?.TriggerRecoil(0.4f);
            Services.Effects?.SpawnMuzzleFlash(muzzle, direction);

            var clip = Services.Audio?.GetShotClip(def);
            if (clip != null && _audio != null) _audio.PlayOneShot(clip, 0.8f);
            GameEvents.RaiseNoise(new NoiseEvent { Position = transform.position, Loudness = 90f, Source = gameObject, IsGunshot = true });
        }

        public void ClearAll()
        {
            _slots.Clear();
            _reserve.Clear();
            var fists = Services.Database?.Weapon("fists");
            if (fists != null) _slots[0] = new Slot { Definition = fists, Magazine = 0 };
            _currentSlot = 0;
            RefreshModel();
        }

        public Dictionary<AmmoType, int> ReserveSnapshot() => new Dictionary<AmmoType, int>(_reserve);
    }
}
