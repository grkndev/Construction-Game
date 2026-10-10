using Game.Core;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// A stray cat or dog wandering the district. When the player comes close it is happy (hearts, a small present), follows them for a few seconds and
    /// then trots off. Built from primitives, animated procedurally.
    /// </summary>
    public sealed class Pet : MonoBehaviour
    {
        const float Speed = 2.2f, GreetRadius = 2.7f, FollowSeconds = 9f, MaxLifetime = 100f;

        GameController game;
        Transform player;
        District district;
        Transform body;
        Transform[] legs;
        Transform tail;
        CharacterController walker;
        float stuckTime;
        int stuckCount;
        Vector3 target;
        float wait, age, followTime;
        float phase;
        bool greeted;
        bool leaving;

        public static Pet Spawn(Vector3 position, District owner, ToonPalette p, GameController controller, Transform playerTransform)
        {
            var root = new GameObject("Gezgin hayvan");
            root.transform.SetParent(owner.transform, false);
            root.transform.position = position;
            var pet = root.AddComponent<Pet>();
            pet.game = controller;
            pet.player = playerTransform;
            pet.district = owner;
            pet.Build(p, cat: Random.value < 0.5f);
            pet.walker = Walk.AddBody(root, 0.5f, 1.4f);
            Walk.IgnorePlayer(pet.walker, playerTransform);
            pet.target = position;
            pet.wait = Random.Range(0.5f, 2f);
            return pet;
        }

        void Build(ToonPalette p, bool cat)
        {
            var coat = cat ? p.cone : p.trunk;
            var size = cat ? 1f : 1.25f;
            body = new GameObject("Body").transform;
            body.SetParent(transform, false);
            body.localScale = Vector3.one * size;

            // Lying along its walking direction (local z).
            var torso = Prim.Capsule(body, "Torso", new Vector3(0f, 0.55f, 0f), new Vector3(0.55f, 0.45f, 0.55f), coat).transform;
            torso.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Prim.Sphere(body, "Head", new Vector3(0f, 0.85f, 0.6f), Vector3.one * 0.52f, coat);
            Prim.Cube(body, "Snout", new Vector3(0f, 0.78f, 0.86f), new Vector3(0.22f, 0.16f, 0.18f), p.white);
            Prim.Sphere(body, "Nose", new Vector3(0f, 0.84f, 0.96f), Vector3.one * 0.08f, p.slate);
            Prim.Sphere(body, "EyeL", new Vector3(-0.13f, 0.93f, 0.82f), Vector3.one * 0.07f, p.eye);
            Prim.Sphere(body, "EyeR", new Vector3(0.13f, 0.93f, 0.82f), Vector3.one * 0.07f, p.eye);
            foreach (var x in new[] { -0.17f, 0.17f })
            {
                var ear = Prim.Cube(body, "Ear", new Vector3(x, cat ? 1.18f : 1.02f, 0.55f), cat ? new Vector3(0.14f, 0.22f, 0.08f) : new Vector3(0.12f, 0.3f, 0.12f), coat).transform;
                ear.localRotation = Quaternion.Euler(0f, 0f, cat ? 0f : (x < 0f ? 25f : -25f));   // cats: pointy ears; dogs: floppy
            }

            tail = new GameObject("Tail").transform;
            tail.SetParent(body, false);
            tail.localPosition = new Vector3(0f, 0.65f, -0.62f);
            Prim.Cylinder(tail, "TailStem", new Vector3(0f, 0.28f, -0.1f), new Vector3(0.1f, cat ? 0.34f : 0.2f, 0.1f), coat).transform.localRotation = Quaternion.Euler(-25f, 0f, 0f);

            legs = new Transform[4];
            var spots = new[] { new Vector2(-0.2f, 0.32f), new Vector2(0.2f, 0.32f), new Vector2(-0.2f, -0.32f), new Vector2(0.2f, -0.32f) };
            for (var i = 0; i < 4; i++)
            {
                legs[i] = new GameObject("Leg").transform;
                legs[i].SetParent(body, false);
                legs[i].localPosition = new Vector3(spots[i].x, 0.4f, spots[i].y);
                Prim.Cylinder(legs[i], "Paw", new Vector3(0f, -0.2f, 0f), new Vector3(0.14f, 0.2f, 0.14f), coat);
            }
        }

        void Update()
        {
            age += Time.deltaTime;
            phase += Time.deltaTime;
            if (age > MaxLifetime && !leaving) Leave();

            var near = Vector3.ProjectOnPlane(player.position - transform.position, Vector3.up).magnitude;
            if (!greeted && !leaving && near < GreetRadius) Greet();

            var moving = false;
            if (greeted && !leaving)
            {
                followTime += Time.deltaTime;
                if (followTime > FollowSeconds) Leave();
                else
                {
                    // Trots after the player, stopping a little way off.
                    var away = transform.position - player.position;
                    away.y = 0f;
                    target = player.position + (away.sqrMagnitude > 0.01f ? away.normalized : Vector3.right) * 2f;
                }
            }

            var to = target - transform.position;
            to.y = 0f;
            var speed = greeted && !leaving ? Speed * 1.5f : Speed;
            if (to.magnitude > 0.25f)
            {
                stuckTime = Walk.Step(transform, walker, to.normalized * Mathf.Min(speed * Time.deltaTime, to.magnitude)) ? 0f : stuckTime + Time.deltaTime;
                if (stuckTime > 0.8f)
                {
                    stuckTime = 0f;
                    wait = 0f;      // blocked: choose somewhere else
                    target = transform.position;
                    if (++stuckCount >= 2)
                    {
                        stuckCount = 0;
                        Walk.Unstick(transform, walker, player.position, district);
                    }
                }
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to), 8f * Time.deltaTime);
                moving = true;
            }
            else if (leaving)
            {
                Destroy(gameObject);
                return;
            }
            else if (!greeted)
            {
                wait -= Time.deltaTime;
                if (wait <= 0f) PickTarget();
            }
            Animate(moving);
        }

        void Greet()
        {
            greeted = true;
            followTime = 0f;
            game.CurrentDistrict?.Fx?.Hearts(transform.position + Vector3.up * 2.2f);
            game.GrantCoins(transform.position + Vector3.up * 1.6f, game.PetReward);
            game.PlaySfx(SfxKind.CoinLand);
        }

        void Leave()
        {
            leaving = true;
            // Off towards the nearest edge of the play area, then gone.
            var local = transform.position - district.Center;
            var edge = new Vector3(Mathf.Sign(local.x) * (district.HalfSize + 6f), 0f, Mathf.Clamp(local.z, -district.HalfSize, district.HalfSize));
            target = district.Center + edge;
        }

        void PickTarget()
        {
            wait = Random.Range(1.5f, 4f);
            if (SpawnPoints.TryFind(district, 3f, null, out var point)) target = point;
        }

        void Animate(bool moving)
        {
            var trot = moving ? Mathf.Sin(phase * 14f) : 0f;
            legs[0].localRotation = Quaternion.Euler(trot * 35f, 0f, 0f);
            legs[3].localRotation = Quaternion.Euler(trot * 35f, 0f, 0f);
            legs[1].localRotation = Quaternion.Euler(-trot * 35f, 0f, 0f);
            legs[2].localRotation = Quaternion.Euler(-trot * 35f, 0f, 0f);
            body.localPosition = new Vector3(0f, moving ? Mathf.Abs(trot) * 0.08f : 0f, 0f);
            tail.localRotation = Quaternion.Euler(0f, Mathf.Sin(phase * (greeted ? 14f : 4f)) * 30f, 0f);
        }
    }
}
