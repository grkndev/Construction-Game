using System;

namespace Game.Core
{
    /// <summary>Tuning for one energy bar. Plain data so it can live in <see cref="Balance"/>.</summary>
    public readonly struct EnergySettings
    {
        public readonly float Max;
        public readonly float Cost;            // energy spent per action
        public readonly float FullRegen;       // per second once rested for RestDelay
        public readonly float RestDelay;       // seconds without spending before FullRegen kicks in
        public readonly float ActiveRegen;     // per second while still being used
        public readonly float ResumeFraction;  // once exhausted, how full it must be before it works again

        public EnergySettings(float max, float cost, float fullRegen, float restDelay, float activeRegen, float resumeFraction)
        {
            Max = max;
            Cost = cost;
            FullRegen = fullRegen;
            RestDelay = restDelay;
            ActiveRegen = activeRegen;
            ResumeFraction = resumeFraction;
        }
    }

    /// <summary>
    /// Stamina that drains per action and refills while resting. Running dry locks it until it has recovered
    /// to <see cref="EnergySettings.ResumeFraction"/>: without that gate you'd get one action per regen tick and
    /// holding the button would never need a real pause, which is exactly what the energy system is meant to prevent.
    /// </summary>
    public sealed class EnergyMeter
    {
        EnergySettings settings;
        float sinceSpent;

        public float Value { get; private set; }
        public float Max => settings.Max;
        public float Fraction => Value / settings.Max;
        public bool IsExhausted { get; private set; }
        public bool CanSpend => !IsExhausted && Value >= settings.Cost;

        public EnergyMeter(EnergySettings settings)
        {
            this.settings = settings;
            Value = settings.Max;
            sinceSpent = settings.RestDelay;   // starts rested
        }

        public bool TrySpend()
        {
            if (!CanSpend) return false;
            Value -= settings.Cost;
            sinceSpent = 0f;
            if (Value < settings.Cost) IsExhausted = true;
            return true;
        }

        public void Tick(float deltaTime)
        {
            // Only the part of this step that lies beyond the rest delay counts as resting, so the result
            // doesn't depend on the frame rate.
            var before = sinceSpent;
            sinceSpent += deltaTime;
            var resting = Math.Max(0f, sinceSpent - Math.Max(before, settings.RestDelay));
            var acting = deltaTime - resting;
            Value = Math.Min(settings.Max, Value + settings.FullRegen * resting + settings.ActiveRegen * acting);
            if (IsExhausted && Value >= settings.ResumeFraction * settings.Max) IsExhausted = false;
        }

        /// <summary>
        /// Swap the tuning (stamina upgrades change max and regen). The fill fraction is kept, so buying
        /// an upgrade never doubles as a free refill.
        /// </summary>
        public void Reconfigure(EnergySettings newSettings)
        {
            var fraction = Fraction;
            settings = newSettings;
            Value = fraction * settings.Max;
        }

        public void Refill()
        {
            Value = settings.Max;
            IsExhausted = false;
        }
    }
}
