using UnityEngine;
using SanMonica.Core;

namespace SanMonica.Characters
{
    /// <summary>
    /// Health, armour, hit zones and death handling shared by the player and
    /// every NPC in San Monica.
    /// </summary>
    public class CharacterHealth : MonoBehaviour, IDamageable
    {
        [Header("Vitals")]
        public float MaxHealth = 100f;
        public float Health = 100f;
        public float MaxArmour = 100f;
        public float Armour = 0f;

        [Header("Behaviour")]
        public bool IsPlayer;
        public bool RegenerateHealth;
        public float RegenRate = 3.5f;
        public float RegenDelay = 6f;
        public float RegenCeiling = 0.45f;   // fraction of max health regen can reach
        public bool CanDrown = true;
        public float BreathSeconds = 26f;

        public System.Action<DamageInfo> Damaged;
        public System.Action<DamageInfo> Died;

        private RagdollController _ragdoll;
        private ProceduralAnimator _animator;
        private float _lastDamageTime = -99f;
        private float _breath;
        private bool _dead;

        public bool IsAlive => !_dead;
        public Transform Transform => transform;
        public float Breath => _breath;
        public float BreathNormalised => Mathf.Clamp01(_breath / Mathf.Max(1f, BreathSeconds));
        public GameObject LastAttacker { get; private set; }
        public float TimeSinceDamage => Time.time - _lastDamageTime;

        public void Bind(RagdollController ragdoll, ProceduralAnimator animator)
        {
            _ragdoll = ragdoll;
            _animator = animator;
        }

        public void ResetVitals(float maxHealth, float armour)
        {
            MaxHealth = Mathf.Max(1f, maxHealth);
            Health = MaxHealth;
            Armour = Mathf.Clamp(armour, 0f, MaxArmour);
            _dead = false;
            _breath = BreathSeconds;
            LastAttacker = null;
            if (_ragdoll != null) _ragdoll.Disable();
            if (_animator != null) _animator.Dead = false;
        }

        private void Update()
        {
            if (_dead) return;
            if (RegenerateHealth && Time.time - _lastDamageTime > RegenDelay)
            {
                float ceiling = MaxHealth * Mathf.Clamp01(Mathf.Max(RegenCeiling, Health / MaxHealth));
                if (Health < ceiling)
                    Health = Mathf.Min(ceiling, Health + RegenRate * Time.deltaTime);
            }
        }

        /// <summary>Called by the controller once per frame while the head is underwater.</summary>
        public void TickUnderwater(float dt, bool headUnderwater)
        {
            if (!CanDrown || _dead) return;
            if (headUnderwater)
            {
                _breath -= dt;
                if (_breath <= 0f)
                {
                    ApplyDamage(DamageInfo.Simple(14f * dt, DamageKind.Drowning, null, transform.position, Vector3.up));
                }
            }
            else _breath = Mathf.Min(BreathSeconds, _breath + dt * 5f);
        }

        public void Heal(float amount)
        {
            if (_dead) return;
            Health = Mathf.Min(MaxHealth, Health + amount);
        }

        public void AddArmour(float amount)
        {
            Armour = Mathf.Clamp(Armour + amount, 0f, MaxArmour);
        }

        public void ApplyDamage(in DamageInfo info)
        {
            if (_dead) return;

            float amount = info.Amount;
            switch (info.Part)
            {
                case BodyPart.Head: amount *= 3.0f; break;
                case BodyPart.LeftArm:
                case BodyPart.RightArm: amount *= 0.72f; break;
                case BodyPart.LeftLeg:
                case BodyPart.RightLeg: amount *= 0.78f; break;
            }

            // Armour soaks damage on the torso and head only.
            if (Armour > 0f && info.Kind != DamageKind.Drowning && info.Kind != DamageKind.Fall)
            {
                float pierce = Mathf.Clamp01(info.ArmourPiercing);
                float soakable = amount * (1f - pierce) * 0.65f;
                float soaked = Mathf.Min(Armour, soakable);
                Armour -= soaked;
                amount -= soaked;
            }

            Health -= amount;
            _lastDamageTime = Time.time;
            if (info.Source != null) LastAttacker = info.Source;
            if (_animator != null) _animator.TriggerHitReaction(Mathf.Clamp01(amount / 40f));
            Damaged?.Invoke(info);

            if (Health <= 0f)
            {
                Health = 0f;
                Kill(info);
            }
        }

        public void Kill(in DamageInfo info)
        {
            if (_dead) return;
            _dead = true;
            Health = 0f;

            if (_animator != null) _animator.Dead = true;
            if (_ragdoll != null)
            {
                Vector3 impulse = info.Direction * Mathf.Min(info.Force, 900f);
                if (info.Kind == DamageKind.Explosion) impulse += Vector3.up * 260f;
                _ragdoll.Enable(impulse, info.Point == Vector3.zero ? transform.position + Vector3.up : info.Point);
            }

            Died?.Invoke(info);
            if (IsPlayer) GameEvents.RaisePlayerDied();
            else GameEvents.RaisePedKilled(gameObject, info.Source);
        }

        /// <summary>Resolves a raycast hit into a damage application with the right body part.</summary>
        public static bool ApplyHit(Collider collider, in DamageInfo baseInfo, out CharacterHealth target)
        {
            target = null;
            if (collider == null) return false;
            var zone = collider.GetComponent<HitZone>();
            if (zone != null && zone.Owner != null)
            {
                var info = baseInfo;
                info.Part = zone.Part;
                zone.Owner.ApplyDamage(in info);
                target = zone.Owner;
                return true;
            }
            var health = collider.GetComponentInParent<CharacterHealth>();
            if (health != null)
            {
                health.ApplyDamage(in baseInfo);
                target = health;
                return true;
            }
            return false;
        }
    }
}
