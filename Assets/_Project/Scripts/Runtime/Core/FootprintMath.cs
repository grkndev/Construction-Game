using UnityEngine;

namespace Game.Core
{
    /// <summary>Small geometry helpers for axis-aligned rectangular footprints (buildings stand on the ground axis-aligned).</summary>
    public static class FootprintMath
    {
        /// <summary>How far from the centre of a rectangle with half sizes <paramref name="half"/> the edge lies along the (flat) direction <paramref name="dir"/>.</summary>
        public static float EdgeDistance(Vector2 half, Vector2 dir)
        {
            var d = dir.normalized;
            var tx = Mathf.Abs(d.x) > 1e-5f ? half.x / Mathf.Abs(d.x) : float.MaxValue;
            var ty = Mathf.Abs(d.y) > 1e-5f ? half.y / Mathf.Abs(d.y) : float.MaxValue;
            return Mathf.Min(tx, ty);
        }

        /// <summary>True when a point lies inside the rectangle (centred at the origin) grown by <paramref name="margin"/>.</summary>
        public static bool Inside(Vector2 half, Vector2 point, float margin = 0f) =>
            Mathf.Abs(point.x) <= half.x + margin && Mathf.Abs(point.y) <= half.y + margin;
    }
}
