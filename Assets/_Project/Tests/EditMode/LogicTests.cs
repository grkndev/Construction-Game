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
