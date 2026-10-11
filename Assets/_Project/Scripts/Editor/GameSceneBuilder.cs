using System.Collections.Generic;
using System.IO;
using Game.Audio;
using Game.Core;
using Game.Feel;
using Game.Player;
using Game.UI;
using Game.World;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem.OnScreen;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Game.Editor
{
    /// <summary>
    /// Builds the single game scene from code. <c>Ensure</c> only creates it when missing (Unity assigns random
    /// fileIDs on creation, so regenerating would churn git and overwrite hand edits); <c>Rebuild</c> forces it.
    /// </summary>
    public static class GameSceneBuilder
    {
        public const string ScenePath = "Assets/_Project/Scenes/Main.unity";
        const string ParticleMatPath = "Assets/_Project/Art/Materials/Particle.mat";
        // A district is a square play area of 2 * HalfSize with the ground running on far past it (see BuildDistrict). Plots sit on a 3 x 2
        // grid. Districts live far apart in the scene (only the one being visited is switched on); the map screen is a separate picture.
        const float HalfSize = 26f;
        const float BeachWidth = 3.5f;        // on a coast the ground ends this far past the play area
        const float DistrictSpacing = 1000f;
        // Where the six decorations of a finished district go (building order): two on the middle lane, four towards the corners, all clear of the plots.
        static readonly Vector2[] DecorationSlotSpots = { new Vector2(-8f, 0f), new Vector2(8f, 0f), new Vector2(-21f, -19f), new Vector2(21f, 19f), new Vector2(-21f, 19f), new Vector2(21f, -19f) };
        const float ViewAspect = 2.6f;        // widest screen the ground must cover (20:9 phones are 2.2)
        static readonly Vector2[] PlotGrid = { new Vector2(-16f, -10f), new Vector2(0f, -10f), new Vector2(-16f, 10f), new Vector2(16f, -10f), new Vector2(0f, 10f), new Vector2(16f, 10f) };

        public static void Ensure()
        {
            if (!File.Exists(ScenePath)) Rebuild();
        }

        [MenuItem("Tools/Project/Rebuild Main Scene")]
        public static void Rebuild()
        {
            EditorWire.EnsureFolder("Assets/_Project/Scenes");

            // Create the scene FIRST: NewScene(Single) unloads unused assets from memory, which would leave
            // palette/font references loaded earlier destroyed (they'd serialize as fileID 0 into the scene).
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var palette = ArtBuilder.BuildPalette();
            var font = ArtBuilder.EnsureFont();
            var outlineMat = ArtBuilder.EnsureOutlineMaterial(font);
            ArtBuilder.EnsurePrefabs(palette, font, outlineMat, force: true);   // Rebuild regenerates prefabs too

            Prim.UseMeshes(palette.sphereMesh, palette.cylinderMesh, palette.capsuleMesh);   // everything below is made of the low-poly sphere/cylinder
            BuildLight();
            BuildSea(palette);

            var floatingText = AssetDatabase.LoadAssetAtPath<GameObject>(ArtBuilder.FloatingTextPrefabPath).GetComponent<FloatingText>();
            var autoWorker = AssetDatabase.LoadAssetAtPath<GameObject>(ArtBuilder.AutoWorkerPrefabPath).GetComponent<AutoWorker>();

            var districts = new District[Balance.Districts.Length];
            for (var i = 0; i < districts.Length; i++) districts[i] = BuildDistrict(i, palette, font, outlineMat);

            var overview = new GameObject("OverviewTarget").transform;
            overview.position = Vector3.zero;

            // The dashboard shows a picture of the country; each district's card sits on its province.
            var map = MapBuilder.Build(palette);
            for (var i = 0; i < districts.Length; i++) EditorWire.Set(districts[i], "mapPoint", map.Anchors[i]);

            var gameGo = new GameObject("Game");
            var game = gameGo.AddComponent<GameController>();
            EditorWire.Set(gameGo.AddComponent<DevCommands>(), "game", game);

            var player = BuildPlayer(game);
            EditorWire.Set(game, "player", player.GetComponent<Character>());
            EditorWire.Set(gameGo.GetComponent<DevCommands>(), "player", player.GetComponent<Character>());
            var cameraRig = BuildCamera(player.transform);

            var spawner = new GameObject("ColaSpawner").AddComponent<ColaSpawner>();
            EditorWire.Set(spawner, "game", game);
            EditorWire.Set(spawner, "player", player.transform);
            EditorWire.Set(spawner, "palette", palette);
            var events = new GameObject("Events").AddComponent<EventSpawner>();   // chests, birds and strays (World/EventSpawner)
            EditorWire.Set(events, "game", game);
            EditorWire.Set(events, "player", player.transform);
            EditorWire.Set(events, "palette", palette);
            var audioManager = BuildAudio(game);
            BuildHud(font, outlineMat, audioManager, out var hud, out var dashboard, out var fade, out var coinFlight, out var banner, out var colaPointer, out var chestPointer);
            EditorWire.Set(colaPointer, "game", game);
            EditorWire.Set(colaPointer, "spawner", spawner);
            EditorWire.Set(chestPointer, "game", game);
            EditorWire.Set(chestPointer, "spawner", events);

            EditorWire.SetArray(game, "districts", districts);
            EditorWire.Set(game, "workerPrefab", autoWorker);
            EditorWire.Set(game, "floatingTextPrefab", floatingText);
            EditorWire.Set(game, "hud", hud);
            EditorWire.Set(game, "dashboard", dashboard);
            EditorWire.Set(game, "fade", fade);

            var feel = new GameObject("Feel").AddComponent<FeelDirector>();
            EditorWire.Set(feel, "game", game);
            EditorWire.Set(feel, "cameraRig", cameraRig);
            EditorWire.Set(feel, "coins", coinFlight);
            EditorWire.Set(feel, "banner", banner);
            EditorWire.Set(feel, "mapFlags", map.Flags);
            EditorWire.Set(feel, "mapFx", map.Fx);
            EditorWire.Set(game, "cameraRig", cameraRig);
            EditorWire.Set(game, "overviewTarget", overview);
            EditorWire.Set(game, "mapRoot", map.Root);
            EditorWire.Set(game, "palette", palette);

            if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new IOException($"Could not save {ScenePath}");
            Debug.Log($"[Scene] Saved {ScenePath}");
        }

        // ---------- world ----------

        static void BuildLight()
        {
            var go = new GameObject("Directional Light");
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.96f, 0.88f);
            light.intensity = 1.15f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.8f;
            go.transform.rotation = Quaternion.Euler(52f, -38f, 0f);
        }

        static void BuildSea(ToonPalette p)
        {
            var sea = new GameObject("Sea").transform;
            Prim.NoShadow(Prim.Cube(sea, "Water", new Vector3(0f, -2.2f, 0f), new Vector3(700f, 0.2f, 700f), p.sea));
        }

        // Sides of the play square: 0 = +x, 1 = -x, 2 = +z, 3 = -z. Sahil is on the sea at +x and Liman at +z (the far edges, so the water
        // shows at the top of the screen); the other districts are inland and never show an edge.
        static int CoastSide(int district) => district == 0 ? 0 : district == 2 ? 2 : -1;

        // One neighbourhood: a square play area with six plots on a grid, scenery, one landmark and a fence. The ground extends past the
        // play area by everything the camera can show from its edge, so there is nothing to see beyond (except the sea on a coast), and
        // scenery is only placed in what can be seen.
        static District BuildDistrict(int index, ToonPalette p, TMP_FontAsset font, Material outlineMat)
        {
            var def = Balance.Districts[index];
            var root = new GameObject($"District {index + 1} - {def.Name}");
            root.transform.position = new Vector3(0f, 0f, DistrictSpacing * (index + 1));
            var t = root.transform;
            var coast = CoastSide(index);

            // What the camera can show beyond the play area, per side (relative to the player standing on the edge).
            var view = ViewFootprint.Extents.Of(new Vector3(-19f, 21f, -19f), GameController.DistrictViewScale, 30f, ViewAspect);
            float[] reach = { view.MaxX, -view.MinX, view.MaxZ, -view.MinZ };   // by side
            var limit = new float[4];
            for (var side = 0; side < 4; side++) limit[side] = HalfSize + (side == coast ? BeachWidth : reach[side] + 6f);

            // Ground: one slab per layer with the real extents (no scaling, so outlines keep their width), dirt below.
            var top = Rect4(-limit[1], limit[0], -limit[3], limit[2]);
            var inset = Rect4(-limit[1] + (coast == 1 ? 1.2f : 0f), limit[0] - (coast == 0 ? 1.2f : 0f), -limit[3] + (coast == 3 ? 1.2f : 0f), limit[2] - (coast == 2 ? 1.2f : 0f));
            MeshPart(t, "IslandBase", IslandMeshBuilder.Prism($"District{index + 1}Base", inset, -2.5f, -0.5f), p.dirt);
            MeshPart(t, "IslandTop", IslandMeshBuilder.Prism($"District{index + 1}Top", top, -0.5f, 0f), p.districtGround[index]);
            Prim.NoShadow(Prim.Cube(t, "Water", new Vector3(0f, -2.2f, 0f), new Vector3(1600f, 0.2f, 1600f), p.sea));
            var ground = new GameObject("GroundCollider");
            ground.transform.SetParent(t, false);
            ground.transform.localPosition = new Vector3(0f, -0.5f, 0f);
            ground.AddComponent<BoxCollider>().size = new Vector3(HalfSize * 2f + 8f, 1f, HalfSize * 2f + 8f);

            // Where things stand: plots first, then the landmark; everything else keeps clear of both.
            var plans = def.Plans;
            var plotSpots = new Vector3[plans.Length];
            var yardRadius = new float[plans.Length];
            for (var n = 0; n < plans.Length; n++)
            {
                plotSpots[n] = new Vector3(PlotGrid[n].x, 0f, PlotGrid[n].y);
                var yard = YardSize(plans[n]);
                yardRadius[n] = Mathf.Max(yard.x, yard.y) * 0.5f;
            }
            // On a coast the landmark stands on the beach just past the play area, its boat or pier reaching out over the water;
            // inland it stands at the left edge, where nothing else is.
            var outwardDeg = coast == 0 ? 0f : coast == 2 ? 90f : 180f;
            var landmarkSpot = coast == 0 ? new Vector3(HalfSize + 1f, 0f, -4f) : coast == 2 ? new Vector3(-4f, 0f, HalfSize + 1f) : new Vector3(-HalfSize + 4f, 0f, 0f);
            BuildLandmark(t, p, index, landmarkSpot, outwardDeg, 1f, onLand: coast < 0);

            bool Free(Vector3 spot, float margin)
            {
                if (Mathf.Abs(spot.x) > HalfSize - 1.5f || Mathf.Abs(spot.z) > HalfSize - 1.5f) return false;
                if (Vector3.Distance(spot, landmarkSpot) < 5.5f) return false;
                foreach (var slotSpot in DecorationSlotSpots)
                    if (Vector2.Distance(new Vector2(spot.x, spot.z), slotSpot) < 4.6f) return false;   // keep the decoration slots clear
                for (var n = 0; n < plotSpots.Length; n++)
                    if (Vector3.Distance(spot, plotSpots[n]) < yardRadius[n] + margin) return false;
                return true;
            }

            // Inside the fence: a few trees, rocks, cones and crates between the plots.
            var random = new System.Random(index * 31 + 7);
            float Rand(float lo, float hi) => lo + (float)random.NextDouble() * (hi - lo);
            var props = new GameObject("Scenery").transform;
            props.SetParent(t, false);
            props.gameObject.AddComponent<MergeChildren>();   // trees, rocks, crates and cones become a handful of meshes on first use
            var scenery = 0;
            for (var attempt = 0; attempt < 400 && scenery < 26; attempt++)
            {
                var spot = new Vector3(Rand(-HalfSize, HalfSize), 0f, Rand(-HalfSize, HalfSize));
                if (!Free(spot, 1.2f)) continue;
                switch (scenery % 6)
                {
                    case 0: case 3: BuildTree(props, spot, Rand(0.85f, 1.1f), p); break;
                    case 1: Prim.Sphere(props, "Rock", spot + Vector3.up * 0.15f, new Vector3(1.1f, 0.65f, 0.9f), p.stone).transform.Rotate(0f, Rand(0f, 180f), 0f); break;
                    case 2:
                        Prim.SplitCollider(Prim.Cube(props, "Crate", spot + Vector3.up * 0.45f, Vector3.one * 0.9f, p.crate, true));
                        var tilted = Prim.Cube(props, "Crate", spot + new Vector3(0.9f, 0.45f, 0.3f), Vector3.one * 0.9f, p.crate, true);
                        tilted.transform.Rotate(0f, 20f, 0f);
                        Prim.SplitCollider(tilted);   // after turning it: the collider twin copies the final pose
                        Prim.Cube(props, "Crate", spot + new Vector3(0.3f, 1.35f, 0.1f), Vector3.one * 0.9f, p.crate).transform.Rotate(0f, 10f, 0f);
                        break;
                    default:
                        Prim.Cylinder(props, "ConeBase", spot + Vector3.up * 0.04f, new Vector3(0.55f, 0.04f, 0.55f), p.cone);
                        Prim.SplitCollider(Prim.Cylinder(props, "ConeBody", spot + Vector3.up * 0.35f, new Vector3(0.3f, 0.3f, 0.3f), p.cone, true));
                        Prim.Cylinder(props, "ConeTip", spot + Vector3.up * 0.7f, new Vector3(0.18f, 0.08f, 0.18f), p.cone);
                        break;
                }
                scenery++;
            }

            // Outside the fence: a forest across everything the camera can show (dense near the fence, thin further out), no coast side.
            // (x, z) of every candidate comes from a jittered grid so rows do not look ruled.
            const float step = 4.6f;
            var forestTrees = new List<Vector3>();
            for (var gx = -limit[1] + 2f; gx <= limit[0] - 2f; gx += step)
            for (var gz = -limit[3] + 2f; gz <= limit[2] - 2f; gz += step)
            {
                var spot = new Vector3(gx + Rand(-1.4f, 1.4f), 0f, gz + Rand(-1.4f, 1.4f));
                var beyond = Mathf.Max(Mathf.Abs(spot.x), Mathf.Abs(spot.z)) - HalfSize;   // distance past the play area (Chebyshev)
                if (beyond < 1.6f) continue;
                if (coast == 0 && spot.x > HalfSize - 1f || coast == 2 && spot.z > HalfSize - 1f) continue;   // the beach and the sea
                if (spot.x > limit[0] - 1f || spot.x < -limit[1] + 1f || spot.z > limit[2] - 1f || spot.z < -limit[3] + 1f) continue;
                if (Vector3.Distance(spot, landmarkSpot) < 6f) continue;
                if (beyond > 13f && Rand(0f, 1f) > 0.3f) continue;   // thinner away from the fence
                forestTrees.Add(new Vector3(spot.x, spot.z, Rand(0.9f, 1.25f)));   // x, z, scale: drawn as merged chunks by Forest
            }
            BuildForest(t, forestTrees, p);
            Debug.Log($"[Scene] {def.Name}: {forestTrees.Count} forest trees, ground x {-limit[1]:0}..{limit[0]:0}, z {-limit[3]:0}..{limit[2]:0}");

            var barriers = new GameObject("Barriers").transform;
            barriers.SetParent(t, false);
            barriers.gameObject.AddComponent<MergeChildren>();   // posts and rails become a few meshes on first use
            var edge = HalfSize + 0.2f;
            if (coast != 0) BuildFence(barriers, new Vector2(edge, -edge), new Vector2(edge, edge), p);
            if (coast != 1) BuildFence(barriers, new Vector2(-edge, -edge), new Vector2(-edge, edge), p);
            if (coast != 2) BuildFence(barriers, new Vector2(-edge, edge), new Vector2(edge, edge), p);
            if (coast != 3) BuildFence(barriers, new Vector2(-edge, -edge), new Vector2(edge, -edge), p);

            var sites = new BuildSite[plans.Length];
            for (var n = 0; n < sites.Length; n++)
                sites[n] = BuildPlot(t, n, plans[n], plotSpots[n], p, font, outlineMat);

            var playerSpawn = new GameObject("PlayerSpawn").transform;
            playerSpawn.SetParent(t, false);
            var workerSpawn = new GameObject("WorkerSpawn").transform;
            workerSpawn.SetParent(t, false);
            workerSpawn.localPosition = new Vector3(0f, 0f, 2.5f);

            var district = root.AddComponent<District>();
            EditorWire.SetArray(district, "sites", sites);
            EditorWire.Set(district, "playerSpawn", playerSpawn);
            EditorWire.Set(district, "workerSpawn", workerSpawn);
            EditorWire.SetFloat(district, "halfSize", HalfSize);
            EditorWire.Set(district, "fx", BuildFeelFx(t));
            var slots = new Transform[DecorationSlotSpots.Length];
            for (var n = 0; n < slots.Length; n++)
            {
                slots[n] = new GameObject($"DecorSlot {n + 1}").transform;
                slots[n].SetParent(t, false);
                slots[n].localPosition = new Vector3(DecorationSlotSpots[n].x, 0f, DecorationSlotSpots[n].y);
            }
            EditorWire.SetArray(district, "decorationSlots", slots);
            root.SetActive(false);   // switched on by the first visit; its forest and fence are merged then, not at startup
            return district;
        }

        internal static void BuildForest(Transform parent, List<Vector3> trees, ToonPalette p)
        {
            var go = new GameObject("Forest");
            go.transform.SetParent(parent, false);
            var forest = go.AddComponent<Forest>();
            EditorWire.Set(forest, "sphereMesh", p.sphereMesh);
            EditorWire.Set(forest, "cylinderMesh", p.cylinderMesh);
            EditorWire.Set(forest, "leaves", p.leaves);
            EditorWire.Set(forest, "trunk", p.trunk);
            EditorWire.SetVector3Array(forest, "trees", trees.ToArray());
        }

        static Vector2[] Rect4(float minX, float maxX, float minZ, float maxZ) =>
            new[] { new Vector2(minX, minZ), new Vector2(maxX, minZ), new Vector2(maxX, maxZ), new Vector2(minX, maxZ) };   // counter-clockwise

        static GameObject MeshPart(Transform parent, string name, Mesh mesh, Material material)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;   // ground slabs: nothing to see in their shadow
            return go;
        }

        // A wooden fence from a to b (x/z): a post every ~2.2 units and two rails.
        static void BuildFence(Transform root, Vector2 a, Vector2 b, ToonPalette p)
        {
            var length = Vector2.Distance(a, b);
            var dir = (b - a) / length;
            var rotation = Quaternion.LookRotation(new Vector3(dir.x, 0f, dir.y));
            var posts = Mathf.CeilToInt(length / 2.2f);
            for (var k = 0; k <= posts; k++)
            {
                var at = Vector2.Lerp(a, b, k / (float)posts);
                Prim.Cube(root, "Post", new Vector3(at.x, 0.55f, at.y), new Vector3(0.26f, 1.1f, 0.26f), p.trunk);
            }
            var mid = (a + b) * 0.5f;
            foreach (var height in new[] { 0.42f, 0.85f })
                Prim.Cube(root, "Rail", new Vector3(mid.x, height, mid.y), new Vector3(0.12f, 0.14f, length), p.crate).transform.localRotation = rotation;
        }

        // One recognisable thing per district. Local +x points towards the sea / away from the centre. On a coast it carries the water
        // parts (boat, pier); on the map (onLand) those are left out.
        internal static void BuildLandmark(Transform parent, ToonPalette p, int index, Vector3 localPosition, float outwardDegrees, float scale, bool onLand)
        {
            var world = new GameObject("Landmark").transform;
            world.SetParent(parent, false);
            world.localPosition = localPosition;
            world.localRotation = Quaternion.Euler(0f, -outwardDegrees, 0f);
            world.localScale = Vector3.one * scale;
            var solid = !onLand;   // landmarks in the district are solid; on the map they are only a picture
            world.gameObject.AddComponent<MergeChildren>();   // its dozen parts become one mesh per colour on first use (solid parts keep their colliders)

            switch (index)
            {
                case 0:   // Sahil: beach umbrella and a rowing boat on the water
                    Prim.SplitCollider(Prim.Cylinder(world, "Pole", new Vector3(0f, 1.1f, 0f), new Vector3(0.08f, 1.1f, 0.08f), p.scaffold, solid));
                    Prim.Sphere(world, "Canopy", new Vector3(0f, 2.2f, 0f), new Vector3(2.4f, 0.7f, 2.4f), p.roof);
                    if (onLand) break;
                    Prim.Cube(world, "Hull", new Vector3(3.6f, -1.75f, 0f), new Vector3(2.8f, 0.5f, 1.2f), p.crate);
                    Prim.Cylinder(world, "Mast", new Vector3(3.6f, -0.9f, 0f), new Vector3(0.06f, 0.9f, 0.06f), p.scaffold);
                    Prim.Cube(world, "Sail", new Vector3(3.6f, -0.7f, 0f), new Vector3(0.05f, 1.0f, 0.9f), p.flag);
                    break;
                case 1:   // Merkez: fountain
                    Prim.SplitCollider(Prim.Cylinder(world, "Basin", new Vector3(0f, 0.25f, 0f), new Vector3(3.2f, 0.25f, 3.2f), p.stone, solid));
                    Prim.Cylinder(world, "Water", new Vector3(0f, 0.52f, 0f), new Vector3(2.7f, 0.02f, 2.7f), p.sea);
                    Prim.Cylinder(world, "Pillar", new Vector3(0f, 1.2f, 0f), new Vector3(0.45f, 0.8f, 0.45f), p.stone);
                    Prim.Sphere(world, "Drop", new Vector3(0f, 2.2f, 0f), Vector3.one * 0.7f, p.window);
                    break;
                case 2:   // Liman: pier with a crane
                    if (!onLand)
                    {
                        Prim.Cube(world, "Pier", new Vector3(3f, 0.1f, 0f), new Vector3(7f, 0.3f, 2.2f), p.crate);
                        Prim.Cylinder(world, "Piling", new Vector3(6.2f, -0.9f, 0.9f), new Vector3(0.25f, 1.2f, 0.25f), p.trunk);
                        Prim.Cylinder(world, "Piling", new Vector3(6.2f, -0.9f, -0.9f), new Vector3(0.25f, 1.2f, 0.25f), p.trunk);
                    }
                    Prim.SplitCollider(Prim.Cylinder(world, "CraneMast", new Vector3(0f, 2.2f, 1.8f), new Vector3(0.3f, 2.2f, 0.3f), p.cone, solid));
                    Prim.Cube(world, "CraneArm", new Vector3(1.4f, 4.3f, 1.8f), new Vector3(3.6f, 0.3f, 0.3f), p.cone);
                    Prim.Cube(world, "Hook", new Vector3(2.8f, 3.5f, 1.8f), new Vector3(0.1f, 1.4f, 0.1f), p.scaffold);
                    break;
                case 3:   // Tepe: windmill
                    Prim.SplitCollider(Prim.Cylinder(world, "Tower", new Vector3(0f, 1.8f, 0f), new Vector3(1.6f, 1.8f, 1.6f), p.walls[1], solid));
                    Prim.Cylinder(world, "Cap", new Vector3(0f, 3.8f, 0f), new Vector3(1.9f, 0.2f, 1.9f), p.roof);
                    var hub = new GameObject("Blades").transform;
                    hub.SetParent(world, false);
                    hub.localPosition = new Vector3(-1.0f, 3.2f, 0f);
                    hub.localRotation = Quaternion.Euler(30f, 0f, 0f);
                    Prim.Cube(hub, "BladeA", Vector3.zero, new Vector3(0.12f, 3.6f, 0.45f), p.door);
                    Prim.Cube(hub, "BladeB", Vector3.zero, new Vector3(0.12f, 0.45f, 3.6f), p.door);
                    break;
                default:  // Sanayi: chimney and a storage tank
                    Prim.SplitCollider(Prim.Cylinder(world, "Chimney", new Vector3(0f, 3f, 0f), new Vector3(1.2f, 3f, 1.2f), p.stone, solid));
                    Prim.Cylinder(world, "Band", new Vector3(0f, 5.6f, 0f), new Vector3(1.3f, 0.35f, 1.3f), p.roof);
                    Prim.SplitCollider(Prim.Cylinder(world, "Tank", new Vector3(3f, 1.1f, 0f), new Vector3(2.2f, 1.1f, 2.2f), p.hammerHead, solid));
                    Prim.Sphere(world, "TankTop", new Vector3(3f, 2.2f, 0f), new Vector3(2.2f, 0.9f, 2.2f), p.hammerHead);
                    break;
            }
        }

        internal static void BuildTree(Transform parent, Vector3 pos, float scale, ToonPalette p, bool collider = true)
        {
            var tree = new GameObject("Tree").transform;
            tree.SetParent(parent, false);
            tree.localPosition = pos;
            tree.localScale = Vector3.one * scale;
            var trunk = Prim.Cylinder(tree, "Trunk", new Vector3(0f, 0.7f, 0f), new Vector3(0.4f, 0.7f, 0.4f), p.trunk, collider);
            if (collider) Prim.SplitCollider(trunk);   // so the trunk can merge with the other props
            Prim.Sphere(tree, "LeavesLow", new Vector3(0f, 1.9f, 0f), Vector3.one * 2.3f, p.leaves);
            Prim.Sphere(tree, "LeavesHigh", new Vector3(0.1f, 2.9f, 0.05f), Vector3.one * 1.6f, p.leaves);
        }

        // The bare-earth oval around a building: its footprint plus room to walk and for helpers to stand.
        static Vector2 YardSize(BuildPlan plan) => BuildingKits.Footprint(plan) + new Vector2(4.2f, 4.2f);

        static BuildSite BuildPlot(Transform parent, int index, BuildPlan plan, Vector3 localPosition, ToonPalette palette, TMP_FontAsset font, Material outlineMat)
        {
            var go = new GameObject($"Plot {index + 1} - {plan.Name}");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;

            var yard = YardSize(plan);
            var dirt = Prim.NoShadow(Prim.Cylinder(go.transform, "Ground", new Vector3(0f, 0.015f, 0f), new Vector3(yard.x, 0.015f, yard.y), palette.dirt));
            dirt.transform.SetParent(go.transform, false);

            var site = go.AddComponent<BuildSite>();
            EditorWire.Set(site, "palette", palette);
            EditorWire.Set(site, "hitFx", BuildHitFx(go.transform));
            EditorWire.Set(site, "completeFx", BuildConfetti(go.transform));

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(go.transform, false);
            var label = labelGo.AddComponent<TextMeshPro>();
            label.font = font;
            label.fontSharedMaterial = outlineMat;
            label.fontSize = 5f;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.rectTransform.sizeDelta = new Vector2(10f, 2f);
            label.text = plan.Name;
            return site;
        }

        // ---------- particles ----------

        static Material ParticleMaterial()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(ParticleMatPath);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
                AssetDatabase.CreateAsset(mat, ParticleMatPath);
            }
            mat.SetColor("_BaseColor", Color.white);   // actual colour comes from the particle start colour
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static ParticleSystem NewBurstSystem(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.enableGPUInstancing = false;   // instanced mesh particles need a compute buffer that Vulkan (Android) did not provide: the draw was skipped
            renderer.mesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            renderer.sharedMaterial = ParticleMaterial();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            return ps;
        }

        static ParticleSystem BuildHitFx(Transform parent)
        {
            var ps = NewBurstSystem(parent, "HitFx");
            var main = ps.main;
            main.duration = 0.5f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.45f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.22f);
            main.gravityModifier = 1.6f;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.86f, 0.25f), new Color(1f, 0.6f, 0.2f));
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.15f;
            return ps;
        }

        static ParticleSystem BuildConfetti(Transform parent)
        {
            var ps = NewBurstSystem(parent, "CompleteFx");
            ps.transform.localPosition = new Vector3(0f, 3f, 0f);
            var main = ps.main;
            main.duration = 0.6f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 1.9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(5f, 9f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.34f);
            main.startRotation3D = true;
            main.startRotationX = new ParticleSystem.MinMaxCurve(0f, 6.28f);
            main.startRotationY = new ParticleSystem.MinMaxCurve(0f, 6.28f);
            main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, 6.28f);
            main.gravityModifier = 1.3f;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(1f, 0.35f, 0.4f), 0f),
                    new GradientColorKey(new Color(1f, 0.85f, 0.2f), 0.33f),
                    new GradientColorKey(new Color(0.3f, 0.85f, 0.5f), 0.66f),
                    new GradientColorKey(new Color(0.3f, 0.6f, 1f), 1f),
                },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            main.startColor = new ParticleSystem.MinMaxGradient(gradient) { mode = ParticleSystemGradientMode.RandomColor };

            var emission = ps.emission;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 45) });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 40f;
            shape.radius = 0.6f;
            shape.rotation = new Vector3(-90f, 0f, 0f);   // cone points up
            return ps;
        }

        // One-shot effects of a place (see FeelFx): all four systems are moved to where they are wanted and fired with Emit.
        internal static FeelFx BuildFeelFx(Transform parent)
        {
            var go = new GameObject("FeelFx");
            go.transform.SetParent(parent, false);
            var fx = go.AddComponent<FeelFx>();

            var dust = NewBurstSystem(go.transform, "Dust");
            var d = dust.main;
            d.duration = 0.6f;
            d.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.6f);
            d.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 2.2f);
            d.startSize = new ParticleSystem.MinMaxCurve(0.2f, 0.45f);
            d.gravityModifier = -0.12f;
            d.startColor = new ParticleSystem.MinMaxGradient(new Color(0.93f, 0.88f, 0.78f), Color.white);
            var dustShape = dust.shape;
            dustShape.shapeType = ParticleSystemShapeType.Sphere;
            dustShape.radius = 0.25f;
            FadeAndShrink(dust);

            var ring = NewBurstSystem(go.transform, "Ring");
            var r = ring.main;
            r.duration = 0.6f;
            r.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.55f);
            r.startSpeed = new ParticleSystem.MinMaxCurve(3f, 4.5f);
            r.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.3f);
            r.gravityModifier = 0f;
            r.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.97f, 0.86f), new Color(1f, 0.88f, 0.55f));
            var ringShape = ring.shape;
            ringShape.shapeType = ParticleSystemShapeType.Circle;
            ringShape.radiusThickness = 0f;   // from the edge only: a ring that spreads outwards
            ringShape.rotation = new Vector3(-90f, 0f, 0f);   // lying on the ground
            FadeAndShrink(ring);

            var sparkles = NewBurstSystem(go.transform, "Sparkles");
            var sp = sparkles.main;
            sp.duration = 1f;
            sp.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.3f);
            sp.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.4f);
            sp.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.36f);
            sp.gravityModifier = -0.1f;
            sp.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.9f, 0.4f), new Color(1f, 1f, 0.85f));
            var sparkleShape = sparkles.shape;
            sparkleShape.shapeType = ParticleSystemShapeType.Box;
            FadeAndShrink(sparkles);

            var firework = NewBurstSystem(go.transform, "Firework");
            var f = firework.main;
            f.duration = 1.5f;
            f.startLifetime = new ParticleSystem.MinMaxCurve(1f, 1.5f);
            f.startSpeed = new ParticleSystem.MinMaxCurve(5f, 9f);
            f.startSize = new ParticleSystem.MinMaxCurve(0.22f, 0.4f);
            f.gravityModifier = 0.8f;
            var fireShape = firework.shape;
            fireShape.shapeType = ParticleSystemShapeType.Sphere;
            fireShape.radius = 0.25f;
            FadeAndShrink(firework);

            EditorWire.Set(fx, "dust", dust);
            EditorWire.Set(fx, "ring", ring);
            EditorWire.Set(fx, "sparkles", sparkles);
            EditorWire.Set(fx, "firework", firework);
            return fx;
        }

        // Particles fade out and shrink towards the end of their life.
        static void FadeAndShrink(ParticleSystem ps)
        {
            var colour = ps.colorOverLifetime;
            colour.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
            colour.color = gradient;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.25f));
        }

        // ---------- player / camera ----------

        static PlayerController BuildPlayer(GameController game)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ArtBuilder.WorkerPrefabPath);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.name = "Player";
            go.transform.position = Vector3.zero;

            var cc = go.AddComponent<CharacterController>();
            cc.height = 1.9f;
            cc.radius = 0.42f;
            cc.center = new Vector3(0f, 0.97f, 0f);
            go.AddComponent<GameInput>();

            var visual = go.GetComponent<WorkerVisual>();
            var controller = go.AddComponent<PlayerController>();
            EditorWire.Set(controller, "visual", visual);

            var hammer = go.AddComponent<PlayerHammer>();
            EditorWire.Set(hammer, "game", game);
            EditorWire.Set(hammer, "visual", visual);

            return controller;
        }

        static IsoCameraFollow BuildCamera(Transform target)
        {
            var go = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            go.tag = "MainCamera";
            var cam = go.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.62f, 0.88f, 1f);
            cam.fieldOfView = 30f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 450f;

            var follow = go.AddComponent<IsoCameraFollow>();
            EditorWire.Set(follow, "target", target);
            var offset = new Vector3(-19f, 21f, -19f);
            go.transform.SetPositionAndRotation(target.position + offset, Quaternion.LookRotation(-offset));
            return follow;
        }

        // ---------- HUD ----------

        static readonly Color PanelColor = new Color(0.43f, 0.35f, 0.74f);   // tints the white kit panel
        static readonly Color InkColor = new Color(0.17f, 0.13f, 0.2f);

        static void BuildHud(TMP_FontAsset font, Material outlineMat, AudioManager audioManager, out GameHud hud, out DashboardHud dashboard, out ScreenFade fade,
            out CoinFlight coinFlight, out CeremonyBanner banner, out ColaPointer colaPointer, out ColaPointer chestPointer)
        {
            var es = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(InputSystemUIInputModule));
            es.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();

            var canvasGo = new GameObject("HUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            // Everything lives under the safe area (notch, Dynamic Island, home indicator); only the fade covers the whole canvas.
            var safeArea = new GameObject("SafeArea", typeof(RectTransform), typeof(SafeAreaFitter)).GetComponent<RectTransform>();
            safeArea.SetParent(canvasGo.transform, false);
            safeArea.anchorMin = Vector2.zero;
            safeArea.anchorMax = Vector2.one;
            safeArea.offsetMin = safeArea.offsetMax = Vector2.zero;
            var root = (Transform)safeArea;

            var rounded = ArtBuilder.EnsureRoundedSprite();   // plain dark plates for on-screen messages
            var kit = UiKitBuilder.Panel;
            var buttonSprite = UiKitBuilder.Button;
            var knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");

            // Dashboard first so it sits behind the in-game widgets in the hierarchy.
            dashboard = BuildDashboard(root, kit, buttonSprite, rounded, font, outlineMat);

            // Always visible: coins and level (top-left), hints and the big centre messages.
            var coinPanel = Panel(root, "CoinPanel", kit, PanelColor, new Vector2(0f, 1f), new Vector2(64f, -40f), new Vector2(380f, 100f));
            var coinIcon = MakeImage(coinPanel, "Coin", UiKitBuilder.Coin, Color.white, new Vector2(0f, 0.5f), new Vector2(14f, 4f), new Vector2(76f, 76f));
            var coinsText = Label(coinPanel, "Coins", font, outlineMat, 58, Color.white, TextAlignmentOptions.MidlineLeft,
                new Vector2(0f, 0.5f), new Vector2(104f, 0f), new Vector2(250f, 90f));

            var levelPanel = Panel(root, "LevelPanel", kit, PanelColor, new Vector2(0f, 1f), new Vector2(64f, -150f), new Vector2(380f, 84f));
            EnergyRow(levelPanel, UiKitBuilder.Star, font, "SEVİYE", -8f, out var levelLabel, out _, out var levelFill, out var levelFillImage);
            levelFillImage.color = new Color(0.45f, 0.82f, 1f);

            // Every on-screen message sits on a translucent dark plate so it stays readable over any background.
            var hintPlate = TextPlate(root, "HintPlate", rounded, font, outlineMat, 34, new Color(1f, 0.96f, 0.8f), new Vector2(0.5f, 0f), new Vector2(0f, 205f));

            // Only while inside a district: the player's energy, the active building, shop, cola, back to the map, touch stick.
            var groupRt = Rect(root, "DistrictGroup", new Vector2(0f, 0f), Vector2.zero, Vector2.zero);
            groupRt.anchorMin = Vector2.zero;
            groupRt.anchorMax = Vector2.one;
            groupRt.offsetMin = groupRt.offsetMax = Vector2.zero;
            var group = groupRt.transform;

            var buildingPlate = TextPlate(group, "BuildingPlate", rounded, font, outlineMat, 46, Color.white, new Vector2(0.5f, 1f), new Vector2(0f, -40f));
            var boostPlate = TextPlate(group, "BoostPlate", rounded, font, outlineMat, 52, new Color(1f, 0.65f, 0.2f), new Vector2(0.5f, 1f), new Vector2(0f, -130f));

            var energyPanel = Panel(group, "EnergyPanel", kit, PanelColor, new Vector2(0f, 1f), new Vector2(64f, -245f), new Vector2(380f, 84f));
            EnergyRow(energyPanel, UiKitBuilder.Bolt, font, "ENERJİ", -8f, out var energyLabel, out var energyBack, out var playerFill, out var playerFillImage);

            var exitButton = TintedButton(group, "ExitButton", buttonSprite, font, new Vector2(1f, 1f), new Vector2(-40f, -40f), new Vector2(300f, 90f),
                new Color(0.62f, 0.85f, 1f), 34, out var exitLabel);
            exitLabel.text = "HARİTA";

            // Bottom-right, where the right thumb rests: shop and the cola you carry.
            var shopButton = TintedButton(group, "ShopButton", buttonSprite, font, new Vector2(1f, 0f), new Vector2(-40f, 40f), new Vector2(300f, 100f),
                new Color(1f, 0.79f, 0.24f), 36, out var shopLabel);
            shopLabel.text = "MAĞAZA";
            var colaUseButton = TintedButton(group, "ColaUseButton", buttonSprite, font, new Vector2(1f, 0f), new Vector2(-40f, 160f), new Vector2(300f, 100f),
                new Color(1f, 0.93f, 0.35f), 32, out var colaUseLabel);

            var shop = BuildShop(group, kit, buttonSprite, font);
            BuildTouchControls(group, knob);

            // The big-moment ribbon and the flying coins (Game.Feel). They sit above the HUD widgets but below the settings window and the fade.
            coinFlight = BuildCoinFlight(root, coinIcon.rectTransform);
            banner = BuildBanner(root, kit, font, outlineMat);
            colaPointer = BuildColaPointer(group, font, outlineMat, "ColaPointer", "Kola", new Color(1f, 0.88f, 0.3f));
            chestPointer = BuildColaPointer(group, font, outlineMat, "ChestPointer", "Sandık", new Color(0.55f, 0.85f, 1f));

            // Settings gear, top-right on every screen (GameHud shifts it left of the map button inside a district). It opens the
            // settings window, which is built last so it covers everything.
            var audioGroup = Rect(root, "AudioGroup", new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            var settingsButton = TintedButton(audioGroup, "SettingsButton", buttonSprite, font, new Vector2(1f, 1f), new Vector2(-40f, -40f), new Vector2(90f, 90f),
                new Color(0.85f, 0.8f, 1f), 28, out var settingsLabel);
            settingsLabel.text = "";
            var gear = MakeImage(settingsButton.transform, "Gear", UiKitBuilder.Gear, Color.white, new Vector2(0.5f, 0.5f), new Vector2(0f, 4f), new Vector2(62f, 62f));
            gear.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            var settings = BuildSettings(root, kit, buttonSprite, font, audioManager, settingsButton);

            hud = canvasGo.AddComponent<GameHud>();
            EditorWire.Set(hud, "audioGroup", audioGroup);
            EditorWire.Set(hud, "districtGroup", groupRt.gameObject);
            EditorWire.Set(hud, "coinsText", coinsText);
            EditorWire.Set(hud, "coinPanel", coinPanel.GetComponent<RectTransform>());
            EditorWire.Set(hud, "levelPanel", levelPanel.GetComponent<RectTransform>());
            EditorWire.Set(hud, "buildingPlate", buildingPlate);
            EditorWire.Set(hud, "hintPlate", hintPlate);
            EditorWire.Set(hud, "boostPlate", boostPlate);
            EditorWire.Set(hud, "shop", shop);
            EditorWire.Set(hud, "shopButton", shopButton);
            EditorWire.Set(hud, "exitButton", exitButton);
            EditorWire.Set(hud, "levelLabel", levelLabel);
            EditorWire.Set(hud, "levelFill", levelFill);
            EditorWire.Set(hud, "levelFillImage", levelFillImage);
            EditorWire.Set(hud, "colaUseButton", colaUseButton);
            EditorWire.Set(hud, "colaUseLabel", colaUseLabel);
            EditorWire.Set(hud, "playerEnergyLabel", energyLabel);
            EditorWire.Set(hud, "playerEnergyBack", energyBack);
            EditorWire.Set(hud, "playerEnergyFill", playerFill);
            EditorWire.Set(hud, "playerEnergyImage", playerFillImage);

            // Covers every view change (map <-> district); last in the canvas so it is above everything, never blocks taps.
            var fadeRt = Rect(canvasGo.transform, "Fade", new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Stretch(fadeRt, 0f, 0f);
            var fadeImage = fadeRt.gameObject.AddComponent<Image>();
            fadeImage.color = new Color(0.12f, 0.09f, 0.22f, 1f);
            fadeImage.raycastTarget = false;
            fade = fadeRt.gameObject.AddComponent<ScreenFade>();
            EditorWire.Set(fade, "cover", fadeImage);

            // A tick for every plain button; purchases and the cola button have sounds of their own.
            var silent = new System.Collections.Generic.HashSet<string> { "HammerButton", "WorkerButton", "StaminaButton", "SpeedButton", "ColaBuyButton", "ColaUseButton" };
            var ticking = new System.Collections.Generic.List<Object>();
            foreach (var button in canvasGo.GetComponentsInChildren<Button>(true))
                if (!silent.Contains(button.name)) ticking.Add(button);
            var clicks = canvasGo.AddComponent<UiClickSounds>();
            EditorWire.Set(clicks, "audioManager", audioManager);
            EditorWire.SetArray(clicks, "buttons", ticking.ToArray());
        }

        // An arrow with a distance label that ColaPointer moves to the screen edge; the host stays active, the arrow object is shown only when needed.
        static ColaPointer BuildColaPointer(Transform parent, TMP_FontAsset font, Material outlineMat, string name, string labelText, Color tint)
        {
            var host = Rect(parent, name, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var rootRt = Rect(host, "Pointer", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(240f, 160f));
            rootRt.pivot = new Vector2(0.5f, 0.5f);
            var arrow = MakeImage(rootRt, "Arrow", UiKitBuilder.Arrow, tint, new Vector2(0.5f, 0.5f), new Vector2(0f, 26f), new Vector2(88f, 88f));
            arrow.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            var label = Label(rootRt, "Distance", font, outlineMat, 34, tint, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0f, -40f), new Vector2(240f, 50f));
            label.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            var pointer = host.gameObject.AddComponent<ColaPointer>();
            EditorWire.SetString(pointer, "label", labelText);
            EditorWire.Set(pointer, "root", rootRt);
            EditorWire.Set(pointer, "arrow", arrow.rectTransform);
            EditorWire.Set(pointer, "distance", label);
            rootRt.gameObject.SetActive(false);
            return pointer;
        }

        static CoinFlight BuildCoinFlight(Transform root, RectTransform target)
        {
            var layer = Rect(root, "CoinLayer", new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Stretch(layer, 0f, 0f);
            var images = new Image[24];
            for (var i = 0; i < images.Length; i++)
            {
                images[i] = MakeImage(layer, $"Coin {i}", UiKitBuilder.Coin, Color.white, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(46f, 46f));
                images[i].rectTransform.pivot = new Vector2(0.5f, 0.5f);
                images[i].gameObject.SetActive(false);
            }
            var flight = layer.gameObject.AddComponent<CoinFlight>();
            EditorWire.SetArray(flight, "coins", images);
            EditorWire.Set(flight, "target", target);
            return flight;
        }

        // A ribbon with a big title and a smaller line under it, and a pool of stars for the level burst. Hidden until a ceremony plays.
        static CeremonyBanner BuildBanner(Transform root, Sprite kit, TMP_FontAsset font, Material outlineMat)
        {
            var host = Rect(root, "Ceremony", new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Stretch(host, 0f, 0f);
            var ribbonRt = Rect(host, "Ribbon", new Vector2(0.5f, 0.5f), new Vector2(0f, 170f), new Vector2(1060f, 270f));
            ribbonRt.pivot = new Vector2(0.5f, 0.5f);
            var group = ribbonRt.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            var ribbon = ribbonRt.gameObject.AddComponent<Image>();
            ribbon.sprite = kit;
            ribbon.type = Image.Type.Sliced;
            ribbon.raycastTarget = false;

            foreach (var side in new[] { -1f, 1f })
            {
                var star = MakeImage(ribbonRt, side < 0f ? "StarLeft" : "StarRight", UiKitBuilder.Star, Color.white, new Vector2(0.5f, 0.5f), new Vector2(side * 470f, 0f), new Vector2(110f, 110f));
                star.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            }

            var title = Label(ribbonRt, "Title", font, outlineMat, 92, Color.white, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0f, 38f), new Vector2(790f, 120f));
            title.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            title.enableAutoSizing = true;   // long names ("SANAYİ TAMAMLANDI") shrink to stay between the side stars
            title.fontSizeMin = 54f;
            title.fontSizeMax = 92f;
            var subtitle = Label(ribbonRt, "Subtitle", font, null, 46, new Color(0.17f, 0.13f, 0.2f), TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0f, -66f), new Vector2(880f, 64f));
            subtitle.rectTransform.pivot = new Vector2(0.5f, 0.5f);

            var burst = new Image[10];
            for (var i = 0; i < burst.Length; i++)
            {
                burst[i] = MakeImage(ribbonRt, $"Burst {i}", UiKitBuilder.Star, Color.white, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(78f, 78f));
                burst[i].rectTransform.pivot = new Vector2(0.5f, 0.5f);
                burst[i].gameObject.SetActive(false);
            }

            var banner = host.gameObject.AddComponent<CeremonyBanner>();
            EditorWire.Set(banner, "root", ribbonRt);
            EditorWire.Set(banner, "group", group);
            EditorWire.Set(banner, "ribbon", ribbon);
            EditorWire.Set(banner, "titleRect", title.rectTransform);
            EditorWire.Set(banner, "title", title);
            EditorWire.Set(banner, "subtitle", subtitle);
            EditorWire.SetArray(banner, "burst", burst);
            return banner;
        }

        // Centred window with a slider for the music level and one for the effects level, over a dimmed screen.
        static SettingsPanel BuildSettings(Transform root, Sprite kit, Sprite buttonSprite, TMP_FontAsset font, AudioManager audioManager, Button openButton)
        {
            var host = Rect(root, "Settings", new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            host.anchorMin = Vector2.zero;
            host.anchorMax = Vector2.one;
            host.offsetMin = host.offsetMax = Vector2.zero;

            var window = Rect(host, "Window", new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            window.anchorMin = Vector2.zero;
            window.anchorMax = Vector2.one;
            window.offsetMin = window.offsetMax = Vector2.zero;
            // The dim layer is a sibling of the panel, not its parent: a click on a non-interactive child bubbles up to the nearest
            // parent handler, which would make every tap inside the panel hit this button and close the window.
            var dimRt = Rect(window, "Dim", new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Stretch(dimRt, 0f, 0f);
            var dim = dimRt.gameObject.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0.05f, 0.55f);
            var overlayButton = dimRt.gameObject.AddComponent<Button>();
            overlayButton.targetGraphic = dim;
            overlayButton.transition = Selectable.Transition.None;

            var panel = Rect(window, "Panel", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760f, 600f));
            panel.pivot = new Vector2(0.5f, 0.5f);
            var back = panel.gameObject.AddComponent<Image>();
            back.sprite = kit;
            back.type = Image.Type.Sliced;
            back.color = PanelColor;   // a raycast target, so taps on the panel never reach the dim layer behind it

            var title = Label(panel, "Title", font, null, 52, Color.white, TextAlignmentOptions.Center, new Vector2(0.5f, 1f), new Vector2(0f, -28f), new Vector2(500f, 70f));
            title.rectTransform.pivot = new Vector2(0.5f, 1f);
            title.text = "AYARLAR";
            var close = TintedButton(panel, "CloseButton", buttonSprite, font, new Vector2(1f, 1f), new Vector2(-18f, -18f), new Vector2(72f, 72f),
                new Color(1f, 0.45f, 0.4f), 38, out var closeLabel);
            closeLabel.text = "X";

            SettingsRow(panel, kit, font, "MÜZİK", -150f, out var musicSlider, out var musicValue);
            SettingsRow(panel, kit, font, "SES", -290f, out var sfxSlider, out var sfxValue);

            var shakeLabelText = Label(panel, "ShakeLabel", font, null, 36, Color.white, TextAlignmentOptions.MidlineLeft, new Vector2(0f, 1f), new Vector2(44f, -420f), new Vector2(430f, 56f));
            shakeLabelText.text = "EKRAN SARSINTISI";
            var shakeButton = TintedButton(panel, "ShakeButton", buttonSprite, font, new Vector2(1f, 1f), new Vector2(-44f, -412f), new Vector2(210f, 72f),
                new Color(0.5f, 0.9f, 0.55f), 34, out var shakeStateLabel);

            var tip = Label(panel, "Tip", font, null, 26, new Color(1f, 1f, 1f, 0.75f), TextAlignmentOptions.Center, new Vector2(0.5f, 0f), new Vector2(0f, 26f), new Vector2(660f, 40f));
            tip.rectTransform.pivot = new Vector2(0.5f, 0f);
            tip.text = "Esc ile de açılır ve kapanır";

            var settings = host.gameObject.AddComponent<SettingsPanel>();
            EditorWire.Set(settings, "audioManager", audioManager);
            EditorWire.Set(settings, "window", window.gameObject);
            EditorWire.Set(settings, "openButton", openButton);
            EditorWire.Set(settings, "closeButton", close);
            EditorWire.Set(settings, "overlayButton", overlayButton);
            EditorWire.Set(settings, "musicSlider", musicSlider);
            EditorWire.Set(settings, "musicValue", musicValue);
            EditorWire.Set(settings, "sfxSlider", sfxSlider);
            EditorWire.Set(settings, "sfxValue", sfxValue);
            EditorWire.Set(settings, "shakeButton", shakeButton);
            EditorWire.Set(settings, "shakeLabel", shakeStateLabel);
            window.gameObject.SetActive(false);
            return settings;
        }

        static void SettingsRow(Transform panel, Sprite kit, TMP_FontAsset font, string title, float y, out Slider slider, out TMP_Text value)
        {
            var label = Label(panel, title + "Label", font, null, 36, Color.white, TextAlignmentOptions.MidlineLeft, new Vector2(0f, 1f), new Vector2(44f, y), new Vector2(190f, 56f));
            label.text = title;

            var root = Rect(panel, title + "Slider", new Vector2(0f, 1f), new Vector2(240f, y - 8f), new Vector2(380f, 44f));
            var bg = Rect(root, "Background", new Vector2(0f, 0f), Vector2.zero, Vector2.zero);
            Stretch(bg, 0f, 0f);
            var bgImg = bg.gameObject.AddComponent<Image>();
            bgImg.sprite = UiKitBuilder.BarBack;
            bgImg.type = Image.Type.Sliced;
            bgImg.color = new Color(0.3f, 0.24f, 0.5f);

            var fillArea = Rect(root, "Fill Area", new Vector2(0f, 0f), Vector2.zero, Vector2.zero);
            Stretch(fillArea, 6f, 8f);
            var fill = Rect(fillArea, "Fill", new Vector2(0f, 0f), Vector2.zero, Vector2.zero);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(0.5f, 1f);
            fill.offsetMin = fill.offsetMax = Vector2.zero;
            var fillImg = fill.gameObject.AddComponent<Image>();
            fillImg.sprite = UiKitBuilder.BarFill;
            fillImg.type = Image.Type.Sliced;
            fillImg.color = new Color(0.45f, 0.82f, 1f);

            var slideArea = Rect(root, "Handle Slide Area", new Vector2(0f, 0f), Vector2.zero, Vector2.zero);
            Stretch(slideArea, 22f, -4f);   // 52 px tall (the bar is 44): a Slider stretches its handle over the whole slide area vertically
            var handle = Rect(slideArea, "Handle", new Vector2(0f, 0f), Vector2.zero, Vector2.zero);
            handle.pivot = new Vector2(0.5f, 0.5f);
            handle.sizeDelta = new Vector2(52f, 0f);
            var handleImg = handle.gameObject.AddComponent<Image>();
            handleImg.sprite = UiKitBuilder.Knob;

            slider = root.gameObject.AddComponent<Slider>();
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handleImg;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 1f;
            slider.transition = Selectable.Transition.None;

            value = Label(panel, title + "Value", font, null, 34, Color.white, TextAlignmentOptions.MidlineRight, new Vector2(1f, 1f), new Vector2(-34f, y), new Vector2(120f, 56f));
            value.rectTransform.pivot = new Vector2(1f, 1f);
        }

        // Fills the parent with the given horizontal and vertical margins.
        static void Stretch(RectTransform rt, float horizontal, float vertical)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(horizontal, vertical);
            rt.offsetMax = new Vector2(-horizontal, -vertical);
        }

        // One object that plays all music and effects; the clips are the synthesised WAVs from AudioBuilder.
        static AudioManager BuildAudio(GameController game)
        {
            var manager = new GameObject("Audio").AddComponent<AudioManager>();
            EditorWire.Set(manager, "game", game);
            var kinds = (SfxKind[])System.Enum.GetValues(typeof(SfxKind));
            var clips = new Object[kinds.Length];
            foreach (var kind in kinds) clips[(int)kind] = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioBuilder.SfxPath(kind));
            EditorWire.SetArray(manager, "sfx", clips);
            EditorWire.Set(manager, "musicDashboard", AssetDatabase.LoadAssetAtPath<AudioClip>(AudioBuilder.DashboardMusicPath));
            EditorWire.Set(manager, "musicDistrict", AssetDatabase.LoadAssetAtPath<AudioClip>(AudioBuilder.DistrictMusicPath));
            return manager;
        }

        // The map screen: world name with arrows on top, a hint at the bottom and one card per district (placed over the
        // district's position by DashboardHud every frame).
        static DashboardHud BuildDashboard(Transform root, Sprite kit, Sprite buttonSprite, Sprite rounded, TMP_FontAsset font, Material outlineMat)
        {
            var groupRt = Rect(root, "DashboardGroup", new Vector2(0f, 0f), Vector2.zero, Vector2.zero);
            groupRt.anchorMin = Vector2.zero;
            groupRt.anchorMax = Vector2.one;
            groupRt.offsetMin = groupRt.offsetMax = Vector2.zero;
            var group = groupRt.transform;

            var titlePanel = Panel(group, "WorldPanel", kit, PanelColor, new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(560f, 100f));
            titlePanel.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 1f);
            var worldName = Label(titlePanel, "WorldName", font, outlineMat, 60, Color.white, TextAlignmentOptions.Center,
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(520f, 90f));
            worldName.rectTransform.pivot = new Vector2(0.5f, 0.5f);

            var previous = TintedButton(group, "PreviousWorld", buttonSprite, font, new Vector2(0.5f, 1f), new Vector2(-340f, -45f), new Vector2(90f, 90f),
                new Color(1f, 0.86f, 0.35f), 44, out var previousLabel);
            previousLabel.text = "<";
            var next = TintedButton(group, "NextWorld", buttonSprite, font, new Vector2(0.5f, 1f), new Vector2(340f, -45f), new Vector2(90f, 90f),
                new Color(1f, 0.86f, 0.35f), 44, out var nextLabel);
            nextLabel.text = ">";
            // TintedButton pivots at its anchor; centre the arrows on their x so they sit symmetrically beside the title.
            previous.GetComponent<RectTransform>().pivot = next.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 1f);

            var tip = TextPlate(group, "TipPlate", rounded, font, outlineMat, 40, new Color(1f, 0.96f, 0.8f), new Vector2(0.5f, 0f), new Vector2(0f, 60f));

            var cards = new DistrictCard[Balance.Districts.Length];
            for (var i = 0; i < cards.Length; i++)
            {
                var button = TintedButton(group, $"Card {i + 1}", buttonSprite, font, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(300f, 120f),
                    new Color(1f, 0.86f, 0.35f), 40, out var label);
                var rt = button.GetComponent<RectTransform>();
                rt.pivot = new Vector2(0.5f, 0.5f);
                label.textWrappingMode = TextWrappingModes.NoWrap;
                var card = button.gameObject.AddComponent<DistrictCard>();
                EditorWire.Set(card, "rect", rt);
                EditorWire.Set(card, "button", button);
                EditorWire.Set(card, "back", button.GetComponent<Image>());
                EditorWire.Set(card, "label", label);
                cards[i] = card;
            }

            var dashboard = group.gameObject.AddComponent<DashboardHud>();
            EditorWire.Set(dashboard, "root", group.gameObject);
            EditorWire.SetArray(dashboard, "cards", cards);
            EditorWire.Set(dashboard, "worldName", worldName);
            EditorWire.Set(dashboard, "previousWorld", previous);
            EditorWire.Set(dashboard, "nextWorld", next);
            EditorWire.Set(dashboard, "tipPlate", tip);
            return dashboard;
        }

        static RectTransform Rect(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        static Transform Panel(Transform parent, string name, Sprite sprite, Color color, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var rt = Rect(parent, name, anchor, pos, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.type = Image.Type.Sliced;
            img.color = color;
            img.raycastTarget = false;
            return rt;
        }

        static Image MakeImage(Transform parent, string name, Sprite sprite, Color color, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var rt = Rect(parent, name, anchor, pos, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        static TMP_Text Label(Transform parent, string name, TMP_FontAsset font, Material material, float size, Color color,
            TextAlignmentOptions align, Vector2 anchor, Vector2 pos, Vector2 rectSize)
        {
            var rt = Rect(parent, name, anchor, pos, rectSize);
            var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.font = font;
            if (material != null) tmp.fontSharedMaterial = material;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = align;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.raycastTarget = false;
            tmp.text = name;
            return tmp;
        }

        // Side panel with two tabs: the upgrades (five cards) and the decorations (six rows). The ShopPanel component sits on an
        // always-active empty object so it can keep sliding the (otherwise inactive) panel in and out.
        static ShopPanel BuildShop(Transform root, Sprite kit, Sprite buttonSprite, TMP_FontAsset font)
        {
            var host = Rect(root, "Shop", new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            var shop = host.gameObject.AddComponent<ShopPanel>();

            var panelRt = Rect(host, "Panel", new Vector2(1f, 1f), new Vector2(-30f, -150f), new Vector2(520f, 800f));
            var back = panelRt.gameObject.AddComponent<Image>();
            back.sprite = kit;
            back.type = Image.Type.Sliced;
            back.color = PanelColor;

            var title = Label(panelRt, "Title", font, null, 40, Color.white, TextAlignmentOptions.MidlineLeft, new Vector2(0f, 1f), new Vector2(28f, -20f), new Vector2(300f, 56f));
            title.text = "MAĞAZA";
            var close = TintedButton(panelRt, "CloseButton", buttonSprite, font, new Vector2(1f, 1f), new Vector2(-16f, -16f), new Vector2(64f, 64f),
                new Color(1f, 0.45f, 0.4f), 38, out var closeLabel);
            closeLabel.text = "X";

            var upgradesTab = TintedButton(panelRt, "UpgradesTab", buttonSprite, font, new Vector2(0.5f, 1f), new Vector2(-128f, -88f), new Vector2(236f, 66f),
                new Color(1f, 0.82f, 0.3f), 28, out var upgradesTabLabel);
            upgradesTabLabel.text = "YÜKSELT";
            var decorTab = TintedButton(panelRt, "DecorTab", buttonSprite, font, new Vector2(0.5f, 1f), new Vector2(128f, -88f), new Vector2(236f, 66f),
                new Color(0.78f, 0.74f, 0.9f), 24, out var decorTabLabel);
            decorTabLabel.textWrappingMode = TextWrappingModes.Normal;
            decorTabLabel.text = "GÜZELLEŞTİR";
            foreach (var pivot in new[] { upgradesTab.GetComponent<RectTransform>(), decorTab.GetComponent<RectTransform>() }) pivot.pivot = new Vector2(0.5f, 1f);

            // --- upgrades tab
            var upgrades = Rect(panelRt, "Upgrades", new Vector2(0.5f, 1f), Vector2.zero, Vector2.zero);
            Stretch(upgrades, 0f, 0f);
            var hammer = ShopCard(upgrades, "HammerButton", buttonSprite, font, 0, new Color(1f, 0.79f, 0.24f), out var hammerLabel);
            var worker = ShopCard(upgrades, "WorkerButton", buttonSprite, font, 1, new Color(0.45f, 0.82f, 1f), out var workerLabel);
            var stamina = ShopCard(upgrades, "StaminaButton", buttonSprite, font, 2, new Color(0.55f, 0.9f, 0.55f), out var staminaLabel);
            var speed = ShopCard(upgrades, "SpeedButton", buttonSprite, font, 3, new Color(0.62f, 0.78f, 1f), out var speedLabel);
            var cola = ShopCard(upgrades, "ColaBuyButton", buttonSprite, font, 4, new Color(1f, 0.55f, 0.3f), out var colaLabel);

            // --- decorations tab
            var decor = Rect(panelRt, "Decorations", new Vector2(0.5f, 1f), Vector2.zero, Vector2.zero);
            Stretch(decor, 0f, 0f);
            var header = Label(decor, "Header", font, null, 28, Color.white, TextAlignmentOptions.Center, new Vector2(0.5f, 1f), new Vector2(0f, -166f), new Vector2(480f, 40f));
            header.rectTransform.pivot = new Vector2(0.5f, 1f);
            var decorButtons = new Button[Balance.DecorationCount];
            var decorLabels = new TMP_Text[Balance.DecorationCount];
            for (var i = 0; i < decorButtons.Length; i++)
            {
                decorButtons[i] = ShopCard(decor, $"Decoration{i + 1}Button", buttonSprite, font, i, Color.white, out decorLabels[i], top: 214f, step: 92f, height: 84f);
                decorLabels[i].fontSize = 26;
            }

            EditorWire.Set(shop, "panel", panelRt);
            EditorWire.Set(shop, "closeButton", close);
            EditorWire.Set(shop, "hammerButton", hammer);
            EditorWire.Set(shop, "hammerLabel", hammerLabel);
            EditorWire.Set(shop, "workerButton", worker);
            EditorWire.Set(shop, "workerLabel", workerLabel);
            EditorWire.Set(shop, "staminaButton", stamina);
            EditorWire.Set(shop, "staminaLabel", staminaLabel);
            EditorWire.Set(shop, "speedButton", speed);
            EditorWire.Set(shop, "speedLabel", speedLabel);
            EditorWire.Set(shop, "colaBuyButton", cola);
            EditorWire.Set(shop, "colaBuyLabel", colaLabel);
            EditorWire.Set(shop, "upgradesGroup", upgrades.gameObject);
            EditorWire.Set(shop, "decorGroup", decor.gameObject);
            EditorWire.Set(shop, "upgradesTab", upgradesTab);
            EditorWire.Set(shop, "decorTab", decorTab);
            EditorWire.Set(shop, "decorTabLabel", decorTabLabel);
            EditorWire.Set(shop, "decorHeader", header);
            EditorWire.SetArray(shop, "decorButtons", decorButtons);
            EditorWire.SetArray(shop, "decorLabels", decorLabels);
            decor.gameObject.SetActive(false);
            panelRt.gameObject.SetActive(false);
            return shop;
        }

        static Button ShopCard(Transform parent, string name, Sprite sprite, TMP_FontAsset font, int row, Color tint, out TMP_Text label,
            float top = 168f, float step = 114f, float height = 104f)
        {
            var button = TintedButton(parent, name, sprite, font, new Vector2(0.5f, 1f), new Vector2(0f, -top - row * step), new Vector2(472f, height), tint, 27, out label);
            label.textWrappingMode = TextWrappingModes.Normal;
            return button;
        }

        // On-screen joystick for touch devices. Everything is parented to a centre-anchored root because OnScreenStick
        // measures in canvas space (origin at the canvas centre). The stick appears wherever the thumb lands inside its zone.
        static void BuildTouchControls(Transform root, Sprite knob)
        {
            var joystick = Rect(root, "Joystick", new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var zone = new Vector2(-690f, -270f);

            var ring = MakeImage(joystick, "Zone", knob, new Color(1f, 1f, 1f, 0.12f), new Vector2(0.5f, 0.5f), zone, new Vector2(300f, 300f));
            ring.rectTransform.pivot = new Vector2(0.5f, 0.5f);

            var handleImage = MakeImage(joystick, "Handle", knob, new Color(1f, 1f, 1f, 0.55f), new Vector2(0.5f, 0.5f), zone, new Vector2(120f, 120f));
            handleImage.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            var stick = handleImage.gameObject.AddComponent<OnScreenStick>();
            EditorWire.SetString(stick, "m_ControlPath", "<Gamepad>/leftStick");
            EditorWire.SetFloat(stick, "m_MovementRange", 100f);
            EditorWire.SetFloat(stick, "m_DynamicOriginRange", 230f);
            EditorWire.SetInt(stick, "m_Behaviour", (int)OnScreenStick.Behaviour.ExactPositionWithDynamicOrigin);

            var controls = root.gameObject.AddComponent<TouchControls>();
            EditorWire.Set(controls, "joystick", joystick.gameObject);
            joystick.gameObject.SetActive(false);   // TouchControls turns it on for touch devices
        }

        static Button TintedButton(Transform parent, string name, Sprite sprite, TMP_FontAsset font, Vector2 anchor, Vector2 pos,
            Vector2 size, Color tint, float fontSize, out TMP_Text label)
        {
            var rt = Rect(parent, name, anchor, pos, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.type = Image.Type.Sliced;
            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = img;
            button.colors = new ColorBlock
            {
                normalColor = tint,
                highlightedColor = Color.Lerp(tint, Color.white, 0.35f),
                pressedColor = Color.Lerp(tint, Color.black, 0.2f),
                selectedColor = tint,
                disabledColor = new Color(0.62f, 0.6f, 0.7f, 0.9f),
                colorMultiplier = 1f,
                fadeDuration = 0.08f,
            };

            label = Label(rt, "Label", font, null, fontSize, InkColor, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0f, 4f), size - new Vector2(20f, 10f));   // +4: clear of the bottom lip
            label.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            return button;
        }

        // Dark translucent plate that grows with its text: Image + layout components, text as the only child.
        static UiPlate TextPlate(Transform parent, string name, Sprite sprite, TMP_FontAsset font, Material material, float fontSize,
            Color textColor, Vector2 anchor, Vector2 pos)
        {
            var rt = Rect(parent, name, anchor, pos, Vector2.zero);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.type = Image.Type.Sliced;
            img.color = new Color(0f, 0f, 0f, 0.5f);
            img.raycastTarget = false;

            var layout = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(28, 28, 10, 12);
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            var fitter = rt.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var label = Label(rt, "Text", font, material, fontSize, textColor, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(100f, 50f));
            label.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            label.text = " ";
            var plate = rt.gameObject.AddComponent<UiPlate>();
            EditorWire.Set(plate, "label", label);
            return plate;
        }

        // Label on top, bar underneath; the fill stretches to its back image so the HUD can drive it with anchorMax.x.
        // The icon hangs off the panel's left edge like a badge.
        static void EnergyRow(Transform panel, Sprite icon, TMP_FontAsset font, string title, float y,
            out TMP_Text label, out RectTransform backRect, out RectTransform fill, out Image fillImage)
        {
            label = Label(panel, title + "Label", font, null, 26, Color.white, TextAlignmentOptions.MidlineLeft,
                new Vector2(0f, 1f), new Vector2(18f, y), new Vector2(340f, 32f));
            label.text = title;
            var back = Rect(panel, title + "Bar", new Vector2(0f, 1f), new Vector2(18f, y - 36f), new Vector2(344f, 30f));
            var backImg = back.gameObject.AddComponent<Image>();
            backImg.sprite = UiKitBuilder.BarBack;
            backImg.type = Image.Type.Sliced;
            backImg.color = new Color(0.3f, 0.24f, 0.5f);   // groove tint: the sprite's ink outline stays dark
            backImg.raycastTarget = false;
            backRect = back;

            fill = Rect(back, "Fill", new Vector2(0f, 0f), Vector2.zero, Vector2.zero);
            fill.pivot = new Vector2(0f, 0.5f);
            fill.anchorMin = new Vector2(0f, 0f);
            fill.anchorMax = new Vector2(1f, 1f);
            fill.offsetMin = new Vector2(4f, 4f);
            fill.offsetMax = new Vector2(-4f, -4f);
            fillImage = fill.gameObject.AddComponent<Image>();
            fillImage.sprite = UiKitBuilder.BarFill;
            fillImage.type = Image.Type.Sliced;
            fillImage.raycastTarget = false;

            var badge = MakeImage(panel, title + "Icon", icon, Color.white, new Vector2(0f, 0.5f), new Vector2(-30f, 0f), new Vector2(64f, 64f));
            badge.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        }
    }
}
