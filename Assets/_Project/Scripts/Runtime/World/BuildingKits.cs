using System.Collections.Generic;
using Game.Core;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// Builds the model of a building out of primitives, one recipe per <see cref="BuildingKind"/>. A recipe produces
    /// <c>plan.Floors</c> stages (a floor for a house, a hall or a chimney for a factory, a hull or a deck for a ship) that pop in as
    /// the work progresses, plus a roof piece that appears when the building is finished. Only the two faces the isometric
    /// camera can see get windows, which keeps the primitive count (and WebGL draw calls) down.
    /// </summary>
    public static class BuildingKits
    {
        public const float Base = 0.3f;   // height of the stone slab every building stands on

        /// <summary>Footprint (x, z) of the unscaled model; the plan's Size multiplies it.</summary>
        public static Vector2 BaseDim(BuildingKind kind)
        {
            switch (kind)
            {
                case BuildingKind.Hut: return new Vector2(2.7f, 2.7f);
                case BuildingKind.House: return new Vector2(3.4f, 3.0f);
                case BuildingKind.Shop: return new Vector2(4.4f, 3.0f);
                case BuildingKind.School: return new Vector2(6.4f, 3.4f);
                case BuildingKind.TownHall: return new Vector2(6.0f, 3.8f);
                case BuildingKind.Apartment: return new Vector2(3.8f, 3.4f);
                case BuildingKind.Factory: return new Vector2(7.2f, 4.6f);
                case BuildingKind.Warehouse: return new Vector2(5.6f, 3.8f);
                case BuildingKind.Ship: return new Vector2(7.0f, 2.5f);
                case BuildingKind.Lighthouse: return new Vector2(2.8f, 2.8f);
                case BuildingKind.Silo: return new Vector2(2.6f, 2.6f);
                default: return new Vector2(2.9f, 2.9f);   // Windmill
            }
        }

        public static Vector2 Footprint(BuildPlan plan) => BaseDim(plan.Kind) * plan.Size;

        sealed class Ctx
        {
            public Transform Model;
            public BuildPlan Plan;
            public ToonPalette P;
            public Material Wall;
            public List<Transform> Stages;
            public Transform Roof, Flag;
            public float Top;   // height of the highest stage, model units
            public int N => Plan.Floors;
            public int V => Plan.Variant;
        }

        /// <summary>
        /// Fills <paramref name="stages"/> (all inactive) and creates the inactive roof under <paramref name="model"/>.
        /// <paramref name="top"/> is the height of the stages without the roof, in model units (before Size).
        /// </summary>
        public static void Build(Transform model, BuildPlan plan, ToonPalette palette, Material wall, List<Transform> stages,
            out Transform roof, out Transform flag, out float top)
        {
            var c = new Ctx { Model = model, Plan = plan, P = palette, Wall = wall, Stages = stages };
            switch (plan.Kind)
            {
                case BuildingKind.Hut: Hut(c); break;
                case BuildingKind.House: House(c); break;
                case BuildingKind.Shop: Shop(c); break;
                case BuildingKind.School: School(c); break;
                case BuildingKind.TownHall: TownHall(c); break;
                case BuildingKind.Apartment: Apartment(c); break;
                case BuildingKind.Factory: Factory(c); break;
                case BuildingKind.Warehouse: Warehouse(c); break;
                case BuildingKind.Ship: Ship(c); break;
                case BuildingKind.Lighthouse: Lighthouse(c); break;
                case BuildingKind.Silo: Silo(c); break;
                default: Windmill(c); break;
            }
            c.Roof.gameObject.SetActive(false);
            roof = c.Roof;
            flag = c.Flag;
            top = c.Top;
        }

        // ---------- small helpers ----------

        static Transform NewStage(Ctx c, float y)
        {
            var t = new GameObject($"Stage {c.Stages.Count + 1}").transform;
            t.SetParent(c.Model, false);
            t.localPosition = new Vector3(0f, y, 0f);
            t.gameObject.SetActive(false);
            c.Stages.Add(t);
            return t;
        }

        static Transform NewRoof(Ctx c, float x, float y, float z)
        {
            c.Roof = new GameObject("Roof").transform;
            c.Roof.SetParent(c.Model, false);
            c.Roof.localPosition = new Vector3(x, y, z);
            return c.Roof;
        }

        static GameObject Box(Transform parent, string name, float cx, float y0, float cz, float w, float h, float d, Material m) =>
            Prim.Cube(parent, name, new Vector3(cx, y0 + h * 0.5f, cz), new Vector3(w, h, d), m);

        static GameObject Cyl(Transform parent, string name, float cx, float y0, float cz, float r, float h, Material m) =>
            Prim.Cylinder(parent, name, new Vector3(cx, y0 + h * 0.5f, cz), new Vector3(r * 2f, h * 0.5f, r * 2f), m);

        /// <summary>
        /// A gabled roof (a square prism turned 45 degrees, flattened by pitch) sitting on y0. Returns its height. The lower half of the
        /// prism is buried in the walls, so <paramref name="d"/> must stay about 0.2 under the wall depth: wider and its tip shows in front as a notch, closer to the wall and its outline draws a line on it.
        /// </summary>
        static float Gable(Transform parent, string name, float cx, float y0, float cz, float w, float d, float pitch, Material m)
        {
            var holder = new GameObject(name).transform;
            holder.SetParent(parent, false);
            holder.localPosition = new Vector3(cx, y0, cz);
            holder.localScale = new Vector3(1f, pitch, 1f);
            var s = w / Mathf.Sqrt(2f);
            Prim.Cube(holder, "Prism", Vector3.zero, new Vector3(s, s, d), m).transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            return w * 0.5f * pitch;
        }

        // Windows on the two faces the camera sees: the front (-z) and the left (-x).
        static void Windows(Transform t, ToonPalette p, float cx, float cz, float w, float d, float y0, float h, int front, int side, bool skipCenter = false)
        {
            var wh = Mathf.Min(0.5f, h * 0.45f);
            for (var k = 0; k < front; k++)
            {
                var x = -w * 0.5f + w * (k + 0.5f) / front;
                if (skipCenter && Mathf.Abs(x) < 0.01f) continue;
                Prim.Cube(t, "Window", new Vector3(cx + x, y0 + h * 0.55f, cz - (d * 0.5f + 0.01f)), new Vector3(0.55f, wh, 0.1f), p.window);
            }
            for (var k = 0; k < side; k++)
            {
                var z = -d * 0.5f + d * (k + 0.5f) / side;
                Prim.Cube(t, "Window", new Vector3(cx - (w * 0.5f + 0.01f), y0 + h * 0.55f, cz + z), new Vector3(0.1f, wh, 0.55f), p.window);
            }
        }

        static void Door(Transform t, ToonPalette p, float cx, float cz, float d, float w = 0.75f, float h = 0.84f) =>
            Prim.Cube(t, "Door", new Vector3(cx, h * 0.5f, cz - (d * 0.5f + 0.01f)), new Vector3(w, h, 0.1f), p.door);

        static Transform Flag(Ctx c, float x, float y0, float z)
        {
            Prim.Cylinder(c.Roof, "Pole", new Vector3(x, y0 + 0.55f, z), new Vector3(0.08f, 0.55f, 0.08f), c.P.scaffold);
            c.Flag = Prim.Cube(c.Roof, "Flag", new Vector3(x + 0.3f, y0 + 1.0f, z), new Vector3(0.55f, 0.32f, 0.05f), c.P.flag).transform;
            return c.Flag;
        }

        static void Smoke(Transform parent, ToonPalette p, float x, float y0, float z, float s)
        {
            Prim.Sphere(parent, "Smoke", new Vector3(x, y0 + 0.4f * s, z), Vector3.one * 0.9f * s, p.white);
            Prim.Sphere(parent, "Smoke", new Vector3(x + 0.25f * s, y0 + 1.1f * s, z), Vector3.one * 1.2f * s, p.white);
            Prim.Sphere(parent, "Smoke", new Vector3(x + 0.7f * s, y0 + 1.9f * s, z), Vector3.one * 1.5f * s, p.white);
        }

        // ---------- kinds ----------

        static void Hut(Ctx c)
        {
            const float w = 2.7f, h = 1.0f;
            for (var i = 0; i < c.N; i++)
            {
                var st = NewStage(c, Base + i * h);
                var ww = w - i * 0.15f;
                Box(st, "Wall", 0f, 0f, 0f, ww, h - 0.02f, ww, c.P.crate);
                Windows(st, c.P, 0f, 0f, ww, ww, 0f, h, 1, 1);
                if (i == 0) Door(st, c.P, 0f, 0f, ww, 0.7f, 0.8f);
            }
            var top = Base + c.N * h;
            c.Top = top;
            var roof = NewRoof(c, 0f, top, 0f);
            var gh = Gable(roof, "Thatch", 0f, 0f, 0f, 3.0f, 2.5f, 0.9f, c.P.roof);
            Box(roof, "Chimney", 0.7f, 0.3f, 0.5f, 0.5f, 1.0f, 0.5f, c.P.stone);
            Flag(c, 0f, gh, 0f);
        }

        static void House(Ctx c)
        {
            const float w = 3.4f, d = 3.0f, h = 1.1f;
            var wall = c.V == 1 ? c.P.brick : c.Wall;
            for (var i = 0; i < c.N; i++)
            {
                var st = NewStage(c, Base + i * h);
                Box(st, "Wall", 0f, 0f, 0f, w, h - 0.02f, d, wall);
                Windows(st, c.P, 0f, 0f, w, d, 0f, h, 2, 2);
                if (i == 0) Door(st, c.P, 0f, 0f, d);
            }
            var top = Base + c.N * h;
            c.Top = top;
            var roof = NewRoof(c, 0f, top, 0f);
            var gh = Gable(roof, "Roof", 0f, 0f, 0f, 3.8f, 2.8f, 0.8f, c.V == 1 ? c.P.slate : c.P.roof);
            Box(roof, "Chimney", 1.0f, 0.4f, 0.7f, 0.5f, 1.1f, 0.5f, c.P.stone);
            Flag(c, -1.0f, gh * 0.6f, 0f);
        }

        static void Shop(Ctx c)
        {
            const float w = 4.4f, d = 3.0f, h = 1.1f;
            var wall = c.V == 0 ? c.Wall : c.V == 1 ? c.P.white : c.P.sand;
            var awning = c.V == 0 ? c.P.flag : c.V == 1 ? c.P.green : c.P.hull;
            for (var i = 0; i < c.N; i++)
            {
                var st = NewStage(c, Base + i * h);
                Box(st, "Wall", 0f, 0f, 0f, w, h - 0.02f, d, wall);
                if (i == 0)
                {
                    foreach (var sx in new[] { -1.35f, 1.35f })
                        Prim.Cube(st, "Display", new Vector3(sx, 0.55f, -(d * 0.5f + 0.01f)), new Vector3(1.2f, 0.62f, 0.1f), c.P.window);
                    Door(st, c.P, 0f, 0f, d, 0.8f, 0.86f);
                    for (var k = 0; k < 5; k++)   // striped awning
                    {
                        var stripe = Prim.Cube(st, "Awning", new Vector3(-1.76f + k * 0.88f, 1.0f, -(d * 0.5f + 0.38f)), new Vector3(0.88f, 0.08f, 0.9f), k % 2 == 0 ? awning : c.P.white);
                        stripe.transform.localRotation = Quaternion.Euler(18f, 0f, 0f);
                    }
                    if (c.V != 0)   // tables with parasols out front
                        foreach (var sx in new[] { -1.4f, 1.4f })
                        {
                            Cyl(st, "Table", sx, 0.4f, -(d * 0.5f + 1.3f), 0.34f, 0.08f, c.P.white);
                            Cyl(st, "Leg", sx, 0f, -(d * 0.5f + 1.3f), 0.06f, 0.42f, c.P.scaffold);
                            Prim.Sphere(st, "Parasol", new Vector3(sx, 1.55f, -(d * 0.5f + 1.3f)), new Vector3(1.1f, 0.3f, 1.1f), awning);
                            Cyl(st, "Stem", sx, 0.45f, -(d * 0.5f + 1.3f), 0.04f, 1.05f, c.P.scaffold);
                        }
                }
                else
                {
                    Windows(st, c.P, 0f, 0f, w, d, 0f, h, 3, 2);
                }
            }
            var top = Base + c.N * h;
            c.Top = top;
            var roof = NewRoof(c, 0f, top, 0f);
            Box(roof, "Cap", 0f, 0f, 0f, w + 0.3f, 0.3f, d + 0.3f, c.P.slate);
            Box(roof, "Sign", 0f, 0.3f, -(d * 0.5f - 0.05f), 2.0f, 0.55f, 0.14f, c.V == 1 ? c.P.green : c.P.yellow);
            Flag(c, w * 0.5f - 0.3f, 0.3f, d * 0.5f - 0.3f);
        }

        static void School(Ctx c)
        {
            const float w = 6.4f, d = 3.4f, h = 1.2f;
            var wall = c.V == 0 ? c.P.brick : c.P.sand;
            var wings = Mathf.Max(1, c.N - 1);
            for (var i = 0; i < wings; i++)
            {
                var st = NewStage(c, Base + i * h);
                Box(st, "Wall", 0f, 0f, 0f, w, h - 0.02f, d, wall);
                Windows(st, c.P, 0f, 0f, w, d, 0f, h, 5, 3, skipCenter: i == 0);
                if (i == 0) Door(st, c.P, 0f, 0f, d, 1.3f, 0.9f);
                if (i == wings - 1) Box(st, "Cap", 0f, h - 0.02f, 0f, w + 0.3f, 0.25f, d + 0.3f, c.P.roof);
            }
            var towerY = Base + wings * h + 0.23f;
            var tower = NewStage(c, towerY);
            Box(tower, "Tower", 0f, 0f, 0f, 1.9f, 1.5f, 1.9f, wall);
            Cyl(tower, "Clock", 0f, 0f, 0f, 0.4f, 0.06f, c.P.white).transform.SetLocalPositionAndRotation(
                new Vector3(0f, 0.85f, -0.97f), Quaternion.Euler(90f, 0f, 0f));
            Box(tower, "Hand", 0f, 0.8f, -1.02f, 0.07f, 0.3f, 0.04f, c.P.slate);
            Box(tower, "Hand", 0.12f, 0.82f, -1.02f, 0.22f, 0.06f, 0.04f, c.P.slate);
            c.Top = towerY + 1.5f;
            var roof = NewRoof(c, 0f, c.Top, 0f);
            var gh = Gable(roof, "Spire", 0f, 0f, 0f, 2.3f, 1.7f, 1.2f, c.P.slate);
            Flag(c, 0f, gh, 0f);
        }

        static void TownHall(Ctx c)
        {
            const float d = 3.8f, h = 1.2f;
            var y = Base;
            for (var i = 0; i < c.N - 1; i++)
            {
                var w = i == 0 ? 6.0f : Mathf.Max(3.4f, 5.0f - (i - 1) * 0.6f);
                var depth = i == 0 ? d : d - 0.6f;
                var st = NewStage(c, y);
                Box(st, "Wall", 0f, 0f, 0f, w, h - 0.02f, depth, c.P.white);
                if (i == 0)
                {
                    Windows(st, c.P, 0f, 0f, w, depth, 0f, h, 5, 3, skipCenter: true);
                    Door(st, c.P, 0f, 0f, depth, 1.2f, 0.95f);
                    foreach (var cx in new[] { -1.8f, -0.6f, 0.6f, 1.8f })
                        Cyl(st, "Column", cx, 0f, -(depth * 0.5f + 0.5f), 0.17f, h, c.P.stone);
                    Box(st, "Portico", 0f, h, -(depth * 0.5f + 0.5f), 4.4f, 0.2f, 1.2f, c.P.concrete);
                }
                else
                {
                    Windows(st, c.P, 0f, 0f, w, depth, 0f, h, 3, 2);
                }
                y += h;
                if (i == 0) y += 0.2f;
            }
            var drum = NewStage(c, y);
            Cyl(drum, "Drum", 0f, 0f, 0f, 1.15f, 0.9f, c.P.white);
            Cyl(drum, "Ring", 0f, 0.55f, 0f, 1.25f, 0.12f, c.P.stone);
            c.Top = y + 0.9f;
            var roof = NewRoof(c, 0f, c.Top, 0f);
            Prim.Sphere(roof, "Dome", new Vector3(0f, 0.15f, 0f), new Vector3(2.5f, 1.8f, 2.5f), c.P.green);
            Cyl(roof, "Spire", 0f, 0.9f, 0f, 0.07f, 0.5f, c.P.scaffold);
            Flag(c, 0f, 1.35f, 0f);
        }

        static void Apartment(Ctx c)
        {
            const float w = 3.8f, d = 3.4f, h = 1.15f;
            var wall = c.V == 0 ? c.Wall : c.V == 1 ? c.P.sand : c.P.brick;
            for (var i = 0; i < c.N; i++)
            {
                var st = NewStage(c, Base + i * h);
                Box(st, "Wall", 0f, 0f, 0f, w, h - 0.02f, d, wall);
                Windows(st, c.P, 0f, 0f, w, d, 0f, h, 2, 2);
                if (i == 0) Door(st, c.P, 0f, 0f, d, 0.9f, 0.9f);
                else foreach (var sx in new[] { -0.95f, 0.95f })
                    Box(st, "Balcony", sx, 0f, -(d * 0.5f + 0.25f), 1.1f, 0.12f, 0.5f, c.P.slate);
            }
            var top = Base + c.N * h;
            c.Top = top;
            var roof = NewRoof(c, 0f, top, 0f);
            Box(roof, "Cap", 0f, 0f, 0f, w + 0.25f, 0.28f, d + 0.25f, c.P.concrete);
            Cyl(roof, "Tank", 0.9f, 0.28f, 0.7f, 0.5f, 0.8f, c.P.trunk);
            Prim.Sphere(roof, "TankTop", new Vector3(0.9f, 1.1f, 0.7f), new Vector3(1.0f, 0.35f, 1.0f), c.P.trunk);
            Cyl(roof, "Antenna", -1.1f, 0.28f, 0.8f, 0.04f, 1.2f, c.P.scaffold);
            Flag(c, -1.3f, 0.28f, -1.0f);
        }

        static void Factory(Ctx c)
        {
            const float w = 7.2f, d = 4.6f, hall = 1.7f;
            var wall = c.V == 0 ? c.P.concrete : c.P.brick;
            for (var i = 0; i < c.N; i++)
            {
                switch (i % 6)
                {
                    case 0:   // main hall
                    {
                        var st = NewStage(c, Base);
                        Box(st, "Hall", 0f, 0f, 0f, w, hall, d, wall);
                        Windows(st, c.P, 0f, 0f, w, d, 0.3f, hall - 0.5f, 5, 3, skipCenter: true);
                        Box(st, "Shutter", 0f, 0f, -(d * 0.5f + 0.01f), 1.9f, 1.3f, 0.1f, c.P.slate);
                        Box(st, "ShutterTop", 0f, 1.3f, -(d * 0.5f + 0.02f), 2.1f, 0.1f, 0.12f, c.P.yellow);
                        break;
                    }
                    case 1:   // sawtooth roof
                    {
                        var st = NewStage(c, Base + hall);
                        foreach (var x in new[] { -2.4f, 0f, 2.4f })
                            Gable(st, "Tooth", x, 0f, 0f, 2.4f, d - 0.2f, 0.55f, c.P.slate);
                        break;
                    }
                    case 2:   // first chimney
                    {
                        var st = NewStage(c, Base + hall);
                        Cyl(st, "Chimney", 2.7f, 0f, 1.0f, 0.55f, 3.6f, c.P.brick);
                        Cyl(st, "Band", 2.7f, 2.5f, 1.0f, 0.6f, 0.3f, c.P.white);
                        Cyl(st, "Rim", 2.7f, 3.6f, 1.0f, 0.62f, 0.15f, c.P.slate);
                        break;
                    }
                    case 3:   // second chimney
                    {
                        var st = NewStage(c, Base + hall);
                        Cyl(st, "Chimney", 1.4f, 0f, 1.2f, 0.42f, 2.8f, c.P.brick);
                        Cyl(st, "Band", 1.4f, 2.0f, 1.2f, 0.47f, 0.25f, c.P.white);
                        Cyl(st, "Rim", 1.4f, 2.8f, 1.2f, 0.5f, 0.12f, c.P.slate);
                        break;
                    }
                    case 4:   // storage tank beside the hall
                    {
                        var st = NewStage(c, Base);
                        Cyl(st, "Tank", -(w * 0.5f + 1.0f), 0f, -0.5f, 0.95f, 1.6f, c.P.white);
                        Prim.Sphere(st, "TankTop", new Vector3(-(w * 0.5f + 1.0f), 1.6f, -0.5f), new Vector3(1.9f, 0.7f, 1.9f), c.P.white);
                        Cyl(st, "Band", -(w * 0.5f + 1.0f), 0.6f, -0.5f, 1.0f, 0.2f, c.P.hull);
                        Box(st, "Pipe", -(w * 0.5f + 0.1f), 0.5f, -0.5f, 0.9f, 0.18f, 0.18f, c.P.slate);
                        break;
                    }
                    default:  // loading yard: crates and a container
                    {
                        var st = NewStage(c, Base);
                        Box(st, "Container", w * 0.5f + 1.2f, 0f, -1.2f, 1.9f, 0.9f, 0.9f, c.P.hull);
                        Box(st, "Container", w * 0.5f + 1.2f, 0.9f, -1.2f, 1.9f, 0.9f, 0.9f, c.P.cone);
                        Box(st, "Crate", w * 0.5f + 1.2f, 0f, 0.4f, 0.9f, 0.9f, 0.9f, c.P.crate);
                        Box(st, "Crate", w * 0.5f + 1.2f, 0f, 1.4f, 0.9f, 0.9f, 0.9f, c.P.crate);
                        break;
                    }
                }
            }
            c.Top = Base + hall + 0.6f;
            var roof = NewRoof(c, 0f, Base + hall, 0f);
            Smoke(roof, c.P, 2.7f, 3.8f, 1.0f, 0.9f);
            Box(roof, "Sign", -2.2f, 0.9f, -(d * 0.5f - 0.1f), 2.2f, 0.5f, 0.14f, c.P.yellow);
            Flag(c, -3.3f, 0.5f, 1.8f);
        }

        static void Warehouse(Ctx c)
        {
            const float w = 5.6f, d = 3.8f, h = 1.5f;
            var wall = c.V == 1 ? c.P.brick : c.V == 2 ? c.P.concrete : c.P.sand;
            var st0 = NewStage(c, Base);
            Box(st0, "Wall", 0f, 0f, 0f, w, h, d, wall);
            Box(st0, "Door", 0f, 0f, -(d * 0.5f + 0.01f), 2.0f, 1.2f, 0.1f, c.V == 1 ? c.P.white : c.P.slate);
            if (c.V != 1) Windows(st0, c.P, 0f, 0f, w, d, 0.2f, 1.1f, 2, 2);
            var colours = new[] { c.P.hull, c.P.cone, c.P.green };
            for (var i = 1; i < c.N; i++)
            {
                var st = NewStage(c, Base);
                if (i == 1)
                {
                    Box(st, "Container", -1.5f, 0f, -(d * 0.5f + 0.7f), 1.9f, 0.9f, 0.9f, colours[c.V % 3]);
                    Box(st, "Container", 1.5f, 0f, -(d * 0.5f + 0.7f), 1.9f, 0.9f, 0.9f, colours[(c.V + 1) % 3]);
                }
                else
                {
                    Box(st, "Crate", w * 0.5f + 0.7f, 0f, -0.6f, 0.9f, 0.9f, 0.9f, c.P.crate);
                    Box(st, "Crate", w * 0.5f + 0.7f, 0f, 0.5f, 0.9f, 0.9f, 0.9f, c.P.crate);
                    Box(st, "Crate", w * 0.5f + 0.7f, 0.9f, -0.05f, 0.9f, 0.9f, 0.9f, c.P.crate);
                }
            }
            c.Top = Base + h;
            var roof = NewRoof(c, 0f, c.Top, 0f);
            var gh = Gable(roof, "Roof", 0f, 0f, 0f, 5.8f, 3.6f, 0.5f, c.V == 1 ? c.P.slate : c.P.roof);
            Flag(c, 2.2f, gh * 0.4f, 0f);
        }

        static void Ship(Ctx c)
        {
            var hull = c.V == 0 ? c.P.hull : c.V == 1 ? c.P.white : c.P.brick;
            var trim = c.V == 1 ? c.P.hull : c.P.white;
            var deckY = Base + 0.25f + 0.9f;
            for (var i = 0; i < c.N; i++)
            {
                switch (i)
                {
                    case 0:   // hull on its cradle
                    {
                        var st = NewStage(c, Base + 0.25f);
                        Box(st, "Hull", -0.6f, 0f, 0f, 5.6f, 0.9f, 2.0f, hull);
                        var bow = Box(st, "Bow", 2.2f, 0f, 0f, 1.45f, 0.9f, 1.45f, hull);
                        bow.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
                        Box(st, "Stripe", -0.6f, 0.9f, 0f, 5.7f, 0.1f, 2.1f, trim);
                        Box(st, "StripeBow", 2.2f, 0.9f, 0f, 1.5f, 0.1f, 1.5f, trim).transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
                        Box(st, "Keel", -0.6f, -0.2f, 0f, 5.0f, 0.2f, 1.2f, c.P.slate);
                        break;
                    }
                    case 1:   // cabin block
                    {
                        var st = NewStage(c, deckY + 0.1f);
                        Box(st, "Cabin", -1.9f, 0f, 0f, 2.1f, 0.9f, 1.6f, c.P.white);
                        Windows(st, c.P, -1.9f, 0f, 2.1f, 1.6f, 0f, 0.9f, 3, 2);
                        break;
                    }
                    case 2:   // bridge and funnel
                    {
                        var st = NewStage(c, deckY + 1.0f);
                        Box(st, "Bridge", -1.9f, 0f, 0f, 1.3f, 0.7f, 1.2f, c.P.white);
                        Box(st, "BridgeGlass", -1.9f, 0.3f, -0.61f, 1.0f, 0.25f, 0.06f, c.P.window);
                        Cyl(st, "Funnel", -2.7f, -0.9f, 0f, 0.34f, 1.7f, c.V == 2 ? c.P.white : c.P.brick);
                        Cyl(st, "FunnelTop", -2.7f, 0.8f, 0f, 0.37f, 0.18f, c.P.slate);
                        break;
                    }
                    case 3:   // mast and lifeboats
                    {
                        var st = NewStage(c, deckY + 0.1f);
                        Cyl(st, "Mast", 0.5f, 0f, 0f, 0.06f, 2.3f, c.P.scaffold);
                        Box(st, "Yard", 0.5f, 1.5f, 0f, 0.1f, 0.1f, 1.1f, c.P.scaffold);
                        Box(st, "Boat", 0.6f, 0f, -0.75f, 1.0f, 0.3f, 0.4f, c.P.cone);
                        Box(st, "Boat", 0.6f, 0f, 0.75f, 1.0f, 0.3f, 0.4f, c.P.cone);
                        break;
                    }
                    default:  // cargo on the foredeck, or an upper deck on passenger ships
                    {
                        var st = NewStage(c, deckY + 0.1f);
                        if (c.V == 0)
                        {
                            var colours = new[] { c.P.cone, c.P.green, c.P.yellow, c.P.brick };
                            for (var k = 0; k < 3; k++)
                                Box(st, "Container", 1.4f, (i - 4) * 0.8f, -0.55f + k * 0.55f, 1.8f, 0.78f, 0.52f, colours[(k + i) % colours.Length]);
                        }
                        else
                        {
                            Box(st, "UpperDeck", -0.3f, 0f, 0f, 2.4f, 0.55f, 1.4f, c.P.white);
                            Windows(st, c.P, -0.3f, 0f, 2.4f, 1.4f, 0f, 0.55f, 4, 2);
                        }
                        break;
                    }
                }
            }
            c.Top = deckY + 1.6f;
            var roof = NewRoof(c, -1.9f, deckY + 1.7f, 0f);
            Box(roof, "Radar", 0f, 0f, 0f, 0.7f, 0.08f, 0.12f, c.P.slate);
            Flag(c, 0f, 0f, 0f);
        }

        static void Lighthouse(Ctx c)
        {
            const float h = 1.25f;
            var segments = Mathf.Max(1, c.N - 1);
            for (var i = 0; i < segments; i++)
            {
                var st = NewStage(c, Base + i * h);
                var r = 1.25f - 0.07f * i;
                Cyl(st, "Tower", 0f, 0f, 0f, r, h - 0.02f, i % 2 == 0 ? c.P.white : c.P.flag);
                if (i == 0) Door(st, c.P, 0f, 0f, r * 2f, 0.7f, 0.9f);
                else Prim.Cube(st, "Window", new Vector3(0f, h * 0.55f, -(r + 0.01f)), new Vector3(0.4f, 0.45f, 0.1f), c.P.window);
            }
            var lampY = Base + segments * h;
            var lamp = NewStage(c, lampY);
            Cyl(lamp, "Gallery", 0f, 0f, 0f, 1.15f, 0.16f, c.P.slate);
            Cyl(lamp, "Lantern", 0f, 0.16f, 0f, 0.6f, 0.85f, c.P.yellow);
            foreach (var a in new[] { 0f, 90f, 180f, 270f })
                Cyl(lamp, "Bar", Mathf.Cos(a * Mathf.Deg2Rad) * 0.6f, 0.16f, Mathf.Sin(a * Mathf.Deg2Rad) * 0.6f, 0.05f, 0.85f, c.P.slate);
            c.Top = lampY + 1.0f;
            var roof = NewRoof(c, 0f, c.Top, 0f);
            Prim.Sphere(roof, "Cap", new Vector3(0f, 0.15f, 0f), new Vector3(1.5f, 0.9f, 1.5f), c.P.flag);
            Flag(c, 0f, 0.5f, 0f);
        }

        static void Silo(Ctx c)
        {
            const float h = 1.2f;
            if (c.V == 1)   // water tower: legs, then the tank
            {
                var legs = Mathf.Max(1, c.N - 2);
                for (var i = 0; i < legs; i++)
                {
                    var st = NewStage(c, Base + i * h);
                    foreach (var sx in new[] { -0.9f, 0.9f })
                    foreach (var sz in new[] { -0.9f, 0.9f })
                        Cyl(st, "Leg", sx, 0f, sz, 0.12f, h - 0.02f, c.P.slate);
                    Box(st, "BraceX", 0f, h * 0.5f, -0.9f, 1.8f, 0.08f, 0.08f, c.P.slate);
                    Box(st, "BraceZ", -0.9f, h * 0.5f, 0f, 0.08f, 0.08f, 1.8f, c.P.slate);
                }
                var tankY = Base + legs * h;
                var tank = NewStage(c, tankY);
                Cyl(tank, "Tank", 0f, 0f, 0f, 1.3f, 1.2f, c.P.hull);
                Cyl(tank, "Band", 0f, 0.5f, 0f, 1.36f, 0.14f, c.P.white);
                c.Top = tankY + 1.2f;
                for (var i = legs + 1; i < c.N; i++)
                {
                    var cap = NewStage(c, c.Top);
                    Cyl(cap, "Hatch", 0f, 0f, 0f, 0.5f, 0.3f, c.P.slate);
                }
                var roofV1 = NewRoof(c, 0f, c.Top + (c.N > legs + 1 ? 0.3f : 0f), 0f);
                Prim.Sphere(roofV1, "Dome", new Vector3(0f, 0.1f, 0f), new Vector3(2.7f, 1.0f, 2.7f), c.P.hull);
                Flag(c, 0f, 0.5f, 0f);
                return;
            }

            for (var i = 0; i < c.N; i++)
            {
                var st = NewStage(c, Base + i * h);
                if (c.V == 2)   // brick chimney tower, tapering, with white rings
                {
                    var r = 1.15f - 0.06f * i;
                    Cyl(st, "Shaft", 0f, 0f, 0f, r, h - 0.02f, c.P.brick);
                    if (i % 2 == 1) Cyl(st, "Ring", 0f, h * 0.35f, 0f, r + 0.04f, 0.18f, c.P.white);
                    if (i == 0) Door(st, c.P, 0f, 0f, r * 2f, 0.7f, 0.9f);
                }
                else
                {
                    Cyl(st, "Shaft", 0f, 0f, 0f, 1.1f, h - 0.02f, c.P.concrete);
                    Cyl(st, "Ring", 0f, 0f, 0f, 1.16f, 0.14f, c.P.slate);
                    Cyl(st, "Ring", 0f, h * 0.6f, 0f, 1.14f, 0.1f, c.P.slate);
                    if (i == 0) Door(st, c.P, 0f, 0f, 2.2f, 0.7f, 0.9f);
                }
            }
            c.Top = Base + c.N * h;
            var roof = NewRoof(c, 0f, c.Top, 0f);
            if (c.V == 2)
            {
                Cyl(roof, "Rim", 0f, 0f, 0f, 0.95f, 0.2f, c.P.slate);
                Smoke(roof, c.P, 0f, 0.2f, 0f, 1.0f);
                Flag(c, 0.8f, 0f, 0f);
            }
            else
            {
                Prim.Sphere(roof, "Dome", new Vector3(0f, 0.05f, 0f), new Vector3(2.2f, 1.0f, 2.2f), c.P.slate);
                Cyl(roof, "Vent", 0f, 0.45f, 0f, 0.15f, 0.5f, c.P.concrete);
                Flag(c, 0.5f, 0.6f, 0f);
            }
        }

        static void Windmill(Ctx c)
        {
            const float h = 1.2f;
            var segments = Mathf.Max(1, c.N - 1);
            for (var i = 0; i < segments; i++)
            {
                var st = NewStage(c, Base + i * h);
                var r = 1.35f - 0.07f * i;
                Cyl(st, "Tower", 0f, 0f, 0f, r, h - 0.02f, c.P.white);
                if (i == 0) Door(st, c.P, 0f, 0f, r * 2f, 0.7f, 0.9f);
                else Prim.Cube(st, "Window", new Vector3(0f, h * 0.55f, -(r + 0.01f)), new Vector3(0.38f, 0.45f, 0.1f), c.P.window);
            }
            var capY = Base + segments * h;
            var cap = NewStage(c, capY);
            Cyl(cap, "Housing", 0f, 0f, 0f, 1.0f, 0.55f, c.P.trunk);
            c.Top = capY + 0.55f;
            var roof = NewRoof(c, 0f, c.Top, 0f);
            Prim.Sphere(roof, "Cap", new Vector3(0f, 0.1f, 0f), new Vector3(2.2f, 1.1f, 2.2f), c.P.roof);
            var hub = new GameObject("Blades").transform;
            hub.SetParent(roof, false);
            hub.localPosition = new Vector3(0f, 0.25f, -1.2f);
            Prim.Sphere(hub, "Hub", Vector3.zero, Vector3.one * 0.5f, c.P.trunk);
            foreach (var angle in new[] { 0f, 90f })
            {
                var arm = new GameObject("Arm").transform;
                arm.SetParent(hub, false);
                arm.localRotation = Quaternion.Euler(0f, 0f, angle);
                Prim.Cube(arm, "Spar", Vector3.zero, new Vector3(0.14f, 4.6f, 0.08f), c.P.door);
                foreach (var sy in new[] { -1.45f, 1.45f })
                    Prim.Cube(arm, "Sail", new Vector3(0.28f, sy, 0f), new Vector3(0.5f, 1.5f, 0.05f), c.P.white);
            }
            hub.gameObject.AddComponent<Spin>();
            Flag(c, 0f, 0.55f, 0f);
        }
    }
}
