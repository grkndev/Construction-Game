using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Game.Core;
using Game.World;

namespace Game.Tests
{
    public class WorkerLifeTests
    {
        // ---- HammerLook

        [Test]
        public void HammerLook_BandsFollowTheTierInGroupsOfFive()
        {
            Assert.AreEqual(0, HammerLook.Band(1));
            Assert.AreEqual(0, HammerLook.Band(5));
            Assert.AreEqual(1, HammerLook.Band(6));
            Assert.AreEqual(4, HammerLook.Band(Balance.MaxHammerLevel));
        }

        [Test]
        public void HammerLook_BandsStayInRange_ForAnyTier()
        {
            foreach (var tier in new[] { -3, 0, 1, 12, 25, 99 })
                Assert.That(HammerLook.Band(tier), Is.InRange(0, HammerLook.Bands - 1), $"tier {tier}");
        }

        [Test]
        public void HammerLook_HeadGrowsFromNormalToBig()
        {
            Assert.AreEqual(1f, HammerLook.HeadScale(1), 1e-4f);
            Assert.AreEqual(1.5f, HammerLook.HeadScale(Balance.MaxHammerLevel), 1e-3f);
            for (var tier = 2; tier <= Balance.MaxHammerLevel; tier++)
                Assert.Greater(HammerLook.HeadScale(tier), HammerLook.HeadScale(tier - 1), $"tier {tier}");
        }

        // ---- IdlePlanner

        [Test]
        public void IdlePlanner_StartsWithAPause_ThenStrolls_ThenPausesAgain()
        {
            var planner = new IdlePlanner(7);
            Assert.AreEqual(IdleAction.Pause, planner.Action);
            Assert.IsFalse(planner.Tick(0.5f), "still pausing");
            Assert.IsTrue(planner.Tick(IdlePlanner.MaxPause), "pause over");
            Assert.AreEqual(IdleAction.Wander, planner.Action);
            Assert.AreEqual(IdleEmote.None, planner.Emote, "no gestures while walking");
            planner.Arrived();
            Assert.AreEqual(IdleAction.Pause, planner.Action);
        }

        [Test]
        public void IdlePlanner_GivesUpAStrollThatTakesTooLong()
        {
            var planner = new IdlePlanner(3);
            planner.Tick(IdlePlanner.MaxPause);
            Assert.AreEqual(IdleAction.Wander, planner.Action);
            Assert.IsTrue(planner.Tick(IdlePlanner.MaxWander + 0.1f));
            Assert.AreEqual(IdleAction.Pause, planner.Action);
        }

        [Test]
        public void IdlePlanner_PausesLastBetweenTheLimits_AndIsDeterministicPerSeed()
        {
            var a = new IdlePlanner(42);
            var b = new IdlePlanner(42);
            for (var i = 0; i < 20; i++)
            {
                var seconds = 0f;
                while (a.Action == IdleAction.Pause) { a.Tick(0.1f); seconds += 0.1f; }
                while (b.Action == IdleAction.Pause) b.Tick(0.1f);
                Assert.That(seconds, Is.InRange(IdlePlanner.MinPause - 0.01f, IdlePlanner.MaxPause + 0.2f));
                a.Arrived();
                b.Arrived();
                Assert.AreEqual(a.Emote, b.Emote);
            }
        }

        [Test]
        public void IdlePlanner_ArrivingWhilePausedChangesNothing()
        {
            var planner = new IdlePlanner(1);
            var emote = planner.Emote;
            planner.Arrived();
            Assert.AreEqual(IdleAction.Pause, planner.Action);
            Assert.AreEqual(emote, planner.Emote);
        }

        // ---- solid world: footprints, decorations, walkers

        [Test]
        public void EdgeDistance_IsTheWallAlongTheDirection()
        {
            var half = new Vector2(4f, 2f);
            Assert.AreEqual(4f, FootprintMath.EdgeDistance(half, Vector2.right), 1e-4f);
            Assert.AreEqual(2f, FootprintMath.EdgeDistance(half, Vector2.up), 1e-4f);
            Assert.AreEqual(2f * Mathf.Sqrt(2f), FootprintMath.EdgeDistance(half, new Vector2(1f, 1f)), 1e-3f, "diagonal hits the long wall's corner region first");
        }

        [Test]
        public void SpotsOutsideAWideBuilding_AreAlwaysOutsideIt()
        {
            // A fixed 3.6 units from the centre put a villager inside the factory (half width 4.3); the wall distance does not.
            var factory = new Vector2(4.3f, 2.8f);
            for (var degrees = 0; degrees < 360; degrees += 15)
            {
                var dir = new Vector2(Mathf.Cos(degrees * Mathf.Deg2Rad), Mathf.Sin(degrees * Mathf.Deg2Rad));
                var spot = dir.normalized * (FootprintMath.EdgeDistance(factory, dir) + 1.6f);
                Assert.IsFalse(FootprintMath.Inside(factory, spot), $"{degrees} degrees");
            }
        }

        [Test]
        public void EveryDecoration_IsSolid_AndLeavesItsHangoutSpotsFree()
        {
            var palette = AssetDatabase.LoadAssetAtPath<ToonPalette>("Assets/_Project/Data/ToonPalette.asset");
            Assert.IsNotNull(palette);
            for (var kind = 0; kind < DecorationKits.Count; kind++)
            {
                var parent = new GameObject("test").transform;
                try
                {
                    var model = DecorationKits.Build(parent, kind, palette, out var spots);
                    var colliders = model.GetComponentsInChildren<Collider>();
                    Assert.Greater(colliders.Length, 0, $"decoration {kind} has no collider: people could walk through it");
                    Assert.Greater(spots.Length, 0);
                    foreach (var spot in spots)
                    {
                        var body = spot + Vector3.up * 0.9f;   // a walker's middle
                        foreach (var collider in colliders)
                            Assert.AreNotEqual(body, collider.ClosestPoint(body), $"decoration {kind}: hangout spot {spot} is inside a collider");
                    }
                }
                finally
                {
                    Object.DestroyImmediate(parent.gameObject);
                }
            }
        }

        [Test]
        public void HelpersAreSolid_ButThePlayersBodyComesFromTheSceneBuilder()
        {
            var helper = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/AutoWorker.prefab");
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Worker.prefab");
            Assert.IsNotNull(helper.GetComponent<CharacterController>(), "helpers need a body to be blocked by walls and each other");
            Assert.IsNull(playerPrefab.GetComponent<CharacterController>(), "the scene builder adds the player's body (a second one would clash)");
        }

        // ---- getting unstuck

        [Test]
        public void FindEscape_PicksTheNearestFreePlace_TowardsWhereTheWalkerWasHeading()
        {
            // Everything on the +x side is blocked (a wall), the -x side is free.
            var found = Walk.FindEscape(Vector3.zero, new Vector3(5f, 0f, 0f), p => p.x < -0.5f, out var spot);
            Assert.IsTrue(found);
            Assert.Less(spot.x, -0.5f);
            Assert.GreaterOrEqual(spot.magnitude, 1.39f, "it moves a real distance, not a nudge back into the squeeze");
            Assert.LessOrEqual(spot.magnitude, 2.3f, "and takes the nearest ring that works");
        }

        [Test]
        public void FindEscape_PrefersTheHeadingDirection_WhenItIsFree()
        {
            Walk.FindEscape(Vector3.zero, new Vector3(0f, 0f, 9f), p => true, out var spot);
            Assert.Greater(spot.z, 1.3f);
            Assert.AreEqual(0f, spot.x, 0.01f);
        }

        [Test]
        public void FindEscape_GivesUp_WhenNothingIsFree()
        {
            Assert.IsFalse(Walk.FindEscape(Vector3.zero, Vector3.right, p => false, out _));
        }

        [Test]
        public void FindEscape_SearchesOutwards_WhenTheNearRingsAreAllBlocked()
        {
            var found = Walk.FindEscape(Vector3.zero, Vector3.forward, p => p.magnitude > 3f, out var spot);
            Assert.IsTrue(found);
            Assert.Greater(spot.magnitude, 3f);
        }
    }
}
