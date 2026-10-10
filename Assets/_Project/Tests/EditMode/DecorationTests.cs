using NUnit.Framework;
using Game.Core;

namespace Game.Tests
{
    public class DecorationTests
    {
        static Economy Rich() { var e = new Economy(); e.Add(1_000_000); return e; }

        [Test]
        public void Decorations_CannotBeOrdered_BeforeTheDistrictIsFinished()
        {
            var progress = new DecorationProgress();
            var economy = Rich();
            Assert.IsFalse(progress.CanOrder(districtComplete: false, economy, 1));
            Assert.IsFalse(progress.TryOrder(districtComplete: false, economy, 1));
            Assert.AreEqual(1_000_000, economy.Coins, "nothing is spent");
        }

        [Test]
        public void Ordering_SpendsTheCoins_AndStartsTheBuild()
        {
            var progress = new DecorationProgress();
            var economy = Rich();
            Assert.IsTrue(progress.TryOrder(true, economy, 4));
            Assert.AreEqual(1_000_000 - Balance.DecorationCost(0, 4), economy.Coins);
            Assert.IsTrue(progress.Ordered);
            Assert.IsFalse(progress.TryOrder(true, economy, 4), "one at a time");
        }

        [Test]
        public void Ordering_Fails_WhenTheDistrictCannotAfford()
        {
            var progress = new DecorationProgress();
            var poor = new Economy();
            poor.Add(Balance.DecorationCost(0, 1) - 1);
            Assert.IsFalse(progress.TryOrder(true, poor, 1));
            Assert.IsFalse(progress.Ordered);
        }

        [Test]
        public void Building_TakesTheBaseTimeForOneHelper_AndFinishesExactlyOnce()
        {
            var progress = new DecorationProgress();
            progress.TryOrder(true, Rich(), 1);
            Assert.IsFalse(progress.AddWork(Balance.DecorationBuildSeconds * 0.5f));
            Assert.AreEqual(0.5f, progress.Progress, 1e-3f);
            Assert.IsTrue(progress.AddWork(Balance.DecorationBuildSeconds * 0.6f));
            Assert.AreEqual(1, progress.Built);
            Assert.IsFalse(progress.Ordered);
            Assert.AreEqual(0f, progress.Progress);
            Assert.IsFalse(progress.AddWork(5f), "nothing to build until the next order");
        }

        [Test]
        public void Helpers_BuildFaster_WhenSeveralWork()
        {
            // Each helper adds its own share of every frame, so three helpers finish in a third of the time.
            const float frame = 0.1f;
            float Seconds(int helpers)
            {
                var progress = new DecorationProgress();
                progress.TryOrder(true, Rich(), 1);
                var seconds = 0f;
                var done = false;
                while (!done && seconds < 100f)
                {
                    for (var h = 0; h < helpers && !done; h++) done = progress.AddWork(frame);
                    seconds += frame;
                }
                return seconds;
            }
            Assert.Less(Seconds(3), Seconds(1) * 0.45f);
        }

        [Test]
        public void SixDecorations_CompleteTheDistrict_AndStopFurtherOrders()
        {
            var progress = new DecorationProgress();
            var economy = Rich();
            for (var i = 0; i < Balance.DecorationCount; i++)
            {
                Assert.IsTrue(progress.TryOrder(true, economy, 1), $"order {i}");
                Assert.IsTrue(progress.AddWork(Balance.DecorationBuildSeconds + 1f));
            }
            Assert.IsTrue(progress.IsComplete);
            Assert.IsFalse(progress.CanOrder(true, economy, 1));
            Assert.AreEqual(0, progress.NextCost(1));
        }

        [Test]
        public void Costs_GrowWithEveryDecoration_AndScaleWithTheDistrict()
        {
            for (var i = 1; i < Balance.DecorationCount; i++)
                Assert.Greater(Balance.DecorationCost(i, 1), Balance.DecorationCost(i - 1, 1));
            Assert.AreEqual(Balance.DecorationCost(2, 1) * 16, Balance.DecorationCost(2, 16));
        }

        [Test]
        public void TouristIncome_StartsAtZero_AndEachDecorationAddsMoreThanTheLast()
        {
            Assert.AreEqual(0.0, Balance.TouristIncome(0, 1));
            for (var built = 2; built <= Balance.DecorationCount; built++)
            {
                var step = Balance.TouristIncome(built, 1) - Balance.TouristIncome(built - 1, 1);
                var previousStep = Balance.TouristIncome(built - 1, 1) - Balance.TouristIncome(built - 2, 1);
                Assert.Greater(step, previousStep, $"decoration {built}");
            }
        }

        [Test]
        public void TheNextDecoration_PaysForItselfInAFewMinutes()
        {
            // Income from what is built should afford the next price within ~3 minutes (otherwise the loop would feel like a wall).
            for (var built = 1; built < Balance.DecorationCount; built++)
            {
                var seconds = Balance.DecorationCost(built, 1) / Balance.TouristIncome(built, 1);
                Assert.Less(seconds, 180.0, $"after {built} decorations");
            }
        }

        [Test]
        public void SaveRoundTrip_KeepsTheDecorationState()
        {
            var state = new DistrictState(Balance.Districts[0]);
            state.Decorations.Restore(3, true, 0.4f);
            var restored = new DistrictState(Balance.Districts[0]);
            restored.Restore(state.ToSave());
            Assert.AreEqual(3, restored.Decorations.Built);
            Assert.IsTrue(restored.Decorations.Ordered);
            Assert.AreEqual(0.4f, restored.Decorations.Progress, 1e-4f);
        }

        [Test]
        public void OlderSaves_WithoutDecorationFields_StillLoad()
        {
            var restored = new DistrictState(Balance.Districts[0]);
            restored.Restore(new DistrictSave());   // all fields default, as a save from before decorations
            Assert.AreEqual(0, restored.Decorations.Built);
            Assert.IsFalse(restored.Decorations.Ordered);
        }

        [Test]
        public void Restore_ClampsNonsense()
        {
            var progress = new DecorationProgress();
            progress.Restore(99, true, 5f);
            Assert.AreEqual(Balance.DecorationCount, progress.Built);
            Assert.IsFalse(progress.Ordered, "nothing left to order");
            progress.Restore(-4, true, 7f);
            Assert.AreEqual(0, progress.Built);
            Assert.Less(progress.Progress, 1f);
        }
    }
}
