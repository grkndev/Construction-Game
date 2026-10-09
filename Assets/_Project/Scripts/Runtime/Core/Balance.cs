using System;

namespace Game.Core
{
    /// <summary>All tuning numbers in one place so the game can be balanced without hunting through code.</summary>
    public static class Balance
    {
        public const float HitCooldown = 0.18f;          // seconds between hammer hits (hold = auto repeat)
        public const float WorkerHitInterval = 1.1f;     // seconds between an auto-worker's hits
        public const int MaxWorkers = 8;

        // Tuned with a small simulation that includes the energy bars (greedy upgrade buying, ~5.5 hits/s when
        // allowed, workers spread over the plots, a free cola every ~45 s): ~8 minutes for an attentive player,
        // first building done after ~25 s, ~22 purchases. Real play is slower than the model, so expect 10-12.
        public static readonly BuildPlan[] Plans =
        {
            new BuildPlan("Kulübe",    floors: 2,  workPerFloor: 9,    completionBonus: 22),
            new BuildPlan("Ev",        floors: 3,  workPerFloor: 75,   completionBonus: 90),
            new BuildPlan("Apartman",  floors: 5,  workPerFloor: 210,  completionBonus: 330),
            new BuildPlan("Otel",      floors: 7,  workPerFloor: 510,  completionBonus: 1200),
            new BuildPlan("Gökdelen",  floors: 10, workPerFloor: 1260, completionBonus: 4500),
        };

        // ---- Energy: the player tires when hammering and recovers when still; every plot has its own energy
        // that any hit (player or worker) drains. Spreading effort over several plots is how you keep going.
        public static readonly EnergySettings PlayerEnergy = new EnergySettings(
            max: 100f, cost: 4f, fullRegen: 20f, restDelay: 0.6f, activeRegen: 0f, resumeFraction: 0.4f);
        public static readonly EnergySettings SiteEnergy = new EnergySettings(
            max: 100f, cost: 3f, fullRegen: 15f, restDelay: 1.0f, activeRegen: 3f, resumeFraction: 0.3f);

        // ---- Sarı Kola
        public const float BoostSeconds = 15f;           // "Coşku": bought cola only
        public const int BoostPowerMultiplier = 2;       // player hits deal double and nothing costs energy while boosted
        public const int MaxColaInventory = 3;
        public const int MaxColaOnMap = 2;
        public const float ColaFirstSpawn = 8f;
        public const float ColaSpawnMin = 18f;
        public const float ColaSpawnMax = 28f;
        public const float ColaLifetime = 35f;

        /// <summary>Scales with the hammer so it stays a real decision as income grows (60 at start, ~1100 at level 14).</summary>
        public static long ColaPrice(int hammerLevel) => 60 + (long)Math.Round(35 * Math.Pow(hammerLevel, 1.3));

        public static UpgradeTrack NewHammerTrack() => new UpgradeTrack(baseCost: 20, growth: 1.5, maxLevel: 25);
        public static UpgradeTrack NewWorkerTrack() => new UpgradeTrack(baseCost: 90, growth: 1.75, maxLevel: MaxWorkers);

        /// <summary>Work (= coins) per hammer hit.</summary>
        public static int HammerPower(int hammerLevel) => 1 + hammerLevel;

        /// <summary>Auto-workers hit weaker than the player so clicking stays worthwhile.</summary>
        public static int WorkerPower(int hammerLevel) => 1 + hammerLevel / 2;
    }
}
