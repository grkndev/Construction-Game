using System;
using System.Collections;
using System.Collections.Generic;
using Game.UI;
using Game.World;
using UnityEngine;

namespace Game.Core
{
    public enum HitResult { Hit, NoTarget, Tired }

    /// <summary>
    /// Scene-level composition root: owns the economy, upgrades, energy and boost state (plain C# objects),
    /// wires plots to the plans in <see cref="Balance"/>, hires workers and tells the HUD what to show.
    /// Everything it references is assigned in the scene by the editor builder, so there are no singletons.
    /// </summary>
    public sealed class GameController : MonoBehaviour
    {
        [SerializeField] BuildSite[] sites;
        [SerializeField] AutoWorker workerPrefab;
        [SerializeField] Transform workerSpawn;
        [SerializeField] FloatingText floatingTextPrefab;
        [SerializeField] GameHud hud;
        [SerializeField] Character player;
        [SerializeField] Color coinColor = new Color(1f, 0.85f, 0.2f);
        [SerializeField] Color bonusColor = new Color(0.45f, 1f, 0.55f);
        [SerializeField] Color colaColor = new Color(1f, 0.93f, 0.35f);

        public Economy Economy { get; } = new Economy();
        public UpgradeTrack Hammer { get; } = Balance.NewHammerTrack();
        public UpgradeTrack Workers { get; } = Balance.NewWorkerTrack();
        public UpgradeTrack Stamina { get; } = Balance.NewStaminaTrack();
        public EnergyMeter PlayerEnergy => player.Energy;
        /// <summary>Coşku: speed-up and free energy for everyone (bought cola only).</summary>
        public BoostTimer Boost { get; } = new BoostTimer();
        /// <summary>Temporary level bonus from any cola.</summary>
        public BoostTimer LevelBuff { get; } = new BoostTimer();
        public int ColaCount { get; private set; }
        public long ColaPrice => Balance.ColaPrice(Hammer.Level);
        /// <summary>Hammer level including the temporary cola bonus; this is what hits actually use.</summary>
        public int EffectiveLevel => Hammer.Level + (LevelBuff.IsActive ? Balance.LevelBonus : 0);
        public int HammerPower => Balance.HammerPower(EffectiveLevel);
        public int BaseHammerPower => Balance.HammerPower(Hammer.Level);
        public IReadOnlyList<BuildSite> Sites => sites;

        /// <summary>The plot the player is currently close enough to work on (null when out of reach). Set by PlayerHammer.</summary>
        public BuildSite FocusSite { get; set; }
        public bool AllDone { get; private set; }
        public int CompletedCount { get; private set; }
        public int TotalCount => sites.Length;

        /// <summary>Raised when something the HUD shows changes outside the economy: cola count, boost, plot finished.</summary>
        public event Action StateChanged;
        public event Action<string> Hint;

        int workersSpawned;

        void Awake()
        {
            for (var i = 0; i < sites.Length; i++)
            {
                sites[i].Initialize(Balance.Plans[i], i, sites[i].GetComponentInChildren<TMPro.TMP_Text>(true));
                sites[i].Completed += OnSiteCompleted;
            }
            Boost.Changed += OnBuffsChanged;
            LevelBuff.Changed += OnBuffsChanged;
        }

        void Start() => hud.Bind(this);

        void Update()
        {
            Boost.Tick(Time.deltaTime);
            LevelBuff.Tick(Time.deltaTime);
        }

        /// <summary>Nearest unfinished plot within <paramref name="radius"/> (flat distance), or null.</summary>
        public BuildSite NearestWorkableSite(Vector3 position, float radius)
        {
            BuildSite best = null;
            var bestDistance = radius;
            foreach (var site in sites)
            {
                if (site.State != SiteState.Active) continue;
                var d = Vector3.ProjectOnPlane(site.transform.position - position, Vector3.up).magnitude;
                if (d > bestDistance) continue;
                best = site;
                bestDistance = d;
            }
            return best;
        }

        /// <summary>Where a free auto-worker should go: the unfinished plot with the fewest helpers, nearest on ties.</summary>
        public BuildSite PickSiteForWorker(Vector3 from)
        {
            BuildSite best = null;
            foreach (var site in sites)
            {
                if (site.State != SiteState.Active) continue;
                if (best == null || site.AssignedWorkers < best.AssignedWorkers ||
                    (site.AssignedWorkers == best.AssignedWorkers &&
                     (site.transform.position - from).sqrMagnitude < (best.transform.position - from).sqrMagnitude))
                    best = site;
            }
            return best;
        }

        /// <summary>
        /// One hammer hit by <paramref name="actor"/>. It costs the actor's own energy; a boost (bought cola)
        /// makes hits free for everyone. Work equals coins earned.
        /// </summary>
        public HitResult TryHit(BuildSite site, int power, Vector3 point, Character actor)
        {
            if (site == null || site.State != SiteState.Active) return HitResult.NoTarget;

            var free = Boost.IsActive;
            if (!free && !actor.Energy.CanSpend) return HitResult.Tired;

            var applied = site.AddWork(power, point);
            if (applied == 0) return HitResult.NoTarget;

            if (!free) actor.Energy.TrySpend();
            Economy.Add(applied);
            Popup($"+{applied}", point, coinColor, 1f);
            return HitResult.Hit;
        }

        void OnSiteCompleted(BuildSite site)
        {
            CompletedCount++;
            Economy.Add(site.Plan.CompletionBonus);
            Popup($"{site.Plan.Name} hazır! +{NumberFormat.Compact(site.Plan.CompletionBonus)}",
                site.transform.position + Vector3.up * 4.5f, bonusColor, 1.7f);
            StateChanged?.Invoke();
            if (CompletedCount == sites.Length) StartCoroutine(FinishAfterDelay(1.4f));
        }

        IEnumerator FinishAfterDelay(float seconds)
        {
            yield return new WaitForSeconds(seconds);   // let the last confetti play before the victory text
            AllDone = true;
            StateChanged?.Invoke();
        }

        public void BuyHammer()
        {
            if (Hammer.TryBuy(Economy)) ShowHint($"Çekiç güçlendi! Vuruş: {HammerPower}");
        }

        public void BuyWorker()
        {
            if (!Workers.TryBuy(Economy)) return;
            var spawn = workerSpawn != null ? workerSpawn.position : Vector3.zero;
            var worker = Instantiate(workerPrefab, spawn, Quaternion.identity);
            worker.Initialize(this);
            ApplyEffectsTo(worker.GetComponent<Character>());
            workersSpawned++;
            ShowHint("Yeni işçi işe başladı!");
        }

        public void BuyStamina()
        {
            if (!Stamina.TryBuy(Economy)) return;
            foreach (var character in FindObjectsByType<Character>(FindObjectsSortMode.None)) character.ApplyStamina(Stamina.Level);
            var hits = Balance.HitsPerBar(Balance.PlayerEnergy, Stamina.Level);
            ShowHint($"Dayanıklılık arttı! Bir dolu enerji artık {hits} vuruş, herkes daha çabuk toparlanıyor.");
            Popup($"DAYANIKLILIK Sv.{Stamina.Level}!", player.transform.position + Vector3.up * 3.6f, new Color(0.5f, 1f, 0.6f), 1.5f);
        }

        // ---------- Sarı Kola ----------

        /// <summary>Picked up from the island. False when the pockets are full.</summary>
        public bool TryAddCola()
        {
            if (ColaCount >= Balance.MaxColaInventory)
            {
                ShowHint($"Cebin dolu ({ColaCount}/{Balance.MaxColaInventory}), önce bir kola iç (E)");
                return false;
            }
            ColaCount++;
            ShowHint($"Sarı Kola alındı! ({ColaCount}/{Balance.MaxColaInventory})  İçmek için E");
            StateChanged?.Invoke();
            return true;
        }

        /// <summary>Drink a found cola: refills every character's energy (player and workers), no boost.</summary>
        public void UseCola()
        {
            if (ColaCount <= 0) return;
            ColaCount--;
            RefillEnergy();
            LevelBuff.Activate(Balance.ColaLevelSeconds);
            ShowHint($"Sarı Kola! Herkesin enerjisi doldu, {Balance.ColaLevelSeconds:0} sn boyunca seviye +{Balance.LevelBonus}.");
            StateChanged?.Invoke();
        }

        /// <summary>Buy and drink immediately: refills every character and also starts the "Coşku" boost.</summary>
        public void BuyCola()
        {
            if (!Economy.TrySpend(ColaPrice)) return;
            RefillEnergy();
            LevelBuff.Activate(Balance.BoostSeconds);
            Boost.Activate(Balance.BoostSeconds);
            ShowHint($"COŞKU! {Balance.BoostSeconds:0} saniye: seviye +{Balance.LevelBonus}, herkes hızlı ve yorulmuyor!");
            if (FocusSite != null) Popup("COŞKU!", FocusSite.transform.position + Vector3.up * 5f, colaColor, 1.8f);
            StateChanged?.Invoke();
        }

        // Pushes the current cola effects to every character; also called when a new worker is hired.
        void OnBuffsChanged()
        {
            foreach (var character in FindObjectsByType<Character>(FindObjectsSortMode.None)) ApplyEffectsTo(character);
            StateChanged?.Invoke();
        }

        void ApplyEffectsTo(Character character)
        {
            character.ApplyStamina(Stamina.Level);
            character.SetBuffs(LevelBuff.IsActive, Boost.IsActive ? Balance.BoostSpeedMultiplier : 1f);
        }

        void RefillEnergy()
        {
            // Found at call time on purpose: workers are hired during play and no registry is needed.
            foreach (var character in FindObjectsByType<Character>(FindObjectsSortMode.None)) character.Refill();
        }

        public void ShowHint(string text) => Hint?.Invoke(text);

        void Popup(string text, Vector3 position, Color color, float scale)
        {
            var popup = Instantiate(floatingTextPrefab, position + Vector3.up * 0.4f, Quaternion.identity);
            popup.Show(text, color, scale);
        }
    }
}
