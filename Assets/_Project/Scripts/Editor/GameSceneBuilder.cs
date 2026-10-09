using System.IO;
using Game.Audio;
using Game.Core;
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
        // Layout numbers below were first drawn for a district of radius 14; Grow stretches every distance inside a district so
        // the player has room to walk between the plots (buildings and props keep their size).
        const float Grow = 1.6f;
        const float PlotRing = 6.8f;
        const float DistrictRadius = 14f * Grow;
        const float DistrictGap = 1.16f;   // >1 leaves open water between neighbouring districts on the dashboard
        const float DistrictRing = DistrictRadius / 0.5878f * DistrictGap;   // distance of each district from the island centre (neighbours would touch at gap 1; sin 36 deg)

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

            BuildLight();
            BuildSea(palette);

            var floatingText = AssetDatabase.LoadAssetAtPath<GameObject>(ArtBuilder.FloatingTextPrefabPath).GetComponent<FloatingText>();
            var autoWorker = AssetDatabase.LoadAssetAtPath<GameObject>(ArtBuilder.AutoWorkerPrefabPath).GetComponent<AutoWorker>();

            var districts = new District[Balance.Districts.Length];
            for (var i = 0; i < districts.Length; i++) districts[i] = BuildDistrict(i, palette, font, outlineMat);

            var overview = new GameObject("OverviewTarget").transform;
            overview.position = Vector3.zero;

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
            var audioManager = BuildAudio(game);
            BuildHud(font, outlineMat, audioManager, out var hud, out var dashboard);

            EditorWire.SetArray(game, "districts", districts);
            EditorWire.Set(game, "workerPrefab", autoWorker);
            EditorWire.Set(game, "floatingTextPrefab", floatingText);
            EditorWire.Set(game, "hud", hud);
            EditorWire.Set(game, "dashboard", dashboard);
            EditorWire.Set(game, "cameraRig", cameraRig);
            EditorWire.Set(game, "overviewTarget", overview);
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
            Prim.Cube(sea, "Water", new Vector3(0f, -2.2f, 0f), new Vector3(700f, 0.2f, 700f), p.sea);
        }

        // One neighbourhood: a round island piece of its own tint, three plots, scenery and one landmark. The five
        // pieces sit on a ring around a small lake so that, seen together, they read as one island.
        static District BuildDistrict(int index, ToonPalette p, TMP_FontAsset font, Material outlineMat)
        {
            var def = Balance.Districts[index];
            var outward = 90f - index * 72f;
            var root = new GameObject($"District {index + 1} - {def.Name}");
            root.transform.position = PolarPoint(DistrictRing, outward);
            var t = root.transform;

            Prim.Cylinder(t, "IslandBase", new Vector3(0f, -1.5f, 0f), new Vector3(26f * Grow, 1f, 26f * Grow), p.dirt);
            Prim.Cylinder(t, "IslandTop", new Vector3(0f, -0.25f, 0f), new Vector3(28f * Grow, 0.25f, 28f * Grow), p.districtGround[index]);
            var ground = new GameObject("GroundCollider");
            ground.transform.SetParent(t, false);
            ground.transform.localPosition = new Vector3(0f, -0.5f, 0f);
            ground.AddComponent<BoxCollider>().size = new Vector3(30f * Grow, 1f, 30f * Grow);

            // Scenery on the rim, away from the plots and the centre; rotated per district so no two look alike.
            var turn = index * 37f;
            var treeAngles = new[] { 10f, 28f, 38f, 62f, 100f, 128f, 150f, 165f, 190f, 205f, 232f, 255f, 280f, 300f, 318f, 335f, 352f };
            for (var i = 0; i < treeAngles.Length; i++)
                BuildTree(t, Local(11.2f + (i % 3) * 0.9f, treeAngles[i] + turn), 0.9f + (i % 3) * 0.15f, p);

            // A few trees and rocks out in the open between the plots so the middle of the district is not bare.
            for (var i = 0; i < 6; i++)
            {
                var angle = 30f + i * 60f + turn;   // plots sit at 90/210/330, so 30/150/270 are the gaps
                var spot = Local(i % 2 == 0 ? 8.6f : 6.4f, angle + (i % 2 == 0 ? 0f : 17f));
                if (i % 2 == 0) BuildTree(t, spot, 0.8f, p);
                else Prim.Sphere(t, "Rock", spot + Vector3.up * 0.15f, new Vector3(1.0f, 0.6f, 0.8f), p.stone).transform.Rotate(0f, angle, 0f);
            }

            foreach (var spot in new[] { Local(10.2f, 62f + turn), Local(9.8f, 228f + turn) })
            {
                Prim.Cube(t, "Crate", spot + Vector3.up * 0.45f, Vector3.one * 0.9f, p.crate, true);
                Prim.Cube(t, "Crate", spot + new Vector3(0.9f, 0.45f, 0.3f), Vector3.one * 0.9f, p.crate, true).transform.Rotate(0f, 20f, 0f);
                Prim.Cube(t, "Crate", spot + new Vector3(0.3f, 1.35f, 0.1f), Vector3.one * 0.9f, p.crate).transform.Rotate(0f, 10f, 0f);
            }

            foreach (var spot in new[] { Local(4.2f, 0f + turn), Local(4.4f, 140f + turn), Local(3.9f, 320f + turn), Local(5.5f, 80f + turn), Local(5.2f, 250f + turn) })
            {
                Prim.Cylinder(t, "ConeBase", spot + Vector3.up * 0.04f, new Vector3(0.55f, 0.04f, 0.55f), p.cone);
                Prim.Cylinder(t, "ConeBody", spot + Vector3.up * 0.35f, new Vector3(0.3f, 0.3f, 0.3f), p.cone, true);
                Prim.Cylinder(t, "ConeTip", spot + Vector3.up * 0.7f, new Vector3(0.18f, 0.08f, 0.18f), p.cone);
            }

            foreach (var angle in new[] { 80f, 190f, 310f })
                Prim.Sphere(t, "Rock", Local(12.3f, angle + turn) + Vector3.up * 0.15f, new Vector3(1.2f, 0.7f, 0.9f), p.stone).transform.Rotate(0f, angle, 0f);

            BuildLandmark(index, t, p);

            var sites = new BuildSite[def.Plans.Length];
            for (var n = 0; n < sites.Length; n++)
                sites[n] = BuildPlot(t, n, def.Plans[n], Local(PlotRing, 90f + n * 120f), p, font, outlineMat);

            var playerSpawn = new GameObject("PlayerSpawn").transform;
            playerSpawn.SetParent(t, false);
            var workerSpawn = new GameObject("WorkerSpawn").transform;
            workerSpawn.SetParent(t, false);
            workerSpawn.localPosition = new Vector3(0f, 0f, 2.5f);

            var district = root.AddComponent<District>();
            EditorWire.SetArray(district, "sites", sites);
            EditorWire.Set(district, "playerSpawn", playerSpawn);
            EditorWire.Set(district, "workerSpawn", workerSpawn);
            EditorWire.SetFloat(district, "walkRadius", DistrictRadius - 1f);
            return district;
        }

        // One recognisable thing per district, on the sea side of the island (away from the lake) so it frames the overview.
        static void BuildLandmark(int index, Transform t, ToonPalette p)
        {
            var outward = 90f - index * 72f;
            var world = new GameObject("Landmark").transform;
            world.SetParent(t, false);
            world.localPosition = Local(11.5f, outward);
            world.localRotation = Quaternion.Euler(0f, -outward, 0f);   // local +x points away from the island centre

            switch (index)
            {
                case 0:   // Sahil: beach umbrella and a rowing boat on the water
                    Prim.Cylinder(world, "Pole", new Vector3(0f, 1.1f, 0f), new Vector3(0.08f, 1.1f, 0.08f), p.scaffold);
                    Prim.Sphere(world, "Canopy", new Vector3(0f, 2.2f, 0f), new Vector3(2.4f, 0.7f, 2.4f), p.roof);
                    Prim.Cube(world, "Hull", new Vector3(3.6f, -1.75f, 0f), new Vector3(2.8f, 0.5f, 1.2f), p.crate);
                    Prim.Cylinder(world, "Mast", new Vector3(3.6f, -0.9f, 0f), new Vector3(0.06f, 0.9f, 0.06f), p.scaffold);
                    Prim.Cube(world, "Sail", new Vector3(3.6f, -0.7f, 0f), new Vector3(0.05f, 1.0f, 0.9f), p.flag);
                    break;
                case 1:   // Merkez: fountain
                    Prim.Cylinder(world, "Basin", new Vector3(0f, 0.25f, 0f), new Vector3(3.2f, 0.25f, 3.2f), p.stone);
                    Prim.Cylinder(world, "Water", new Vector3(0f, 0.52f, 0f), new Vector3(2.7f, 0.02f, 2.7f), p.sea);
                    Prim.Cylinder(world, "Pillar", new Vector3(0f, 1.2f, 0f), new Vector3(0.45f, 0.8f, 0.45f), p.stone);
                    Prim.Sphere(world, "Drop", new Vector3(0f, 2.2f, 0f), Vector3.one * 0.7f, p.window);
                    break;
                case 2:   // Liman: pier with a crane
                    Prim.Cube(world, "Pier", new Vector3(3f, 0.1f, 0f), new Vector3(7f, 0.3f, 2.2f), p.crate);
                    Prim.Cylinder(world, "Piling", new Vector3(6.2f, -0.9f, 0.9f), new Vector3(0.25f, 1.2f, 0.25f), p.trunk);
                    Prim.Cylinder(world, "Piling", new Vector3(6.2f, -0.9f, -0.9f), new Vector3(0.25f, 1.2f, 0.25f), p.trunk);
                    Prim.Cylinder(world, "CraneMast", new Vector3(0f, 2.2f, 1.8f), new Vector3(0.3f, 2.2f, 0.3f), p.cone);
                    Prim.Cube(world, "CraneArm", new Vector3(1.4f, 4.3f, 1.8f), new Vector3(3.6f, 0.3f, 0.3f), p.cone);
                    Prim.Cube(world, "Hook", new Vector3(2.8f, 3.5f, 1.8f), new Vector3(0.1f, 1.4f, 0.1f), p.scaffold);
                    break;
                case 3:   // Tepe: windmill
                    Prim.Cylinder(world, "Tower", new Vector3(0f, 1.8f, 0f), new Vector3(1.6f, 1.8f, 1.6f), p.walls[1]);
                    Prim.Cylinder(world, "Cap", new Vector3(0f, 3.8f, 0f), new Vector3(1.9f, 0.2f, 1.9f), p.roof);
                    var hub = new GameObject("Blades").transform;
                    hub.SetParent(world, false);
                    hub.localPosition = new Vector3(-1.0f, 3.2f, 0f);
                    hub.localRotation = Quaternion.Euler(30f, 0f, 0f);
                    Prim.Cube(hub, "BladeA", Vector3.zero, new Vector3(0.12f, 3.6f, 0.45f), p.door);
                    Prim.Cube(hub, "BladeB", Vector3.zero, new Vector3(0.12f, 0.45f, 3.6f), p.door);
                    break;
                default:  // Sanayi: chimney and a storage tank
                    Prim.Cylinder(world, "Chimney", new Vector3(0f, 3f, 0f), new Vector3(1.2f, 3f, 1.2f), p.stone);
                    Prim.Cylinder(world, "Band", new Vector3(0f, 5.6f, 0f), new Vector3(1.3f, 0.35f, 1.3f), p.roof);
                    Prim.Cylinder(world, "Tank", new Vector3(3f, 1.1f, 0f), new Vector3(2.2f, 1.1f, 2.2f), p.hammerHead);
                    Prim.Sphere(world, "TankTop", new Vector3(3f, 2.2f, 0f), new Vector3(2.2f, 0.9f, 2.2f), p.hammerHead);
                    break;
            }
        }

        static void BuildTree(Transform parent, Vector3 pos, float scale, ToonPalette p)
        {
            var tree = new GameObject("Tree").transform;
            tree.SetParent(parent, false);
            tree.localPosition = pos;
            tree.localScale = Vector3.one * scale;
            Prim.Cylinder(tree, "Trunk", new Vector3(0f, 0.7f, 0f), new Vector3(0.4f, 0.7f, 0.4f), p.trunk, true);
            Prim.Sphere(tree, "LeavesLow", new Vector3(0f, 1.9f, 0f), Vector3.one * 2.3f, p.leaves);
            Prim.Sphere(tree, "LeavesHigh", new Vector3(0.1f, 2.9f, 0.05f), Vector3.one * 1.6f, p.leaves);
        }

        // A point inside a district, in first-draft units (see Grow).
        static Vector3 Local(float radius, float degrees) => PolarPoint(radius * Grow, degrees);

        static Vector3 PolarPoint(float radius, float degrees)
        {
            var a = degrees * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
        }

        static BuildSite BuildPlot(Transform parent, int index, BuildPlan plan, Vector3 localPosition, ToonPalette palette, TMP_FontAsset font, Material outlineMat)
        {
            var go = new GameObject($"Plot {index + 1} - {plan.Name}");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;

            var dirt = Prim.Cylinder(go.transform, "Ground", new Vector3(0f, 0.015f, 0f), new Vector3(7.8f, 0.015f, 7.8f), palette.dirt);
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

        static readonly Color PanelColor = new Color(0.17f, 0.13f, 0.25f, 0.86f);
        static readonly Color InkColor = new Color(0.17f, 0.13f, 0.2f);

        static void BuildHud(TMP_FontAsset font, Material outlineMat, AudioManager audioManager, out GameHud hud, out DashboardHud dashboard)
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
            var root = canvasGo.transform;

            var rounded = ArtBuilder.EnsureRoundedSprite();
            var knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");

            // Dashboard first so it sits behind the in-game widgets in the hierarchy.
            dashboard = BuildDashboard(root, rounded, font, outlineMat);

            // Always visible: coins and level (top-left), hints and the big centre messages.
            var coinPanel = Panel(root, "CoinPanel", rounded, PanelColor, new Vector2(0f, 1f), new Vector2(40f, -40f), new Vector2(380f, 100f));
            var coin = MakeImage(coinPanel, "Coin", knob, new Color(1f, 0.84f, 0.2f), new Vector2(0f, 0.5f), new Vector2(18f, 0f), new Vector2(68f, 68f));
            MakeImage(coin.transform, "Shine", knob, new Color(1f, 0.95f, 0.6f), new Vector2(0.5f, 0.5f), new Vector2(-6f, 6f), new Vector2(22f, 22f));
            var coinsText = Label(coinPanel, "Coins", font, outlineMat, 58, Color.white, TextAlignmentOptions.MidlineLeft,
                new Vector2(0f, 0.5f), new Vector2(104f, 0f), new Vector2(250f, 90f));

            var levelPanel = Panel(root, "LevelPanel", rounded, PanelColor, new Vector2(0f, 1f), new Vector2(40f, -150f), new Vector2(380f, 84f));
            EnergyRow(levelPanel, rounded, font, "SEVİYE", -8f, out var levelLabel, out _, out var levelFill, out var levelFillImage);
            levelFillImage.color = new Color(0.45f, 0.82f, 1f);

            // Every on-screen message sits on a translucent dark plate so it stays readable over any background.
            var hintPlate = TextPlate(root, "HintPlate", rounded, font, outlineMat, 34, new Color(1f, 0.96f, 0.8f), new Vector2(0.5f, 0f), new Vector2(0f, 205f));
            var victoryPlate = TextPlate(root, "VictoryPlate", rounded, font, outlineMat, 110, new Color(1f, 0.86f, 0.25f), new Vector2(0.5f, 0.5f), new Vector2(0f, 60f));
            var levelPlate = TextPlate(root, "LevelPlate", rounded, font, outlineMat, 96, new Color(0.55f, 0.88f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, 200f));

            // Only while inside a district: the player's energy, the active building, shop, cola, back to the map, touch stick.
            var groupRt = Rect(root, "DistrictGroup", new Vector2(0f, 0f), Vector2.zero, Vector2.zero);
            groupRt.anchorMin = Vector2.zero;
            groupRt.anchorMax = Vector2.one;
            groupRt.offsetMin = groupRt.offsetMax = Vector2.zero;
            var group = groupRt.transform;

            var buildingPlate = TextPlate(group, "BuildingPlate", rounded, font, outlineMat, 46, Color.white, new Vector2(0.5f, 1f), new Vector2(0f, -40f));
            var boostPlate = TextPlate(group, "BoostPlate", rounded, font, outlineMat, 52, new Color(1f, 0.65f, 0.2f), new Vector2(0.5f, 1f), new Vector2(0f, -130f));

            var energyPanel = Panel(group, "EnergyPanel", rounded, PanelColor, new Vector2(0f, 1f), new Vector2(40f, -245f), new Vector2(380f, 84f));
            EnergyRow(energyPanel, rounded, font, "ENERJİ", -8f, out var energyLabel, out var energyBack, out var playerFill, out var playerFillImage);

            var exitButton = TintedButton(group, "ExitButton", rounded, font, new Vector2(1f, 1f), new Vector2(-40f, -40f), new Vector2(300f, 90f),
                new Color(0.62f, 0.85f, 1f), 34, out var exitLabel);
            exitLabel.text = "HARİTA";

            // Bottom-right, where the right thumb rests: shop and the cola you carry.
            var shopButton = TintedButton(group, "ShopButton", rounded, font, new Vector2(1f, 0f), new Vector2(-40f, 40f), new Vector2(300f, 100f),
                new Color(1f, 0.79f, 0.24f), 36, out var shopLabel);
            shopLabel.text = "MAĞAZA";
            var colaUseButton = TintedButton(group, "ColaUseButton", rounded, font, new Vector2(1f, 0f), new Vector2(-40f, 160f), new Vector2(300f, 100f),
                new Color(1f, 0.93f, 0.35f), 32, out var colaUseLabel);

            var shop = BuildShop(group, rounded, font);
            BuildTouchControls(group, knob);

            // Music and effects switches, top-right on every screen (GameHud shifts them left of the map button inside a district).
            var audioGroup = Rect(root, "AudioGroup", new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            var musicButton = TintedButton(audioGroup, "MusicButton", rounded, font, new Vector2(1f, 1f), new Vector2(-205f, -40f), new Vector2(150f, 90f),
                new Color(0.85f, 0.8f, 1f), 28, out var musicLabel);
            var sfxButton = TintedButton(audioGroup, "SfxButton", rounded, font, new Vector2(1f, 1f), new Vector2(-40f, -40f), new Vector2(150f, 90f),
                new Color(0.85f, 0.8f, 1f), 28, out var sfxLabel);
            var audioSettings = audioGroup.gameObject.AddComponent<AudioSettingsButtons>();
            EditorWire.Set(audioSettings, "audioManager", audioManager);
            EditorWire.Set(audioSettings, "musicButton", musicButton);
            EditorWire.Set(audioSettings, "musicLabel", musicLabel);
            EditorWire.Set(audioSettings, "sfxButton", sfxButton);
            EditorWire.Set(audioSettings, "sfxLabel", sfxLabel);

            hud = canvasGo.AddComponent<GameHud>();
            EditorWire.Set(hud, "audioGroup", audioGroup);
            EditorWire.Set(hud, "districtGroup", groupRt.gameObject);
            EditorWire.Set(hud, "coinsText", coinsText);
            EditorWire.Set(hud, "coinPanel", coinPanel.GetComponent<RectTransform>());
            EditorWire.Set(hud, "levelPanel", levelPanel.GetComponent<RectTransform>());
            EditorWire.Set(hud, "buildingPlate", buildingPlate);
            EditorWire.Set(hud, "hintPlate", hintPlate);
            EditorWire.Set(hud, "victoryPlate", victoryPlate);
            EditorWire.Set(hud, "levelPlate", levelPlate);
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

            // A tick for every plain button; purchases and the cola button have sounds of their own.
            var silent = new System.Collections.Generic.HashSet<string> { "HammerButton", "WorkerButton", "StaminaButton", "SpeedButton", "ColaBuyButton", "ColaUseButton" };
            var ticking = new System.Collections.Generic.List<Object>();
            foreach (var button in canvasGo.GetComponentsInChildren<Button>(true))
                if (!silent.Contains(button.name)) ticking.Add(button);
            var clicks = canvasGo.AddComponent<UiClickSounds>();
            EditorWire.Set(clicks, "audioManager", audioManager);
            EditorWire.SetArray(clicks, "buttons", ticking.ToArray());
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
        static DashboardHud BuildDashboard(Transform root, Sprite rounded, TMP_FontAsset font, Material outlineMat)
        {
            var groupRt = Rect(root, "DashboardGroup", new Vector2(0f, 0f), Vector2.zero, Vector2.zero);
            groupRt.anchorMin = Vector2.zero;
            groupRt.anchorMax = Vector2.one;
            groupRt.offsetMin = groupRt.offsetMax = Vector2.zero;
            var group = groupRt.transform;

            var titlePanel = Panel(group, "WorldPanel", rounded, PanelColor, new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(560f, 100f));
            titlePanel.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 1f);
            var worldName = Label(titlePanel, "WorldName", font, outlineMat, 60, Color.white, TextAlignmentOptions.Center,
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(520f, 90f));
            worldName.rectTransform.pivot = new Vector2(0.5f, 0.5f);

            var previous = TintedButton(group, "PreviousWorld", rounded, font, new Vector2(0.5f, 1f), new Vector2(-340f, -45f), new Vector2(90f, 90f),
                new Color(1f, 0.86f, 0.35f), 44, out var previousLabel);
            previousLabel.text = "<";
            var next = TintedButton(group, "NextWorld", rounded, font, new Vector2(0.5f, 1f), new Vector2(340f, -45f), new Vector2(90f, 90f),
                new Color(1f, 0.86f, 0.35f), 44, out var nextLabel);
            nextLabel.text = ">";
            // TintedButton pivots at its anchor; centre the arrows on their x so they sit symmetrically beside the title.
            previous.GetComponent<RectTransform>().pivot = next.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 1f);

            var tip = TextPlate(group, "TipPlate", rounded, font, outlineMat, 40, new Color(1f, 0.96f, 0.8f), new Vector2(0.5f, 0f), new Vector2(0f, 60f));

            var cards = new DistrictCard[Balance.Districts.Length];
            for (var i = 0; i < cards.Length; i++)
            {
                var button = TintedButton(group, $"Card {i + 1}", rounded, font, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(300f, 120f),
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

        // Side panel with the four purchases. The ShopPanel component sits on an always-active empty object so it can
        // keep sliding the (otherwise inactive) panel in and out.
        static ShopPanel BuildShop(Transform root, Sprite rounded, TMP_FontAsset font)
        {
            var host = Rect(root, "Shop", new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            var shop = host.gameObject.AddComponent<ShopPanel>();

            var panelRt = Rect(host, "Panel", new Vector2(1f, 1f), new Vector2(-30f, -150f), new Vector2(520f, 662f));
            var back = panelRt.gameObject.AddComponent<Image>();
            back.sprite = rounded;
            back.type = Image.Type.Sliced;
            back.color = PanelColor;

            var title = Label(panelRt, "Title", font, null, 40, Color.white, TextAlignmentOptions.MidlineLeft, new Vector2(0f, 1f), new Vector2(28f, -20f), new Vector2(300f, 56f));
            title.text = "MAĞAZA";
            var close = TintedButton(panelRt, "CloseButton", rounded, font, new Vector2(1f, 1f), new Vector2(-16f, -16f), new Vector2(64f, 64f),
                new Color(1f, 0.45f, 0.4f), 36, out var closeLabel);
            closeLabel.text = "X";

            var hammer = ShopCard(panelRt, "HammerButton", rounded, font, 0, new Color(1f, 0.79f, 0.24f), out var hammerLabel);
            var worker = ShopCard(panelRt, "WorkerButton", rounded, font, 1, new Color(0.45f, 0.82f, 1f), out var workerLabel);
            var stamina = ShopCard(panelRt, "StaminaButton", rounded, font, 2, new Color(0.55f, 0.9f, 0.55f), out var staminaLabel);
            var speed = ShopCard(panelRt, "SpeedButton", rounded, font, 3, new Color(0.62f, 0.78f, 1f), out var speedLabel);
            var cola = ShopCard(panelRt, "ColaBuyButton", rounded, font, 4, new Color(1f, 0.55f, 0.3f), out var colaLabel);

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
            panelRt.gameObject.SetActive(false);
            return shop;
        }

        static Button ShopCard(Transform parent, string name, Sprite sprite, TMP_FontAsset font, int row, Color tint, out TMP_Text label)
        {
            var button = TintedButton(parent, name, sprite, font, new Vector2(0.5f, 1f), new Vector2(0f, -88f - row * 114f), new Vector2(472f, 104f), tint, 27, out label);
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

            label = Label(rt, "Label", font, null, fontSize, InkColor, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), Vector2.zero, size - new Vector2(20f, 10f));
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
        static void EnergyRow(Transform panel, Sprite rounded, TMP_FontAsset font, string title, float y,
            out TMP_Text label, out RectTransform backRect, out RectTransform fill, out Image fillImage)
        {
            label = Label(panel, title + "Label", font, null, 26, Color.white, TextAlignmentOptions.MidlineLeft,
                new Vector2(0f, 1f), new Vector2(18f, y), new Vector2(340f, 32f));
            label.text = title;
            var back = Rect(panel, title + "Bar", new Vector2(0f, 1f), new Vector2(18f, y - 36f), new Vector2(344f, 24f));
            var backImg = back.gameObject.AddComponent<Image>();
            backImg.sprite = rounded;
            backImg.type = Image.Type.Sliced;
            backImg.pixelsPerUnitMultiplier = 2f;   // bars are only 24 px tall: halve the border so the corners stay slight
            backImg.color = new Color(0.08f, 0.06f, 0.12f, 0.9f);
            backImg.raycastTarget = false;
            backRect = back;

            fill = Rect(back, "Fill", new Vector2(0f, 0f), Vector2.zero, Vector2.zero);
            fill.pivot = new Vector2(0f, 0.5f);
            fill.anchorMin = new Vector2(0f, 0f);
            fill.anchorMax = new Vector2(1f, 1f);
            fill.offsetMin = new Vector2(3f, 3f);
            fill.offsetMax = new Vector2(-3f, -3f);
            fillImage = fill.gameObject.AddComponent<Image>();
            fillImage.raycastTarget = false;
        }
    }
}
