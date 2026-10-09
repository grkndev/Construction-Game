using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// Animates the primitive-built worker procedurally (bob while walking, hammer swing, squash on hit)
    /// so we don't need rigging or an Animator for placeholder art.
    /// </summary>
    public sealed class WorkerVisual : MonoBehaviour
    {
        const float SwingDuration = 0.24f;

        [SerializeField] Transform body;
        [SerializeField] Transform hammerPivot;

        float move;
        float swingTime = -1f;
        Vector3 bodyBasePos;

        void Awake() => bodyBasePos = body.localPosition;

        /// <summary>0 = standing, 1 = full speed.</summary>
        public void SetMove(float amount01) => move = Mathf.Lerp(move, amount01, 12f * Time.deltaTime);

        public void Swing() => swingTime = 0f;

        void Update()
        {
            var bob = Mathf.Abs(Mathf.Sin(Time.time * 11f)) * 0.1f * move;
            var squash = 1f + Mathf.Sin(Time.time * 22f) * 0.03f * move;
            body.localPosition = bodyBasePos + Vector3.up * bob;
            body.localScale = new Vector3(1f / Mathf.Sqrt(squash), squash, 1f / Mathf.Sqrt(squash));

            // Resting pose holds the hammer up; a swing goes up-back then slams down.
            var angle = -35f;
            if (swingTime >= 0f)
            {
                swingTime += Time.deltaTime;
                var t = swingTime / SwingDuration;
                if (t >= 1f) swingTime = -1f;
                else angle = t < 0.35f
                    ? Mathf.Lerp(-35f, -110f, Core.Easing.OutCubic(t / 0.35f))
                    : Mathf.Lerp(-110f, 55f, Core.Easing.OutCubic((t - 0.35f) / 0.65f));
            }
            hammerPivot.localRotation = Quaternion.Euler(angle, 0f, 0f);
        }
    }
}
