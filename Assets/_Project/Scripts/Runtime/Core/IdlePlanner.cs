using System;

namespace Game.Core
{
    public enum IdleAction { Pause, Wander }

    public enum IdleEmote { None, Look, Rest, Hop }

    /// <summary>
    /// Decides what a helper with no work does: stand around for a few seconds (looking about, resting on the hammer, hopping), stroll to
    /// somewhere else, stand around again. Plain logic with its own seeded random, so it is deterministic and unit-testable; the caller does the
    /// walking and tells it when it has arrived.
    /// </summary>
    public sealed class IdlePlanner
    {
        public const float MinPause = 2f, MaxPause = 5f, MaxWander = 9f;

        readonly Random random;
        float timer;

        public IdleAction Action { get; private set; }
        public IdleEmote Emote { get; private set; }

        public IdlePlanner(int seed)
        {
            random = new Random(seed);
            StartPause();
        }

        /// <summary>Advances the clock; true when the plan changed (a new pause with an emote, or a new stroll).</summary>
        public bool Tick(float deltaTime)
        {
            timer -= deltaTime;
            if (timer > 0f) return false;
            if (Action == IdleAction.Pause) StartWander();
            else StartPause();   // a stroll that took too long (blocked, far): give up and rest
            return true;
        }

        /// <summary>The caller reached the stroll's destination.</summary>
        public void Arrived()
        {
            if (Action == IdleAction.Wander) StartPause();
        }

        void StartPause()
        {
            Action = IdleAction.Pause;
            timer = MinPause + (float)random.NextDouble() * (MaxPause - MinPause);
            var roll = random.NextDouble();
            Emote = roll < 0.40 ? IdleEmote.Look : roll < 0.70 ? IdleEmote.Rest : roll < 0.85 ? IdleEmote.Hop : IdleEmote.None;
        }

        void StartWander()
        {
            Action = IdleAction.Wander;
            Emote = IdleEmote.None;
            timer = MaxWander;
        }
    }
}
