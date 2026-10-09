using System;
using System.Collections;
using System.Collections.Generic;
using Game.Player;
using Game.UI;
using Game.World;
using UnityEngine;

namespace Game.Core
{
    public enum HitResult { Hit, NoTarget, Tired }
    public enum GameMode { Dashboard, InDistrict }

    /// <summary>
    /// Scene-level composition root: owns the shared state (coins, level, cola, save file) and the per-district state
    /// (upgrades, workers, build progress), switches between the dashboard and a district visit, hires workers and tells
    /// the HUDs what to show. Everything it references is assigned in the scene by the editor builder, so there are no singletons.
    /// </summary>
    public sealed class GameController : MonoBehaviour
    {
        const float AutosaveSeconds = 30f;

        [SerializeField] District[] districts;
        [SerializeField] AutoWorker workerPrefab;
        [SerializeField] FloatingText floatingTextPrefab;
        [SerializeField] GameHud hud;
        [SerializeField] DashboardHud dashboard;
        [SerializeField] Character player;
        [SerializeField] IsoCameraFollow cameraRig;
        [SerializeField] Transform overviewTarget;
        [SerializeField] ToonPalette palette;
        [SerializeField] float overviewScale = 6.3f;
        [SerializeField] Color coinColor = new Color(1f, 0.85f, 0.2f);
        [SerializeField] Color bonusColor = new Color(0.45f, 1f, 0.55f);
        [SerializeField] Color colaColor = new Color(1f, 0.93f, 0.35f);

        // ---- shared across the whole game
        public Economy Economy { get; } = new Economy();
        public LevelTrack Level { get; } = new LevelTrack(Balance.LevelThresholds);
        public int ColaCount { get; private set; }
        /// <summary>Coşku: speed-up and free energy for everyone (bought cola only).</summary>
        public BoostTimer Boost { get; } = new BoostTimer();
        /// <summary>Temporary level bonus from any cola.</summary>
        public BoostTimer LevelBuff { get; } = new BoostTimer();
        public EnergyMeter PlayerEnergy => player.Energy;
        public bool CityComplete { get; private set; }

        // ---- where the player is
        public GameMode Mode { get; private set; } = GameMode.Dashboard;
        public bool InDistrict => Mode == GameMode.InDistrict;
        public int CurrentIndex { get; private set; } = -1;
        public IReadOnlyList<District> Districts => districts;
        public District CurrentDistrict => InDistrict ? districts[CurrentIndex] : null;
        public DistrictState CurrentState => InDistrict ? states[CurrentIndex] : null;
        public DistrictState StateOf(int index) => states[index];
        public bool IsUnlocked(int index) => Level.Level >= Balance.Districts[index].RequiredLevel;

        // ---- the current district's upgrades (only meaningful while InDistrict)
        public UpgradeTrack Hammer => CurrentState.Hammer;
        public UpgradeTrack Workers => CurrentState.Workers;
        public UpgradeTrack Stamina => CurrentState.Stamina;
        int Scale => CurrentState?.Def.Scale ?? 1;
        public long ColaPrice => Balance.ColaPrice(Hammer.Level) * Scale;
        /// <summary>Hammer level including the temporary cola bonus; this is what hits actually use.</summary>
        public int EffectiveLevel => Hammer.Level + (LevelBuff.IsActive ? Balance.LevelBonus : 0);
        public int HammerPower => Balance.HammerPower(EffectiveLevel) * Scale;
        public int BaseHammerPower => Balance.HammerPower(Hammer.Level) * Scale;
        public int WorkerPower => Balance.WorkerPower(EffectiveLevel) * Scale;
        public IReadOnlyList<BuildSite> Sites => CurrentDistrict != null ? CurrentDistrict.Sites : Array.Empty<BuildSite>();

        /// <summary>The plot the player is currently close enough to work on (null when out of reach). Set by PlayerHammer.</summary>
        public BuildSite FocusSite { get; set; }
        public int CompletedCount => CurrentState?.CompletedCount ?? 0;
        public int TotalCount => CurrentState?.PlotCount ?? 0;

        /// <summary>Raised when something the HUD shows changes outside the economy: cola count, boost, plot finished, upgrades.</summary>
        public event Action StateChanged;
        public event Action ModeChanged;
        public event Action<string> Hint;
        /// <summary>Asks the HUD to open/close the shop (keyboard/gamepad shortcut).</summary>
        public event Action ShopToggleRequested;
        public event Action<int> LeveledUp;
        /// <summary>A longer story line (text, seconds) for the HUD: the opening and each district's first visit.</summary>
        public event Action<string, float> Story;

        DistrictState[] states;
        readonly Dictionary<BuildSite, (int district, int plot)> siteLookup = new Dictionary<BuildSite, (int, int)>();
        readonly List<AutoWorker> workers = new List<AutoWorker>();
        PlayerController playerController;
        readonly Dictionary<BuildSite, Resident> residents = new Dictionary<BuildSite, Resident>();
        readonly System.Random random = new System.Random();
        SaveStore store;
        bool persist;
        bool openingSeen;
        long savedAtTicks;
        float autosave = AutosaveSeconds;
        float scheduleTimer;

        void Awake()
        {
            playerController = player.GetComponent<PlayerController>();

            states = new DistrictState[districts.Length];
            for (var d = 0; d < districts.Length; d++) states[d] = new DistrictState(Balance.Districts[d]);

            // A demo or screenshot run must neither read nor overwrite the player's real save (unless -persist asks for it, to test saving).
            persist = LaunchArgs.Has("-persist") || (!LaunchArgs.Has("-demo") && LaunchArgs.Value("-shot") == null);
            store = SaveStore.AtDefaultLocation();
            if (persist && LaunchArgs.Has("-reset")) store.Delete();
            ApplySave(persist ? store.Load() : null);

            // Time spent away counts towards new problems (the caps keep it from piling up).
            if (savedAtTicks > 0)
            {
                var away = (DateTime.UtcNow - new DateTime(savedAtTicks, DateTimeKind.Utc)).TotalSeconds;
                if (away > 0) foreach (var state in states) RepairSchedule.Advance(state, Math.Min(away, 7 * 86400.0), random);
            }

            // Every plot's building is built once up front (the dashboard shows them all) and put back as saved.
            for (var d = 0; d < districts.Length; d++)
            {
                var sites = districts[d].Sites;
                for (var p = 0; p < sites.Length; p++)
                {
                    sites[p].Initialize(states[d].Def.Plans[p], d * sites.Length + p, sites[p].GetComponentInChildren<TMPro.TMP_Text>(true));
                    sites[p].Restore(states[d].PlotWork[p]);
                    sites[p].Completed += OnSiteCompleted;
                    siteLookup[sites[p]] = (d, p);
                    if (states[d].IsPlotComplete(p)) SpawnResident(d, p, announce: false);
                    ApplyIssue(d, p);
                }
            }
            CityComplete = AllDistrictsComplete();

            Boost.Changed += OnBuffsChanged;
            LevelBuff.Changed += OnBuffsChanged;
            Level.LeveledUp += OnLeveledUp;
        }

        void Start()
        {
            hud.Bind(this);
            dashboard.Bind(this);
            ShowDashboard(snapCamera: true);

            if (!openingSeen)
            {
                openingSeen = true;
                Story?.Invoke(StoryBeats.Opening, 7f);
            }
        }

        void Update()
        {
            Boost.Tick(Time.deltaTime);
            LevelBuff.Tick(Time.deltaTime);

            scheduleTimer += Time.deltaTime;
            if (scheduleTimer >= 5f)
            {
                TickRepairSchedule(scheduleTimer);
                scheduleTimer = 0f;
            }

            if (!InDistrict) return;
            autosave -= Time.deltaTime;
            if (autosave > 0f) return;
            SaveNow();
        }

        void OnApplicationPause(bool paused)
        {
            if (paused) SaveNow();
        }

        void OnApplicationQuit() => SaveNow();

        // ---------- save / load ----------

        void ApplySave(SaveData save)
        {
            if (save == null) return;
            Economy.Add(save.coins);
            Level.Restore(save.xp);
            ColaCount = Mathf.Clamp(save.cola, 0, Balance.MaxColaInventory);
            openingSeen = save.openingSeen;
            savedAtTicks = save.savedAtUtcTicks;
            for (var d = 0; d < states.Length && d < save.districts.Length; d++) states[d].Restore(save.districts[d]);
        }

        void SaveNow()
        {
            autosave = AutosaveSeconds;
            if (!persist || states == null) return;
            SyncCurrentToState();
            store.Save(new SaveData
            {
                coins = Economy.Coins,
                xp = Level.Xp,
                cola = ColaCount,
                savedAtUtcTicks = DateTime.UtcNow.Ticks,
                openingSeen = openingSeen,
                districts = Array.ConvertAll(states, s => s.ToSave()),
            });
        }

        // The plots own the live progress while a district is open; copy it into the state before saving or leaving.
        void SyncCurrentToState()
        {
            if (!InDistrict) return;
            var sites = districts[CurrentIndex].Sites;
            for (var p = 0; p < sites.Length; p++) states[CurrentIndex].PlotWork[p] = sites[p].Work;
        }

        // ---------- dashboard <-> district ----------

        void ShowDashboard(bool snapCamera = false)
        {
            Mode = GameMode.Dashboard;
            CurrentIndex = -1;
            FocusSite = null;
            foreach (var district in districts) district.SetVisible(true);
            player.gameObject.SetActive(false);
            cameraRig.SetView(overviewTarget, overviewScale, snapCamera);
            ModeChanged?.Invoke();
            StateChanged?.Invoke();
        }

        /// <summary>Visit a district from the dashboard. False (with a hint) when it is still locked.</summary>
        public bool EnterDistrict(int index)
        {
            if (Mode != GameMode.Dashboard || index < 0 || index >= districts.Length) return false;
            var def = Balance.Districts[index];
            if (!IsUnlocked(index))
            {
                ShowHint($"{def.Name} için seviye {def.RequiredLevel} gerekli");
                return false;
            }

            Mode = GameMode.InDistrict;
            CurrentIndex = index;
            for (var d = 0; d < districts.Length; d++) districts[d].SetVisible(d == index);

            var district = districts[index];
            player.transform.SetPositionAndRotation(district.PlayerSpawn, Quaternion.Euler(0f, 135f, 0f));
            playerController.SetBounds(district.Center, district.WalkRadius);
            player.gameObject.SetActive(true);
            for (var n = 0; n < states[index].Workers.Level; n++) SpawnWorker(district);
            ApplyEffectsTo(player);
            player.Refill();

            cameraRig.SetView(player.transform, 1f, snap: true);
            ModeChanged?.Invoke();
            StateChanged?.Invoke();
            if (!states[index].IntroSeen)
            {
                states[index].IntroSeen = true;
                Story?.Invoke(StoryBeats.DistrictIntro[index], 6f);
            }
            return true;
        }

        public void ExitToDashboard()
        {
            if (!InDistrict) return;
            SaveNow();
            foreach (var worker in workers) if (worker != null) Destroy(worker.gameObject);
            workers.Clear();
            ShowDashboard();
        }

        void SpawnWorker(District district)
        {
            var worker = Instantiate(workerPrefab, district.WorkerSpawn, Quaternion.identity);
            worker.Initialize(this);
            ApplyEffectsTo(worker.GetComponent<Character>());
            workers.Add(worker);
        }

        // ---------- work ----------

        /// <summary>Nearest unfinished plot within <paramref name="radius"/> (flat distance), or null.</summary>
        public BuildSite NearestWorkableSite(Vector3 position, float radius)
        {
            BuildSite best = null;
            var bestDistance = radius;
            foreach (var site in Sites)
            {
                if (!site.NeedsWork) continue;
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
            foreach (var site in Sites)
            {
                if (!site.NeedsWork) continue;
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
            if (site == null || !site.NeedsWork) return HitResult.NoTarget;

            var free = Boost.IsActive;
            if (!free && !actor.Energy.CanSpend) return HitResult.Tired;
            if (site.State == SiteState.Broken)
            {
                if (!free) actor.Energy.TrySpend();
                RepairHit(site, power, point);
                return HitResult.Hit;
            }

            var applied = site.AddWork(power, point);
            if (applied == 0) return HitResult.NoTarget;

            if (!free) actor.Energy.TrySpend();
            Economy.Add(applied);
            Popup($"+{NumberFormat.Compact(applied)}", point, coinColor, 1f);
            return HitResult.Hit;
        }

        // A hit on a broken building adds repair work instead of building progress; finishing it pays a bonus.
        void RepairHit(BuildSite site, int power, Vector3 point)
        {
            var (d, p) = siteLookup[site];
            var state = states[d];
            var needed = Balance.RepairWork(state.Def.Scale);
            var done = state.AddRepairWork(p, power);
            site.RepairHit(done ? 1f : Mathf.Clamp01(state.RepairWork[p] / (float)needed), point);
            if (!done)
            {
                Popup("Tamir", point, coinColor, 0.8f);
                return;
            }

            var reward = Balance.RepairReward(state.Def.Scale);
            site.SetIssue(0);
            ApplyIssue(d, p);
            Economy.Add(reward);
            Level.Add(Balance.RepairXp);
            var top = site.transform.position + Vector3.up * 4.5f;
            Popup($"{StoryBeats.ThanksLine(d * 7 + p)} +{NumberFormat.Compact(reward)}", top, bonusColor, 1.4f);
            StateChanged?.Invoke();
            SaveNow();
        }

        void OnSiteCompleted(BuildSite site)
        {
            var (d, p) = siteLookup[site];
            states[d].PlotWork[p] = site.Plan.TotalWork;
            SpawnResident(d, p, announce: true);

            Economy.Add(site.Plan.CompletionBonus);
            Popup($"{site.Plan.Name} hazır! +{NumberFormat.Compact(site.Plan.CompletionBonus)}",
                site.transform.position + Vector3.up * 4.5f, bonusColor, 1.7f);
            Level.Add(site.Plan.Xp);
            if (states[d].IsComplete) ShowHint($"{states[d].Def.Name} tamamlandı!");

            StateChanged?.Invoke();
            SaveNow();
            if (!CityComplete && AllDistrictsComplete()) StartCoroutine(FinishAfterDelay(1.4f));
        }

        bool AllDistrictsComplete()
        {
            foreach (var state in states) if (!state.IsComplete) return false;
            return true;
        }

        IEnumerator FinishAfterDelay(float seconds)
        {
            yield return new WaitForSeconds(seconds);   // let the last confetti play before the victory text
            CityComplete = true;
            StateChanged?.Invoke();
        }

        void OnLeveledUp(int level)
        {
            LeveledUp?.Invoke(level);
            for (var d = 0; d < states.Length; d++)
                if (Balance.Districts[d].RequiredLevel == level) ShowHint($"Yeni mahalle açıldı: {Balance.Districts[d].Name}!");
            StateChanged?.Invoke();
        }

        // ---------- residents and repairs ----------

        void SpawnResident(int d, int p, bool announce)
        {
            var site = districts[d].Sites[p];
            if (residents.ContainsKey(site)) return;

            // Home: in front of the building, on the side facing the district centre (and the camera).
            var toCentre = districts[d].Center - site.transform.position;
            toCentre.y = 0f;
            var side = Quaternion.Euler(0f, 40f, 0f) * toCentre.normalized;
            var home = site.transform.position + side * 3.6f;
            residents[site] = Resident.Spawn(districts[d].transform, home, palette, d * 3 + p);
            if (announce) ShowHint(StoryBeats.MovedIn(site.Plan.Name));
        }

        // Makes the building (and its resident) match the problem recorded in the district state.
        void ApplyIssue(int d, int p)
        {
            var site = districts[d].Sites[p];
            var kind = states[d].Issues[p];
            site.SetIssue(kind);
            if (residents.TryGetValue(site, out var resident)) resident.SetProblem(kind != 0);
        }

        void TickRepairSchedule(float seconds)
        {
            var any = false;
            for (var d = 0; d < states.Length; d++)
            {
                var broken = RepairSchedule.Advance(states[d], seconds, random);
                foreach (var p in broken)
                {
                    any = true;
                    ApplyIssue(d, p);
                    if (InDistrict && CurrentIndex == d) ShowHint(StoryBeats.Problem(states[d].Def.Plans[p].Name, states[d].Issues[p]));
                }
            }
            if (any) StateChanged?.Invoke();
        }

        /// <summary>Testing hook (see DevCommands): gives a finished building a problem right now.</summary>
        public void ForceIssue(int district, int plot, int kind)
        {
            states[district].Break(plot, kind);
            ApplyIssue(district, plot);
            StateChanged?.Invoke();
        }

        // ---------- shop ----------

        public void BuyHammer()
        {
            if (!InDistrict || !Hammer.TryBuy(Economy)) return;
            ShowHint($"Çekiç güçlendi! Vuruş: {HammerPower}");
            StateChanged?.Invoke();
        }

        public void BuyWorker()
        {
            if (!InDistrict || !Workers.TryBuy(Economy)) return;
            SpawnWorker(CurrentDistrict);
            ShowHint("Yeni işçi işe başladı!");
            StateChanged?.Invoke();
        }

        public void BuyStamina()
        {
            if (!InDistrict || !Stamina.TryBuy(Economy)) return;
            foreach (var character in FindObjectsByType<Character>(FindObjectsSortMode.None)) character.ApplyStamina(Stamina.Level);
            var hits = Balance.HitsPerBar(Balance.PlayerEnergy, Stamina.Level);
            ShowHint($"Dayanıklılık arttı! Bir dolu enerji artık {hits} vuruş, herkes daha çabuk toparlanıyor.");
            Popup($"DAYANIKLILIK Sv.{Stamina.Level}!", player.transform.position + Vector3.up * 3.6f, new Color(0.5f, 1f, 0.6f), 1.5f);
            StateChanged?.Invoke();
        }

        // ---------- Sarı Kola ----------

        /// <summary>Picked up from the island. False when the pockets are full.</summary>
        public bool TryAddCola()
        {
            if (ColaCount >= Balance.MaxColaInventory)
            {
                ShowHint($"Cebin dolu ({ColaCount}/{Balance.MaxColaInventory}), önce bir kola iç");
                return false;
            }
            ColaCount++;
            ShowHint($"Sarı Kola alındı! ({ColaCount}/{Balance.MaxColaInventory})");
            StateChanged?.Invoke();
            return true;
        }

        /// <summary>Drink a found cola: refills every character's energy (player and workers), no boost.</summary>
        public void UseCola()
        {
            if (!InDistrict || ColaCount <= 0) return;
            ColaCount--;
            RefillEnergy();
            LevelBuff.Activate(Balance.ColaLevelSeconds);
            ShowHint($"Sarı Kola! Herkesin enerjisi doldu, {Balance.ColaLevelSeconds:0} sn boyunca seviye +{Balance.LevelBonus}.");
            StateChanged?.Invoke();
        }

        /// <summary>Buy and drink immediately: refills every character and also starts the "Coşku" boost.</summary>
        public void BuyCola()
        {
            if (!InDistrict || !Economy.TrySpend(ColaPrice)) return;
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
            character.ApplyStamina(CurrentState != null ? CurrentState.Stamina.Level : 0);
            character.SetBuffs(LevelBuff.IsActive, Boost.IsActive ? Balance.BoostSpeedMultiplier : 1f);
        }

        void RefillEnergy()
        {
            // Found at call time on purpose: workers are hired during play and no registry is needed.
            foreach (var character in FindObjectsByType<Character>(FindObjectsSortMode.None)) character.Refill();
        }

        public void ToggleShop() => ShopToggleRequested?.Invoke();

        public void ShowHint(string text) => Hint?.Invoke(text);

        void Popup(string text, Vector3 position, Color color, float scale)
        {
            var popup = Instantiate(floatingTextPrefab, position + Vector3.up * 0.4f, Quaternion.identity);
            popup.Show(text, color, scale);
        }
    }
}
