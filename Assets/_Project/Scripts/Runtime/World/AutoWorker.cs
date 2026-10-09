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
        int slot;
        float timer;

        public void Initialize(GameController owner, int slotIndex)
        {
            game = owner;
            slot = slotIndex;
            timer = Random.value * Balance.WorkerHitInterval;   // desynchronise helpers so hits don't stack up
        }

        void Update()
        {
            var site = game != null ? game.ActiveSite : null;
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
                transform.position += to.normalized * Mathf.Min(Speed * Time.deltaTime, to.magnitude);
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to), 12f * Time.deltaTime);
                visual.SetMove(1f);
                return;
            }

            visual.SetMove(0f);
            var look = site.transform.position - transform.position;
            look.y = 0f;
            if (look.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(look), 10f * Time.deltaTime);

            timer += Time.deltaTime;
            if (timer < Balance.WorkerHitInterval) return;
            timer = 0f;
            visual.Swing();
            var point = Vector3.Lerp(transform.position, site.transform.position, 0.55f) + Vector3.up * 1.2f;
            game.TryHit(site, Balance.WorkerPower(game.Hammer.Level), point);
        }
    }
}
