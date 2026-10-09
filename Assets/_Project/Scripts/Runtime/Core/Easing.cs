using UnityEngine;

namespace Game.Core
{
    /// <summary>Tiny tween helpers (the DOTween package is not available on OpenUPM anymore).</summary>
    public static class Easing
    {
        /// <summary>Overshoots past 1 and settles back: the "pop" used for floors appearing.</summary>
        public static float OutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            t = Mathf.Clamp01(t) - 1f;
            return 1f + c3 * t * t * t + c1 * t * t;
        }

        public static float OutCubic(float t)
        {
            t = 1f - Mathf.Clamp01(t);
            return 1f - t * t * t;
        }
    }
}
