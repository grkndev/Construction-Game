using System;

namespace Game.Core
{
    /// <summary>
    /// A repeatable upgrade whose price grows geometrically: cost(level) = baseCost * growth^level.
    /// Geometric growth is the usual idle-game curve: early levels are cheap and fast, later ones slow down.
    /// </summary>
    public sealed class UpgradeTrack
    {
        readonly long baseCost;
        readonly double growth;
        readonly int maxLevel;

        public int Level { get; private set; }
        public bool IsMaxed => Level >= maxLevel;
        public long NextCost => (long)Math.Round(baseCost * Math.Pow(growth, Level));
        public event Action Changed;

        public UpgradeTrack(long baseCost, double growth, int maxLevel = int.MaxValue)
        {
            this.baseCost = baseCost;
            this.growth = growth;
            this.maxLevel = maxLevel;
        }

        public bool TryBuy(Economy economy)
        {
            if (IsMaxed || !economy.TrySpend(NextCost)) return false;
            Level++;
            Changed?.Invoke();
            return true;
        }
    }
}
