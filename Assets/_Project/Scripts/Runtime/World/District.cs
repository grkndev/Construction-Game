using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// One neighbourhood: a square play area (the ground goes on well past it, so the camera never sees an edge, except on a coast) with
    /// its plots, where the player spawns and where hired workers appear. The root GameObject is only switched on during a visit; the map
    /// screen is a separate picture of the country (<see cref="MapPoint"/> is where this district sits on it).
    /// </summary>
    public sealed class District : MonoBehaviour
    {
        [SerializeField] BuildSite[] sites;
        [SerializeField] Transform playerSpawn;
        [SerializeField] Transform workerSpawn;
        [SerializeField] Transform mapPoint;
        [SerializeField] float halfSize = 26f;     // the play area is a square of 2 * halfSize
        [SerializeField] float borderInset = 0.8f; // the player stops this far inside the square, in front of the fence

        public BuildSite[] Sites => sites;
        public Vector3 Center => transform.position;
        public Vector3 PlayerSpawn => playerSpawn.position;
        public Vector3 WorkerSpawn => workerSpawn.position;
        public float HalfSize => halfSize;
        /// <summary>The district's centre on the country map (world space), where its card is shown.</summary>
        public Vector3 MapPoint => mapPoint.position;

        public void SetVisible(bool visible)
        {
            if (gameObject.activeSelf != visible) gameObject.SetActive(visible);
        }

        /// <summary>The nearest point of the play area (world space, y kept).</summary>
        public Vector3 Clamp(Vector3 world)
        {
            var inside = ClampToSquare(new Vector2(world.x - Center.x, world.z - Center.z), halfSize - borderInset);
            return new Vector3(Center.x + inside.x, world.y, Center.z + inside.y);
        }

        public static Vector2 ClampToSquare(Vector2 local, float limit) =>
            new Vector2(Mathf.Clamp(local.x, -limit, limit), Mathf.Clamp(local.y, -limit, limit));
    }
}
