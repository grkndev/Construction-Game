namespace Game.Core
{
    /// <summary>All tuning numbers in one place so the game can be balanced without hunting through code.</summary>
    public static class Balance
    {
        public const float HitCooldown = 0.18f;          // seconds between hammer hits (hold = auto repeat)
        public const float WorkerHitInterval = 1.1f;     // seconds between an auto-worker's hits
        public const int MaxWorkers = 8;

        public static readonly BuildPlan[] Plans =
        {
            new BuildPlan("Kulübe",    floors: 2,  workPerFloor: 6,   completionBonus: 15),
            new BuildPlan("Ev",        floors: 3,  workPerFloor: 25,  completionBonus: 60),
            new BuildPlan("Apartman",  floors: 5,  workPerFloor: 70,  completionBonus: 220),
            new BuildPlan("Otel",      floors: 7,  workPerFloor: 170, completionBonus: 800),
            new BuildPlan("Gökdelen",  floors: 10, workPerFloor: 420, completionBonus: 3000),
        };

        public static UpgradeTrack NewHammerTrack() => new UpgradeTrack(baseCost: 10, growth: 1.55, maxLevel: 25);
        public static UpgradeTrack NewWorkerTrack() => new UpgradeTrack(baseCost: 40, growth: 1.7, maxLevel: MaxWorkers);

        /// <summary>Work (= coins) per hammer hit.</summary>
        public static int HammerPower(int hammerLevel) => 1 + hammerLevel;

        /// <summary>Auto-workers hit weaker than the player so clicking stays worthwhile.</summary>
        public static int WorkerPower(int hammerLevel) => 1 + hammerLevel / 2;
    }
}
