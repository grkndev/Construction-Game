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
        [SerializeField] Transform aura;     // spinning ring at the feet while the cola level bonus is active

        float move;
        float tired;        // 0..1 smoothed
        bool isTired;
        bool isBuffed;
        float swingTime = -1f;
        Vector3 bodyBasePos;

        void Awake() => bodyBasePos = body.localPosition;

        /// <summary>0 = standing, 1 = full speed.</summary>
        public void SetMove(float amount01) => move = Mathf.Lerp(move, amount01, 12f * Time.deltaTime);

        public void Swing() => swingTime = 0f;

        /// <summary>Slumps forward and pants while the player's energy is exhausted.</summary>
        public void SetTired(bool value) => isTired = value;

        /// <summary>Shows the golden ring while the temporary level bonus is on.</summary>
        public void SetBuffed(bool value) => isBuffed = value;

        void Update()
        {
            var bob = Mathf.Abs(Mathf.Sin(Time.time * 11f)) * 0.1f * move;
            var squash = 1f + Mathf.Sin(Time.time * 22f) * 0.03f * move;
            body.localPosition = bodyBasePos + Vector3.up * bob;
            body.localScale = new Vector3(1f / Mathf.Sqrt(squash), squash, 1f / Mathf.Sqrt(squash));

            tired = Mathf.MoveTowards(tired, isTired ? 1f : 0f, 4f * Time.deltaTime);
            var pant = Mathf.Sin(Time.time * 14f) * 2f * tired;
            body.localRotation = Quaternion.Euler(tired * 18f + pant, 0f, 0f);

            if (aura != null)
            {
                if (aura.gameObject.activeSelf != isBuffed) aura.gameObject.SetActive(isBuffed);
                if (isBuffed) aura.Rotate(0f, 240f * Time.deltaTime, 0f, Space.Self);
            }

            // Resting pose holds the hammer up; a swing goes up-back then slams down.
            var angle = -35f;
            if (swingTime >= 0f)
            {
                swingTime += Time.deltaTime;
                var t = swingTime / SwingDuration;
                if (t >= 1f) swingTime = -1f;
                else angle = t < 0.35f
                    ? Mathf.Lerp(-35f, -110f, Core.Easing.OutCubic(t / 0.35f))
                    : Mathf.Lerp(-110f, 70f, Core.Easing.OutCubic((t - 0.35f) / 0.65f));
            }
            hammerPivot.localRotation = Quaternion.Euler(angle, 0f, 0f);
        }
    }
}
