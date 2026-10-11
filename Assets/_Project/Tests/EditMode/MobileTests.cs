using Game.UI;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public class MobileTests
    {
        [Test]
        public void SafeArea_FullScreen_StretchesToTheWholeCanvas()
        {
            SafeAreaFitter.Anchors(new Rect(0, 0, 1920, 1080), 1920, 1080, out var min, out var max);
            Assert.AreEqual(Vector2.zero, min);
            Assert.AreEqual(Vector2.one, max);
        }

        [Test]
        public void SafeArea_NotchOnTheLeft_InsetsOnlyThatSide()
        {
            // An iPhone in landscape (2622 x 1206 px): the notch side loses ~186 px, the home indicator 63 px at the bottom.
            SafeAreaFitter.Anchors(new Rect(186, 63, 2622 - 186 - 186, 1206 - 63), 2622, 1206, out var min, out var max);
            Assert.AreEqual(186f / 2622f, min.x, 1e-5f);
            Assert.AreEqual(63f / 1206f, min.y, 1e-5f);
            Assert.AreEqual((2622f - 186f) / 2622f, max.x, 1e-5f);
            Assert.AreEqual(1f, max.y, 1e-5f);
        }

        [Test]
        public void SafeArea_NoData_FallsBackToTheWholeScreen()
        {
            SafeAreaFitter.Anchors(new Rect(0, 0, 0, 0), 1920, 1080, out var min, out var max);
            Assert.AreEqual(Vector2.zero, min);
            Assert.AreEqual(Vector2.one, max);
            SafeAreaFitter.Anchors(new Rect(0, 0, 100, 100), 0, 0, out min, out max);
            Assert.AreEqual(Vector2.one, max);
        }
    }
}
