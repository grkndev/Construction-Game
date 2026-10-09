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

        public Economy Economy { get; } = new Economy();
        public UpgradeTrack Hammer { get; } = Balance.NewHammerTrack();
        public UpgradeTrack Workers { get; } = Balance.NewWorkerTrack();
        public int HammerPower => Balance.HammerPower(Hammer.Level);
        /// <summary>The plot the player is currently close enough to work on (null when out of reach). Set by PlayerHammer.</summary>
        public BuildSite FocusSite { get; set; }
        public bool AllDone { get; private set; }
        public int CompletedCount { get; private set; }
        public int TotalCount => sites.Length;

        /// <summary>Raised when a plot finishes or everything is done.</summary>
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

        void Start() => hud.Bind(this);

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

        /// <summary>Applies one hit of <paramref name="power"/> work. Work equals coins earned.</summary>
        public bool TryHit(BuildSite site, int power, Vector3 point)
        {
            if (site == null || site.State != SiteState.Active) return false;
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
            SiteChanged?.Invoke();
            if (CompletedCount == sites.Length) StartCoroutine(FinishAfterDelay(1.4f));
        }

        IEnumerator FinishAfterDelay(float seconds)
        {
            yield return new WaitForSeconds(seconds);   // let the last confetti play before the victory text
            AllDone = true;
            SiteChanged?.Invoke();
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
