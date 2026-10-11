using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// Animates the primitive-built worker procedurally (bob while walking, hammer swing, squash on hit)
    /// so we don't need rigging or an Animator for placeholder art.
    /// </summary>
    public sealed class WorkerVisual : MonoBehaviour
    {
        const float DefaultSwingDuration = 0.24f;

        [SerializeField] Transform body;
        [SerializeField] Transform hammerPivot;
        [SerializeField] Transform aura;     // spinning ring at the feet while the cola level bonus is active
        [SerializeField] Material[] headMaterials;   // hammer head material per band (see HammerLook), stone first

        float move;
        float tired;        // 0..1 smoothed
        bool isTired;
        bool isBuffed;
        float swingTime = -1f;
        float swingDuration = DefaultSwingDuration;
        float leanSmooth;
        Core.IdleEmote emote;
        float emoteTime;
        int hops;                 // hops left in a cheer (Cheer() sets several)
        float restAngle = RestHammerAngle;   // hammer angle while idle (smoothed)
        Vector3 bodyBasePos;

        Transform hammerHead;
        Vector3 hammerHeadBaseScale;
        MeshRenderer shirtRenderer, hatRenderer;   // the merged body parts that SetLook recolours

        void Awake()
        {
            // The body is eight little primitives; one mesh per colour draws the same figure with a third fewer draws (and shadow casters).
            var shirt = PartMaterial("Torso");
            var hat = PartMaterial("Hat");
            foreach (var (material, meshRenderer) in MeshCombiner.Combine(body))
            {
                if (material == shirt) shirtRenderer = meshRenderer;
                if (material == hat) hatRenderer = meshRenderer;
            }
            if (shirtRenderer == hatRenderer) shirtRenderer = hatRenderer = null;   // the same colour in one mesh cannot be recoloured apart: keep the original outfit

            bodyBasePos = body.localPosition;
            hammerHead = hammerPivot.Find("Head");
            if (hammerHead != null) hammerHeadBaseScale = hammerHead.localScale;
        }

        /// <summary>The hammer head grows and changes material as the hammer is upgraded.</summary>
        public void SetHammerTier(int tier)
        {
            if (hammerHead == null) return;
            hammerHead.localScale = hammerHeadBaseScale * HammerLookScale(tier);
            if (headMaterials == null || headMaterials.Length == 0) return;
            hammerHead.GetComponent<Renderer>().sharedMaterial = headMaterials[Mathf.Min(Core.HammerLook.Band(tier), headMaterials.Length - 1)];
        }

        static float HammerLookScale(int tier) => Core.HammerLook.HeadScale(tier);

        /// <summary>Gives a helper their own shirt and hat colour (null keeps the original).</summary>
        public void SetLook(Material shirt, Material hat)
        {
            if (shirt != null && shirtRenderer != null) shirtRenderer.sharedMaterial = shirt;
            if (hat != null && hatRenderer != null) hatRenderer.sharedMaterial = hat;
        }

        Material PartMaterial(string part)
        {
            var found = body.Find(part);
            return found != null ? found.GetComponent<Renderer>().sharedMaterial : null;
        }

        /// <summary>0 = standing, 1 = full speed.</summary>
        public void SetMove(float amount01) => move = Mathf.Lerp(move, amount01, 12f * Time.deltaTime);

        /// <summary>Starts a hammer swing that lasts <paramref name="seconds"/> (the caller matches it to the hit pace).</summary>
        public void Swing(float seconds = DefaultSwingDuration)
        {
            swingDuration = Mathf.Max(0.08f, seconds);
            swingTime = 0f;
        }

        /// <summary>A short idle gesture: look about, rest the hammer on the shoulder (until the next move) or hop once.</summary>
        public void Emote(Core.IdleEmote kind)
        {
            emote = kind;
            emoteTime = 0f;
            if (kind == Core.IdleEmote.Hop) hops = 1;
        }

        /// <summary>Celebrates: three hops with the hammer raised.</summary>
        public void Cheer()
        {
            emote = Core.IdleEmote.Hop;
            emoteTime = 0f;
            hops = 3;
        }

        /// <summary>Slumps forward and pants while the player's energy is exhausted.</summary>
        public void SetTired(bool value) => isTired = value;

        /// <summary>Shows the golden ring while the temporary level bonus is on.</summary>
        public void SetBuffed(bool value) => isBuffed = value;

        const float RestHammerAngle = -35f;
        bool restPosed;   // the standing pose has been written; nothing to do until something moves again

        // Standing still, not tired, no gesture, no swing, no glowing ring and the smoothed values have settled: the pose would come out identical every frame.
        bool AtRest() =>
            move < 0.002f && emote == Core.IdleEmote.None && swingTime < 0f && !isTired && tired < 0.002f && !isBuffed
            && (aura == null || !aura.gameObject.activeSelf)
            && Mathf.Abs(restAngle - RestHammerAngle) < 0.05f && Mathf.Abs(leanSmooth) < 0.05f;

        void Update()
        {
            // Most characters stand around most of the time (waiting for work, resting); skip the whole pose maths for them.
            if (AtRest())
            {
                if (!restPosed)
                {
                    restPosed = true;
                    move = 0f; tired = 0f; leanSmooth = 0f; restAngle = RestHammerAngle;
                    body.localPosition = bodyBasePos;
                    body.localScale = Vector3.one;
                    body.localRotation = Quaternion.identity;
                    hammerPivot.localRotation = Quaternion.Euler(RestHammerAngle, 0f, 0f);
                }
                return;
            }
            restPosed = false;

            if (move > 0.25f && emote != Core.IdleEmote.None && emote != Core.IdleEmote.Hop) emote = Core.IdleEmote.None;   // walking ends a rest or a look

            var hop = 0f;
            var look = 0f;
            var targetRest = RestHammerAngle;
            if (emote != Core.IdleEmote.None)
            {
                emoteTime += Time.deltaTime;
                switch (emote)
                {
                    case Core.IdleEmote.Look:
                        look = Mathf.Sin(emoteTime * 2.2f) * 38f;
                        break;
                    case Core.IdleEmote.Rest:
                        targetRest = -75f;   // hammer on the shoulder
                        break;
                    case Core.IdleEmote.Hop:
                        targetRest = -125f;   // hammer raised
                        var hopTime = emoteTime / 0.42f;
                        if (hopTime >= hops) { emote = Core.IdleEmote.None; hops = 0; }
                        else hop = Mathf.Abs(Mathf.Sin(hopTime * Mathf.PI)) * 0.55f;
                        break;
                }
            }
            restAngle = Mathf.Lerp(restAngle, targetRest, 10f * Time.deltaTime);

            var bob = Mathf.Abs(Mathf.Sin(Time.time * 11f)) * 0.1f * move + hop;
            var squash = 1f + Mathf.Sin(Time.time * 22f) * 0.03f * move;
            body.localPosition = bodyBasePos + Vector3.up * bob;
            body.localScale = new Vector3(1f / Mathf.Sqrt(squash), squash, 1f / Mathf.Sqrt(squash));

            tired = Mathf.MoveTowards(tired, isTired ? 1f : 0f, 4f * Time.deltaTime);
            var pant = Mathf.Sin(Time.time * 14f) * 2f * tired;

            if (aura != null)
            {
                if (aura.gameObject.activeSelf != isBuffed) aura.gameObject.SetActive(isBuffed);
                if (isBuffed) aura.Rotate(0f, 240f * Time.deltaTime, 0f, Space.Self);
            }

            // Resting pose holds the hammer up. A swing is: a quick wind-up past the resting pose (anticipation), a fast accelerating slam,
            // a small rebound off the impact, then settling back. The body leans into the slam.
            var angle = restAngle;
            var lean = emote == Core.IdleEmote.Rest ? -6f : 0f;
            if (swingTime >= 0f)
            {
                swingTime += Time.deltaTime;
                var t = swingTime / swingDuration;
                if (t >= 1f) swingTime = -1f;
                else if (t < 0.2f)
                {
                    angle = Mathf.Lerp(-35f, -115f, Core.Easing.OutCubic(t / 0.2f));
                }
                else if (t < 0.5f)
                {
                    var k = (t - 0.2f) / 0.3f;
                    angle = Mathf.Lerp(-115f, 78f, k * k);      // accelerates into the hit
                    lean = Mathf.Lerp(0f, 9f, k);
                }
                else
                {
                    var k = Core.Easing.OutCubic((t - 0.5f) / 0.5f);
                    angle = Mathf.Lerp(78f, -35f, k) + Mathf.Sin(k * Mathf.PI) * -14f;   // rebound, then back to rest
                    lean = Mathf.Lerp(9f, 0f, k);
                }
            }
            leanSmooth = Mathf.Lerp(leanSmooth, lean, 25f * Time.deltaTime);
            body.localRotation = Quaternion.Euler(tired * 18f + pant + leanSmooth, look, 0f);
            hammerPivot.localRotation = Quaternion.Euler(angle, 0f, 0f);
        }
    }
}
