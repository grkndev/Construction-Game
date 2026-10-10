using System;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// Moves walking characters (helpers, villagers, strays) through the world's colliders instead of through everything: a CharacterController
    /// slides along walls, buildings, decorations and other walkers. The player is left out of this (they pass through crowds) so nobody can pin
    /// them down.
    /// </summary>
    public static class Walk
    {
        /// <summary>Gives a walker a body (radius and height in the object's own units). Returns it.</summary>
        public static CharacterController AddBody(GameObject walker, float radius, float height)
        {
            var body = walker.AddComponent<CharacterController>();
            body.radius = radius;
            body.height = height;
            body.center = new Vector3(0f, height * 0.5f, 0f);
            body.stepOffset = 0.15f;
            body.skinWidth = 0.04f;
            return body;
        }

        /// <summary>Walkers and the player never block each other.</summary>
        public static void IgnorePlayer(CharacterController body, Transform player)
        {
            if (body == null || player == null) return;
            var playerBody = player.GetComponent<CharacterController>();
            if (playerBody != null) Physics.IgnoreCollision(body, playerBody);
        }

        /// <summary>
        /// Moves by <paramref name="delta"/> (flat). True when it got most of the way; false when something was in the way, so the caller can give up
        /// or pick another spot.
        /// </summary>
        public static bool Step(Transform walker, CharacterController body, Vector3 delta)
        {
            delta.y = 0f;
            var before = walker.position;
            if (body != null && body.enabled) body.Move(delta + Vector3.down * 0.05f);
            else walker.position += delta;
            var moved = walker.position - before;
            moved.y = 0f;
            return moved.magnitude >= delta.magnitude * 0.3f;
        }

        static readonly Collider[] hits = new Collider[24];

        /// <summary>
        /// True when a walker of the given radius could stand at the point: no building, decoration or tree there, and (when
        /// avoidWalkers is set) nobody else standing on it. The player never counts, they pass through everybody.
        /// </summary>
        public static bool IsFree(Vector3 point, float radius, Collider self = null, bool avoidWalkers = true)
        {
            var count = Physics.OverlapSphereNonAlloc(new Vector3(point.x, 0.95f, point.z), radius, hits, ~0, QueryTriggerInteraction.Ignore);
            for (var i = 0; i < count; i++)
            {
                var hit = hits[i];
                if (hit == self) continue;
                if (hit is CharacterController)
                {
                    if (avoidWalkers && hit.GetComponent<Game.Player.PlayerController>() == null) return false;
                    continue;
                }
                if (hit.bounds.max.y < 0.3f) continue;   // the ground itself
                return false;
            }
            return true;
        }

        /// <summary>
        /// A nearby free place for a walker that is wedged in (between a wall and a villager, in a pocket of a decoration): rings of growing radius
        /// around the walker, the directions closest to the preferred one first. False when nothing is free within the search.
        /// </summary>
        public static bool FindEscape(Vector3 from, Vector3 prefer, Func<Vector3, bool> isFree, out Vector3 spot)
        {
            var towards = prefer - from;
            towards.y = 0f;
            var baseAngle = towards.sqrMagnitude > 1e-4f ? Mathf.Atan2(towards.z, towards.x) : 0f;
            foreach (var radius in new[] { 1.4f, 2.2f, 3.2f, 4.4f })
            {
                // 16 directions ordered from the preferred one outwards (0, +1, -1, +2, -2 ...).
                for (var k = 0; k < 16; k++)
                {
                    var step = (k + 1) / 2 * (k % 2 == 1 ? 1 : -1);
                    var angle = baseAngle + step * Mathf.PI * 2f / 16f;
                    var candidate = from + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                    if (!isFree(candidate)) continue;
                    spot = candidate;
                    return true;
                }
            }
            spot = from;
            return false;
        }

        /// <summary>Lifts a wedged walker out: moves it to the nearest free place (preferring the way it was heading). True when it moved.</summary>
        public static bool Unstick(Transform walker, CharacterController body, Vector3 prefer, District district)
        {
            var radius = body != null ? Mathf.Max(0.3f, body.bounds.extents.x) + 0.15f : 0.6f;
            bool Free(Vector3 p)
            {
                if (district != null && (district.Clamp(p) - p).sqrMagnitude > 0.01f) return false;   // not outside the play area
                return IsFree(p, radius, body);
            }
            if (!FindEscape(walker.position, prefer, Free, out var spot)) return false;
            if (body != null) body.enabled = false;
            walker.position = new Vector3(spot.x, walker.position.y, spot.z);
            if (body != null) body.enabled = true;
            return true;
        }
    }
}
