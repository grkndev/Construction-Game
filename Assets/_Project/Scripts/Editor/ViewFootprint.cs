using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Where the follow camera can see on the ground (y = 0), relative to the player, for a given aspect ratio. The district builder uses it
    /// to make the ground reach beyond everything the camera can ever show, and to put scenery only where it can be seen.
    /// </summary>
    public static class ViewFootprint
    {
        public readonly struct Extents
        {
            public readonly float MinX, MaxX, MinZ, MaxZ;

            public Extents(float minX, float maxX, float minZ, float maxZ)
            {
                MinX = minX; MaxX = maxX; MinZ = minZ; MaxZ = maxZ;
            }

            public static Extents Of(Vector3 offset, float viewScale, float verticalFovDegrees, float aspect) =>
                Compute(offset, viewScale, verticalFovDegrees, aspect);
        }

        /// <summary>Distance along the camera's view direction to the farthest ground the screen can show (what a shadow distance has to cover).</summary>
        public static float FarthestDepth(Vector3 offset, float viewScale, float verticalFovDegrees, float aspect)
        {
            var origin = offset * viewScale;
            var forward = (-origin).normalized;
            var right = Vector3.Cross(Vector3.up, forward).normalized;
            var up = Vector3.Cross(forward, right);
            var tanV = Mathf.Tan(verticalFovDegrees * 0.5f * Mathf.Deg2Rad);
            var farthest = 0f;
            foreach (var sx in new[] { -1f, 1f })
            foreach (var sy in new[] { -1f, 1f })
            {
                var dir = forward + right * (sx * tanV * aspect) + up * (sy * tanV);
                if (dir.y >= -1e-3f) continue;
                var hit = origin + dir * (-origin.y / dir.y);
                farthest = Mathf.Max(farthest, Vector3.Dot(hit - origin, forward));
            }
            return farthest;
        }

        /// <param name="offset">Camera position relative to the player at scale 1 (IsoCameraFollow's offset).</param>
        public static Extents Compute(Vector3 offset, float viewScale, float verticalFovDegrees, float aspect)
        {
            var origin = offset * viewScale;
            var forward = (-origin).normalized;
            var right = Vector3.Cross(Vector3.up, forward).normalized;
            var up = Vector3.Cross(forward, right);
            var tanV = Mathf.Tan(verticalFovDegrees * 0.5f * Mathf.Deg2Rad);
            var tanH = tanV * aspect;

            float minX = 0f, maxX = 0f, minZ = 0f, maxZ = 0f;
            foreach (var sx in new[] { -1f, 1f })
            foreach (var sy in new[] { -1f, 1f })
            {
                var dir = forward + right * (sx * tanH) + up * (sy * tanV);
                // A ray that never reaches the ground is cut at a far distance.
                var t = dir.y < -1e-3f ? -origin.y / dir.y : 400f;
                var hit = origin + dir * t;
                minX = Mathf.Min(minX, hit.x); maxX = Mathf.Max(maxX, hit.x);
                minZ = Mathf.Min(minZ, hit.z); maxZ = Mathf.Max(maxZ, hit.z);
            }
            return new Extents(minX, maxX, minZ, maxZ);
        }
    }
}
