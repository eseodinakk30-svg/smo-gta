using UnityEngine;

namespace SanMonica.Characters
{
    public enum BodyPart { Torso, Head, LeftArm, RightArm, LeftLeg, RightLeg }

    public enum DamageKind { Bullet, Melee, Explosion, Vehicle, Fall, Drowning, Fire, Environment }

    public struct DamageInfo
    {
        public float Amount;
        public Vector3 Point;
        public Vector3 Direction;
        public float Force;
        public GameObject Source;      // who caused it
        public DamageKind Kind;
        public BodyPart Part;
        public float ArmourPiercing;
        /// <summary>Weapon's own headshot multiplier. 0 leaves the default in place.</summary>
        public float HeadMultiplier;
        /// <summary>Weapon's own arm/leg multiplier. 0 leaves the default in place.</summary>
        public float LimbMultiplier;

        public static DamageInfo Simple(float amount, DamageKind kind, GameObject source, Vector3 point, Vector3 dir, float force = 0f)
        {
            return new DamageInfo
            {
                Amount = amount, Kind = kind, Source = source, Point = point,
                Direction = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector3.forward,
                Force = force, Part = BodyPart.Torso
            };
        }
    }

    public interface IDamageable
    {
        void ApplyDamage(in DamageInfo info);
        bool IsAlive { get; }
        Transform Transform { get; }
    }

    /// <summary>Per-bone hit box used to resolve headshots and limb hits.</summary>
    public class HitZone : MonoBehaviour
    {
        public BodyPart Part = BodyPart.Torso;
        public CharacterHealth Owner;
    }
}
