using System.Collections.Generic;
using NUnit.Framework;
using Game.Core;

namespace Game.Tests
{
    public class EconomyTests
    {
        [Test]
        public void Add_IncreasesCoins_AndNotifies()
        {
            var e = new Economy();
            var changes = 0;
            e.Changed += () => changes++;
            e.Add(10);
            Assert.AreEqual(10, e.Coins);
            Assert.AreEqual(1, changes);
        }

        [Test]
        public void Add_IgnoresNonPositive()
        {
            var e = new Economy();
            e.Add(0);
            e.Add(-5);
            Assert.AreEqual(0, e.Coins);
        }

        [Test]
        public void TrySpend_FailsWhenTooPoor_AndKeepsBalance()
        {
            var e = new Economy();
            e.Add(5);
            Assert.IsFalse(e.TrySpend(6));
            Assert.AreEqual(5, e.Coins);
        }

        [Test]
        public void TrySpend_DeductsWhenAffordable()
        {
            var e = new Economy();
            e.Add(50);
            Assert.IsTrue(e.TrySpend(50));
            Assert.AreEqual(0, e.Coins);
        }
    }

    public class UpgradeTrackTests
    {
        [Test]
        public void Cost_GrowsGeometrically()
        {
            var t = new UpgradeTrack(10, 2.0);
            var e = new Economy();
            e.Add(1000);
            Assert.AreEqual(10, t.NextCost);
            t.TryBuy(e);
            Assert.AreEqual(20, t.NextCost);
            t.TryBuy(e);
            Assert.AreEqual(40, t.NextCost);
            Assert.AreEqual(1000 - 10 - 20, e.Coins);
        }

        [Test]
        public void TryBuy_FailsWithoutFunds_LevelUnchanged()
        {
            var t = new UpgradeTrack(10, 2.0);
            Assert.IsFalse(t.TryBuy(new Economy()));
            Assert.AreEqual(0, t.Level);
        }

        [Test]
        public void TryBuy_StopsAtMaxLevel()
        {
            var t = new UpgradeTrack(1, 1.0, maxLevel: 2);
            var e = new Economy();
            e.Add(100);
            Assert.IsTrue(t.TryBuy(e));
            Assert.IsTrue(t.TryBuy(e));
            Assert.IsFalse(t.TryBuy(e));
            Assert.IsTrue(t.IsMaxed);
            Assert.AreEqual(98, e.Coins);
        }
    }

    public class BuildProgressTests
    {
        static BuildPlan Plan() => new BuildPlan("Test", floors: 3, workPerFloor: 10, completionBonus: 5);

        [Test]
        public void FloorsAppear_AtEachThreshold()
        {
            var p = new BuildProgress(Plan());
            var floors = new List<int>();
            p.FloorBuilt += floors.Add;

            p.Add(9);
            Assert.AreEqual(0, p.FloorsBuilt);
            p.Add(1);
            Assert.AreEqual(1, p.FloorsBuilt);
            p.Add(15);
            CollectionAssert.AreEqual(new[] { 1, 2 }, floors);
        }

        [Test]
        public void BigHit_RaisesEveryCrossedFloor()
        {
            var p = new BuildProgress(Plan());
            var floors = new List<int>();
            p.FloorBuilt += floors.Add;
            p.Add(25);
            CollectionAssert.AreEqual(new[] { 1, 2 }, floors);
        }

        [Test]
        public void Add_IsClampedToRemainingWork_AndCompletesOnce()
        {
            var p = new BuildProgress(Plan());
            var completed = 0;
            p.Completed += () => completed++;

            Assert.AreEqual(30, p.Add(1000));
            Assert.IsTrue(p.IsComplete);
            Assert.AreEqual(0, p.Add(5));
            Assert.AreEqual(1, completed);
            Assert.AreEqual(1f, p.Fraction);
        }

        [Test]
        public void Add_IgnoresNonPositiveWork()
        {
            var p = new BuildProgress(Plan());
            Assert.AreEqual(0, p.Add(0));
            Assert.AreEqual(0, p.Add(-3));
            Assert.AreEqual(0, p.Work);
        }
    }

    public class BalanceTests
    {
        [Test]
        public void Plans_GetBiggerAndMoreRewarding()
        {
            for (var i = 1; i < Balance.Plans.Length; i++)
            {
                Assert.Greater(Balance.Plans[i].TotalWork, Balance.Plans[i - 1].TotalWork, Balance.Plans[i].Name);
                Assert.Greater(Balance.Plans[i].CompletionBonus, Balance.Plans[i - 1].CompletionBonus, Balance.Plans[i].Name);
            }
        }

        [Test]
        public void FirstHammerUpgrade_IsAffordableFromFirstBuilding()
        {
            // The first building alone must earn enough to buy the first upgrade, otherwise the game stalls.
            var first = Balance.Plans[0];
            var earned = first.TotalWork + first.CompletionBonus;
            Assert.GreaterOrEqual(earned, Balance.NewHammerTrack().NextCost);
        }
    }

    public class NumberFormatTests
    {
        [TestCase(0, "0")]
        [TestCase(999, "999")]
        [TestCase(9999, "9999")]
        [TestCase(12_340, "12.3K")]
        [TestCase(2_500_000, "2.5M")]
        public void Compact_FormatsLargeNumbers(long value, string expected)
        {
            Assert.AreEqual(expected, NumberFormat.Compact(value));
        }
    }
}

namespace Game.Tests
{
    public class EnergyMeterTests
    {
        // Small round numbers: 10 energy, 2 per action, rest after 1s at 4/s, no regen while acting, resume at 50%.
        static EnergyMeter Meter(float activeRegen = 0f) =>
            new EnergyMeter(new EnergySettings(max: 10f, cost: 2f, fullRegen: 4f, restDelay: 1f, activeRegen: activeRegen, resumeFraction: 0.5f));

        [Test]
        public void StartsFull_AndSpendingReducesIt()
        {
            var m = Meter();
            Assert.AreEqual(1f, m.Fraction);
            Assert.IsTrue(m.TrySpend());
            Assert.AreEqual(8f, m.Value);
        }

        [Test]
        public void DoesNotRegenerateWhileStillActing()
        {
            var m = Meter();
            m.TrySpend();
            m.Tick(0.5f);   // shorter than the rest delay
            Assert.AreEqual(8f, m.Value, 1e-4);
        }

        [Test]
        public void RegeneratesAfterRestDelay()
        {
            var m = Meter();
            m.TrySpend();
            m.Tick(1f);     // rest delay reached but no time elapsed beyond it yet
            m.Tick(0.5f);   // 0.5s of full regen at 4/s = +2
            Assert.AreEqual(10f, m.Value, 1e-4);
        }

        [Test]
        public void ActiveRegen_AppliesBeforeTheRestDelay()
        {
            var m = Meter(activeRegen: 2f);
            m.TrySpend();
            m.Tick(0.5f);
            Assert.AreEqual(9f, m.Value, 1e-4);
        }

        [Test]
        public void RunningDry_LocksUntilResumeFraction()
        {
            var m = Meter();
            for (var i = 0; i < 5; i++) Assert.IsTrue(m.TrySpend());   // 10 -> 0
            Assert.IsTrue(m.IsExhausted);
            Assert.IsFalse(m.TrySpend());

            m.Tick(1f);       // rested, nothing regenerated yet
            m.Tick(0.5f);     // +2: value 2, still below the 5 needed
            Assert.IsTrue(m.IsExhausted);
            m.Tick(1f);       // +4: value 6, above 50%
            Assert.IsFalse(m.IsExhausted);
            Assert.IsTrue(m.TrySpend());
        }

        [Test]
        public void ExhaustsWhenLeftoverCannotPayForAnotherAction()
        {
            var m = new EnergyMeter(new EnergySettings(5f, 2f, 4f, 1f, 0f, 0.5f));
            m.TrySpend();
            m.TrySpend();   // 1 energy left, cost is 2
            Assert.IsTrue(m.IsExhausted);
        }

        [Test]
        public void Refill_FullyRestoresAndUnlocks()
        {
            var m = Meter();
            for (var i = 0; i < 5; i++) m.TrySpend();
            m.Refill();
            Assert.AreEqual(1f, m.Fraction);
            Assert.IsFalse(m.IsExhausted);
            Assert.IsTrue(m.CanSpend);
        }

        [Test]
        public void NeverExceedsMax()
        {
            var m = Meter();
            m.Tick(100f);
            Assert.AreEqual(10f, m.Value);
        }
    }

    public class BoostTimerTests
    {
        [Test]
        public void CountsDown_AndEnds()
        {
            var b = new BoostTimer();
            b.Activate(2f);
            Assert.IsTrue(b.IsActive);
            b.Tick(1.5f);
            Assert.IsTrue(b.IsActive);
            b.Tick(1f);
            Assert.IsFalse(b.IsActive);
            Assert.AreEqual(0f, b.Remaining);
        }

        [Test]
        public void ActivatingAgain_NeverShortensIt()
        {
            var b = new BoostTimer();
            b.Activate(10f);
            b.Activate(3f);
            Assert.AreEqual(10f, b.Remaining);
        }
    }

    public class ColaBalanceTests
    {
        [Test]
        public void ColaPrice_GrowsWithHammerLevel_AndStartsCheap()
        {
            Assert.AreEqual(60, Balance.ColaPrice(0));
            for (var l = 1; l <= 25; l++) Assert.Greater(Balance.ColaPrice(l), Balance.ColaPrice(l - 1));
        }

        [Test]
        public void PlayerCanHitAFewTimesBeforeTiring()
        {
            // Sanity for the feel: a full bar must allow a decent burst, but not an endless one.
            var m = new EnergyMeter(Balance.PlayerEnergy);
            var hits = 0;
            while (m.TrySpend()) hits++;
            Assert.That(hits, Is.InRange(15, 40));
        }
    }
}
