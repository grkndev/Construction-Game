using Game.Core;
using UnityEngine;

namespace Game.World
{
    /// <summary>A hired helper: walks to its spot around the active plot and hammers on a timer.</summary>
    public sealed class AutoWorker : MonoBehaviour
    {
        const float Speed = 3.6f;

        [SerializeField] WorkerVisual visual;

        GameController game;
        Character character;
        BuildSite site;
        int slot;
        float timer;

        public void Initialize(GameController owner)
        {
            game = owner;
            character = GetComponent<Character>();
            timer = Random.value * Balance.WorkerHitInterval;   // desynchronise helpers so hits don't stack up
        }

        void OnDestroy() => site?.ReleaseWorker();

        void Update()
        {
            // Out of energy: stand and catch your breath (the Character component refills it over time).
            if (character.Energy.IsExhausted)
            {
                visual.SetMove(0f);
                return;
            }

            // Pick (or re-pick after the plot is finished) the unfinished plot with the fewest helpers.
            if (site == null || site.IsComplete)
            {
                site?.ReleaseWorker();
                site = game != null ? game.PickSiteForWorker(transform.position) : null;
                if (site != null) slot = site.ClaimSlot();
            }
            if (site == null)
            {
                visual.SetMove(0f);
                return;
            }

            var target = site.WorkSlot(slot);
            var to = target - transform.position;
            to.y = 0f;

            if (to.magnitude > 0.2f)
            {
                transform.position += to.normalized * Mathf.Min(Speed * character.SpeedMultiplier * Time.deltaTime, to.magnitude);
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to), 12f * Time.deltaTime);
                visual.SetMove(1f);
                return;
            }

            visual.SetMove(0f);
            var look = site.transform.position - transform.position;
            look.y = 0f;
            if (look.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(look), 10f * Time.deltaTime);

            timer += Time.deltaTime * character.SpeedMultiplier;
            if (timer < Balance.WorkerHitInterval) return;

            var point = Vector3.Lerp(transform.position, site.transform.position, 0.55f) + Vector3.up * 1.2f;
            var result = game.TryHit(site, Balance.WorkerPower(game.EffectiveLevel), point, character);
            if (result == HitResult.Hit)
            {
                timer = 0f;
                visual.Swing();
            }
            else
            {
                timer = Balance.WorkerHitInterval - 0.3f;   // couldn't hit (tired or plot finished): check back soon
            }
        }
    }
}
