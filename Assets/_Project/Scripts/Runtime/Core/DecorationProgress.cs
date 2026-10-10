using System;

namespace Game.Core
{
    /// <summary>
    /// The beautifying of one district: decorations are ordered one at a time (paid from the district's coins), built by idle helpers, and the
    /// last one earns the district its golden star. Only a finished district can be beautified. Plain logic (saved with the district).
    /// </summary>
    public sealed class DecorationProgress
    {
        public int Built { get; private set; }
        /// <summary>A decoration has been paid for and is being built (it is number <see cref="Built"/>).</summary>
        public bool Ordered { get; private set; }
        /// <summary>How far the decoration under construction is, 0..1.</summary>
        public float Progress { get; private set; }

        public bool IsComplete => Built >= Balance.DecorationCount;
        public int NextIndex => Built;

        public long NextCost(int scale) => IsComplete ? 0L : Balance.DecorationCost(Built, scale);

        public bool CanOrder(bool districtComplete, Economy economy, int scale) =>
            districtComplete && !IsComplete && !Ordered && economy.CanAfford(NextCost(scale));

        /// <summary>Pays for the next decoration and starts building it.</summary>
        public bool TryOrder(bool districtComplete, Economy economy, int scale)
        {
            if (!CanOrder(districtComplete, economy, scale) || !economy.TrySpend(NextCost(scale))) return false;
            Ordered = true;
            Progress = 0f;
            return true;
        }

        /// <summary>Adds <paramref name="seconds"/> of one helper's work; true when that finishes the decoration.</summary>
        public bool AddWork(float seconds)
        {
            if (!Ordered || seconds <= 0f) return false;
            Progress = Math.Min(1f, Progress + seconds / Balance.DecorationBuildSeconds);
            if (Progress < 1f) return false;
            Built++;
            Ordered = false;
            Progress = 0f;
            return true;
        }

        /// <summary>Restores a saved state (clamped; an order that no longer fits is dropped).</summary>
        public void Restore(int built, bool ordered, float progress)
        {
            Built = Math.Max(0, Math.Min(Balance.DecorationCount, built));
            Ordered = ordered && Built < Balance.DecorationCount;
            Progress = Ordered ? Math.Max(0f, Math.Min(0.999f, progress)) : 0f;
        }
    }
}
