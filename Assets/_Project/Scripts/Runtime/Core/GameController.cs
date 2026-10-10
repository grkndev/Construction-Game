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
        /// <summary>Inside a district the camera sits this much farther out than its base offset (so the bigger buildings fit); the editor sizes the ground from it.</summary>
        public const float DistrictViewScale = 1.25f;

        [SerializeField] District[] districts;
        [SerializeField] AutoWorker workerPrefab;
        [SerializeField] FloatingText floatingTextPrefab;
        [SerializeField] GameHud hud;
        [SerializeField] DashboardHud dashboard;
        [SerializeField] ScreenFade fade;
        [SerializeField] Character player;
        [SerializeField] IsoCameraFollow cameraRig;
        [SerializeField] Transform overviewTarget;
        [SerializeField] GameObject mapRoot;            // the picture of the country shown on the dashboard
        [SerializeField] ToonPalette palette;
        [SerializeField] float overviewScale = 5.8f;
        [SerializeField] Color coinColor = new Color(1f, 0.85f, 0.2f);
        [SerializeField] Color bonusColor = new Color(0.45f, 1f, 0.55f);
        [SerializeField] Color colaColor = new Color(1f, 0.93f, 0.35f);

        // ---- shared across the whole game
        /// <summary>The current district's coins (an empty purse on the dashboard): every district has its own money.</summary>
        public Economy Economy => CurrentState != null ? CurrentState.Economy : noEconomy;
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
        /// <summary>The player (walkers ignore colliding with them).</summary>
        public Transform PlayerTransform => player.transform;
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
        public UpgradeTrack Speed => CurrentState.Speed;
        /// <summary>Progress of beautifying the district being visited (see <see cref="DecorationProgress"/>).</summary>
        public DecorationProgress Decorations => CurrentState.Decorations;
        int Scale => CurrentState?.Def.Scale ?? 1;
        public long ColaPrice => Balance.ColaPrice(Hammer.Level, Workers.Tier, Speed.Level, Scale);
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
        /// <summary>The camera jumped to another place (map or a district): the HUD covers the cut with a fade.</summary>
        public event Action ViewChanged;
        /// <summary>The coins of the district being visited changed (or the purse in use changed).</summary>
        public event Action CoinsChanged;
        public event Action<string> Hint;
        /// <summary>Asks the HUD to open/close the shop (keyboard/gamepad shortcut).</summary>
        public event Action ShopToggleRequested;
        public event Action<int> LeveledUp;
        // ---- what the presentation layer (Game.Feel) listens to; gameplay never depends on anyone listening.
        /// <summary>A hammer hit landed and paid coins (not raised for repairs).</summary>
        public event Action<HitInfo> HitLanded;
        /// <summary>A floor (build stage) of a building was completed; the number counts from 1.</summary>
        public event Action<BuildSite, int> FloorBuilt;
        /// <summary>A building was finished, with the coin bonus it paid.</summary>
        public event Action<BuildSite, long> BuildingCompleted;
        /// <summary>The last building of a district was finished (the argument is the district index).</summary>
        public event Action<int> DistrictCompleted;
        /// <summary>The hammer was upgraded (with where the player stands, for a little burst).</summary>
        public event Action<Vector3> HammerUpgraded;
        /// <summary>Coins arrived from somewhere other than a hammer hit (tourists, a chest): where from, how many.</summary>
        public event Action<Vector3, long> CoinsGranted;
        /// <summary>A decoration was finished: the district and which one (0-based).</summary>
        public event Action<int, int> DecorationBuilt;
        /// <summary>The last decoration of a district was finished: it earned its golden star.</summary>
        public event Action<int> GoldStarEarned;
        /// <summary>The last building of the last district was just finished (not raised for a save that was already complete).</summary>
        public event Action CityCompleted;
        /// <summary>Something happened that deserves a sound (played by the audio manager).</summary>
        public event Action<SfxKind> Sfx;
        /// <summary>A longer story line (text, seconds) for the HUD: the opening and each district's first visit.</summary>
        public event Action<string, float> Story;

        // Hat colours the hired helpers cycle through (the first helper keeps the original yellow).
        static readonly Func<ToonPalette, Material>[] HelperHats = { p => p.hat, p => p.white, p => p.cone, p => p.green, p => p.hull };

        DistrictState[] states;
        List<DecorationSite>[] decorations;   // per district: the decorations that stand (their models and spots)
        DecorationSite[] decorationBuilding;  // per district: the one under construction, if any
        float touristTimer;
        bool[] built;   // districts whose buildings exist (they are made on the first visit, not at startup)
        readonly Economy noEconomy = new Economy();
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
            Prim.UseMeshes(palette.sphereMesh, palette.cylinderMesh);   // low-poly spheres and cylinders for everything built from here on
            if (Application.isMobilePlatform) Application.targetFrameRate = 60;   // phones default to 30
            playerController = player.GetComponent<PlayerController>();

            states = new DistrictState[districts.Length];
            for (var d = 0; d < districts.Length; d++)
            {
                states[d] = new DistrictState(Balance.Districts[d]);
                var index = d;
                states[d].Economy.Changed += () => { if (CurrentIndex == index) CoinsChanged?.Invoke(); };
            }

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

            built = new bool[districts.Length];
            decorations = new List<DecorationSite>[districts.Length];
            decorationBuilding = new DecorationSite[districts.Length];
            CityComplete = AllDistrictsComplete();

            Boost.Changed += OnBuffsChanged;
            LevelBuff.Changed += OnBuffsChanged;
            Level.LeveledUp += OnLeveledUp;
        }

        void Start()
        {
            hud.Bind(this);
            dashboard.Bind(this);
            fade.Bind(this);
            ShowDashboard();

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
            TickTourists();
            autosave -= Time.deltaTime;
            if (autosave > 0f) return;
            SaveNow();
        }

        // Every finished decoration brings tourists who tip a little: coins fly from a decoration to the purse every few seconds.
        void TickTourists()
        {
            var state = CurrentState;
            var count = state.Decorations.Built;
            if (count <= 0) { touristTimer = 0f; return; }
            touristTimer += Time.deltaTime;
            if (touristTimer < Balance.TouristTipSeconds) return;
            touristTimer = 0f;
            var tip = (long)System.Math.Round(Balance.TouristIncome(count, Scale) * Balance.TouristTipSeconds);
            var from = decorations[CurrentIndex][UnityEngine.Random.Range(0, decorations[CurrentIndex].Count)].transform.position + Vector3.up * 2f;
            GrantCoins(from, tip);
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

        /// <summary>
        /// Builds a district's buildings (put back as saved) the first time they are needed: its first visit, or a testing hook. Nothing is built
        /// for districts the player never opens, which keeps start-up and memory small.
        /// </summary>
        public void EnsureBuilt(int d)
        {
            if (built[d]) return;
            built[d] = true;
            var sites = districts[d].Sites;
            for (var p = 0; p < sites.Length; p++)
            {
                sites[p].Initialize(states[d].Def.Plans[p], d * sites.Length + p, sites[p].GetComponentInChildren<TMPro.TMP_Text>(true));
                sites[p].Restore(states[d].PlotWork[p]);
                sites[p].Completed += OnSiteCompleted;
                sites[p].FloorBuilt += (built, floor) => FloorBuilt?.Invoke(built, floor);
                siteLookup[sites[p]] = (d, p);
                if (states[d].IsPlotComplete(p)) SpawnResident(d, p, announce: false);
                ApplyIssue(d, p);
            }
            RestoreDecorations(d);
        }

        // Puts back what the save says: finished decorations stand, an ordered one is under construction where it was left.
        void RestoreDecorations(int d)
        {
            var progress = states[d].Decorations;
            decorations[d] = new List<DecorationSite>();
            for (var i = 0; i < progress.Built; i++)
                decorations[d].Add(DecorationSite.Finished(districts[d].DecorationSlot(i), i, palette));
            if (progress.Ordered)
                decorationBuilding[d] = DecorationSite.Begin(districts[d].DecorationSlot(progress.Built), progress.Built, palette, progress.Progress);
        }

        // ---------- decorations ----------

        /// <summary>Pays for the next decoration of the district being visited; helpers without work come and build it.</summary>
        public bool OrderDecoration()
        {
            if (!InDistrict) return false;
            var state = CurrentState;
            if (!state.IsComplete)
            {
                ShowHint("Önce mahalledeki bütün binaları bitir");
                return false;
            }
            if (!state.Decorations.TryOrder(true, Economy, Scale)) return false;
            var d = CurrentIndex;
            var index = state.Decorations.NextIndex;
            decorationBuilding[d] = DecorationSite.Begin(CurrentDistrict.DecorationSlot(index), index, palette, 0f);
            ShowHint($"{StoryBeats.DecorationNames[index]} için işçiler yolda!");
            Sfx?.Invoke(SfxKind.Buy);
            StateChanged?.Invoke();
            SaveNow();
            return true;
        }

        /// <summary>The decoration under construction in this district, for a free helper to work on (null when there is none).</summary>
        public DecorationSite PickDecorationJob() => InDistrict ? decorationBuilding[CurrentIndex] : null;

        /// <summary>A helper hammered at the decoration for <paramref name="seconds"/>.</summary>
        public void AddDecorationWork(float seconds)
        {
            if (!InDistrict) return;
            var d = CurrentIndex;
            var site = decorationBuilding[d];
            if (site == null) return;
            var progress = states[d].Decorations;
            var finished = progress.AddWork(seconds);
            site.SetProgress(finished ? 1f : progress.Progress);
            if (!finished) return;

            var index = progress.Built - 1;
            site.Finish();
            decorations[d].Add(site);
            decorationBuilding[d] = null;
            Sfx?.Invoke(SfxKind.BuildingDone);
            DecorationBuilt?.Invoke(d, index);
            if (progress.IsComplete) GoldStarEarned?.Invoke(d);
            StateChanged?.Invoke();
            SaveNow();
        }

        /// <summary>A spot next to a finished decoration where a idle helper can stand around (false when there is none yet).</summary>
        public bool TryPickHangout(out Vector3 point)
        {
            point = default;
            if (!InDistrict) return false;
            var spots = 0;
            foreach (var site in decorations[CurrentIndex]) spots += site.Hangouts.Length;
            if (spots == 0) return false;
            var pick = UnityEngine.Random.Range(0, spots);
            foreach (var site in decorations[CurrentIndex])
            {
                if (pick < site.Hangouts.Length) { point = site.Hangouts[pick]; return true; }
                pick -= site.Hangouts.Length;
            }
            return false;
        }

        /// <summary>Coins from somewhere other than a hammer hit (a chest, a happy pet): added to the district's purse, flown in by the HUD.</summary>
        public void GrantCoins(Vector3 from, long coins)
        {
            if (!InDistrict || coins <= 0) return;
            Economy.Add(coins);
            CoinsGranted?.Invoke(from, coins);
        }

        /// <summary>Coins in a treasure chest right now (scales with what the district earns).</summary>
        public long ChestReward => Balance.ChestReward(Hammer.Level, Workers.Tier, Speed.Level, Scale);
        public long PetReward => Balance.PetReward(Hammer.Level, Workers.Tier, Speed.Level, Scale);
        public long BirdTip => Balance.BirdTip(Hammer.Level, Workers.Tier, Speed.Level, Scale);

        /// <summary>True when the player or any helper stands within <paramref name="radius"/> (flat) of the point.</summary>
        public bool AnyoneNear(Vector3 point, float radius)
        {
            if (Vector3.ProjectOnPlane(player.transform.position - point, Vector3.up).magnitude < radius) return true;
            foreach (var worker in workers)
                if (worker != null && Vector3.ProjectOnPlane(worker.transform.position - point, Vector3.up).magnitude < radius) return true;
            return false;
        }

        /// <summary>Testing hook (see DevCommands -decorate): builds the first <paramref name="count"/> decorations of the current district at once.</summary>
        public void DevDecorate(int count)
        {
            if (!InDistrict) return;
            var d = CurrentIndex;
            var progress = states[d].Decorations;
            while (progress.Built < Mathf.Min(count, Balance.DecorationCount))
            {
                var index = progress.Built;
                progress.Restore(index + 1, false, 0f);
                decorations[d].Add(DecorationSite.Finished(districts[d].DecorationSlot(index), index, palette));
            }
            StateChanged?.Invoke();
        }

        void ShowDashboard()
        {
            Mode = GameMode.Dashboard;
            CurrentIndex = -1;
            FocusSite = null;
            foreach (var district in districts) district.SetVisible(false);
            if (mapRoot != null) mapRoot.SetActive(true);
            player.gameObject.SetActive(false);
            cameraRig.SetView(overviewTarget, overviewScale);
            ViewChanged?.Invoke();
            ModeChanged?.Invoke();
            CoinsChanged?.Invoke();
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

            EnsureBuilt(index);
            Mode = GameMode.InDistrict;
            CurrentIndex = index;
            for (var d = 0; d < districts.Length; d++) districts[d].SetVisible(d == index);
            if (mapRoot != null) mapRoot.SetActive(false);

            var district = districts[index];
            player.transform.SetPositionAndRotation(district.PlayerSpawn, Quaternion.Euler(0f, 135f, 0f));
            playerController.SetBounds(district);
            player.gameObject.SetActive(true);
            for (var n = 0; n < states[index].Workers.Tier; n++) SpawnWorker(district, n);   // tier 1 = the free first helper
            ApplyEffectsTo(player);
            player.Refill();

            cameraRig.SetView(player.transform, DistrictViewScale);
            ViewChanged?.Invoke();
            ModeChanged?.Invoke();
            CoinsChanged?.Invoke();
            StateChanged?.Invoke();
            Sfx?.Invoke(SfxKind.Enter);
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
            Sfx?.Invoke(SfxKind.Exit);
            ShowDashboard();
        }

        void SpawnWorker(District district, int number)
        {
            var worker = Instantiate(workerPrefab, district.WorkerSpawn, Quaternion.identity);
            worker.Initialize(this, number);
            // Helpers differ from each other: the first keeps the original outfit, the rest cycle through shirt and hat colours.
            if (number > 0) worker.GetComponent<WorkerVisual>().SetLook(palette.walls[number % palette.walls.Length], HelperHats[number % HelperHats.Length](palette));
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
            var earned = Balance.CoinsFor(applied);
            Economy.Add(earned);
            Sfx?.Invoke(actor.IsPlayer ? SfxKind.HitPlayer : SfxKind.HitWorker);
            HitLanded?.Invoke(new HitInfo(point, earned, actor.IsPlayer));   // the coins fly to the purse (Feel/CoinFlight)
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
                Sfx?.Invoke(SfxKind.RepairHit);
                Popup("Tamir", point, coinColor, 0.8f);
                return;
            }

            var reward = Balance.RepairReward(state.Def.Scale);
            site.SetIssue(0);
            ApplyIssue(d, p);
            state.Economy.Add(reward);
            Level.Add(Balance.RepairXp);
            var top = site.transform.position + Vector3.up * 4.5f;
            Popup($"{StoryBeats.ThanksLine(d * 7 + p)} +{NumberFormat.Compact(reward)}", top, bonusColor, 1.4f);
            Sfx?.Invoke(SfxKind.RepairDone);
            StateChanged?.Invoke();
            SaveNow();
        }

        void OnSiteCompleted(BuildSite site)
        {
            var (d, p) = siteLookup[site];
            states[d].PlotWork[p] = site.Plan.TotalWork;
            SpawnResident(d, p, announce: true);

            var bonus = Balance.CoinsFor(site.Plan.CompletionBonus);
            states[d].Economy.Add(bonus);
            Sfx?.Invoke(SfxKind.BuildingDone);
            Level.Add(site.Plan.Xp);
            BuildingCompleted?.Invoke(site, bonus);   // the banner shows the name, bonus and XP
            if (states[d].IsComplete) DistrictCompleted?.Invoke(d);

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
            CityCompleted?.Invoke();
            StateChanged?.Invoke();
        }

        void OnLeveledUp(int level)
        {
            LeveledUp?.Invoke(level);
            Sfx?.Invoke(SfxKind.LevelUp);
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
            var home = site.SpotOutside(side, 3.0f);   // outside the walls, however wide the building (a fixed distance put the factory's neighbour inside it)
            residents[site] = Resident.Spawn(districts[d].transform, home, palette, d * 3 + p, player.transform);
            if (announce) ShowHint(StoryBeats.MovedIn(site.Plan.Name));
        }

        // Makes the building (and its resident) match the problem recorded in the district state.
        void ApplyIssue(int d, int p)
        {
            if (!built[d]) return;   // applied when the district is built
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
                    if (!InDistrict || CurrentIndex != d) continue;
                    ShowHint(StoryBeats.Problem(states[d].Def.Plans[p].Name, states[d].Issues[p]));
                    Sfx?.Invoke(SfxKind.Problem);
                }
            }
            if (any) StateChanged?.Invoke();
        }

        /// <summary>Testing hook (see DevCommands): gives a finished building a problem right now.</summary>
        public void ForceIssue(int district, int plot, int kind)
        {
            EnsureBuilt(district);
            states[district].Break(plot, kind);
            ApplyIssue(district, plot);
            StateChanged?.Invoke();
        }

        // ---------- shop ----------

        public void BuyHammer()
        {
            if (!InDistrict || !Hammer.TryBuy(Economy)) return;
            foreach (var character in FindObjectsByType<Character>(FindObjectsSortMode.None)) ApplyEffectsTo(character);   // a bigger hammer for everyone
            HammerUpgraded?.Invoke(player.transform.position);
            ShowHint($"Çekiç güçlendi! Vuruş: {HammerPower}");
            Sfx?.Invoke(SfxKind.Buy);
            StateChanged?.Invoke();
        }

        public void BuyWorker()
        {
            if (!InDistrict || !Workers.TryBuy(Economy)) return;
            SpawnWorker(CurrentDistrict, workers.Count);
            ShowHint("Yeni işçi işe başladı!");
            Sfx?.Invoke(SfxKind.Buy);
            StateChanged?.Invoke();
        }

        public void BuyStamina()
        {
            if (!InDistrict || !Stamina.TryBuy(Economy)) return;
            foreach (var character in FindObjectsByType<Character>(FindObjectsSortMode.None)) character.ApplyStamina(Stamina.Level);
            var hits = Balance.HitsPerBar(Balance.PlayerEnergy, Stamina.Level);
            ShowHint($"Dayanıklılık arttı! Bir dolu enerji artık {hits} vuruş, herkes daha çabuk toparlanıyor.");
            Sfx?.Invoke(SfxKind.Buy);
            Popup($"DAYANIKLILIK Sv.{Stamina.Level}!", player.transform.position + Vector3.up * 3.6f, new Color(0.5f, 1f, 0.6f), 1.5f);
            StateChanged?.Invoke();
        }

        public void BuySpeed()
        {
            if (!InDistrict || !Speed.TryBuy(Economy)) return;
            foreach (var character in FindObjectsByType<Character>(FindObjectsSortMode.None)) ApplyEffectsTo(character);
            var percent = Mathf.RoundToInt((Balance.SpeedMultiplier(Speed.Level) - 1f) * 100f);
            ShowHint($"Hız arttı! Herkes %{percent} daha hızlı yürüyor, vuruyor ve toparlanıyor.");
            Sfx?.Invoke(SfxKind.Buy);
            Popup($"HIZ Sv.{Speed.Level}!", player.transform.position + Vector3.up * 3.6f, new Color(0.55f, 0.85f, 1f), 1.5f);
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
            Sfx?.Invoke(SfxKind.ColaPickup);
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
            Sfx?.Invoke(SfxKind.ColaDrink);
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
            Sfx?.Invoke(SfxKind.Buy);
            Sfx?.Invoke(SfxKind.Boost);
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
            character.ApplyHammerTier(CurrentState != null ? CurrentState.Hammer.Tier : 1);
            var pace = CurrentState != null ? Balance.SpeedMultiplier(CurrentState.Speed.Level) : 1f;
            character.SetBuffs(LevelBuff.IsActive, (Boost.IsActive ? Balance.BoostSpeedMultiplier : 1f) * pace);
        }

        void RefillEnergy()
        {
            // Found at call time on purpose: workers are hired during play and no registry is needed.
            foreach (var character in FindObjectsByType<Character>(FindObjectsSortMode.None)) character.Refill();
        }

        public void ToggleShop() => ShopToggleRequested?.Invoke();

        public void ShowHint(string text) => Hint?.Invoke(text);

        /// <summary>Lets the presentation layer ask for a sound (banners, fanfares) without owning an audio source.</summary>
        public void PlaySfx(SfxKind kind) => Sfx?.Invoke(kind);

        /// <summary>All hired helpers hop for joy (a building or district was finished).</summary>
        public void CheerWorkers()
        {
            foreach (var worker in workers) if (worker != null) worker.Cheer();
        }

        /// <summary>The villager living in this building celebrates (called when it is finished).</summary>
        public void CheerResident(BuildSite site)
        {
            if (residents.TryGetValue(site, out var resident)) resident.Cheer();
        }

        void Popup(string text, Vector3 position, Color color, float scale)
        {
            var popup = Instantiate(floatingTextPrefab, position + Vector3.up * 0.4f, Quaternion.identity);
            popup.Show(text, color, scale);
        }
    }
}
