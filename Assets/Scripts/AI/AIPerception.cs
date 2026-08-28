using System.Collections.Generic;
using UnityEngine;
using SanMonica.Core;

namespace SanMonica.AI
{
    /// <summary>
    /// Sight and hearing for NPCs. Vision is a cone with line-of-sight checks
    /// throttled by the AI level of detail; hearing is event driven off the
    /// global noise bus, so a gunshot in an alley is heard by exactly the NPCs
    /// that should hear it.
    /// </summary>
    public class AIPerception : MonoBehaviour
    {
        [Header("Vision")]
        public float ViewDistance = 34f;
        public float ViewAngle = 118f;
        public float PeripheralDistance = 8f;
        public float NightVisionPenalty = 0.55f;

        [Header("Awareness")]
        public float Alertness = 0.5f;
        public float MemoryDuration = 12f;

        public Transform Eyes;
        public bool CanSeePlayer { get; private set; }
        public float TimeSincePlayerSeen { get; private set; } = 999f;
        public Vector3 LastKnownPlayerPosition { get; private set; }
        public Vector3 LastHeardPosition { get; private set; }
        public float TimeSinceHeard { get; private set; } = 999f;
        public GameObject CurrentThreat { get; private set; }

        private float _scanTimer;
        private float _scanInterval = 0.25f;
        private static readonly Collider[] Buffer = new Collider[16];

        public void SetLod(int lod)
        {
            _scanInterval = lod == 0 ? 0.2f : (lod == 1 ? 0.6f : 1.6f);
            enabled = lod < 2;
        }

        private void OnEnable()
        {
            GameEvents.NoiseMade += OnNoise;
        }

        private void OnDisable()
        {
            GameEvents.NoiseMade -= OnNoise;
        }

        private void OnNoise(NoiseEvent e)
        {
            if (e.Source == gameObject) return;
            float distance = Vector3.Distance(transform.position, e.Position);
            if (distance > e.Loudness) return;
            LastHeardPosition = e.Position;
            TimeSinceHeard = 0f;
            if (e.IsGunshot) Alertness = Mathf.Min(1f, Alertness + 0.4f);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            TimeSincePlayerSeen += dt;
            TimeSinceHeard += dt;

            _scanTimer -= dt;
            if (_scanTimer > 0f) return;
            _scanTimer = _scanInterval;

            var player = Services.Player;
            if (player == null) { CanSeePlayer = false; return; }

            Vector3 target = player.transform.position + Vector3.up * 1.1f;
            CanSeePlayer = CanSee(target, out _);
            if (CanSeePlayer)
            {
                TimeSincePlayerSeen = 0f;
                LastKnownPlayerPosition = player.transform.position;
                CurrentThreat = player.gameObject;
            }
            else if (TimeSincePlayerSeen > MemoryDuration) CurrentThreat = null;
        }

        public Vector3 EyePosition => Eyes != null ? Eyes.position : transform.position + Vector3.up * 1.6f;

        public bool CanSee(Vector3 worldPoint, out float distance)
        {
            Vector3 eye = EyePosition;
            Vector3 delta = worldPoint - eye;
            distance = delta.magnitude;

            float range = ViewDistance;
            var clock = Services.Clock;
            if (clock != null && clock.IsNight) range *= NightVisionPenalty;
            var weather = Services.Weather;
            if (weather != null) range *= weather.VisibilityScale;

            if (distance > range) return false;
            Vector3 dir = delta / Mathf.Max(0.001f, distance);
            float angle = Vector3.Angle(transform.forward, dir);
            if (angle > ViewAngle * 0.5f && distance > PeripheralDistance) return false;

            return !Physics.Raycast(eye, dir, distance - 0.35f, GameLayers.VisionBlockMask, QueryTriggerInteraction.Ignore);
        }

        public bool CanSee(Transform target, out float distance)
        {
            distance = 999f;
            if (target == null) return false;
            return CanSee(target.position + Vector3.up * 1.1f, out distance);
        }

        /// <summary>Finds the closest hostile character within the view cone.</summary>
        public SanMonica.Characters.CharacterHealth FindHostile(SanMonica.Data.Faction myFaction, float radius)
        {
            int count = Physics.OverlapSphereNonAlloc(transform.position, radius, Buffer,
                GameLayers.CharacterMask, QueryTriggerInteraction.Ignore);
            SanMonica.Characters.CharacterHealth best = null;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                var other = Buffer[i].GetComponentInParent<SanMonica.Characters.CharacterHealth>();
                if (other == null || !other.IsAlive || other.gameObject == gameObject) continue;
                var brain = other.GetComponent<PedBrain>();
                var faction = brain != null ? brain.Faction : SanMonica.Data.Faction.Player;
                if (!FactionRelations.IsHostile(myFaction, faction)) continue;
                if (!CanSee(other.transform, out float d)) continue;
                if (d < bestDistance) { bestDistance = d; best = other; }
            }
            return best;
        }
    }

    /// <summary>Who fights whom in San Monica.</summary>
    public static class FactionRelations
    {
        public static bool IsHostile(SanMonica.Data.Faction a, SanMonica.Data.Faction b)
        {
            if (a == b) return false;
            switch (a)
            {
                case SanMonica.Data.Faction.SMPD:
                    return b == SanMonica.Data.Faction.SerranoCartel || b == SanMonica.Data.Faction.IronBaySyndicate
                        || b == SanMonica.Data.Faction.CalleNueve;
                case SanMonica.Data.Faction.SerranoCartel:
                    return b == SanMonica.Data.Faction.SMPD || b == SanMonica.Data.Faction.IronBaySyndicate
                        || b == SanMonica.Data.Faction.CalleNueve || b == SanMonica.Data.Faction.VanguardSecurity;
                case SanMonica.Data.Faction.IronBaySyndicate:
                    return b == SanMonica.Data.Faction.SMPD || b == SanMonica.Data.Faction.SerranoCartel
                        || b == SanMonica.Data.Faction.VanguardSecurity;
                case SanMonica.Data.Faction.CalleNueve:
                    return b == SanMonica.Data.Faction.SerranoCartel || b == SanMonica.Data.Faction.SMPD;
                case SanMonica.Data.Faction.VanguardSecurity:
                case SanMonica.Data.Faction.HalcyonDynamics:
                    return b == SanMonica.Data.Faction.SerranoCartel || b == SanMonica.Data.Faction.IronBaySyndicate;
                default:
                    return false;
            }
        }

        /// <summary>Faction attitude toward the player, adjusted by story state.</summary>
        public static bool IsHostileToPlayer(SanMonica.Data.Faction faction)
        {
            var missions = Services.Missions;
            if (missions == null) return false;
            return missions.IsFactionHostile(faction);
        }
    }
}
