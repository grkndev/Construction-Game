using System;
using System.Collections;
using Game.UI;
using Game.World;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// Scene-level composition root: owns the economy and upgrades (plain C# objects), wires plots to
    /// the plans in <see cref="Balance"/>, hires workers and tells the HUD what to show.
    /// Everything it references is assigned in the scene by the editor builder, so there are no singletons.
    /// </summary>
    public sealed class GameController : MonoBehaviour
    {
        [SerializeField] BuildSite[] sites;
        [SerializeField] AutoWorker workerPrefab;
        [SerializeField] Transform workerSpawn;
        [SerializeField] FloatingText floatingTextPrefab;
        [SerializeField] GameHud hud;
        [SerializeField] Color coinColor = new Color(1f, 0.85f, 0.2f);
        [SerializeField] Color bonusColor = new Color(0.45f, 1f, 0.55f);

        int activeIndex = -1;
        int workersSpawned;

        public Economy Economy { get; } = new Economy();
        public UpgradeTrack Hammer { get; } = Balance.NewHammerTrack();
        public UpgradeTrack Workers { get; } = Balance.NewWorkerTrack();
        public int HammerPower => Balance.HammerPower(Hammer.Level);
        public BuildSite ActiveSite => activeIndex >= 0 && activeIndex < sites.Length ? sites[activeIndex] : null;
        public bool AllDone { get; private set; }
        public int CompletedCount { get; private set; }
        public int TotalCount => sites.Length;

        /// <summary>Raised when the active plot changes or everything is finished.</summary>
        public event Action SiteChanged;
        public event Action<string> Hint;

        void Awake()
        {
            for (var i = 0; i < sites.Length; i++)
            {
                sites[i].Initialize(Balance.Plans[i], i, sites[i].GetComponentInChildren<TMPro.TMP_Text>(true));
                sites[i].Completed += OnSiteCompleted;
            }
        }

        void Start()
        {
            hud.Bind(this);
            Activate(0);
        }

        void Activate(int index)
        {
            activeIndex = index;
            for (var i = 0; i < sites.Length; i++)
            {
                if (sites[i].IsComplete) continue;
                sites[i].SetState(i == index ? SiteState.Active : SiteState.Locked);
            }
            SiteChanged?.Invoke();
        }

        /// <summary>Applies one hit of <paramref name="power"/> work. Work equals coins earned.</summary>
        public bool TryHit(BuildSite site, int power, Vector3 point)
        {
            if (site != ActiveSite) return false;
            var applied = site.AddWork(power, point);
            if (applied == 0) return false;

            Economy.Add(applied);
            Popup($"+{applied}", point, coinColor, 1f);
            return true;
        }

        void OnSiteCompleted(BuildSite site)
        {
            CompletedCount++;
            Economy.Add(site.Plan.CompletionBonus);
            Popup($"{site.Plan.Name} hazır! +{NumberFormat.Compact(site.Plan.CompletionBonus)}",
                site.transform.position + Vector3.up * 4.5f, bonusColor, 1.7f);
            StartCoroutine(AdvanceAfterDelay(1.4f));
        }

        IEnumerator AdvanceAfterDelay(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            var next = Array.FindIndex(sites, s => !s.IsComplete);
            if (next < 0)
            {
                AllDone = true;
                activeIndex = -1;
                SiteChanged?.Invoke();
                yield break;
            }
            Activate(next);
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
            worker.Initialize(this, workersSpawned++);
            ShowHint("Yeni işçi işe başladı!");
        }

        public void ShowHint(string text) => Hint?.Invoke(text);

        void Popup(string text, Vector3 position, Color color, float scale)
        {
            var popup = Instantiate(floatingTextPrefab, position + Vector3.up * 0.4f, Quaternion.identity);
            popup.Show(text, color, scale);
        }
    }
}
