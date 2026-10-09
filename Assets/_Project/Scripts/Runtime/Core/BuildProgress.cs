using System;

namespace Game.Core
{
    /// <summary>Tracks work done on a <see cref="BuildPlan"/> and raises events as floors appear.</summary>
    public sealed class BuildProgress
    {
        readonly BuildPlan plan;

        public int Work { get; private set; }
        public int FloorsBuilt => Math.Min(plan.Floors, Work / plan.WorkPerFloor);
        public bool IsComplete => Work >= plan.TotalWork;
        public float Fraction => plan.TotalWork == 0 ? 1f : (float)Work / plan.TotalWork;

        /// <summary>Raised once per newly finished floor (argument = 1-based floor number).</summary>
        public event Action<int> FloorBuilt;
        public event Action Completed;

        public BuildProgress(BuildPlan plan) => this.plan = plan;

        /// <summary>Restores saved work without raising any events (floors/completion already happened in an earlier session).</summary>
        public void Restore(int work) => Work = Math.Max(0, Math.Min(work, plan.TotalWork));

        /// <summary>Adds work, clamped to what is left. Returns how much was actually applied.</summary>
        public int Add(int work)
        {
            if (work <= 0 || IsComplete) return 0;

            var before = FloorsBuilt;
            var applied = Math.Min(work, plan.TotalWork - Work);
            Work += applied;

            for (var floor = before + 1; floor <= FloorsBuilt; floor++)
                FloorBuilt?.Invoke(floor);
            if (IsComplete) Completed?.Invoke();
            return applied;
        }
    }
}
