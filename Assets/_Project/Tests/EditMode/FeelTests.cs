using NUnit.Framework;
using UnityEngine;
using Game.Core;

namespace Game.Tests
{
    public class FeelTests
    {
        // ---- CoinBatcher

        [Test]
        public void CoinBatcher_HoldsCoinsUntilTheIntervalPasses_ThenSendsThemAsOneBatch()
        {
            var batcher = new CoinBatcher(0.4f);
            batcher.Add(5, Vector3.zero);
            batcher.Add(7, Vector3.one);
            Assert.IsFalse(batcher.Tick(0.3f, out _, out _));
            Assert.IsTrue(batcher.Tick(0.15f, out var coins, out var where));
            Assert.AreEqual(12, coins);
            Assert.AreEqual(Vector3.one, where, "the batch leaves from the latest hit");
            Assert.AreEqual(0, batcher.Pending);
        }

        [Test]
        public void CoinBatcher_SendsNothing_WhenNothingWasEarned()
        {
            var batcher = new CoinBatcher(0.4f);
            Assert.IsFalse(batcher.Tick(1f, out var coins, out _));
            Assert.AreEqual(0, coins);
        }

        [Test]
        public void CoinBatcher_IgnoresNonPositiveAmounts_AndCanBeCleared()
        {
            var batcher = new CoinBatcher(0.4f);
            batcher.Add(0, Vector3.zero);
            batcher.Add(-3, Vector3.zero);
            Assert.AreEqual(0, batcher.Pending);
            batcher.Add(9, Vector3.zero);
            batcher.Clear();
            Assert.IsFalse(batcher.Tick(1f, out _, out _));
        }

        // ---- FloorPitch

        [Test]
        public void FloorPitch_StartsAtNormalPitch_AndClimbsWithEveryFloor()
        {
            Assert.AreEqual(1f, FloorPitch.Multiplier(1), 1e-4f);
            for (var floor = 2; floor <= 10; floor++)
                Assert.Greater(FloorPitch.Multiplier(floor), FloorPitch.Multiplier(floor - 1), $"floor {floor}");
        }

        [Test]
        public void FloorPitch_StaysWithinTwoOctaves_AndHandlesBadInput()
        {
            Assert.LessOrEqual(FloorPitch.Multiplier(500), 4f + 1e-3f);
            Assert.AreEqual(1f, FloorPitch.Multiplier(0), 1e-4f);
            Assert.AreEqual(1f, FloorPitch.Multiplier(-4), 1e-4f);
        }

        [Test]
        public void FloorPitch_SixthFloorIsAnOctaveUp()
        {
            Assert.AreEqual(2f, FloorPitch.Multiplier(6), 1e-3f);
        }

        // ---- CeremonyQueue

        static CeremonyRequest Req(CeremonyTier tier, string title = "x") => new CeremonyRequest(CeremonyKind.Building, tier, title, "", 2f);

        [Test]
        public void CeremonyQueue_PlaysOneAtATime()
        {
            var queue = new CeremonyQueue();
            queue.Request(Req(CeremonyTier.Small, "a"));
            queue.Request(Req(CeremonyTier.Small, "b"));
            Assert.IsTrue(queue.TryNext(out var first));
            Assert.AreEqual("a", first.Title);
            Assert.IsFalse(queue.TryNext(out _), "busy until Finish");
            queue.Finish();
            Assert.IsTrue(queue.TryNext(out var second));
            Assert.AreEqual("b", second.Title);
        }

        [Test]
        public void CeremonyQueue_BiggerOnesGoFirst_OldestFirstAmongEquals()
        {
            var queue = new CeremonyQueue();
            queue.Request(Req(CeremonyTier.Small, "s1"));
            queue.Request(Req(CeremonyTier.Medium, "m1"));
            queue.Request(Req(CeremonyTier.Medium, "m2"));
            queue.TryNext(out var a);
            Assert.AreEqual("m1", a.Title);
            queue.Finish();
            queue.TryNext(out var b);
            Assert.AreEqual("m2", b.Title);
        }

        [Test]
        public void CeremonyQueue_DropsSmallOnesThatWouldOnlyPileUp()
        {
            var queue = new CeremonyQueue();
            for (var i = 0; i < 10; i++) queue.Request(Req(CeremonyTier.Small, i.ToString()));
            Assert.AreEqual(CeremonyQueue.MaxWaitingSmall, queue.Waiting);

            queue.Clear();
            queue.Request(Req(CeremonyTier.Large, "big"));
            queue.Request(Req(CeremonyTier.Small, "small"));
            Assert.AreEqual(1, queue.Waiting, "a small banner is pointless while a big one is waiting");
        }

        [Test]
        public void CeremonyQueue_KeepsEveryBigOne()
        {
            var queue = new CeremonyQueue();
            for (var i = 0; i < 5; i++) queue.Request(Req(CeremonyTier.Large, i.ToString()));
            Assert.AreEqual(5, queue.Waiting);
        }

        // ---- FeelSettings

        [Test]
        public void ShakeSetting_DefaultsToOn_AndRemembersTheChoice()
        {
            const string key = "feel.shake";
            var had = PlayerPrefs.HasKey(key);
            var old = PlayerPrefs.GetInt(key, 1);
            try
            {
                PlayerPrefs.DeleteKey(key);
                FeelSettings.ResetCache();
                Assert.IsTrue(FeelSettings.ShakeEnabled);

                FeelSettings.ShakeEnabled = false;
                FeelSettings.ResetCache();
                Assert.IsFalse(FeelSettings.ShakeEnabled, "read back from PlayerPrefs");

                FeelSettings.ShakeEnabled = true;
                FeelSettings.ResetCache();
                Assert.IsTrue(FeelSettings.ShakeEnabled);
            }
            finally
            {
                if (had) PlayerPrefs.SetInt(key, old); else PlayerPrefs.DeleteKey(key);
                FeelSettings.ResetCache();
            }
        }

        [Test]
        public void CameraKick_ShakesOnlyWhenTheSettingIsOn()
        {
            const string key = "feel.shake";
            var had = PlayerPrefs.HasKey(key);
            var old = PlayerPrefs.GetInt(key, 1);
            var go = new GameObject("cam");
            try
            {
                var rig = go.AddComponent<Game.World.IsoCameraFollow>();
                FeelSettings.ShakeEnabled = false;
                rig.Kick(0.1f);
                Assert.AreEqual(0f, rig.ShakeStrength, "off: no shake");

                FeelSettings.ShakeEnabled = true;
                rig.Kick(0.1f);
                Assert.Greater(rig.ShakeStrength, 0f, "on: shakes");
            }
            finally
            {
                Object.DestroyImmediate(go);
                if (had) PlayerPrefs.SetInt(key, old); else PlayerPrefs.DeleteKey(key);
                FeelSettings.ResetCache();
            }
        }
    }
}
