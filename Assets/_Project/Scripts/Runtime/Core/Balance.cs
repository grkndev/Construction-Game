using System;

namespace Game.Core
{
    /// <summary>All tuning numbers in one place so the game can be balanced without hunting through code.</summary>
    public static class Balance
    {
        public const float HitCooldown = 0.18f;          // seconds between hammer hits (hold = auto repeat)
        public const float WorkerHitInterval = 1.1f;     // seconds between an auto-worker's hits
        public const int MaxWorkers = 8;

        // Tuned with tools/balance_sim.py (energy included, greedy upgrade buying, ~5.5 hits/s when allowed,
        // a free cola every ~45 s): buildings finish at roughly 0.2 / 0.7 / 1.9 / 4.1 / 8.3 minutes for an
        // attentive player. Real play is slower than the model, so expect 10-12 minutes in total.
        public static readonly BuildPlan[] Plans =
        {
            new BuildPlan("Kulübe",    floors: 2,  workPerFloor: 30,   completionBonus: 37),
            new BuildPlan("Ev",        floors: 3,  workPerFloor: 125,  completionBonus: 150),
            new BuildPlan("Apartman",  floors: 5,  workPerFloor: 350,  completionBonus: 550),
            new BuildPlan("Otel",      floors: 7,  workPerFloor: 850,  completionBonus: 2000),
            new BuildPlan("Gökdelen",  floors: 10, workPerFloor: 2100, completionBonus: 7500),
        };

        // ---- Energy: every character (the player and each hired worker) has their own bar. A hit costs energy,
        // standing still refills it, and running dry forces a rest until it has recovered to ResumeFraction.
        public static readonly EnergySettings PlayerEnergy = new EnergySettings(
            max: 100f, cost: 4f, fullRegen: 20f, restDelay: 0.6f, activeRegen: 0f, resumeFraction: 0.4f);
        // A worker hits every ~1.1 s: ~12 hits per full bar, then a ~4 s rest.
        public static readonly EnergySettings WorkerEnergy = new EnergySettings(
            max: 100f, cost: 8f, fullRegen: 12f, restDelay: 1.0f, activeRegen: 0f, resumeFraction: 0.4f);

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
