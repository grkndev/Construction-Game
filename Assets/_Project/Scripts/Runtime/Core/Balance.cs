using System;

namespace Game.Core
{
    /// <summary>All tuning numbers in one place so the game can be balanced without hunting through code.</summary>
    public static class Balance
    {
        public const float HitCooldown = 0.18f;          // seconds between the player's automatic hammer hits
        public const float WorkerHitInterval = 1.1f;     // seconds between an auto-worker's hits
        public const int MaxWorkers = 8;

        // ---- Districts: five areas of three buildings. Each one plays like the first at a bigger scale (DistrictDef.Scale: x4 per
        // district for prices, hit power, work and rewards) with its own upgrades, and later districts also have bigger buildings
        // (the numbers below are first-district shapes times a per-district size: x1, 2.6, 5.2, 9, 13), so they take longer.
        // Tuned with tools/balance_sim.py (attentive player, all upgrades incl. Hız): about 2.3 / 3.1 / 4.5 / 6.8 / 7.7 minutes per
        // district, 24 in all (34 without the speed upgrade); real play is ~25% slower and has walking on top.
        public static readonly DistrictDef[] Districts =
        {
            new DistrictDef("Sahil",  requiredLevel: 1, scale: 1,
                new BuildPlan("Kulübe",         3, 90,   120,   xp: 1),
                new BuildPlan("Balıkçı Evi",    4, 300,  420,   xp: 2),
                new BuildPlan("Pansiyon",       6, 800,  1300,  xp: 3)),
            new DistrictDef("Merkez", requiredLevel: 3, scale: 4,
                new BuildPlan("Dükkan",         3, 234,  312,   xp: 2),
                new BuildPlan("Kafe",           4, 780,  1092,  xp: 3),
                new BuildPlan("Belediye",       6, 2080, 3380,  xp: 4)),
            new DistrictDef("Liman",  requiredLevel: 5, scale: 16,
                new BuildPlan("Depo",           3, 468,  624,   xp: 3),
                new BuildPlan("Lokanta",        4, 1560, 2184,  xp: 4),
                new BuildPlan("Gümrük",         6, 4160, 6760,  xp: 6)),
            new DistrictDef("Tepe",   requiredLevel: 7, scale: 64,
                new BuildPlan("Değirmen",       3, 810,  1080,  xp: 4),
                new BuildPlan("Çiftlik Evi",    4, 2700, 3780,  xp: 5),
                new BuildPlan("Fener",          6, 7200, 11700, xp: 7)),
            new DistrictDef("Sanayi", requiredLevel: 9, scale: 256,
                new BuildPlan("Atölye",         3, 1170, 1560,  xp: 5),
                new BuildPlan("Fabrika",        4, 3900, 5460,  xp: 6),
                new BuildPlan("Baca Kulesi",    6, 10400, 16900, xp: 8)),
        };

        /// <summary>Total XP needed to reach level i + 1 (so index 0 is level 1 = 0 XP).</summary>
        public static readonly int[] LevelThresholds = { 0, 2, 5, 8, 12, 17, 23, 30, 38, 47 };

        // ---- Repairs: finished buildings sometimes get a problem. No penalty, fixing it pays a bonus.
        public const float RepairInterval = 1500f;       // seconds (25 min) between problems in a district
        public const int MaxIssuesPerDistrict = 3;
        public const int RepairKinds = 4;                // roof leak, broken pipe, bad wiring, cracked wall
        public static int RepairWork(int scale) => 12 * scale;   // a handful of hits even at hammer level 0
        public static int RepairReward(int scale) => 60 * scale;
        public const int RepairXp = 1;

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

        public static UpgradeTrack NewStaminaTrack(int scale = 1) => new UpgradeTrack(baseCost: 60L * scale, growth: 1.6, maxLevel: MaxStaminaLevel);

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

        /// <summary>Scales with the hammer so it stays a real decision as income grows (60 at start, ~1100 at level 14).</summary>
        public static long ColaPrice(int hammerLevel) => 60 + (long)Math.Round(35 * Math.Pow(hammerLevel, 1.3));

        // ---- Speed upgrade: a character's whole pace (walking, swinging and recovering energy) gets faster.
        public const int MaxSpeedLevel = 5;
        public const float SpeedPerLevel = 0.15f;

        public static UpgradeTrack NewSpeedTrack(int scale = 1) => new UpgradeTrack(baseCost: 100L * scale, growth: 2.0, maxLevel: MaxSpeedLevel);

        /// <summary>Pace multiplier for a speed level: 1.0 at level 0, +15% per level (1.75 at the maximum).</summary>
        public static float SpeedMultiplier(int level) => 1f + SpeedPerLevel * level;

        public static UpgradeTrack NewHammerTrack(int scale = 1) => new UpgradeTrack(baseCost: 20L * scale, growth: 1.5, maxLevel: 25);
        public static UpgradeTrack NewWorkerTrack(int scale = 1) => new UpgradeTrack(baseCost: 90L * scale, growth: 1.75, maxLevel: MaxWorkers);

        /// <summary>Work (= coins) per hammer hit.</summary>
        public static int HammerPower(int hammerLevel) => 1 + hammerLevel;

        /// <summary>Auto-workers hit weaker than the player so clicking stays worthwhile.</summary>
        public static int WorkerPower(int hammerLevel) => 1 + hammerLevel / 2;
    }
}
