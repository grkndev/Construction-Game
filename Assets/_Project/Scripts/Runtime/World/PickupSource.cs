using UnityEngine;

namespace Game.World
{
    /// <summary>Something that drops collectables in a district (cans, chests); the screen-edge arrow asks it where the nearest one is.</summary>
    public abstract class PickupSource : MonoBehaviour
    {
        /// <summary>The nearest collectable to the player and its flat distance; false when there is none.</summary>
        public abstract bool TryNearest(out Vector3 position, out float distance);

        /// <summary>False while collecting makes no sense (e.g. the pockets are full), so no arrow is shown.</summary>
        public virtual bool Wanted => true;
    }
}
