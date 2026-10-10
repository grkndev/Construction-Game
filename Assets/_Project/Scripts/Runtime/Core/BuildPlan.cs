namespace Game.Core
{
    /// <summary>Static description of one building: how tall it is and how much work it takes.</summary>
    public sealed class BuildPlan
    {
        public string Name { get; }
        public int Floors { get; }
        public int WorkPerFloor { get; }
        public int CompletionBonus { get; }
        /// <summary>Player XP awarded when the building is finished.</summary>
        public int Xp { get; }
        public BuildingKind Kind { get; }
        /// <summary>Uniform scale of the finished model (1 = the kind's normal size).</summary>
        public float Size { get; }
        /// <summary>Kind-specific look: ship hull colour, awning colour, tower paint and so on.</summary>
        public int Variant { get; }
        public int TotalWork => Floors * WorkPerFloor;

        /// <summary>Floors are the build stages: each one pops in when its share of the work is done (for a factory they are halls and chimneys).</summary>
        public BuildPlan(string name, int floors, int workPerFloor, int completionBonus, int xp = 0,
            BuildingKind kind = BuildingKind.House, float size = 1f, int variant = 0)
        {
            Name = name;
            Floors = floors;
            WorkPerFloor = workPerFloor;
            CompletionBonus = completionBonus;
            Xp = xp;
            Kind = kind;
            Size = size;
            Variant = variant;
        }
    }
}
