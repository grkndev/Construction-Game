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
        public int TotalWork => Floors * WorkPerFloor;

        public BuildPlan(string name, int floors, int workPerFloor, int completionBonus, int xp = 0)
        {
            Name = name;
            Floors = floors;
            WorkPerFloor = workPerFloor;
            CompletionBonus = completionBonus;
            Xp = xp;
        }
    }
}
