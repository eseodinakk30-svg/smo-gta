using UnityEngine;

namespace SanMonica.Data
{
    public enum WeaponCategory { Unarmed, Melee, Pistol, SMG, Shotgun, Rifle, Sniper, Heavy, Thrown }
    public enum AmmoType { None, Pistol, SMG, Shell, Rifle, Sniper, Rocket, Grenade }

    [CreateAssetMenu(menuName = "San Monica/Weapon", fileName = "Weapon")]
    public class WeaponDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string id = "weapon";
        public string displayName = "Weapon";
        public WeaponCategory category = WeaponCategory.Pistol;
        public AmmoType ammoType = AmmoType.Pistol;
        public int price = 500;
        public int ammoPrice = 2;
        public int slot = 1;

        [Header("Ballistics")]
        public float damage = 26f;
        public float headshotMultiplier = 3.2f;
        public float limbMultiplier = 0.65f;
        public float range = 90f;
        public float roundsPerMinute = 320f;
        public bool automatic = false;
        public int pelletsPerShot = 1;
        public float spreadDegrees = 0.8f;
        public float aimSpreadMultiplier = 0.35f;
        public float moveSpreadPenalty = 2.2f;
        public float armourPiercing = 0f;
        public float impactForce = 180f;

        [Header("Ballistics - range and penetration")]
        /// <summary>Distance at which damage starts to drop. 0 = 45% of range.</summary>
        public float falloffStart = 0f;
        /// <summary>Fraction of the damage left once the round reaches maximum range.</summary>
        public float falloffDamageFraction = 0.55f;
        /// <summary>How many extra surfaces a round can punch through. 0 = stops at the first.</summary>
        public int penetration = -1;
        /// <summary>Damage lost on each surface a round penetrates.</summary>
        public float penetrationLoss = 0.4f;
        /// <summary>How far the shot is heard, in metres. 0 = derived from the category.</summary>
        public float noiseRadius = 0f;
        public bool suppressed = false;

        [Header("Burst fire")]
        /// <summary>Rounds per trigger pull. 0 = not a burst weapon.</summary>
        public int burstCount = 0;
        /// <summary>Forced pause after a burst, in seconds.</summary>
        public float burstInterval = 0.32f;

        [Header("Recoil")]
        public float recoilVertical = 1.4f;
        public float recoilHorizontal = 0.5f;
        public float recoilRecovery = 6f;
        public float cameraShake = 0.15f;

        [Header("Magazine")]
        public int magazineSize = 12;
        public int maxReserve = 120;
        public float reloadTime = 1.9f;

        [Header("Melee")]
        public float meleeArc = 70f;
        public float meleeReach = 2.1f;
        public float meleeCooldown = 0.55f;

        [Header("Thrown / explosive")]
        public float explosionRadius = 0f;
        public float explosionDamage = 0f;
        public float fuseTime = 0f;
        public float throwForce = 18f;

        [Header("Presentation (procedural model)")]
        public Vector3 bodySize = new Vector3(0.05f, 0.13f, 0.22f);
        public float barrelLength = 0.14f;
        public float barrelRadius = 0.014f;
        public bool hasStock = false;
        public bool hasMagazine = true;
        public bool hasScope = false;
        public bool hasForegrip = false;
        public Color bodyColor = new Color(0.16f, 0.16f, 0.18f);
        public Color accentColor = new Color(0.32f, 0.30f, 0.28f);

        [Header("Audio (procedural synthesis)")]
        public float shotPitch = 1f;
        public float shotBody = 0.6f;
        public float shotTail = 0.35f;

        public float FireInterval => roundsPerMinute > 0f ? 60f / roundsPerMinute : 0.5f;
        public bool IsGun => category != WeaponCategory.Melee && category != WeaponCategory.Unarmed && category != WeaponCategory.Thrown;
        public bool IsBurst => burstCount > 1;
        public float FalloffStart => falloffStart > 0.01f ? falloffStart : range * 0.45f;

        /// <summary>
        /// Damage this round still carries after travelling <paramref name="distance"/>
        /// metres. Pistol rounds lose most of their sting across a street; rifle
        /// rounds barely notice, which is what makes the choice of weapon matter
        /// at range rather than only in a corridor.
        /// </summary>
        public float DamageAtRange(float distance)
        {
            float start = FalloffStart;
            if (distance <= start || range <= start) return damage;
            float t = Mathf.Clamp01((distance - start) / (range - start));
            return damage * Mathf.Lerp(1f, Mathf.Clamp01(falloffDamageFraction), t);
        }
    }

    public static class WeaponCatalogData
    {
        private static System.Collections.Generic.List<WeaponDefinition> _all;
        public static System.Collections.Generic.List<WeaponDefinition> All
        {
            get { if (_all == null) Build(); return _all; }
        }

        private static WeaponDefinition W(string id, string name, WeaponCategory cat, AmmoType ammo, int slot)
        {
            var w = ScriptableObject.CreateInstance<WeaponDefinition>();
            w.name = "Wpn_" + id;
            w.id = id; w.displayName = name; w.category = cat; w.ammoType = ammo; w.slot = slot;
            return w;
        }

        private static void Build()
        {
            _all = new System.Collections.Generic.List<WeaponDefinition>();
            WeaponDefinition w;

            // Slot 0 - unarmed / melee
            w = W("fists", "Fists", WeaponCategory.Unarmed, AmmoType.None, 0);
            w.damage = 9f; w.meleeReach = 1.5f; w.meleeCooldown = 0.38f; w.price = 0; w.impactForce = 90f;
            _all.Add(w);

            w = W("bat", "Ash Bat", WeaponCategory.Melee, AmmoType.None, 0);
            w.damage = 32f; w.meleeReach = 2.2f; w.meleeCooldown = 0.62f; w.price = 180; w.impactForce = 320f;
            w.bodySize = new Vector3(0.05f, 0.05f, 0.85f); w.bodyColor = new Color(0.55f, 0.40f, 0.24f);
            _all.Add(w);

            w = W("wrench", "Pipe Wrench", WeaponCategory.Melee, AmmoType.None, 0);
            w.damage = 38f; w.meleeReach = 1.9f; w.meleeCooldown = 0.70f; w.price = 140; w.impactForce = 300f;
            w.bodySize = new Vector3(0.06f, 0.05f, 0.48f); w.bodyColor = new Color(0.42f, 0.44f, 0.47f);
            _all.Add(w);

            w = W("machete", "Machete", WeaponCategory.Melee, AmmoType.None, 0);
            w.damage = 52f; w.meleeReach = 2.0f; w.meleeCooldown = 0.55f; w.price = 320; w.impactForce = 210f;
            w.bodySize = new Vector3(0.02f, 0.09f, 0.60f); w.bodyColor = new Color(0.62f, 0.64f, 0.66f);
            _all.Add(w);

            // Slot 1 - pistols
            w = W("p9", "Vireo P9", WeaponCategory.Pistol, AmmoType.Pistol, 1);
            w.damage = 26f; w.range = 85f; w.roundsPerMinute = 340f; w.magazineSize = 12; w.maxReserve = 120;
            w.reloadTime = 1.6f; w.spreadDegrees = 0.9f; w.recoilVertical = 1.5f; w.price = 900; w.ammoPrice = 2;
            _all.Add(w);

            w = W("p9-heavy", "Corvale Magnum", WeaponCategory.Pistol, AmmoType.Pistol, 1);
            w.damage = 54f; w.range = 95f; w.roundsPerMinute = 140f; w.magazineSize = 6; w.maxReserve = 72;
            w.reloadTime = 2.3f; w.spreadDegrees = 1.3f; w.recoilVertical = 3.6f; w.recoilHorizontal = 1.1f;
            w.cameraShake = 0.28f; w.price = 4200; w.ammoPrice = 6; w.impactForce = 380f;
            w.bodySize = new Vector3(0.055f, 0.15f, 0.26f);
            _all.Add(w);

            w = W("machine-pistol", "Vireo Rapid", WeaponCategory.Pistol, AmmoType.Pistol, 1);
            w.damage = 19f; w.range = 62f; w.roundsPerMinute = 780f; w.automatic = true; w.magazineSize = 20;
            w.maxReserve = 200; w.reloadTime = 1.8f; w.spreadDegrees = 2.4f; w.recoilVertical = 1.1f;
            w.price = 3600; w.ammoPrice = 2; w.hasMagazine = true;
            _all.Add(w);

            // Slot 2 - SMG
            w = W("smg-9", "Ashford Vector 9", WeaponCategory.SMG, AmmoType.SMG, 2);
            w.damage = 22f; w.range = 78f; w.roundsPerMinute = 820f; w.automatic = true; w.magazineSize = 30;
            w.maxReserve = 300; w.reloadTime = 2.1f; w.spreadDegrees = 1.9f; w.recoilVertical = 1.2f;
            w.recoilHorizontal = 0.7f; w.price = 8500; w.ammoPrice = 3; w.hasStock = true; w.hasForegrip = true;
            w.bodySize = new Vector3(0.06f, 0.16f, 0.40f); w.barrelLength = 0.20f;
            _all.Add(w);

            w = W("smg-heavy", "Brackett Sweeper", WeaponCategory.SMG, AmmoType.SMG, 2);
            w.damage = 27f; w.range = 84f; w.roundsPerMinute = 640f; w.automatic = true; w.magazineSize = 36;
            w.maxReserve = 320; w.reloadTime = 2.4f; w.spreadDegrees = 1.7f; w.recoilVertical = 1.6f;
            w.price = 16500; w.ammoPrice = 4; w.hasStock = true; w.hasForegrip = true;
            w.bodySize = new Vector3(0.07f, 0.17f, 0.46f); w.barrelLength = 0.24f;
            _all.Add(w);

            // Slot 3 - shotguns
            w = W("pump", "Steadman Coast Pump", WeaponCategory.Shotgun, AmmoType.Shell, 3);
            w.damage = 15f; w.pelletsPerShot = 9; w.range = 38f; w.roundsPerMinute = 72f; w.magazineSize = 6;
            w.maxReserve = 60; w.reloadTime = 3.1f; w.spreadDegrees = 5.5f; w.recoilVertical = 4.2f;
            w.cameraShake = 0.35f; w.price = 11000; w.ammoPrice = 8; w.hasStock = true; w.impactForce = 620f;
            w.bodySize = new Vector3(0.07f, 0.16f, 0.62f); w.barrelLength = 0.42f; w.barrelRadius = 0.022f;
            _all.Add(w);

            w = W("auto-shotgun", "Iron Bay Breaker", WeaponCategory.Shotgun, AmmoType.Shell, 3);
            w.damage = 13f; w.pelletsPerShot = 8; w.range = 34f; w.roundsPerMinute = 190f; w.automatic = true;
            w.magazineSize = 10; w.maxReserve = 80; w.reloadTime = 3.4f; w.spreadDegrees = 6.5f;
            w.recoilVertical = 3.4f; w.price = 28000; w.ammoPrice = 10; w.hasStock = true; w.hasForegrip = true;
            w.bodySize = new Vector3(0.08f, 0.18f, 0.58f); w.barrelLength = 0.36f; w.barrelRadius = 0.023f;
            _all.Add(w);

            // Slot 4 - rifles
            w = W("carbine", "Ashford AR-7", WeaponCategory.Rifle, AmmoType.Rifle, 4);
            w.damage = 31f; w.range = 165f; w.roundsPerMinute = 620f; w.automatic = true; w.magazineSize = 30;
            w.maxReserve = 300; w.reloadTime = 2.4f; w.spreadDegrees = 1.2f; w.recoilVertical = 1.7f;
            w.recoilHorizontal = 0.6f; w.armourPiercing = 0.25f; w.price = 24000; w.ammoPrice = 5;
            w.hasStock = true; w.hasForegrip = true; w.bodySize = new Vector3(0.07f, 0.17f, 0.58f);
            w.barrelLength = 0.34f; w.barrelRadius = 0.016f;
            _all.Add(w);

            w = W("battle-rifle", "Vanguard Bulldog", WeaponCategory.Rifle, AmmoType.Rifle, 4);
            w.damage = 44f; w.range = 195f; w.roundsPerMinute = 420f; w.automatic = true; w.magazineSize = 20;
            w.maxReserve = 240; w.reloadTime = 2.7f; w.spreadDegrees = 1.0f; w.recoilVertical = 2.6f;
            w.armourPiercing = 0.45f; w.price = 52000; w.ammoPrice = 8; w.hasStock = true; w.hasForegrip = true;
            w.hasScope = true; w.bodySize = new Vector3(0.075f, 0.18f, 0.68f); w.barrelLength = 0.42f;
            _all.Add(w);

            // Slot 5 - snipers
            w = W("marksman", "Pinecrest Marksman", WeaponCategory.Sniper, AmmoType.Sniper, 5);
            w.damage = 110f; w.headshotMultiplier = 4.5f; w.range = 420f; w.roundsPerMinute = 48f;
            w.magazineSize = 5; w.maxReserve = 40; w.reloadTime = 3.2f; w.spreadDegrees = 0.15f;
            w.aimSpreadMultiplier = 0.02f; w.recoilVertical = 5.5f; w.cameraShake = 0.4f; w.armourPiercing = 0.7f;
            w.price = 68000; w.ammoPrice = 25; w.hasStock = true; w.hasScope = true; w.impactForce = 900f;
            w.bodySize = new Vector3(0.07f, 0.17f, 0.92f); w.barrelLength = 0.62f; w.barrelRadius = 0.017f;
            _all.Add(w);

            // Slot 6 - heavy
            w = W("lmg", "Brackett Foundry LMG", WeaponCategory.Heavy, AmmoType.Rifle, 6);
            w.damage = 36f; w.range = 175f; w.roundsPerMinute = 720f; w.automatic = true; w.magazineSize = 100;
            w.maxReserve = 400; w.reloadTime = 5.2f; w.spreadDegrees = 2.6f; w.recoilVertical = 2.0f;
            w.recoilHorizontal = 1.2f; w.moveSpreadPenalty = 3.4f; w.price = 140000; w.ammoPrice = 6;
            w.hasStock = true; w.hasForegrip = true; w.bodySize = new Vector3(0.09f, 0.20f, 0.86f);
            w.barrelLength = 0.52f; w.barrelRadius = 0.020f;
            _all.Add(w);

            w = W("rpg", "Halcyon Tube", WeaponCategory.Heavy, AmmoType.Rocket, 6);
            w.damage = 0f; w.range = 320f; w.roundsPerMinute = 24f; w.magazineSize = 1; w.maxReserve = 12;
            w.reloadTime = 4.0f; w.explosionRadius = 11f; w.explosionDamage = 340f; w.cameraShake = 0.6f;
            w.price = 320000; w.ammoPrice = 900; w.hasStock = false;
            w.bodySize = new Vector3(0.10f, 0.11f, 1.05f); w.barrelLength = 0.30f; w.barrelRadius = 0.055f;
            w.hasMagazine = false;
            _all.Add(w);

            // Slot 7 - thrown
            w = W("grenade", "Frag Charge", WeaponCategory.Thrown, AmmoType.Grenade, 7);
            w.damage = 0f; w.explosionRadius = 9.5f; w.explosionDamage = 220f; w.fuseTime = 3.2f;
            w.throwForce = 18f; w.magazineSize = 1; w.maxReserve = 25; w.price = 1200; w.ammoPrice = 400;
            w.bodySize = new Vector3(0.08f, 0.11f, 0.08f); w.bodyColor = new Color(0.22f, 0.28f, 0.20f);
            w.roundsPerMinute = 45f; w.cameraShake = 0.5f;
            _all.Add(w);

            w = W("molotov", "Firebottle", WeaponCategory.Thrown, AmmoType.Grenade, 7);
            w.damage = 0f; w.explosionRadius = 6.5f; w.explosionDamage = 90f; w.fuseTime = 0f;
            w.throwForce = 16f; w.magazineSize = 1; w.maxReserve = 20; w.price = 600; w.ammoPrice = 120;
            w.bodySize = new Vector3(0.07f, 0.22f, 0.07f); w.bodyColor = new Color(0.40f, 0.52f, 0.24f);
            w.roundsPerMinute = 50f; w.cameraShake = 0.3f;
            _all.Add(w);

            // ----------------------------------------------------------------
            // Quiet and specialist options. A city this size needs more than one
            // answer to a locked gate or a rooftop sniper.
            w = W("knife", "Boning Knife", WeaponCategory.Melee, AmmoType.None, 0);
            w.damage = 34f; w.meleeReach = 1.5f; w.meleeCooldown = 0.34f; w.price = 90; w.impactForce = 120f;
            w.limbMultiplier = 0.85f; w.headshotMultiplier = 2.4f;
            w.bodySize = new Vector3(0.015f, 0.05f, 0.30f); w.bodyColor = new Color(0.70f, 0.72f, 0.75f);
            _all.Add(w);

            w = W("baton", "SMPD Baton", WeaponCategory.Melee, AmmoType.None, 0);
            w.damage = 22f; w.meleeReach = 1.8f; w.meleeCooldown = 0.45f; w.price = 220; w.impactForce = 260f;
            w.bodySize = new Vector3(0.035f, 0.035f, 0.58f); w.bodyColor = new Color(0.12f, 0.12f, 0.14f);
            _all.Add(w);

            w = W("p9-suppressed", "Vireo P9-S", WeaponCategory.Pistol, AmmoType.Pistol, 1);
            w.damage = 23f; w.range = 72f; w.roundsPerMinute = 300f; w.magazineSize = 12; w.maxReserve = 120;
            w.reloadTime = 1.7f; w.spreadDegrees = 0.7f; w.recoilVertical = 1.0f; w.price = 6400; w.ammoPrice = 3;
            w.suppressed = true; w.cameraShake = 0.08f;
            w.bodySize = new Vector3(0.05f, 0.13f, 0.24f); w.barrelLength = 0.26f; w.barrelRadius = 0.021f;
            _all.Add(w);

            w = W("sawn-off", "Coast Stubby", WeaponCategory.Shotgun, AmmoType.Shell, 3);
            w.damage = 17f; w.pelletsPerShot = 10; w.range = 22f; w.roundsPerMinute = 110f; w.magazineSize = 2;
            w.maxReserve = 40; w.reloadTime = 2.4f; w.spreadDegrees = 9.5f; w.recoilVertical = 5.0f;
            w.cameraShake = 0.42f; w.price = 5200; w.ammoPrice = 8; w.impactForce = 700f;
            w.bodySize = new Vector3(0.07f, 0.15f, 0.34f); w.barrelLength = 0.20f; w.barrelRadius = 0.024f;
            _all.Add(w);

            w = W("burst-carbine", "Ashford AR-7B", WeaponCategory.Rifle, AmmoType.Rifle, 4);
            w.damage = 33f; w.range = 170f; w.roundsPerMinute = 900f; w.burstCount = 3; w.burstInterval = 0.30f;
            w.magazineSize = 30; w.maxReserve = 300; w.reloadTime = 2.4f; w.spreadDegrees = 0.9f;
            w.recoilVertical = 1.5f; w.armourPiercing = 0.3f; w.price = 31000; w.ammoPrice = 5;
            w.hasStock = true; w.hasForegrip = true; w.bodySize = new Vector3(0.07f, 0.17f, 0.56f);
            w.barrelLength = 0.32f; w.barrelRadius = 0.016f;
            _all.Add(w);

            w = W("dmr", "Pinecrest Scout", WeaponCategory.Sniper, AmmoType.Sniper, 5);
            w.damage = 68f; w.headshotMultiplier = 3.6f; w.range = 280f; w.roundsPerMinute = 150f;
            w.magazineSize = 10; w.maxReserve = 80; w.reloadTime = 2.6f; w.spreadDegrees = 0.35f;
            w.aimSpreadMultiplier = 0.06f; w.recoilVertical = 3.4f; w.cameraShake = 0.3f; w.armourPiercing = 0.55f;
            w.price = 44000; w.ammoPrice = 14; w.hasStock = true; w.hasScope = true; w.impactForce = 600f;
            w.bodySize = new Vector3(0.07f, 0.17f, 0.74f); w.barrelLength = 0.46f; w.barrelRadius = 0.016f;
            _all.Add(w);

            w = W("minigun", "Foundry Rotary", WeaponCategory.Heavy, AmmoType.Rifle, 6);
            w.damage = 24f; w.range = 140f; w.roundsPerMinute = 1800f; w.automatic = true; w.magazineSize = 250;
            w.maxReserve = 750; w.reloadTime = 7.5f; w.spreadDegrees = 4.2f; w.recoilVertical = 0.8f;
            w.recoilHorizontal = 1.6f; w.moveSpreadPenalty = 5.5f; w.price = 420000; w.ammoPrice = 6;
            w.cameraShake = 0.32f; w.hasStock = false; w.hasForegrip = true;
            w.bodySize = new Vector3(0.14f, 0.20f, 0.70f); w.barrelLength = 0.60f; w.barrelRadius = 0.045f;
            _all.Add(w);

            w = W("pipebomb", "Pipe Charge", WeaponCategory.Thrown, AmmoType.Grenade, 7);
            w.damage = 0f; w.explosionRadius = 7.5f; w.explosionDamage = 160f; w.fuseTime = 5.0f;
            w.throwForce = 15f; w.magazineSize = 1; w.maxReserve = 20; w.price = 700; w.ammoPrice = 180;
            w.bodySize = new Vector3(0.06f, 0.24f, 0.06f); w.bodyColor = new Color(0.45f, 0.42f, 0.38f);
            w.roundsPerMinute = 45f; w.cameraShake = 0.42f;
            _all.Add(w);

            ApplyDerivedStats();
        }

        /// <summary>
        /// Fills in the stats that follow from the weapon class, so every entry in
        /// the table only has to state what makes it different. Anything already
        /// set by hand above is left alone.
        /// </summary>
        private static void ApplyDerivedStats()
        {
            foreach (var w in _all)
            {
                if (w.penetration < 0)
                {
                    switch (w.category)
                    {
                        case WeaponCategory.Rifle: w.penetration = 1; break;
                        case WeaponCategory.Sniper: w.penetration = 3; break;
                        case WeaponCategory.Heavy: w.penetration = 2; break;
                        default: w.penetration = 0; break;
                    }
                }

                if (w.noiseRadius <= 0f)
                {
                    switch (w.category)
                    {
                        case WeaponCategory.Unarmed: w.noiseRadius = 6f; break;
                        case WeaponCategory.Melee: w.noiseRadius = 12f; break;
                        case WeaponCategory.Thrown: w.noiseRadius = 20f; break;
                        case WeaponCategory.Pistol: w.noiseRadius = 95f; break;
                        case WeaponCategory.SMG: w.noiseRadius = 105f; break;
                        case WeaponCategory.Shotgun: w.noiseRadius = 130f; break;
                        case WeaponCategory.Rifle: w.noiseRadius = 140f; break;
                        case WeaponCategory.Sniper: w.noiseRadius = 165f; break;
                        case WeaponCategory.Heavy: w.noiseRadius = 155f; break;
                    }
                }

                // A suppressor is the whole point of carrying one: the shot stops
                // being a city block event and becomes a room event.
                if (w.suppressed) w.noiseRadius *= 0.18f;
            }
        }
    }
}
