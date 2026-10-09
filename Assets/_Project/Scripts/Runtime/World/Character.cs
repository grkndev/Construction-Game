using Game.Core;
using UnityEngine;

namespace Game.World
{
    public enum CharacterKind { Player, Worker }

    /// <summary>
    /// What every person on the island has in common: their own energy, the bar above their head, and the
    /// tired posture. Putting this in one component (instead of in PlayerHammer and AutoWorker separately)
    /// means a new kind of character automatically tires, rests and gets refilled by a Sarı Kola.
    /// </summary>
    public sealed class Character : MonoBehaviour
    {
        [SerializeField] CharacterKind kind;
        [SerializeField] WorkerVisual visual;
        [SerializeField] MeterBar energyBar;

        public CharacterKind Kind => kind;
        public bool IsPlayer => kind == CharacterKind.Player;
        public EnergyMeter Energy { get; private set; }

        void Awake() => Energy = new EnergyMeter(IsPlayer ? Balance.PlayerEnergy : Balance.WorkerEnergy);

        void Update()
        {
            Energy.Tick(Time.deltaTime);
            visual.SetTired(Energy.IsExhausted);
            energyBar.Set(Energy.Fraction, Energy.IsExhausted);
        }

        public void Refill() => Energy.Refill();
    }
}
