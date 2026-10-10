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
        IdlePlanner idle;
        CharacterController body;   // makes helpers solid: they slide around buildings, decorations and each other
        float sidestepTime;         // after being blocked head-on, walk along the wall for a moment instead of pushing into it
        Vector3 sidestepDirection;
        int blockedInARow;          // consecutive "not getting closer" verdicts: after a couple the helper is lifted out of the squeeze
        float progressTimer;
        float bestDistance;
        Vector3 lastTarget;
        int slotShift;              // moves a helper to another place around the plot when its own is blocked
        float decorationTimer;
        int decorationSpot;
        Vector3 wanderTarget;

        public void Initialize(GameController owner, int number)
        {
            game = owner;
            body = GetComponent<CharacterController>();
            Walk.IgnorePlayer(body, owner.PlayerTransform);
            decorationSpot = number;   // every helper has its own place around the scaffold (two on one spot would block each other)
            character = GetComponent<Character>();
            timer = Random.value * Balance.WorkerHitInterval;   // desynchronise helpers so hits don't stack up
        }

        void OnDestroy() => site?.ReleaseWorker();

        /// <summary>What the helper is doing right now (for the -stats check that nobody is stuck): "working", "walking" or "idle".</summary>
        /// <summary>How many times this helper had to be lifted out of a squeeze (for the -stats check).</summary>
        public int Unsticks { get; private set; }

        public string Status { get; private set; } = "idle";

        // ---------- nothing to build: stroll about, look around, rest, hop ----------

        // A decoration has been ordered: help build it (the first thing a free helper does, before strolling).
        bool DecorationJob()
        {
            var job = game.PickDecorationJob();
            if (job == null) return false;
            ResetIdle();
            Status = "decorating";
            if (!WalkTo(job.WorkSlot(decorationSpot), 0.4f, 1f, out var blocked))
            {
                if (blocked) decorationSpot = (decorationSpot + 1) % 8;   // someone is standing there: try the next place
                return true;
            }

            var look = job.transform.position - transform.position;
            look.y = 0f;
            if (look.sqrMagnitude > 0.01f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(look), 10f * Time.deltaTime);

            decorationTimer += Time.deltaTime * character.SpeedMultiplier;
            if (decorationTimer < 0.55f) return true;
            decorationTimer = 0f;
            visual.Swing(Balance.WorkerSwingSeconds / character.SpeedMultiplier);
            game.CurrentDistrict?.Fx?.Dust(job.transform.position + Vector3.up * 0.6f);
            game.AddDecorationWork(0.55f);
            return true;
        }

        void Idle()
        {
            idle ??= new IdlePlanner(Random.Range(0, int.MaxValue));
            if (idle.Tick(Time.deltaTime)) PlanChanged();

            if (idle.Action == IdleAction.Wander)
            {
                if (WalkTo(wanderTarget, 0.35f, 0.6f, out var blocked) || blocked) { idle.Arrived(); PlanChanged(); }   // blocked counts as arrived: stand there
                return;
            }
            visual.SetMove(0f);
        }

        void PlanChanged()
        {
            blockedInARow = 0;   // strolling that hit a wall just ends the stroll: only jobs (a plot, a scaffold) count towards being lifted out
            if (idle.Action == IdleAction.Wander) wanderTarget = PickStrollTarget();
            else visual.Emote(idle.Emote);
        }

        // Back to work (or just arrived somewhere new): forget the plan so the next idle spell starts fresh.
        void ResetIdle()
        {
            if (idle == null) return;
            idle = null;
            visual.Emote(IdleEmote.None);
        }

        Vector3 PickStrollTarget()
        {
            if (Random.value < 0.6f && game.TryPickHangout(out var hangout) && Walk.IsFree(hangout, 0.8f, body)) return hangout;   // like to stand around the decorations
            var district = game.CurrentDistrict;
            if (district != null && SpawnPoints.TryFind(district, 6f, p => Walk.IsFree(p, 0.9f, body), out var point)) return point;
            return transform.position;   // nowhere good: stay (the plan times out and tries again)
        }

        /// <summary>Walks towards a point; true once close enough.</summary>
        bool WalkTo(Vector3 target, float stopDistance, float speedFactor, out bool blocked)
        {
            blocked = false;
            var to = target - transform.position;
            to.y = 0f;
            var distance = to.magnitude;
            if (distance <= stopDistance)
            {
                visual.SetMove(0f);
                blockedInARow = 0;
                ResetProgress(distance);
                return true;
            }

            var heading = to.normalized;
            if (sidestepTime > 0f)
            {
                // Mostly sideways, a little forward: slides round the corner of whatever is in the way.
                sidestepTime -= Time.deltaTime;
                heading = (heading * 0.35f + sidestepDirection * 0.95f).normalized;
            }
            var step = heading * Mathf.Min(Speed * speedFactor * character.SpeedMultiplier * Time.deltaTime, distance);
            Walk.Step(transform, body, step);
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to), 10f * Time.deltaTime);
            visual.SetMove(speedFactor);

            // Stuck means "not getting closer", not "not moving": pushing against a wall slides sideways, which is movement but no progress.
            if ((target - lastTarget).sqrMagnitude > 0.25f) ResetProgress(distance);
            lastTarget = target;
            progressTimer += Time.deltaTime;
            if (progressTimer >= 0.6f)
            {
                var gained = bestDistance - distance;
                bestDistance = Mathf.Min(bestDistance, distance);
                progressTimer = 0f;
                if (gained < 0.35f * speedFactor)
                {
                    blocked = true;
                    ResetProgress(distance);
                    // First try walking round the obstacle (a random side), and only when that does not help either lift the helper out.
                    sidestepTime = 0.9f;
                    var side = Random.value < 0.5f ? 1f : -1f;
                    sidestepDirection = new Vector3(-to.z, 0f, to.x).normalized * side;
                    if (++blockedInARow >= 3)
                    {
                        // Wedged (between a wall and a villager, in a pocket of a decoration): step out to the nearest free place.
                        blockedInARow = 0;
                        if (Walk.Unstick(transform, body, target, game.CurrentDistrict)) Unsticks++;
                    }
                }
                else blockedInARow = 0;
            }
            return false;
        }

        void ResetProgress(float distance)
        {
            progressTimer = 0f;
            bestDistance = distance;
        }

        /// <summary>Everybody celebrates for a moment (a building or district was finished).</summary>
        public void Cheer() => visual.Cheer();

        void Update()
        {
            // Out of energy: stand and catch your breath (the Character component refills it over time).
            if (character.Energy.IsExhausted)
            {
                visual.SetMove(0f);
                return;
            }

            // Pick (or re-pick after the plot is finished) the unfinished plot with the fewest helpers.
            if (site == null || !site.NeedsWork)
            {
                site?.ReleaseWorker();
                site = game != null ? game.PickSiteForWorker(transform.position) : null;
                if (site != null) slot = site.ClaimSlot();
            }
            if (site == null)
            {
                Status = "idle";
                if (!DecorationJob()) Idle();
                return;
            }
            ResetIdle();
            Status = "walking";

            if (!WalkTo(site.WorkSlot(slot + slotShift), 0.2f, 1f, out var blockedAtSlot))
            {
                if (blockedAtSlot) slotShift++;   // the place is taken (or behind a wall): try the next one around the plot
                return;
            }

            Status = "working";
            visual.SetMove(0f);
            var look = site.transform.position - transform.position;
            look.y = 0f;
            if (look.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(look), 10f * Time.deltaTime);

            timer += Time.deltaTime * character.SpeedMultiplier;
            if (timer < Balance.WorkerHitInterval) return;

            var point = Vector3.Lerp(transform.position, site.transform.position, 0.55f) + Vector3.up * 1.2f;
            var result = game.TryHit(site, game.WorkerPower, point, character);
            if (result == HitResult.Hit)
            {
                timer = 0f;
                visual.Swing(Balance.WorkerSwingSeconds / character.SpeedMultiplier);
            }
            else
            {
                timer = Balance.WorkerHitInterval - 0.3f;   // couldn't hit (tired or plot finished): check back soon
            }
        }
    }
}
