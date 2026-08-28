using UnityEngine;
using SanMonica.Characters;
using SanMonica.Core;
using SanMonica.Data;

namespace SanMonica.Weapons
{
    /// <summary>Grenades, firebombs and rockets: fuse, blast radius and falloff.</summary>
    public class Explosive : MonoBehaviour
    {
        private WeaponDefinition _definition;
        private GameObject _owner;
        private float _fuse;
        private bool _detonateOnImpact;
        private bool _spent;

        public void Setup(WeaponDefinition definition, GameObject owner)
        {
            _definition = definition;
            _owner = owner;
            _fuse = definition.fuseTime;
            _detonateOnImpact = definition.fuseTime <= 0.01f;
            if (!_detonateOnImpact) Destroy(gameObject, definition.fuseTime + 6f);
            else Destroy(gameObject, 12f);
        }

        private void Update()
        {
            if (_detonateOnImpact || _spent) return;
            _fuse -= Time.deltaTime;
            if (_fuse <= 0f) Detonate(transform.position);
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!_detonateOnImpact || _spent) return;
            Detonate(collision.GetContact(0).point);
        }

        private void Detonate(Vector3 position)
        {
            if (_spent) return;
            _spent = true;

            float radius = Mathf.Max(1f, _definition.explosionRadius);
            Services.Effects?.SpawnExplosion(position, radius);
            GameEvents.RaiseExplosion(position, radius);
            GameEvents.RaiseNoise(new NoiseEvent { Position = position, Loudness = 160f, Source = _owner, IsGunshot = true });

            if (_owner != null && Services.Player != null && _owner == Services.Player.gameObject)
                GameEvents.RaiseCrime(new CrimeEvent { Type = CrimeType.Explosion, Position = position, Perpetrator = _owner });

            var hits = Physics.OverlapSphere(position, radius, GameLayers.ShootableMask | GameLayers.VehicleMask, QueryTriggerInteraction.Ignore);
            var processed = new System.Collections.Generic.HashSet<int>();

            foreach (var hit in hits)
            {
                float distance = Vector3.Distance(position, hit.ClosestPoint(position));
                float falloff = Mathf.Clamp01(1f - distance / radius);
                if (falloff <= 0f) continue;
                float damage = _definition.explosionDamage * falloff * falloff;
                Vector3 direction = (hit.transform.position - position).normalized + Vector3.up * 0.4f;

                var health = hit.GetComponentInParent<CharacterHealth>();
                if (health != null)
                {
                    if (!processed.Add(health.GetInstanceID())) continue;
                    health.ApplyDamage(DamageInfo.Simple(damage, DamageKind.Explosion, _owner, hit.ClosestPoint(position), direction, 420f * falloff));
                    continue;
                }

                var vehicle = hit.GetComponentInParent<SanMonica.Vehicles.Vehicle>();
                if (vehicle != null)
                {
                    if (!processed.Add(vehicle.GetInstanceID())) continue;
                    vehicle.ApplyDamage(DamageInfo.Simple(damage, DamageKind.Explosion, _owner, position, direction, 0f));
                    if (vehicle.Body != null) vehicle.Body.AddExplosionForce(damage * 40f, position, radius, 1.2f, ForceMode.Impulse);
                    continue;
                }

                var body = hit.attachedRigidbody;
                if (body != null && !body.isKinematic) body.AddExplosionForce(damage * 22f, position, radius, 0.8f, ForceMode.Impulse);
            }

            Destroy(gameObject);
        }
    }
}
