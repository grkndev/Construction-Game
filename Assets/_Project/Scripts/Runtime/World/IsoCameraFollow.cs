using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// Fixed-angle isometric-style follow camera. Kept deliberately simple; swapping to Cinemachine or a
    /// third-person rig later only means replacing this one component.
    /// <see cref="SetView"/> puts the camera on another target at once (the dashboard map or a district: they are far apart, so a flight
    /// between them would only show empty sea; the HUD covers the cut with a short fade instead).
    /// </summary>
    public sealed class IsoCameraFollow : MonoBehaviour
    {
        [SerializeField] Transform target;
        [SerializeField] Vector3 offset = new Vector3(-19f, 21f, -19f);
        [SerializeField] float smoothTime = 0.18f;

        Vector3 velocity;
        float scale = 1f;

        void Start() => Snap();

        void LateUpdate()
        {
            if (target == null) return;
            transform.position = Vector3.SmoothDamp(transform.position, target.position + offset * scale, ref velocity, smoothTime);
        }

        /// <summary>Looks at <paramref name="newTarget"/> from <paramref name="viewScale"/> times the normal distance, immediately.</summary>
        public void SetView(Transform newTarget, float viewScale)
        {
            target = newTarget;
            scale = viewScale;
            Snap();
        }

        public void Snap()
        {
            if (target == null) return;
            transform.position = target.position + offset * scale;
            transform.rotation = Quaternion.LookRotation(-offset);
            velocity = Vector3.zero;
        }
    }
}
