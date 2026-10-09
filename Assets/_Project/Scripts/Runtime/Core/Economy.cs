using System;

namespace Game.Core
{
    /// <summary>Coin balance. Plain C# so it can be unit-tested without the engine.</summary>
    public sealed class Economy
    {
        public long Coins { get; private set; }
        public event Action Changed;

        public void Add(long amount)
        {
            if (amount <= 0) return;
            Coins += amount;
            Changed?.Invoke();
        }

        public bool CanAfford(long cost) => Coins >= cost;

        public bool TrySpend(long cost)
        {
            if (cost < 0 || Coins < cost) return false;
            Coins -= cost;
            Changed?.Invoke();
            return true;
        }
    }
}
