using System;

namespace Game.Core
{
    /// <summary>Countdown for a temporary power-up (the "Coşku" boost from a bought Sarı Kola).</summary>
    public sealed class BoostTimer
    {
        public float Remaining { get; private set; }
        public bool IsActive => Remaining > 0f;
        public event Action Changed;

        public void Activate(float seconds)
        {
            Remaining = Math.Max(Remaining, seconds);   // drinking another one refreshes, it never shortens
            Changed?.Invoke();
        }

        public void Tick(float deltaTime)
        {
            if (!IsActive) return;
            Remaining = Math.Max(0f, Remaining - deltaTime);
            if (!IsActive) Changed?.Invoke();
        }
    }
}
