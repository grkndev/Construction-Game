using Game.Core;
using UnityEngine;

namespace Game.World
{
    public enum CharacterKind { Player, Worker }

    /// <summary>
    /// What every person on the island has in common: their own energy, the bar above their head, the tired
    /// posture, and the temporary effects of a Sarı Kola. Putting this in one component (instead of in
    /// PlayerHammer and AutoWorker separately) means a new kind of character automatically tires, rests,
    /// gets stamina upgrades and is affected by cola.
    /// </summary>
    public sealed class Character : MonoBehaviour
    {
        [SerializeField] CharacterKind kind;
        [SerializeField] WorkerVisual visual;
        [SerializeField] MeterBar energyBar;

        public CharacterKind Kind => kind;
        public bool IsPlayer => kind == CharacterKind.Player;
        public EnergyMeter Energy { get; private set; }

        /// <summary>Multiplies walking speed, how fast this character swings and how fast energy comes back (1 normally; the Hız upgrade and Coşku raise it).</summary>
        public float SpeedMultiplier { get; private set; } = 1f;

        EnergySettings BaseSettings => IsPlayer ? Balance.PlayerEnergy : Balance.WorkerEnergy;

        void Awake() => Energy = new EnergyMeter(BaseSettings);

        void Update()
        {
            Energy.Tick(Time.deltaTime * SpeedMultiplier);
            visual.SetTired(Energy.IsExhausted);
            energyBar.Set(Energy.Fraction, Energy.IsExhausted);
        }

        public void Refill() => Energy.Refill();

        /// <summary>Stamina upgrade: bigger bar, faster refill; keeps the current fill fraction.</summary>
        public void ApplyStamina(int level)
        {
            var settings = Balance.WithStamina(BaseSettings, level);
            Energy.Reconfigure(settings);
            energyBar.SetWidthScale(1f + (settings.Max / BaseSettings.Max - 1f) * 0.6f);   // half-way: visible, but not huge
        }

        /// <summary>Hammer upgrade: the hammer head grows and changes material.</summary>
        public void ApplyHammerTier(int tier) => visual.SetHammerTier(tier);

        /// <summary>Called by the game when a cola effect starts or ends.</summary>
        public void SetBuffs(bool levelBonus, float speedMultiplier)
        {
            visual.SetBuffed(levelBonus);
            SpeedMultiplier = speedMultiplier;
        }
    }
}
