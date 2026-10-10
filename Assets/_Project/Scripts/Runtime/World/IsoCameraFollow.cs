using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// Fixed-angle isometric-style follow camera. Kept deliberately simple; swapping to Cinemachine or a
    /// third-person rig later only means replacing this one component.
    /// <see cref="SetView"/> puts the camera on another target at once (the dashboard map or a district: they are far apart, so a flight
    /// between them would only show empty sea; the HUD covers the cut with a short fade instead).
    /// <see cref="Kick"/> and <see cref="Punch"/> are short effects on top of the follow: a hit shake and a push-in for big moments.
    /// </summary>
    public sealed class IsoCameraFollow : MonoBehaviour
    {
        [SerializeField] Transform target;
        [SerializeField] Vector3 offset = new Vector3(-19f, 21f, -19f);
        [SerializeField] float smoothTime = 0.18f;

        Vector3 velocity;
        Vector3 followed;          // where the follow wants the camera, without the effects
        float scale = 1f;
        float shake;               // current shake strength in world units, decays quickly
        float punchZoom = 1f;      // multiplies the distance while a push-in is on
        float punchLength, punchTime = -1f;

        void Start() => Snap();

        void LateUpdate()
        {
            if (target == null) return;
            followed = Vector3.SmoothDamp(followed, target.position + offset * scale * punchZoom, ref velocity, smoothTime);
            var shaken = followed;
            if (shake > 0.002f)
            {
                shaken += Random.insideUnitSphere * shake;
                shake = Mathf.MoveTowards(shake, 0f, (shake * 10f + 0.03f) * Time.unscaledDeltaTime);
            }
            transform.position = shaken;

            if (punchTime >= 0f)
            {
                punchTime += Time.unscaledDeltaTime;
                var t = punchTime / punchLength;
                // Pushes in over the first quarter, then eases back out.
                var k = t < 0.25f ? Core.Easing.OutCubic(t / 0.25f) : 1f - Core.Easing.OutCubic((t - 0.25f) / 0.75f);
                punchZoom = Mathf.Lerp(1f, punchTarget, k);
                if (t >= 1f)
                {
                    punchTime = -1f;
                    punchZoom = 1f;
                }
            }
        }

        float punchTarget = 1f;

        /// <summary>The current shake strength in world units (0 = still).</summary>
        public float ShakeStrength => shake;

        /// <summary>A short camera shake of about <paramref name="strength"/> world units (stacks up to a small limit).</summary>
        public void Kick(float strength)
        {
            if (!Core.FeelSettings.ShakeEnabled) return;   // the player can turn the shake off in the settings window
            shake = Mathf.Min(shake + strength, 0.18f);
        }

        /// <summary>Moves the camera closer (<paramref name="zoom"/> below 1) and back over <paramref name="seconds"/>.</summary>
        public void Punch(float zoom, float seconds)
        {
            punchTarget = Mathf.Clamp(zoom, 0.6f, 1f);
            punchLength = Mathf.Max(0.1f, seconds);
            punchTime = 0f;
        }

        /// <summary>Looks at <paramref name="newTarget"/> from <paramref name="viewScale"/> times the normal distance, immediately.</summary>
        public void SetView(Transform newTarget, float viewScale)
        {
            target = newTarget;
            scale = viewScale;
            shake = 0f;
            punchTime = -1f;
            punchZoom = 1f;
            Snap();
        }

        public void Snap()
        {
            if (target == null) return;
            followed = target.position + offset * scale;
            transform.position = followed;
            transform.rotation = Quaternion.LookRotation(-offset);
            velocity = Vector3.zero;
        }
    }
}
