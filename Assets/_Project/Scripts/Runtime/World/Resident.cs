using Game.Core;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// A villager who moved into a finished building. Built from primitives; stands near their home, strolls a little,
    /// waves now and then, and shows a red alert while the building has a problem. No collider: nobody gets blocked.
    /// </summary>
    public sealed class Resident : MonoBehaviour
    {
        const float WanderRadius = 1.0f;   // small: the villager keeps to a patch of ground in front of their home, away from where helpers work
        const float WalkSpeed = 1.1f;

        Vector3 home;
        Vector3 target;
        Transform body;
        Transform arm;
        CharacterController walker;
        float stuckTime;
        int stuckCount;
        District district;
        GameObject alert;
        float wait;
        float waveTime;
        bool problem;
        float phase;

        public static Resident Spawn(Transform parent, Vector3 homePosition, ToonPalette palette, int variant, Transform player)
        {
            var go = new GameObject("Resident");
            go.transform.SetParent(parent, false);
            go.transform.position = homePosition;
            var resident = go.AddComponent<Resident>();
            resident.Build(palette, variant);
            resident.district = parent.GetComponent<District>();
            resident.walker = Walk.AddBody(go, 0.3f, 1.7f);   // solid: villagers do not walk through walls, decorations or each other
            Walk.IgnorePlayer(resident.walker, player);
            resident.home = resident.target = homePosition;
            resident.wait = Random.Range(0.5f, 3f);
            resident.phase = Random.value * 6.28f;
            return resident;
        }

        void Build(ToonPalette p, int variant)
        {
            body = new GameObject("Body").transform;
            body.SetParent(transform, false);
            var shirt = p.walls[variant % p.walls.Length];
            Prim.Capsule(body, "Torso", new Vector3(0f, 0.62f, 0f), new Vector3(0.5f, 0.45f, 0.5f), shirt);
            Prim.Sphere(body, "Head", new Vector3(0f, 1.3f, 0f), Vector3.one * 0.5f, p.skin);
            Prim.Cube(body, "Hair", new Vector3(0f, 1.52f, 0f), new Vector3(0.46f, 0.14f, 0.46f), p.trunk);

            var pivot = new GameObject("Arm").transform;
            pivot.SetParent(body, false);
            pivot.localPosition = new Vector3(0.3f, 0.95f, 0f);
            Prim.Cube(pivot, "Sleeve", new Vector3(0f, -0.2f, 0f), new Vector3(0.12f, 0.42f, 0.12f), p.skin);
            arm = pivot;

            alert = new GameObject("Alert");
            alert.transform.SetParent(transform, false);
            alert.transform.localPosition = new Vector3(0f, 2.4f, 0f);
            alert.AddComponent<Billboard>();
            Prim.Sphere(alert.transform, "Bubble", Vector3.zero, Vector3.one * 0.7f, p.roof);
            Prim.Cube(alert.transform, "Bar", new Vector3(0f, 0.08f, -0.3f), new Vector3(0.1f, 0.3f, 0.06f), p.window);
            Prim.Cube(alert.transform, "Dot", new Vector3(0f, -0.18f, -0.3f), new Vector3(0.1f, 0.1f, 0.06f), p.window);
            alert.SetActive(false);
        }

        /// <summary>Stops strolling and waves for a moment (a finished building: the new neighbour is happy).</summary>
        public void Cheer()
        {
            if (problem) return;
            target = transform.position;
            waveTime = 2.4f;
            wait = 3f;
        }

        /// <summary>True while the home has a problem: stop strolling, show the alert, wave for help.</summary>
        public void SetProblem(bool value)
        {
            problem = value;
            alert.SetActive(value);
            if (value) target = transform.position;
        }

        void Update()
        {
            phase += Time.deltaTime;

            if (problem)
            {
                arm.localRotation = Quaternion.Euler(0f, 0f, 150f + Mathf.Sin(phase * 9f) * 25f);
                body.localPosition = new Vector3(0f, Mathf.Abs(Mathf.Sin(phase * 4f)) * 0.08f, 0f);
                alert.transform.localPosition = new Vector3(0f, 2.4f + Mathf.Sin(phase * 5f) * 0.1f, 0f);
                return;
            }

            var to = target - transform.position;
            to.y = 0f;
            if (to.magnitude > 0.08f)
            {
                stuckTime = Walk.Step(transform, walker, to.normalized * (WalkSpeed * Time.deltaTime)) ? 0f : stuckTime + Time.deltaTime;
                if (stuckTime > 0.8f)
                {
                    stuckTime = 0f;
                    target = transform.position;   // something is in the way: stop here and look around instead of pushing on
                    if (++stuckCount >= 2)
                    {
                        stuckCount = 0;
                        Walk.Unstick(transform, walker, home, district);   // wedged: go back to the front door
                    }
                }
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to), 8f * Time.deltaTime);
                body.localPosition = new Vector3(0f, Mathf.Abs(Mathf.Sin(phase * 8f)) * 0.06f, 0f);
                arm.localRotation = Quaternion.Euler(Mathf.Sin(phase * 8f) * 30f, 0f, 0f);
                return;
            }

            body.localPosition = Vector3.zero;
            if (waveTime > 0f)
            {
                waveTime -= Time.deltaTime;
                arm.localRotation = Quaternion.Euler(0f, 0f, 150f + Mathf.Sin(phase * 10f) * 20f);
            }
            else
            {
                arm.localRotation = Quaternion.identity;
            }

            wait -= Time.deltaTime;
            if (wait > 0f) return;

            // Next: wave in place or stroll to a new spot near home.
            if (Random.value < 0.4f)
            {
                waveTime = 1.4f;
                wait = Random.Range(2.5f, 5f);
            }
            else
            {
                for (var attempt = 0; attempt < 6; attempt++)
                {
                    var offset = Random.insideUnitCircle * WanderRadius;
                    var candidate = home + new Vector3(offset.x, 0f, offset.y);
                    if (!Walk.IsFree(candidate, 0.6f, walker)) continue;   // not into a wall, a helper or a decoration
                    target = candidate;
                    break;
                }
                wait = Random.Range(1.5f, 4f);
            }
        }
    }
}
