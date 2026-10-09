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

        /// <summary>Multiplies walking speed and how fast this character swings (1 normally, higher during Coşku).</summary>
        public float SpeedMultiplier { get; private set; } = 1f;

        EnergySettings BaseSettings => IsPlayer ? Balance.PlayerEnergy : Balance.WorkerEnergy;

        void Awake() => Energy = new EnergyMeter(BaseSettings);

        void Update()
        {
            Energy.Tick(Time.deltaTime);
            visual.SetTired(Energy.IsExhausted);
            energyBar.Set(Energy.Fraction, Energy.IsExhausted);
        }

        public void Refill() => Energy.Refill();

        /// <summary>Stamina upgrade: bigger bar, faster refill; keeps the current fill fraction.</summary>
        public void ApplyStamina(int level) => Energy.Reconfigure(Balance.WithStamina(BaseSettings, level));

        /// <summary>Called by the game when a cola effect starts or ends.</summary>
        public void SetBuffs(bool levelBonus, float speedMultiplier)
        {
            visual.SetBuffed(levelBonus);
            SpeedMultiplier = speedMultiplier;
        }
    }
}
