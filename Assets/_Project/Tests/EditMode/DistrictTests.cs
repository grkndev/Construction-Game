using System;
using System.IO;
using NUnit.Framework;
using Game.Core;

namespace Game.Tests
{
    public class LevelTrackTests
    {
        static LevelTrack Track() => new LevelTrack(new[] { 0, 2, 5, 9 });

        [Test]
        public void StartsAtLevelOne()
        {
            var t = Track();
            Assert.AreEqual(1, t.Level);
            Assert.AreEqual(0, t.Xp);
        }

        [Test]
        public void Add_RaisesLevelAtThreshold_AndReportsEachLevelGained()
        {
            var t = Track();
            var gained = new System.Collections.Generic.List<int>();
            t.LeveledUp += gained.Add;

            t.Add(1);
            Assert.AreEqual(1, t.Level);
            t.Add(1);                       // 2 XP -> level 2
            Assert.AreEqual(2, t.Level);
            t.Add(10);                      // jumps over level 3 and 4
            CollectionAssert.AreEqual(new[] { 2, 3, 4 }, gained);
            Assert.IsTrue(t.IsMax);
        }

        [Test]
        public void Fraction_FillsWithinTheCurrentLevel()
        {
            var t = Track();
            t.Add(3);   // level 2 (2..5), 1 of 3 into it
            Assert.AreEqual(1, t.XpIntoLevel);
            Assert.AreEqual(3, t.XpForLevel);
            Assert.AreEqual(1f / 3f, t.Fraction, 1e-5);
        }

        [Test]
        public void Restore_SetsLevelWithoutEvents()
        {
            var t = Track();
            var events = 0;
            t.LeveledUp += _ => events++;
            t.Restore(6);
            Assert.AreEqual(3, t.Level);
            Assert.AreEqual(0, events);
        }

        [Test]
        public void Add_IgnoresNonPositive()
        {
            var t = Track();
            t.Add(0);
            t.Add(-4);
            Assert.AreEqual(0, t.Xp);
        }

        [Test]
        public void Constructor_RequiresZeroFirstThreshold()
        {
            Assert.Throws<ArgumentException>(() => new LevelTrack(new[] { 5, 10 }));
        }
    }

    public class DistrictStateTests
    {
        static DistrictState State() => new DistrictState(Balance.Districts[1]);

        static void Finish(DistrictState s, int plot) => s.PlotWork[plot] = s.Def.Plans[plot].TotalWork;

        [Test]
        public void NewState_StartsFromZero_WithScaledUpgradePrices()
        {
            var s = State();
            Assert.AreEqual(0, s.Hammer.Level);
            Assert.AreEqual(0, s.CompletedCount);
            Assert.AreEqual(Balance.NewHammerTrack().NextCost * s.Def.Scale, s.Hammer.NextCost);
        }

        [Test]
        public void SaveRoundTrip_KeepsEverything()
        {
            var s = State();
            s.Hammer.SetLevel(4);
            s.Workers.SetLevel(2);
            s.Stamina.SetLevel(3);
            Finish(s, 0);
            s.PlotWork[1] = 50;
            s.Break(0, 2);
            s.RepairWork[0] = 7;
            s.RepairClock = 120f;
            s.IntroSeen = true;

            var copy = State();
            copy.Restore(s.ToSave());

            Assert.AreEqual(4, copy.Hammer.Level);
            Assert.AreEqual(2, copy.Workers.Level);
            Assert.AreEqual(3, copy.Stamina.Level);
            CollectionAssert.AreEqual(s.PlotWork, copy.PlotWork);
            CollectionAssert.AreEqual(s.Issues, copy.Issues);
            Assert.AreEqual(7, copy.RepairWork[0]);
            Assert.AreEqual(120f, copy.RepairClock);
            Assert.IsTrue(copy.IntroSeen);
        }

        [Test]
        public void Restore_ToleratesShortArraysAndOutOfRangeValues()
        {
            var s = State();
            s.Restore(new DistrictSave { hammer = 999, plotWork = new[] { int.MaxValue }, issues = new[] { 9 } });
            Assert.AreEqual(s.Def.Plans[0].TotalWork, s.PlotWork[0]);   // clamped
            Assert.AreEqual(Balance.RepairKinds, s.Issues[0]);          // clamped
            Assert.AreEqual(0, s.PlotWork[2]);                          // missing -> 0
            Assert.AreEqual(25, s.Hammer.Level);                        // clamped to max
        }

        [Test]
        public void Restore_DropsProblemsOnUnfinishedPlots()
        {
            var s = State();
            s.Restore(new DistrictSave { issues = new[] { 1, 1, 1 } });
            Assert.AreEqual(0, s.IssueCount);
        }

        [Test]
        public void Break_OnlyAppliesToFinishedPlots_AndOncePerPlot()
        {
            var s = State();
            s.Break(0, 1);
            Assert.AreEqual(0, s.IssueCount);   // plot 0 not finished

            Finish(s, 0);
            s.Break(0, 1);
            s.Break(0, 3);                      // already broken: keeps the first
            Assert.AreEqual(1, s.Issues[0]);
        }

        [Test]
        public void AddRepairWork_FixesAtTheRequiredAmount()
        {
            var s = State();
            Finish(s, 0);
            s.Break(0, 2);
            var needed = Balance.RepairWork(s.Def.Scale);

            Assert.IsFalse(s.AddRepairWork(0, needed - 1));
            Assert.AreEqual(1, s.IssueCount);
            Assert.IsTrue(s.AddRepairWork(0, 1));
            Assert.AreEqual(0, s.IssueCount);
            Assert.AreEqual(0, s.RepairWork[0]);
        }

        [Test]
        public void Fraction_ReflectsWorkDone()
        {
            var s = State();
            Assert.AreEqual(0f, s.Fraction);
            for (var i = 0; i < s.PlotCount; i++) Finish(s, i);
            Assert.AreEqual(1f, s.Fraction);
            Assert.IsTrue(s.IsComplete);
        }
    }

    public class SaveStoreTests
    {
        string dir;

        [SetUp]
        public void SetUp() => dir = Path.Combine(Path.GetTempPath(), "construction-tests-" + Guid.NewGuid().ToString("N"));

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }

        [Test]
        public void SaveThenLoad_ReturnsTheSameData()
        {
            var store = new SaveStore(Path.Combine(dir, "save.json"));
            var data = new SaveData
            {
                coins = 123456789012L,
                xp = 17,
                cola = 2,
                savedAtUtcTicks = 638_000_000_000_000_000L,
                districts = new[] { new DistrictSave { hammer = 5, plotWork = new[] { 1, 2, 3 }, introSeen = true } },
            };

            Assert.IsTrue(store.Save(data));
            var loaded = store.Load();

            Assert.IsNotNull(loaded);
            Assert.AreEqual(123456789012L, loaded.coins);
            Assert.AreEqual(17, loaded.xp);
            Assert.AreEqual(2, loaded.cola);
            Assert.AreEqual(638_000_000_000_000_000L, loaded.savedAtUtcTicks);
            Assert.AreEqual(5, loaded.districts[0].hammer);
            CollectionAssert.AreEqual(new[] { 1, 2, 3 }, loaded.districts[0].plotWork);
            Assert.IsTrue(loaded.districts[0].introSeen);
        }

        [Test]
        public void Save_Overwrites_AndLeavesNoTempFile()
        {
            var path = Path.Combine(dir, "save.json");
            var store = new SaveStore(path);
            store.Save(new SaveData { coins = 1 });
            store.Save(new SaveData { coins = 2 });
            Assert.AreEqual(2, store.Load().coins);
            Assert.IsFalse(File.Exists(path + ".tmp"));
        }

        [Test]
        public void Load_ReturnsNull_WhenMissing()
        {
            Assert.IsNull(new SaveStore(Path.Combine(dir, "nope.json")).Load());
        }

        [Test]
        public void Load_ReturnsNull_WhenCorruptOrWrongVersion()
        {
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, "save.json");
            var store = new SaveStore(path);

            File.WriteAllText(path, "{ this is not json");
            Assert.IsNull(store.Load());

            File.WriteAllText(path, "{\"version\":999}");
            Assert.IsNull(store.Load());
        }

        [Test]
        public void Delete_RemovesTheSave()
        {
            var store = new SaveStore(Path.Combine(dir, "save.json"));
            store.Save(new SaveData());
            store.Delete();
            Assert.IsFalse(store.Exists);
        }
    }

    public class RepairScheduleTests
    {
        static DistrictState Finished(int plots)
        {
            var s = new DistrictState(Balance.Districts[0]);
            for (var i = 0; i < plots; i++) s.PlotWork[i] = s.Def.Plans[i].TotalWork;
            return s;
        }

        [Test]
        public void NothingBreaks_BeforeTheInterval()
        {
            var s = Finished(3);
            var created = RepairSchedule.Advance(s, Balance.RepairInterval - 1, new System.Random(1));
            Assert.IsEmpty(created);
            Assert.AreEqual(0, s.IssueCount);
        }

        [Test]
        public void OneProblemPerInterval()
        {
            var s = Finished(3);
            var created = RepairSchedule.Advance(s, Balance.RepairInterval, new System.Random(1));
            Assert.AreEqual(1, created.Count);
            Assert.AreEqual(1, s.IssueCount);
            Assert.That(s.Issues[created[0]], Is.InRange(1, Balance.RepairKinds));
        }

        [Test]
        public void LongAbsence_NeverExceedsTheCaps()
        {
            var s = Finished(3);
            RepairSchedule.Advance(s, Balance.RepairInterval * 100, new System.Random(2));
            Assert.AreEqual(Math.Min(3, Balance.MaxIssuesPerDistrict), s.IssueCount);
            foreach (var kind in s.Issues) Assert.LessOrEqual(kind, Balance.RepairKinds);   // one problem per plot by construction
        }

        [Test]
        public void OnlyFinishedBuildings_CanBreak()
        {
            var s = Finished(1);
            RepairSchedule.Advance(s, Balance.RepairInterval * 100, new System.Random(3));
            Assert.AreEqual(1, s.IssueCount);
            Assert.AreNotEqual(0, s.Issues[0]);
            Assert.AreEqual(0, s.Issues[1]);
        }

        [Test]
        public void NoFinishedBuildings_BanksNoTime()
        {
            var s = Finished(0);
            RepairSchedule.Advance(s, Balance.RepairInterval * 5, new System.Random(4));
            Assert.AreEqual(0, s.IssueCount);
            Assert.AreEqual(0f, s.RepairClock);
        }

        [Test]
        public void Partial_TimeAccumulatesAcrossCalls()
        {
            var s = Finished(3);
            var rng = new System.Random(5);
            RepairSchedule.Advance(s, Balance.RepairInterval * 0.6, rng);
            Assert.AreEqual(0, s.IssueCount);
            RepairSchedule.Advance(s, Balance.RepairInterval * 0.6, rng);
            Assert.AreEqual(1, s.IssueCount);
        }
    }

    public class DistrictBalanceTests
    {
        [Test]
        public void Districts_UnlockInAscendingLevelOrder_StartingAtLevelOne()
        {
            Assert.AreEqual(1, Balance.Districts[0].RequiredLevel);
            for (var i = 1; i < Balance.Districts.Length; i++)
                Assert.Greater(Balance.Districts[i].RequiredLevel, Balance.Districts[i - 1].RequiredLevel, Balance.Districts[i].Name);
        }

        [Test]
        public void FinishingEveryDistrict_ReachesTheNextOnesRequiredLevel()
        {
            // Level is the only gate, so completing the districts so far must always be enough to open the next one.
            var level = new LevelTrack(Balance.LevelThresholds);
            for (var i = 0; i < Balance.Districts.Length; i++)
            {
                Assert.GreaterOrEqual(level.Level, Balance.Districts[i].RequiredLevel, $"{Balance.Districts[i].Name} unreachable");
                foreach (var plan in Balance.Districts[i].Plans) level.Add(plan.Xp);
            }
            Assert.AreEqual(level.MaxLevel, level.Level, "the last district should reach the top level");
        }

        [Test]
        public void EachDistrict_HasThreeGrowingBuildings()
        {
            foreach (var d in Balance.Districts)
            {
                Assert.AreEqual(3, d.Plans.Length, d.Name);
                for (var i = 1; i < d.Plans.Length; i++)
                {
                    Assert.Greater(d.Plans[i].TotalWork, d.Plans[i - 1].TotalWork, d.Name);
                    Assert.Greater(d.Plans[i].Xp, d.Plans[i - 1].Xp, d.Name);
                }
            }
        }

        [Test]
        public void FirstBuildingOfEveryDistrict_PaysForTheFirstHammerUpgrade()
        {
            // Upgrades start from zero in each district, so the first building there must afford the first one.
            foreach (var d in Balance.Districts)
            {
                var first = d.Plans[0];
                Assert.GreaterOrEqual(first.TotalWork + first.CompletionBonus, Balance.NewHammerTrack(d.Scale).NextCost, d.Name);
            }
        }

        [Test]
        public void Scale_MultipliesWorkAndRewards()
        {
            var plan = Balance.Districts[2].Plans[0];
            Assert.AreEqual(3 * 90 * Balance.Districts[2].Scale, plan.TotalWork);
            Assert.AreEqual(120 * Balance.Districts[2].Scale, plan.CompletionBonus);
        }

        [Test]
        public void LevelThresholds_AreStrictlyIncreasing()
        {
            Assert.AreEqual(0, Balance.LevelThresholds[0]);
            for (var i = 1; i < Balance.LevelThresholds.Length; i++)
                Assert.Greater(Balance.LevelThresholds[i], Balance.LevelThresholds[i - 1]);
        }
    }
}
