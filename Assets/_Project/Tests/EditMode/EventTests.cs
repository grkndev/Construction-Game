using System;
using System.Collections.Generic;
using NUnit.Framework;
using Game.Core;

namespace Game.Tests
{
    public class EventTests
    {
        [Test]
        public void EventPicker_NeverPicksAKindThatIsAlreadyAround()
        {
            var random = new Random(1);
            for (var i = 0; i < 200; i++)
            {
                Assert.AreNotEqual(EventKind.Chest, EventPicker.Pick(random, true, false, false));
                Assert.AreNotEqual(EventKind.Birds, EventPicker.Pick(random, false, true, false));
                Assert.AreNotEqual(EventKind.Pet, EventPicker.Pick(random, false, false, true));
            }
        }

        [Test]
        public void EventPicker_ReturnsNothing_WhenAllThreeAreAround()
        {
            Assert.IsNull(EventPicker.Pick(new Random(2), true, true, true));
        }

        [Test]
        public void EventPicker_PicksTheOnlyFreeKind()
        {
            Assert.AreEqual(EventKind.Pet, EventPicker.Pick(new Random(3), true, true, false));
            Assert.AreEqual(EventKind.Birds, EventPicker.Pick(new Random(4), true, false, true));
        }

        [Test]
        public void EventPicker_IsWeighted_BirdsMostOftenAndPetsLeast()
        {
            var random = new Random(5);
            var counts = new Dictionary<EventKind, int> { { EventKind.Chest, 0 }, { EventKind.Birds, 0 }, { EventKind.Pet, 0 } };
            for (var i = 0; i < 3000; i++) counts[EventPicker.Pick(random, false, false, false).Value]++;
            Assert.Greater(counts[EventKind.Birds], counts[EventKind.Chest]);
            Assert.Greater(counts[EventKind.Chest], counts[EventKind.Pet]);
            Assert.Greater(counts[EventKind.Pet], 300, "pets still show up");
        }

        [Test]
        public void EventPicker_FirstEventComesSooner_AndDelaysStayInRange()
        {
            var random = new Random(6);
            for (var i = 0; i < 100; i++)
            {
                Assert.That(EventPicker.Delay(random, true), Is.InRange(EventPicker.FirstMin, EventPicker.FirstMax));
                Assert.That(EventPicker.Delay(random, false), Is.InRange(EventPicker.LaterMin, EventPicker.LaterMax));
            }
            Assert.Less(EventPicker.FirstMax, EventPicker.LaterMax);
        }

        [Test]
        public void Rewards_AreFloored_AndScaleWithTheDistrict()
        {
            Assert.GreaterOrEqual(Balance.ChestReward(0, 1, 0, 1), 40);
            Assert.GreaterOrEqual(Balance.ChestReward(0, 1, 0, 16), 40 * 16);
            Assert.Greater(Balance.ChestReward(10, 6, 3, 16), Balance.ChestReward(0, 1, 0, 16), "a stronger district pays more");
        }

        [Test]
        public void Rewards_AreOrdered_ChestOverPetOverBirdTip()
        {
            const int hammer = 12, workers = 6, speed = 3, scale = 64;
            Assert.Greater(Balance.ChestReward(hammer, workers, speed, scale), Balance.PetReward(hammer, workers, speed, scale));
            Assert.Greater(Balance.PetReward(hammer, workers, speed, scale), Balance.BirdTip(hammer, workers, speed, scale));
        }
    }
}
