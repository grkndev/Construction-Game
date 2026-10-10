using System.Collections.Generic;
using System.Globalization;
using Game.Core;
using Game.World;
using UnityEngine;

namespace Game.Feel
{
    /// <summary>
    /// Turns what happens in the game (<see cref="GameController"/> events) into how it feels: flying coins, hit dust and camera kick, rings and
    /// notes for every floor, banners and fireworks for the big moments. Gameplay never waits for this and nothing here changes game state.
    /// </summary>
    public sealed class FeelDirector : MonoBehaviour
    {
        const float ImpactDelay = 0.08f;   // the hammer lands a moment after the swing starts; dust and shake follow it
        static readonly CultureInfo Turkish = new CultureInfo("tr-TR");

        [SerializeField] GameController game;
        [SerializeField] IsoCameraFollow cameraRig;
        [SerializeField] CoinFlight coins;
        [SerializeField] CeremonyBanner banner;
        [SerializeField] MapFlags mapFlags;
        [SerializeField] FeelFx mapFx;

        struct PendingHit
        {
            public float Due;
            public Vector3 Point;
            public int CoinCount;
        }

        readonly List<PendingHit> pending = new List<PendingHit>();
        readonly CoinBatcher workerCoins = new CoinBatcher(0.4f);
        readonly CeremonyQueue queue = new CeremonyQueue();
        float stampDue = -1f;
        bool finalePending;
        int hitCounter;

        FeelFx Fx => game.InDistrict ? game.CurrentDistrict.Fx : mapFx;

        void Start()
        {
            game.HitLanded += OnHit;
            game.FloorBuilt += OnFloorBuilt;
            game.BuildingCompleted += OnBuildingCompleted;
            game.DistrictCompleted += OnDistrictCompleted;
            game.LeveledUp += OnLeveledUp;
            game.CityCompleted += OnCityCompleted;
            game.ModeChanged += OnModeChanged;
            game.HammerUpgraded += OnHammerUpgraded;
            game.CoinsGranted += OnCoinsGranted;
            game.DecorationBuilt += OnDecorationBuilt;
            game.GoldStarEarned += OnGoldStar;
            coins.Landed += () => game.PlaySfx(SfxKind.CoinLand);
            mapFlags.Bind(game);
        }

        void OnDestroy()
        {
            if (game == null) return;
            game.HitLanded -= OnHit;
            game.FloorBuilt -= OnFloorBuilt;
            game.BuildingCompleted -= OnBuildingCompleted;
            game.DistrictCompleted -= OnDistrictCompleted;
            game.LeveledUp -= OnLeveledUp;
            game.CityCompleted -= OnCityCompleted;
            game.ModeChanged -= OnModeChanged;
            game.HammerUpgraded -= OnHammerUpgraded;
            game.CoinsGranted -= OnCoinsGranted;
            game.DecorationBuilt -= OnDecorationBuilt;
            game.GoldStarEarned -= OnGoldStar;
        }

        // ---------- every hit ----------

        void OnHit(HitInfo hit)
        {
            if (!game.InDistrict) return;
            if (!hit.ByPlayer)
            {
                workerCoins.Add(hit.Coins, hit.Point);   // helpers are batched: eight of them would flood the screen
                return;
            }
            hitCounter++;
            pending.Add(new PendingHit { Due = Time.unscaledTime + ImpactDelay, Point = hit.Point, CoinCount = hitCounter % 4 == 0 ? 2 : 1 });
        }

        void Update()
        {
            var now = Time.unscaledTime;
            for (var i = pending.Count - 1; i >= 0; i--)
            {
                if (pending[i].Due > now) continue;
                var hit = pending[i];
                pending.RemoveAt(i);
                if (!game.InDistrict) continue;
                Fx.Dust(hit.Point);
                cameraRig.Kick(0.05f);
                coins.Fly(hit.Point, hit.CoinCount);
            }

            if (workerCoins.Tick(Time.unscaledDeltaTime, out var batch, out var where) && game.InDistrict)
                coins.Fly(where, batch > 0 ? 2 : 0);

            if (stampDue > 0f && now >= stampDue)
            {
                stampDue = -1f;
                game.PlaySfx(SfxKind.Stamp);
            }

            if (!banner.IsPlaying && queue.TryNext(out var next))
            {
                banner.Play(next, queue.Finish);
                if (next.Kind == CeremonyKind.District || next.Kind == CeremonyKind.City || next.Kind == CeremonyKind.Gold)
                {
                    game.PlaySfx(SfxKind.DistrictDone);
                    stampDue = now + 0.22f;
                }
            }
        }

        // ---------- decorations and gifts ----------

        void OnCoinsGranted(Vector3 from, long coinCount)
        {
            if (game.InDistrict) coins.Fly(from, coinCount > 0 ? 3 : 0);
        }

        void OnDecorationBuilt(int district, int index)
        {
            if (!game.InDistrict) return;
            var site = game.CurrentDistrict.DecorationSlot(index).position;
            Fx.Sparkles(site, 4f, 4f);
            Fx.Ring(site, 3.6f);
            game.CheerWorkers();
            var income = Balance.TouristIncome(index + 1, game.CurrentState.Def.Scale);
            queue.Request(new CeremonyRequest(CeremonyKind.Building, CeremonyTier.Small, $"{StoryBeats.DecorationNames[index]} hazır!",
                $"Turist geliri  +{NumberFormat.Compact((long)System.Math.Round(income))}/sn", 1.5f));
        }

        void OnGoldStar(int district)
        {
            if (!game.InDistrict) return;
            game.CheerWorkers();
            queue.Request(new CeremonyRequest(CeremonyKind.Gold, CeremonyTier.Large, "ALTIN YILDIZ", $"{Balance.Districts[district].Name} tamamen güzelleşti!", 3.2f));
            Fx.Show(game.CurrentDistrict.Center, 14f, 9, 3.4f);
        }

        void OnHammerUpgraded(Vector3 where)
        {
            if (game.InDistrict) Fx.Sparkles(where, 2.6f, 1.4f);
        }

        // ---------- floors and buildings ----------

        void OnFloorBuilt(BuildSite site, int floor)
        {
            if (!game.InDistrict) return;
            Fx.Ring(site.BasePoint, site.EffectRadius);
            site.PulseBar();
        }

        void OnBuildingCompleted(BuildSite site, long bonus)
        {
            if (!game.InDistrict) return;
            Fx.Sparkles(site.BasePoint, 5.5f, site.EffectRadius * 1.5f);
            cameraRig.Punch(0.93f, 1.0f);
            game.CheerResident(site);
            game.CheerWorkers();
            queue.Request(new CeremonyRequest(CeremonyKind.Building, CeremonyTier.Small, $"{site.Plan.Name} hazır!",
                $"+{NumberFormat.Compact(bonus)}  •  +{site.Plan.Xp} XP", 1.5f));
        }

        void OnLeveledUp(int level)
        {
            if (!game.InDistrict) return;
            var opened = "";
            for (var d = 0; d < Balance.Districts.Length; d++)
                if (Balance.Districts[d].RequiredLevel == level && level > 1) opened = $"Yeni mahalle: {Balance.Districts[d].Name}!";
            queue.Request(new CeremonyRequest(CeremonyKind.Level, CeremonyTier.Medium, $"SEVİYE {level}", opened, 1.7f));
        }

        void OnDistrictCompleted(int index)
        {
            if (!game.InDistrict) return;
            var name = Balance.Districts[index].Name.ToUpper(Turkish);
            game.CheerWorkers();
            queue.Request(new CeremonyRequest(CeremonyKind.District, CeremonyTier.Large, $"{name} TAMAMLANDI", "Haritaya bayrak dikildi!", 3.1f));
            Fx.Show(game.CurrentDistrict.Center, 14f, 9, 3.4f);
        }

        void OnCityCompleted()
        {
            finalePending = true;
            if (!game.InDistrict) return;
            queue.Request(new CeremonyRequest(CeremonyKind.City, CeremonyTier.Large, "Çekiçköy yeniden doğdu!", "Bütün mahalleler tamamlandı", 4.6f));
            Fx.Show(game.CurrentDistrict.Center, 18f, 14, 4.5f);
        }

        // ---------- changing view ----------

        void OnModeChanged()
        {
            coins.Clear();
            banner.Cancel();
            queue.Clear();
            pending.Clear();
            workerCoins.Clear();
            stampDue = -1f;
            if (game.Mode == GameMode.Dashboard && finalePending)
            {
                finalePending = false;
                mapFx.Show(Vector3.zero, 38f, 16, 5f);   // the whole country celebrates once
            }
        }

        // ---------- testing hook (see DevCommands -ceremony) ----------

        /// <summary>Plays one effect on demand: coins, building, level, district or city.</summary>
        public void Demo(string what)
        {
            switch (what)
            {
                case "coins":
                    if (game.InDistrict) coins.Fly(game.CurrentDistrict.Center + new Vector3(0f, 2f, 6f), 12);
                    break;
                case "building":
                    queue.Request(new CeremonyRequest(CeremonyKind.Building, CeremonyTier.Small, "Okul hazır!", "+1.2K  •  +3 XP", 1.5f));
                    break;
                case "level":
                    queue.Request(new CeremonyRequest(CeremonyKind.Level, CeremonyTier.Medium, "SEVİYE 5", "Yeni mahalle: Liman!", 1.7f));
                    break;
                case "district":
                    queue.Request(new CeremonyRequest(CeremonyKind.District, CeremonyTier.Large, "SAHİL TAMAMLANDI", "Haritaya bayrak dikildi!", 3.1f));
                    if (game.InDistrict) Fx.Show(game.CurrentDistrict.Center, 14f, 9, 3.4f);
                    break;
                case "gold":
                    queue.Request(new CeremonyRequest(CeremonyKind.Gold, CeremonyTier.Large, "ALTIN YILDIZ", "Sahil tamamen güzelleşti!", 3.2f));
                    if (game.InDistrict) Fx.Show(game.CurrentDistrict.Center, 14f, 9, 3.4f);
                    break;
                case "city":
                    queue.Request(new CeremonyRequest(CeremonyKind.City, CeremonyTier.Large, "Çekiçköy yeniden doğdu!", "Bütün mahalleler tamamlandı", 4.6f));
                    if (game.InDistrict) Fx.Show(game.CurrentDistrict.Center, 18f, 14, 4.5f);
                    break;
            }
        }
    }
}
