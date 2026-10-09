namespace Game.Core
{
    /// <summary>All tuning numbers in one place so the game can be balanced without hunting through code.</summary>
    public static class Balance
    {
        public const float HitCooldown = 0.18f;          // seconds between hammer hits (hold = auto repeat)
        public const float WorkerHitInterval = 1.1f;     // seconds between an auto-worker's hits
        public const int MaxWorkers = 8;

        // Tuned with a small simulation (greedy upgrade buying, ~4 hits/s, workers spread over the plots):
        // a full playthrough takes ~15 minutes with an attentive player, ~25 purchases, one every ~35 s.
        // Work per floor is large on purpose; the HUD shows compact numbers (1.2K).
        public static readonly BuildPlan[] Plans =
        {
            new BuildPlan("Kulübe",    floors: 2,  workPerFloor: 96,   completionBonus: 120),
            new BuildPlan("Ev",        floors: 3,  workPerFloor: 400,  completionBonus: 480),
            new BuildPlan("Apartman",  floors: 5,  workPerFloor: 1120, completionBonus: 1760),
            new BuildPlan("Otel",      floors: 7,  workPerFloor: 2720, completionBonus: 6400),
            new BuildPlan("Gökdelen",  floors: 10, workPerFloor: 6720, completionBonus: 24000),
        };

        public static UpgradeTrack NewHammerTrack() => new UpgradeTrack(baseCost: 25, growth: 1.55, maxLevel: 25);
        public static UpgradeTrack NewWorkerTrack() => new UpgradeTrack(baseCost: 120, growth: 1.8, maxLevel: MaxWorkers);

        /// <summary>Work (= coins) per hammer hit.</summary>
        public static int HammerPower(int hammerLevel) => 1 + hammerLevel;

        /// <summary>Auto-workers hit weaker than the player so clicking stays worthwhile.</summary>
        public static int WorkerPower(int hammerLevel) => 1 + hammerLevel / 2;
    }
}
