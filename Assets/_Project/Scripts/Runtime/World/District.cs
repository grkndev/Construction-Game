using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// One neighbourhood of the island: a ground disc with three plots, where the player spawns and where hired workers appear.
    /// The root GameObject is switched on/off by the game: the dashboard shows all five, a visit shows only this one.
    /// </summary>
    public sealed class District : MonoBehaviour
    {
        [SerializeField] BuildSite[] sites;
        [SerializeField] Transform playerSpawn;
        [SerializeField] Transform workerSpawn;
        [SerializeField] float walkRadius = 13f;

        public BuildSite[] Sites => sites;
        public Vector3 Center => transform.position;
        public Vector3 PlayerSpawn => playerSpawn.position;
        public Vector3 WorkerSpawn => workerSpawn.position;
        public float WalkRadius => walkRadius;

        public void SetVisible(bool visible)
        {
            if (gameObject.activeSelf != visible) gameObject.SetActive(visible);
        }
    }
}
