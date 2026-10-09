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
            transform.rotation = cam.rotation;
        }
    }
}
