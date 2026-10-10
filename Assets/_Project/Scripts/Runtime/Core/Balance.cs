using System;

namespace Game.Core
{
    /// <summary>All tuning numbers in one place so the game can be balanced without hunting through code.</summary>
    public static class Balance
    {
        public const float HitCooldown = 0.35f;          // seconds between the player's automatic hammer hits
        public const float WorkerHitInterval = 0.8f;     // seconds between an auto-worker's hits
        public const float PlayerSwingSeconds = 0.32f;   // length of the player's hammer swing animation (a bit under the cooldown, so each hit reads)
        public const float WorkerSwingSeconds = 0.30f;
        // Upgrade caps below are "tiers" as the shop shows them: everything starts at tier 1 (workers: one free helper), the cap is
        // reached after (cap - 1) purchases. The tracks are built with maxLevel = cap - 1.
        public const int MaxWorkers = 8;
        public const int MaxHammerLevel = 25;

        // ---- Districts: five areas of six buildings (BuildPlan.Kind picks the model: hut, shop, school, factory, ship...). Each one plays like the first at a bigger scale (DistrictDef.Scale: x4 per
        // district for prices, hit power, work and rewards) with its own upgrades, and later districts also have bigger buildings
        // (the numbers below are first-district shapes times a per-district size: x0.85, 2, 4, 7, 10), so they take longer.
        // Tuned with tools/balance_sim.py (attentive player, all upgrades, own coins in every district, one free helper to start,
        // coins = 60% of the work): about 3.1 / 4.7 / 6.8 / 9.3 / 11.6 minutes per district, 36 in all; real play is ~25% slower
        // and has walking on top.
        public static readonly DistrictDef[] Districts =
        {
            new DistrictDef("Sahil",  requiredLevel: 1, scale: 1,
                new BuildPlan("Kulübe",         2, 145, 130, xp: 1, kind: BuildingKind.Hut),
                new BuildPlan("Dondurmacı",     2, 232, 195, xp: 1, kind: BuildingKind.Shop, variant: 1),
                new BuildPlan("Balıkçı Teknesi",3, 232, 264, xp: 2, kind: BuildingKind.Ship, size: 0.8f, variant: 2),
                new BuildPlan("Balıkçı Evi",    3, 348, 355, xp: 2, kind: BuildingKind.House),
                new BuildPlan("Pansiyon",       4, 377, 452, xp: 3, kind: BuildingKind.Apartment),
                new BuildPlan("Fener",          5, 464, 626, xp: 3, kind: BuildingKind.Lighthouse)),
            new DistrictDef("Merkez", requiredLevel: 3, scale: 4,
                new BuildPlan("Dükkan",         2, 341, 307, xp: 2, kind: BuildingKind.Shop),
                new BuildPlan("Kafe",           3, 364, 458, xp: 2, kind: BuildingKind.Shop, variant: 1),
                new BuildPlan("Postane",        3, 546, 622, xp: 3, kind: BuildingKind.House, variant: 1),
                new BuildPlan("Okul",           3, 818, 835, xp: 3, kind: BuildingKind.School),
                new BuildPlan("Apartman",       5, 709, 1064, xp: 4, kind: BuildingKind.Apartment, variant: 1),
                new BuildPlan("Belediye",       5, 1091, 1473, xp: 4, kind: BuildingKind.TownHall)),
            new DistrictDef("Liman",  requiredLevel: 5, scale: 16,
                new BuildPlan("Depo",           2, 682, 614, xp: 3, kind: BuildingKind.Warehouse),
                new BuildPlan("Lokanta",        3, 727, 917, xp: 4, kind: BuildingKind.Shop, variant: 2),
                new BuildPlan("Römorkör",       3, 1091, 1244, xp: 4, kind: BuildingKind.Ship, size: 0.8f, variant: 2),
                new BuildPlan("Gümrük",         4, 1228, 1670, xp: 5, kind: BuildingKind.School, variant: 1),
                new BuildPlan("Yolcu Gemisi",   5, 1419, 2128, xp: 5, kind: BuildingKind.Ship, variant: 1),
                new BuildPlan("Kargo Gemisi",   6, 1819, 2946, xp: 5, kind: BuildingKind.Ship, size: 1.2f)),
            new DistrictDef("Tepe",   requiredLevel: 7, scale: 64,
                new BuildPlan("Ahır",           2, 1194, 1074, xp: 4, kind: BuildingKind.Warehouse, variant: 1),
                new BuildPlan("Çiftlik Evi",    3, 1273, 1604, xp: 5, kind: BuildingKind.House),
                new BuildPlan("Konak",          4, 1432, 2177, xp: 5, kind: BuildingKind.Apartment, variant: 2),
                new BuildPlan("Değirmen",       4, 2148, 2922, xp: 6, kind: BuildingKind.Windmill),
                new BuildPlan("Su Kulesi",      5, 2482, 3724, xp: 6, kind: BuildingKind.Silo, variant: 1),
                new BuildPlan("Büyük Değirmen", 6, 3183, 5156, xp: 6, kind: BuildingKind.Windmill, size: 1.25f)),
            new DistrictDef("Sanayi", requiredLevel: 9, scale: 256,
                new BuildPlan("Atölye",         2, 1705, 1534, xp: 5, kind: BuildingKind.Warehouse, variant: 2),
                new BuildPlan("İşçi Evleri",    3, 1819, 2292, xp: 5, kind: BuildingKind.Apartment, variant: 2),
                new BuildPlan("Silo",           4, 2046, 3110, xp: 6, kind: BuildingKind.Silo),
                new BuildPlan("Küçük Fabrika",  3, 4092, 4174, xp: 6, kind: BuildingKind.Factory, size: 0.85f),
                new BuildPlan("Baca Kulesi",    6, 2955, 5320, xp: 8, kind: BuildingKind.Silo, variant: 2),
                new BuildPlan("Büyük Fabrika",  6, 4547, 7366, xp: 8, kind: BuildingKind.Factory, size: 1.2f)),
        };

        /// <summary>Total XP needed to reach level i + 1 (so index 0 is level 1 = 0 XP).</summary>
        public static readonly int[] LevelThresholds = { 0, 4, 10, 16, 24, 34, 46, 60, 76, 94 };

        // ---- Repairs: finished buildings sometimes get a problem. No penalty, fixing it pays a bonus.
        public const float RepairInterval = 1500f;       // seconds (25 min) between problems in a district
        public const int MaxIssuesPerDistrict = 3;
        public const int RepairKinds = 4;                // roof leak, broken pipe, bad wiring, cracked wall
        public static int RepairWork(int scale) => 12 * scale;   // a handful of hits even at hammer level 0
        public static int RepairReward(int scale) => 60 * scale;
        public const int RepairXp = 2;

        // ---- Energy: every character (the player and each hired worker) has their own bar. A hit costs energy,
        // standing still refills it, and running dry forces a rest until it has recovered to ResumeFraction.
        public static readonly EnergySettings PlayerEnergy = new EnergySettings(
            max: 100f, cost: 4f, fullRegen: 20f, restDelay: 0.6f, activeRegen: 0f, resumeFraction: 0.4f);
        // A worker hits every ~1.1 s: ~12 hits per full bar, then a ~4 s rest.
        public static readonly EnergySettings WorkerEnergy = new EnergySettings(
            max: 100f, cost: 8f, fullRegen: 12f, restDelay: 1.0f, activeRegen: 0f, resumeFraction: 0.4f);

        // ---- Stamina upgrade: every level makes ALL characters' bars bigger and faster to refill.
        public const int MaxStaminaLevel = 10;
        const float StaminaMaxPerLevel = 0.20f;
        const float StaminaRegenPerLevel = 0.15f;

        public static UpgradeTrack NewStaminaTrack(int scale = 1) => new UpgradeTrack(baseCost: 60L * scale, growth: 1.6, maxLevel: MaxStaminaLevel - 1);

        /// <summary>How many hits one full bar pays for at a stamina level; the unit the player actually feels.</summary>
        public static int HitsPerBar(EnergySettings s, int level) => (int)(WithStamina(s, level).Max / s.Cost);

        public static EnergySettings WithStamina(EnergySettings s, int level) => new EnergySettings(
            s.Max * (1f + StaminaMaxPerLevel * level), s.Cost, s.FullRegen * (1f + StaminaRegenPerLevel * level),
            s.RestDelay, s.ActiveRegen, s.ResumeFraction);

        // ---- Sarı Kola
        // Any cola gives everyone a temporary level bonus (hit power is computed as if the hammer were +5 levels).
        // The bought one is the "Coşku": same bonus for longer, plus faster movement/hitting and free energy for everybody.
        public const int LevelBonus = 5;
        public const float ColaLevelSeconds = 10f;       // found cola
        public const float BoostSeconds = 15f;           // bought cola (Coşku)
        public const float BoostSpeedMultiplier = 1.5f;
        public const int MaxColaInventory = 3;
        public const int MaxColaOnMap = 2;
        public const float ColaFirstSpawn = 8f;
        public const float ColaSpawnMin = 18f;
        public const float ColaSpawnMax = 28f;
        public const float ColaLifetime = 35f;

        // ---- Coins: every point of work earns this many coins (and the same share of a building's completion bonus).
        public const float CoinsPerWork = 0.6f;

        public static long CoinsFor(long work) => Math.Max(1L, (long)Math.Round(work * CoinsPerWork));

        // ---- Coşku price. The bought cola makes everybody work ~3x as fast for 15 s, so it is priced by what a district earns:
        // ColaPriceSeconds seconds of normal income. Measured with tools/balance_sim.py: a player lands ~2.0 hits/s and a helper ~0.8
        // hits/s (energy limits both), and Coşku is worth about 30 s of normal income on top of what it costs.
        public const float ColaPriceSeconds = 40f;
        const double PlayerHitsPerSecond = 2.0;
        const double WorkerHitsPerSecond = 0.8;

        /// <summary>Rough coins per second a district earns with this setup when nobody drinks cola (only used to price Coşku).</summary>
        public static double EstimatedIncomePerSecond(int hammerLevel, int workers, int speedLevel, int scale)
        {
            var player = PlayerHitsPerSecond * HammerPower(hammerLevel);
            var helpers = workers * WorkerHitsPerSecond * WorkerPower(hammerLevel);
            return scale * CoinsPerWork * SpeedMultiplier(speedLevel) * (player + helpers);
        }

        /// <summary>Grows with the hammer, the helpers and the speed upgrade, so Coşku never gets cheap as income grows.</summary>
        public static long ColaPrice(int hammerLevel, int workers, int speedLevel, int scale) =>
            Math.Max(60L * scale, (long)Math.Round(ColaPriceSeconds * EstimatedIncomePerSecond(hammerLevel, workers, speedLevel, scale)));

        // ---- Speed upgrade: a character's whole pace (walking, swinging and recovering energy) gets faster.
        public const int MaxSpeedLevel = 5;
        public const float SpeedPerLevel = 0.15f;

        public static UpgradeTrack NewSpeedTrack(int scale = 1) => new UpgradeTrack(baseCost: 100L * scale, growth: 2.0, maxLevel: MaxSpeedLevel - 1);

        /// <summary>Pace multiplier for a speed level: 1.0 at level 0, +15% per level (1.6 at the last tier, level 4).</summary>
        public static float SpeedMultiplier(int level) => 1f + SpeedPerLevel * level;

        public static UpgradeTrack NewHammerTrack(int scale = 1) => new UpgradeTrack(baseCost: 20L * scale, growth: 1.5, maxLevel: MaxHammerLevel - 1);
        public static UpgradeTrack NewWorkerTrack(int scale = 1) => new UpgradeTrack(baseCost: 90L * scale, growth: 1.75, maxLevel: MaxWorkers - 1);

        /// <summary>Work (= coins) per hammer hit.</summary>
        public static int HammerPower(int hammerLevel) => 1 + hammerLevel;

        /// <summary>Auto-workers hit weaker than the player so clicking stays worthwhile.</summary>
        public static int WorkerPower(int hammerLevel) => 1 + hammerLevel / 2;
    }
}
