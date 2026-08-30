using UnityEngine;
using SanMonica.Data;

namespace SanMonica.AI
{
    /// <summary>
    /// How a citizen spends their day. PedRole was written into every archetype
    /// and read by nothing, so a jogger, a security guard, a dockworker and a
    /// tourist all did the same thing: walk to a random point, stand for a few
    /// seconds, repeat. This turns the role into behaviour.
    /// </summary>
    public struct RoleProfile
    {
        public float RadiusMin, RadiusMax;   // how far a wander destination may be
        public float SpeedScale;             // multiplier on the archetype's walk speed
        public float IdleChance;             // chance of standing rather than walking on
        public float IdleMin, IdleMax;       // seconds spent standing
        public float WanderMin, WanderMax;   // seconds walking before reconsidering
        public float WorkChance;             // chance of settling into a task
        public float PostRadius;             // 0 = free to roam; otherwise stays near the spawn

        public static RoleProfile For(PedRole role)
        {
            // The default citizen: a short walk, a brief pause, no hurry.
            var p = new RoleProfile
            {
                RadiusMin = 20f, RadiusMax = 70f, SpeedScale = 1f,
                IdleChance = 0.4f, IdleMin = 3f, IdleMax = 9f,
                WanderMin = 18f, WanderMax = 45f, WorkChance = 0f, PostRadius = 0f
            };

            switch (role)
            {
                case PedRole.Jogger:
                    // Long circuits at a running pace, and they do not stop.
                    p.RadiusMin = 60f; p.RadiusMax = 160f; p.SpeedScale = 2.5f;
                    p.IdleChance = 0.05f; p.IdleMin = 2f; p.IdleMax = 5f;
                    p.WanderMin = 40f; p.WanderMax = 90f;
                    break;

                case PedRole.Commuter:
                    p.RadiusMin = 70f; p.RadiusMax = 190f; p.SpeedScale = 1.35f;
                    p.IdleChance = 0.12f; p.IdleMin = 2f; p.IdleMax = 5f;
                    p.WanderMin = 35f; p.WanderMax = 70f;
                    break;

                case PedRole.Tourist:
                case PedRole.Beachgoer:
                    // Two steps and another photograph.
                    p.RadiusMin = 12f; p.RadiusMax = 45f; p.SpeedScale = 0.85f;
                    p.IdleChance = 0.7f; p.IdleMin = 6f; p.IdleMax = 16f;
                    p.WanderMin = 10f; p.WanderMax = 25f;
                    break;

                case PedRole.Vendor:
                    p.PostRadius = 9f; p.RadiusMin = 2f; p.RadiusMax = 7f; p.SpeedScale = 0.8f;
                    p.IdleChance = 0.85f; p.IdleMin = 10f; p.IdleMax = 26f;
                    p.WorkChance = 0.5f;
                    break;

                case PedRole.Guard:
                case PedRole.Police:
                case PedRole.SwatOfficer:
                    // A patrol, not a wander: they stay on their beat.
                    p.PostRadius = 26f; p.RadiusMin = 8f; p.RadiusMax = 24f; p.SpeedScale = 1.1f;
                    p.IdleChance = 0.45f; p.IdleMin = 6f; p.IdleMax = 14f;
                    p.WanderMin = 20f; p.WanderMax = 40f;
                    break;

                case PedRole.Worker:
                case PedRole.Dockworker:
                case PedRole.Mechanic:
                case PedRole.Farmer:
                    p.PostRadius = 40f; p.RadiusMin = 6f; p.RadiusMax = 30f; p.SpeedScale = 0.95f;
                    p.IdleChance = 0.3f; p.IdleMin = 4f; p.IdleMax = 10f;
                    p.WorkChance = 0.55f;
                    break;

                case PedRole.Executive:
                    p.RadiusMin = 40f; p.RadiusMax = 110f; p.SpeedScale = 1.25f;
                    p.IdleChance = 0.2f; p.IdleMin = 3f; p.IdleMax = 7f;
                    break;

                case PedRole.Student:
                    p.RadiusMin = 25f; p.RadiusMax = 90f; p.SpeedScale = 1.05f;
                    p.IdleChance = 0.5f; p.IdleMin = 6f; p.IdleMax = 18f;
                    break;

                case PedRole.Homeless:
                    // Stays where they are, moves rarely and slowly.
                    p.PostRadius = 18f; p.RadiusMin = 3f; p.RadiusMax = 14f; p.SpeedScale = 0.65f;
                    p.IdleChance = 0.85f; p.IdleMin = 15f; p.IdleMax = 45f;
                    p.WanderMin = 8f; p.WanderMax = 20f;
                    break;

                case PedRole.Nightlife:
                    p.RadiusMin = 15f; p.RadiusMax = 60f; p.SpeedScale = 0.9f;
                    p.IdleChance = 0.6f; p.IdleMin = 8f; p.IdleMax = 22f;
                    break;

                case PedRole.Criminal:
                case PedRole.Gangster:
                    // Corner-holding: a small territory, watched.
                    p.PostRadius = 34f; p.RadiusMin = 8f; p.RadiusMax = 28f; p.SpeedScale = 0.95f;
                    p.IdleChance = 0.55f; p.IdleMin = 8f; p.IdleMax = 20f;
                    break;

                case PedRole.Medic:
                    p.RadiusMin = 20f; p.RadiusMax = 70f; p.SpeedScale = 1.2f;
                    p.IdleChance = 0.3f;
                    break;

                case PedRole.Driver:
                    p.RadiusMin = 15f; p.RadiusMax = 50f; p.SpeedScale = 1f;
                    p.IdleChance = 0.5f; p.IdleMin = 5f; p.IdleMax = 14f;
                    break;
            }
            return p;
        }
    }
}
