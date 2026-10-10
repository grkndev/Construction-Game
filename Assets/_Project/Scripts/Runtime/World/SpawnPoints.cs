using System;
using UnityEngine;

namespace Game.World
{
    /// <summary>Finds a spot inside a district's play area that is clear of the building plots (cans, events, strolling helpers all need one).</summary>
    public static class SpawnPoints
    {
        /// <param name="isFree">Extra test for the caller (distance to other cans, the player, decorations...); null = no extra test.</param>
        public static bool TryFind(District district, float minPlotDistance, Func<Vector3, bool> isFree, out Vector3 point, int attempts = 30)
        {
            var reach = district.HalfSize * 0.85f;
            for (var attempt = 0; attempt < attempts; attempt++)
            {
                point = district.Clamp(district.Center + new Vector3(UnityEngine.Random.Range(-reach, reach), 0f, UnityEngine.Random.Range(-reach, reach)));
                var clear = true;
                foreach (var site in district.Sites)
                    if ((site.transform.position - point).magnitude < minPlotDistance) { clear = false; break; }
                if (clear && !district.IsNearDecoration(point, 4.6f) && (isFree == null || isFree(point))) return true;
            }
            point = default;
            return false;
        }
    }
}
