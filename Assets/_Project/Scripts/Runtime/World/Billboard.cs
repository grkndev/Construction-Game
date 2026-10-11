using UnityEngine;

namespace Game.World
{
    /// <summary>Keeps world-space bars/labels facing the camera plane.</summary>
    public sealed class Billboard : MonoBehaviour
    {
        Transform cam;

        void LateUpdate()
        {
            if (cam == null)
            {
                var main = Camera.main;
                if (main == null) return;
                cam = main.transform;
            }
            // Only write when it differs: a bar on a standing character already faces the camera, and a write dirties the transform.
            // (Compared with the real rotation, not a cached one: the parent turns, which turns the bar with it.)
            var rotation = cam.rotation;
            if (transform.rotation != rotation) transform.rotation = rotation;
        }
    }
}
