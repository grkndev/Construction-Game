using System.Collections.Generic;
using Game.Core;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// A small flock: either flying over the district, or sitting on the ground until somebody comes close, when it bursts into the air (feathers,
    /// a chirp, a little tip for the surprise). Pure decoration with a tiny reward.
    /// </summary>
    public sealed class BirdFlock : MonoBehaviour
    {
        enum Mode { Flyby, Landed, Fleeing }

        sealed class Bird
        {
            public Transform Root, WingL, WingR;
            public Vector3 Velocity;
            public float Phase;
        }

        const int Count = 6;

        readonly List<Bird> birds = new List<Bird>();
        GameController game;
        Mode mode;
        Vector3 centre;        // where a landed flock sits
        float age, fleeTime;
        bool tipped;

        public static BirdFlock Spawn(District district, ToonPalette p, GameController owner)
        {
            var go = new GameObject("Kuş sürüsü");
            go.transform.SetParent(district.transform, false);
            var flock = go.AddComponent<BirdFlock>();
            flock.game = owner;
            var colours = new[] { p.white, p.hull, p.cone, p.concrete };
            for (var i = 0; i < Count; i++) flock.birds.Add(flock.MakeBird(go.transform, p, colours[i % colours.Length]));

            if (Random.value < 0.5f || !SpawnPoints.TryFind(district, 4f, null, out flock.centre)) flock.StartFlyby(district);
            else flock.StartLanded();
            return flock;
        }

        Bird MakeBird(Transform parent, ToonPalette p, Material colour)
        {
            var bird = new Bird { Root = new GameObject("Bird").transform, Phase = Random.value * 6.28f };
            bird.Root.SetParent(parent, false);
            Prim.Sphere(bird.Root, "Body", new Vector3(0f, 0.3f, 0f), new Vector3(0.5f, 0.42f, 0.8f), colour);
            Prim.Sphere(bird.Root, "Head", new Vector3(0f, 0.5f, 0.38f), Vector3.one * 0.34f, colour);
            Prim.Cube(bird.Root, "Beak", new Vector3(0f, 0.48f, 0.62f), new Vector3(0.1f, 0.08f, 0.2f), p.cone);
            Prim.Cube(bird.Root, "Tail", new Vector3(0f, 0.34f, -0.45f), new Vector3(0.25f, 0.06f, 0.4f), colour);
            bird.WingL = Wing(bird.Root, -1f, colour);
            bird.WingR = Wing(bird.Root, 1f, colour);
            bird.Root.localScale = Vector3.one * 1.3f;
            return bird;
        }

        static Transform Wing(Transform bird, float side, Material colour)
        {
            var pivot = new GameObject(side < 0f ? "WingL" : "WingR").transform;
            pivot.SetParent(bird, false);
            pivot.localPosition = new Vector3(side * 0.22f, 0.4f, 0f);
            Prim.Cube(pivot, "Feathers", new Vector3(side * 0.4f, 0f, 0f), new Vector3(0.8f, 0.05f, 0.45f), colour);
            return pivot;
        }

        void StartFlyby(District district)
        {
            mode = Mode.Flyby;
            var angle = Random.value * Mathf.PI * 2f;
            var dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            var side = new Vector3(-dir.z, 0f, dir.x);
            var start = district.Center - dir * (district.HalfSize + 12f) + Vector3.up * Random.Range(9f, 13f);
            for (var i = 0; i < birds.Count; i++)
            {
                var row = (i + 1) / 2;
                var lateral = (i % 2 == 0 ? 1f : -1f) * row * 1.6f;
                birds[i].Root.position = start + side * lateral - dir * row * 1.4f;
                birds[i].Root.rotation = Quaternion.LookRotation(dir);
                birds[i].Velocity = dir * Random.Range(6.5f, 8f);
            }
        }

        void StartLanded()
        {
            mode = Mode.Landed;
            foreach (var bird in birds)
            {
                var offset = Random.insideUnitCircle * 1.5f;
                bird.Root.position = centre + new Vector3(offset.x, 0f, offset.y);
                bird.Root.rotation = Quaternion.Euler(0f, Random.value * 360f, 0f);
            }
        }

        void Update()
        {
            age += Time.deltaTime;
            switch (mode)
            {
                case Mode.Flyby: UpdateFlyby(); break;
                case Mode.Landed: UpdateLanded(); break;
                default: UpdateFleeing(); break;
            }
        }

        void UpdateFlyby()
        {
            foreach (var bird in birds)
            {
                bird.Root.position += bird.Velocity * Time.deltaTime + Vector3.up * Mathf.Sin(Time.time * 3f + bird.Phase) * 0.4f * Time.deltaTime;
                Flap(bird, 14f, 38f);
            }
            if (age > 9f) Destroy(gameObject);   // long gone by then
        }

        void UpdateLanded()
        {
            foreach (var bird in birds)
            {
                // Pecking at the ground: the body dips every now and then.
                var peck = Mathf.Max(0f, Mathf.Sin(Time.time * 4f + bird.Phase * 3f) - 0.5f);
                bird.Root.localRotation = Quaternion.Euler(peck * 30f, bird.Root.localEulerAngles.y, 0f);
                Flap(bird, 0f, 4f);
            }
            if (game.AnyoneNear(centre, 4.4f) || age > 40f) Scare(tip: age <= 40f);
        }

        void Scare(bool tip)
        {
            mode = Mode.Fleeing;
            fleeTime = 0f;
            foreach (var bird in birds)
            {
                var spread = Random.insideUnitCircle.normalized;
                bird.Velocity = new Vector3(spread.x * 3.5f, Random.Range(5f, 8f), spread.y * 3.5f);
                bird.Root.rotation = Quaternion.LookRotation(new Vector3(spread.x, 0.2f, spread.y));
            }
            game.CurrentDistrict?.Fx?.Feathers(centre + Vector3.up * 0.5f);
            game.PlaySfx(SfxKind.Chirp);
            if (tip && !tipped)
            {
                tipped = true;
                game.GrantCoins(centre + Vector3.up * 1.4f, game.BirdTip);
            }
        }

        void UpdateFleeing()
        {
            fleeTime += Time.deltaTime;
            foreach (var bird in birds)
            {
                bird.Root.position += bird.Velocity * Time.deltaTime;
                bird.Velocity += Vector3.up * 1.5f * Time.deltaTime;   // keep climbing
                Flap(bird, 16f, 45f);
            }
            if (fleeTime > 2.6f) Destroy(gameObject);
        }

        static void Flap(Bird bird, float rate, float amplitude)
        {
            var angle = Mathf.Sin(Time.time * rate + bird.Phase) * amplitude;
            bird.WingL.localRotation = Quaternion.Euler(0f, 0f, angle);
            bird.WingR.localRotation = Quaternion.Euler(0f, 0f, -angle);
        }
    }
}
