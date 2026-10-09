using System;

namespace Game.Core
{
    /// <summary>
    /// Player level from accumulated XP. <c>thresholds[i]</c> is the total XP needed to reach level i + 1,
    /// so thresholds[0] is 0 (everybody starts at level 1).
    /// </summary>
    public sealed class LevelTrack
    {
        readonly int[] thresholds;

        public int Xp { get; private set; }
        public int MaxLevel => thresholds.Length;
        public int Level { get; private set; } = 1;
        public bool IsMax => Level >= MaxLevel;

        /// <summary>XP collected inside the current level, and the size of this level (for a progress bar).</summary>
        public int XpIntoLevel => Xp - thresholds[Level - 1];
        public int XpForLevel => IsMax ? 0 : thresholds[Level] - thresholds[Level - 1];
        public float Fraction => IsMax ? 1f : (float)XpIntoLevel / XpForLevel;

        public event Action Changed;
        /// <summary>Raised once per level gained (argument = the new level).</summary>
        public event Action<int> LeveledUp;

        public LevelTrack(int[] thresholds)
        {
            if (thresholds == null || thresholds.Length == 0 || thresholds[0] != 0)
                throw new ArgumentException("thresholds must start with 0", nameof(thresholds));
            this.thresholds = thresholds;
        }

        public void Add(int xp)
        {
            if (xp <= 0) return;
            Xp += xp;
            while (!IsMax && Xp >= thresholds[Level])
            {
                Level++;
                LeveledUp?.Invoke(Level);
            }
            Changed?.Invoke();
        }

        /// <summary>Loads saved XP without level-up events.</summary>
        public void Restore(int xp)
        {
            Xp = Math.Max(0, xp);
            Level = 1;
            while (!IsMax && Xp >= thresholds[Level]) Level++;
            Changed?.Invoke();
        }
    }
}
