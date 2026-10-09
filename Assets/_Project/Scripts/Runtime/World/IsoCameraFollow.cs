using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// Fixed-angle isometric-style follow camera. Kept deliberately simple; swapping to Cinemachine or a
    /// third-person rig later only means replacing this one component.
    /// </summary>
    public sealed class IsoCameraFollow : MonoBehaviour
    {
        [SerializeField] Transform target;
        [SerializeField] Vector3 offset = new Vector3(-19f, 21f, -19f);
        [SerializeField] float smoothTime = 0.18f;

        Vector3 velocity;

        void Start() => Snap();

        void LateUpdate()
        {
            if (target == null) return;
            transform.position = Vector3.SmoothDamp(transform.position, target.position + offset, ref velocity, smoothTime);
        }

        public void Snap()
        {
            if (target == null) return;
            transform.position = target.position + offset;
            transform.rotation = Quaternion.LookRotation(-offset);
        }
    }
}
