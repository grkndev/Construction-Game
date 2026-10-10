using System.Collections.Generic;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// The six decorations a finished district can get, built from primitives like the buildings: flower bed, bench and lamp, little park,
    /// fountain, gazebo and the golden worker statue. Each kit also says where idle helpers like to hang out around it.
    /// </summary>
    public static class DecorationKits
    {
        public const int Count = 6;

        /// <summary>Everything is about 6 units across (a radius of ~3) so a decoration fits its slot between the plots.</summary>
        public static GameObject Build(Transform parent, int kind, ToonPalette p, out Vector3[] hangouts)
        {
            var root = new GameObject($"Decoration {kind + 1}").transform;
            root.SetParent(parent, false);
            var spots = new List<Vector3>();
            switch (kind)
            {
                case 0: FlowerBed(root, p, spots); break;
                case 1: BenchAndLamp(root, p, spots); break;
                case 2: LittlePark(root, p, spots); break;
                case 3: Fountain(root, p, spots); break;
                case 4: Gazebo(root, p, spots); break;
                default: Statue(root, p, spots); break;
            }
            hangouts = spots.ToArray();
            AddColliders(root, kind);
            return root.gameObject;
        }

        // Decorations are solid: the player (a CharacterController) and every walker stop at them. Plain shapes that follow the silhouette, leaving the
        // hangout spots outside (or, for the gazebo, between the posts).
        static void AddColliders(Transform root, int kind)
        {
            switch (kind)
            {
                case 0: Cylinder(root, new Vector3(0f, 0.5f, 0f), 2.6f, 1f); break;
                case 1:
                    Box(root, new Vector3(0f, 0.6f, -1.5f), new Vector3(2.7f, 1.2f, 0.9f));
                    Box(root, new Vector3(0f, 0.6f, 1.5f), new Vector3(2.7f, 1.2f, 0.9f));
                    Cylinder(root, new Vector3(2.4f, 1.6f, 0f), 0.3f, 3.2f);
                    break;
                case 2:
                    foreach (var offset in new[] { new Vector3(-1.8f, 1f, 1.4f), new Vector3(1.9f, 1f, 1.7f), new Vector3(0.2f, 1f, -2.1f) }) Cylinder(root, offset, 0.5f, 2f);
                    Box(root, new Vector3(0f, 0.5f, 0.1f), new Vector3(2.3f, 1f, 0.8f));
                    break;
                case 3: Cylinder(root, new Vector3(0f, 0.8f, 0f), 2.9f, 1.6f); break;
                case 4:
                    for (var i = 0; i < 6; i++)
                    {
                        var a = i * Mathf.PI / 3f;
                        Cylinder(root, new Vector3(Mathf.Cos(a) * 2.3f, 1.4f, Mathf.Sin(a) * 2.3f), 0.32f, 2.8f);
                    }
                    Box(root, new Vector3(0f, 0.5f, 0.9f), new Vector3(2.5f, 1f, 0.7f));
                    break;
                default: Box(root, new Vector3(0f, 0.7f, 0f), new Vector3(3.3f, 1.4f, 3.3f)); break;
            }
        }

        static void Box(Transform root, Vector3 centre, Vector3 size)
        {
            var go = new GameObject("Collider");
            go.transform.SetParent(root, false);
            var box = go.AddComponent<BoxCollider>();
            box.center = centre;
            box.size = size;
        }

        static void Cylinder(Transform root, Vector3 centre, float radius, float height)
        {
            var go = new GameObject("Collider");
            go.transform.SetParent(root, false);
            var capsule = go.AddComponent<CapsuleCollider>();
            capsule.center = centre;
            capsule.radius = radius;
            capsule.height = Mathf.Max(height, radius * 2f);
        }

        static void FlowerBed(Transform t, ToonPalette p, List<Vector3> spots)
        {
            Prim.Cylinder(t, "Edge", new Vector3(0f, 0.12f, 0f), new Vector3(5.2f, 0.12f, 5.2f), p.stone);
            Prim.Cylinder(t, "Lawn", new Vector3(0f, 0.28f, 0f), new Vector3(4.6f, 0.1f, 4.6f), p.green);
            var colours = new[] { p.flag, p.yellow, p.roof, p.white, p.cone };
            for (var i = 0; i < 14; i++)
            {
                var angle = i * 2.4f;
                var radius = i < 8 ? 1.7f : 0.8f;
                Prim.Sphere(t, "Flower", new Vector3(Mathf.Cos(angle) * radius, 0.55f, Mathf.Sin(angle) * radius), Vector3.one * 0.5f, colours[i % colours.Length]);
            }
            Prim.Sphere(t, "Bush", new Vector3(0f, 0.7f, 0f), new Vector3(1.4f, 1.0f, 1.4f), p.leaves);
            spots.Add(new Vector3(3.4f, 0f, 0.4f));
            spots.Add(new Vector3(-3.4f, 0f, -0.4f));
        }

        static void BenchAndLamp(Transform t, ToonPalette p, List<Vector3> spots)
        {
            foreach (var z in new[] { -1.5f, 1.5f })
            {
                Prim.Cube(t, "Seat", new Vector3(0f, 0.62f, z), new Vector3(2.6f, 0.18f, 0.7f), p.crate);
                Prim.Cube(t, "Back", new Vector3(0f, 1.05f, z + (z < 0f ? 0.3f : -0.3f)), new Vector3(2.6f, 0.5f, 0.12f), p.crate);
                foreach (var x in new[] { -1.1f, 1.1f })
                    Prim.Cube(t, "Leg", new Vector3(x, 0.3f, z), new Vector3(0.14f, 0.6f, 0.5f), p.trunk);
                spots.Add(new Vector3(0f, 0f, z * 1.9f));
            }
            Prim.Cylinder(t, "LampPole", new Vector3(2.4f, 1.6f, 0f), new Vector3(0.14f, 1.6f, 0.14f), p.slate);
            Prim.Sphere(t, "Lamp", new Vector3(2.4f, 3.3f, 0f), Vector3.one * 0.7f, p.yellow);
            Prim.Cylinder(t, "LampCap", new Vector3(2.4f, 3.7f, 0f), new Vector3(0.6f, 0.06f, 0.6f), p.slate);
        }

        static void LittlePark(Transform t, ToonPalette p, List<Vector3> spots)
        {
            Prim.Cylinder(t, "Lawn", new Vector3(0f, 0.12f, 0f), new Vector3(6.4f, 0.12f, 6.4f), p.green);
            foreach (var offset in new[] { new Vector3(-1.8f, 0f, 1.4f), new Vector3(1.9f, 0f, 1.7f), new Vector3(0.2f, 0f, -2.1f) })
            {
                Prim.Cylinder(t, "Trunk", offset + new Vector3(0f, 0.8f, 0f), new Vector3(0.4f, 0.8f, 0.4f), p.trunk);
                Prim.Sphere(t, "Leaves", offset + new Vector3(0f, 2.3f, 0f), Vector3.one * 2.2f, p.leaves);
                Prim.Sphere(t, "LeavesTop", offset + new Vector3(0.1f, 3.3f, 0f), Vector3.one * 1.5f, p.leaves);
            }
            Prim.Cube(t, "Seat", new Vector3(0f, 0.55f, 0.1f), new Vector3(2.2f, 0.16f, 0.6f), p.crate);
            Prim.Cube(t, "Back", new Vector3(0f, 0.95f, 0.38f), new Vector3(2.2f, 0.45f, 0.1f), p.crate);
            spots.Add(new Vector3(-4.1f, 0f, -0.6f));   // outside the lawn: the pockets between the bench and the trees are too tight to stand in
            spots.Add(new Vector3(4.1f, 0f, -0.6f));
        }

        static void Fountain(Transform t, ToonPalette p, List<Vector3> spots)
        {
            Prim.Cylinder(t, "Basin", new Vector3(0f, 0.3f, 0f), new Vector3(5.6f, 0.3f, 5.6f), p.stone);
            Prim.Cylinder(t, "Water", new Vector3(0f, 0.6f, 0f), new Vector3(5.0f, 0.04f, 5.0f), p.sea);
            Prim.Cylinder(t, "Pillar", new Vector3(0f, 1.5f, 0f), new Vector3(0.6f, 0.9f, 0.6f), p.stone);
            Prim.Cylinder(t, "Bowl", new Vector3(0f, 2.3f, 0f), new Vector3(2.2f, 0.18f, 2.2f), p.stone);
            Prim.Cylinder(t, "BowlWater", new Vector3(0f, 2.48f, 0f), new Vector3(1.9f, 0.03f, 1.9f), p.sea);
            Prim.Cylinder(t, "Spout", new Vector3(0f, 3.0f, 0f), new Vector3(0.25f, 0.6f, 0.25f), p.stone);
            Prim.Sphere(t, "Drop", new Vector3(0f, 3.8f, 0f), Vector3.one * 0.8f, p.window);
            for (var i = 0; i < 4; i++)
            {
                var a = i * Mathf.PI * 0.5f + 0.4f;
                spots.Add(new Vector3(Mathf.Cos(a) * 3.8f, 0f, Mathf.Sin(a) * 3.8f));
            }
        }

        static void Gazebo(Transform t, ToonPalette p, List<Vector3> spots)
        {
            Prim.Cylinder(t, "Floor", new Vector3(0f, 0.18f, 0f), new Vector3(5.6f, 0.18f, 5.6f), p.crate);
            for (var i = 0; i < 6; i++)
            {
                var a = i * Mathf.PI / 3f;
                Prim.Cylinder(t, "Post", new Vector3(Mathf.Cos(a) * 2.3f, 1.5f, Mathf.Sin(a) * 2.3f), new Vector3(0.22f, 1.3f, 0.22f), p.white);
            }
            Prim.Cylinder(t, "Rim", new Vector3(0f, 2.9f, 0f), new Vector3(5.4f, 0.14f, 5.4f), p.white);
            Prim.Sphere(t, "Roof", new Vector3(0f, 3.0f, 0f), new Vector3(5.6f, 2.1f, 5.6f), p.roof);
            Prim.Cylinder(t, "Spire", new Vector3(0f, 4.2f, 0f), new Vector3(0.08f, 0.5f, 0.08f), p.scaffold);
            Prim.Sphere(t, "Knob", new Vector3(0f, 4.75f, 0f), Vector3.one * 0.36f, p.yellow);
            Prim.Cube(t, "Bench", new Vector3(0f, 0.7f, 0.9f), new Vector3(2.4f, 0.16f, 0.6f), p.trunk);
            spots.Add(new Vector3(0f, 0f, -0.2f));
            spots.Add(new Vector3(0f, 0f, 3.6f));
            spots.Add(new Vector3(-3.6f, 0f, 0f));
        }

        static void Statue(Transform t, ToonPalette p, List<Vector3> spots)
        {
            Prim.Cube(t, "Plinth", new Vector3(0f, 0.5f, 0f), new Vector3(3.2f, 1.0f, 3.2f), p.stone);
            Prim.Cube(t, "PlinthTop", new Vector3(0f, 1.1f, 0f), new Vector3(2.6f, 0.2f, 2.6f), p.concrete);
            Prim.Cube(t, "Plaque", new Vector3(0f, 0.6f, -1.62f), new Vector3(1.6f, 0.5f, 0.08f), p.yellow);
            // A golden worker raising a hammer.
            Prim.Capsule(t, "Body", new Vector3(0f, 2.3f, 0f), new Vector3(1.1f, 0.95f, 1.1f), p.yellow);
            Prim.Sphere(t, "Head", new Vector3(0f, 3.5f, 0f), new Vector3(1.0f, 0.95f, 1.0f), p.yellow);
            Prim.Sphere(t, "Hat", new Vector3(0f, 3.85f, 0f), new Vector3(1.1f, 0.6f, 1.1f), p.yellow);
            Prim.Cylinder(t, "Handle", new Vector3(0.9f, 3.5f, 0f), new Vector3(0.12f, 0.9f, 0.12f), p.yellow);
            Prim.Cube(t, "HammerHead", new Vector3(0.9f, 4.5f, 0f), new Vector3(0.9f, 0.5f, 0.6f), p.yellow);
            for (var i = 0; i < 4; i++)
            {
                var a = i * Mathf.PI * 0.5f + 0.78f;
                spots.Add(new Vector3(Mathf.Cos(a) * 3.2f, 0f, Mathf.Sin(a) * 3.2f));
            }
        }
    }
}
