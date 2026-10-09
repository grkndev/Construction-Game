using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// Fixed-angle isometric-style follow camera. Kept deliberately simple; swapping to Cinemachine or a
    /// third-person rig later only means replacing this one component.
    /// <see cref="SetView"/> moves the camera to look at another target from farther away (the dashboard overview).
    /// </summary>
    public sealed class IsoCameraFollow : MonoBehaviour
    {
        [SerializeField] Transform target;
        [SerializeField] Vector3 offset = new Vector3(-19f, 21f, -19f);
        [SerializeField] float smoothTime = 0.18f;

        const float TransitionTime = 0.9f;

        Vector3 velocity;
        float scale = 1f, targetScale = 1f, scaleVelocity;
        float currentSmooth;

        void Start()
        {
            currentSmooth = smoothTime;
            Snap();
        }

        void LateUpdate()
        {
            if (target == null) return;
            currentSmooth = Mathf.MoveTowards(currentSmooth, smoothTime, Time.deltaTime * 0.8f);
            scale = Mathf.SmoothDamp(scale, targetScale, ref scaleVelocity, currentSmooth);
            transform.position = Vector3.SmoothDamp(transform.position, target.position + offset * scale, ref velocity, currentSmooth);
        }

        /// <summary>Looks at <paramref name="newTarget"/> from <paramref name="viewScale"/> times the normal distance.</summary>
        public void SetView(Transform newTarget, float viewScale, bool snap = false)
        {
            target = newTarget;
            targetScale = viewScale;
            if (snap)
            {
                scale = viewScale;
                scaleVelocity = 0f;
                Snap();
            }
            else
            {
                currentSmooth = TransitionTime;
            }
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
