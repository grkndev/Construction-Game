using System.IO;
using Game.Core;
using Game.Player;
using Game.UI;
using Game.World;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
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
        const float PlotRing = 7.6f;

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
            BuildWorld(palette);

            var spawn = new GameObject("WorkerSpawn").transform;
            spawn.position = new Vector3(0f, 0f, 2.5f);

            var floatingText = AssetDatabase.LoadAssetAtPath<GameObject>(ArtBuilder.FloatingTextPrefabPath).GetComponent<FloatingText>();
            var autoWorker = AssetDatabase.LoadAssetAtPath<GameObject>(ArtBuilder.AutoWorkerPrefabPath).GetComponent<AutoWorker>();

            var sites = new BuildSite[Balance.Plans.Length];
            for (var i = 0; i < sites.Length; i++) sites[i] = BuildPlot(i, palette, font, outlineMat);

            var gameGo = new GameObject("Game");
            var game = gameGo.AddComponent<GameController>();
            EditorWire.Set(gameGo.AddComponent<DevCommands>(), "game", game);

            var player = BuildPlayer(game);
            BuildCamera(player.transform);
            var hud = BuildHud(font, outlineMat);

            EditorWire.SetArray(game, "sites", sites);
            EditorWire.Set(game, "workerPrefab", autoWorker);
            EditorWire.Set(game, "workerSpawn", spawn);
            EditorWire.Set(game, "floatingTextPrefab", floatingText);
            EditorWire.Set(game, "hud", hud);

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

        static void BuildWorld(ToonPalette p)
        {
            var world = new GameObject("World").transform;

            // Sea, island (rounded grass cap on a dirt base) and one big invisible box to walk on.
            Prim.Cube(world, "Sea", new Vector3(0f, -2.2f, 0f), new Vector3(140f, 0.2f, 140f), p.sea);
            Prim.Cylinder(world, "IslandBase", new Vector3(0f, -1.5f, 0f), new Vector3(28f, 1f, 28f), p.dirt);
            Prim.Cylinder(world, "IslandTop", new Vector3(0f, -0.25f, 0f), new Vector3(30f, 0.25f, 30f), p.grass);
            var ground = new GameObject("GroundCollider");
            ground.transform.SetParent(world, false);
            ground.transform.localPosition = new Vector3(0f, -0.5f, 0f);
            ground.AddComponent<BoxCollider>().size = new Vector3(32f, 1f, 32f);

            // Scenery on the rim, away from the plots (ring radius 7.6) and the centre.
            var treeAngles = new[] { 10f, 38f, 100f, 165f, 205f, 255f, 300f, 335f };
            for (var i = 0; i < treeAngles.Length; i++)
                BuildTree(world, PolarPoint(11.2f + (i % 3) * 0.9f, treeAngles[i]), 0.9f + (i % 3) * 0.15f, p);

            var crateSpots = new[] { PolarPoint(10.2f, 62f), PolarPoint(10.6f, 133f), PolarPoint(9.8f, 228f), PolarPoint(10.4f, 283f) };
            foreach (var spot in crateSpots)
            {
                Prim.Cube(world, "Crate", spot + Vector3.up * 0.45f, Vector3.one * 0.9f, p.crate, true);
                Prim.Cube(world, "Crate", spot + new Vector3(0.9f, 0.45f, 0.3f), Vector3.one * 0.9f, p.crate, true).transform.Rotate(0f, 20f, 0f);
                Prim.Cube(world, "Crate", spot + new Vector3(0.3f, 1.35f, 0.1f), Vector3.one * 0.9f, p.crate).transform.Rotate(0f, 10f, 0f);
            }

            var coneSpots = new[] { PolarPoint(4.2f, 0f), PolarPoint(4.4f, 140f), PolarPoint(4.1f, 220f), PolarPoint(3.9f, 320f) };
            foreach (var spot in coneSpots)
            {
                Prim.Cylinder(world, "ConeBase", spot + Vector3.up * 0.04f, new Vector3(0.55f, 0.04f, 0.55f), p.cone);
                Prim.Cylinder(world, "ConeBody", spot + Vector3.up * 0.35f, new Vector3(0.3f, 0.3f, 0.3f), p.cone, true);
                Prim.Cylinder(world, "ConeTip", spot + Vector3.up * 0.7f, new Vector3(0.18f, 0.08f, 0.18f), p.cone);
            }

            foreach (var angle in new[] { 80f, 190f, 310f })
                Prim.Sphere(world, "Rock", PolarPoint(12.3f, angle) + Vector3.up * 0.15f, new Vector3(1.2f, 0.7f, 0.9f), p.stone).transform.Rotate(0f, angle, 0f);
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

        static Vector3 PolarPoint(float radius, float degrees)
        {
            var a = degrees * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
        }

        static BuildSite BuildPlot(int index, ToonPalette palette, TMP_FontAsset font, Material outlineMat)
        {
            var go = new GameObject($"Plot {index + 1} - {Balance.Plans[index].Name}");
            go.transform.position = PolarPoint(PlotRing, 90f - index * 72f);

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
            label.text = Balance.Plans[index].Name;
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

        static void BuildCamera(Transform target)
        {
            var go = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            go.tag = "MainCamera";
            var cam = go.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.62f, 0.88f, 1f);
            cam.fieldOfView = 30f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 120f;

            var follow = go.AddComponent<IsoCameraFollow>();
            EditorWire.Set(follow, "target", target);
            var offset = new Vector3(-19f, 21f, -19f);
            go.transform.SetPositionAndRotation(target.position + offset, Quaternion.LookRotation(-offset));
        }

        // ---------- HUD ----------

        static readonly Color PanelColor = new Color(0.17f, 0.13f, 0.25f, 0.86f);
        static readonly Color InkColor = new Color(0.17f, 0.13f, 0.2f);

        static GameHud BuildHud(TMP_FontAsset font, Material outlineMat)
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

            // Coins: top-left pill with a yellow coin.
            var coinPanel = Panel(root, "CoinPanel", rounded, PanelColor, new Vector2(0f, 1f), new Vector2(40f, -40f), new Vector2(380f, 100f));
            var coin = MakeImage(coinPanel, "Coin", knob, new Color(1f, 0.84f, 0.2f), new Vector2(0f, 0.5f), new Vector2(18f, 0f), new Vector2(68f, 68f));
            MakeImage(coin.transform, "Shine", knob, new Color(1f, 0.95f, 0.6f), new Vector2(0.5f, 0.5f), new Vector2(-6f, 6f), new Vector2(22f, 22f));
            var coinsText = Label(coinPanel, "Coins", font, outlineMat, 58, Color.white, TextAlignmentOptions.MidlineLeft,
                new Vector2(0f, 0.5f), new Vector2(104f, 0f), new Vector2(250f, 90f));

            var buildingText = Label(root, "BuildingText", font, outlineMat, 46, Color.white, TextAlignmentOptions.Center,
                new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(1000f, 80f));

            var hintText = Label(root, "Hint", font, outlineMat, 34, new Color(1f, 0.96f, 0.8f), TextAlignmentOptions.Center,
                new Vector2(0.5f, 0f), new Vector2(0f, 230f), new Vector2(1500f, 60f));

            var victoryText = Label(root, "Victory", font, outlineMat, 110, new Color(1f, 0.86f, 0.25f), TextAlignmentOptions.Center,
                new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(1500f, 300f));

            var hammerButton = UpgradeButton(root, "HammerButton", rounded, font, new Vector2(-290f, 50f), new Color(1f, 0.79f, 0.24f), out var hammerLabel);
            var workerButton = UpgradeButton(root, "WorkerButton", rounded, font, new Vector2(290f, 50f), new Color(0.45f, 0.82f, 1f), out var workerLabel);

            var hud = canvasGo.AddComponent<GameHud>();
            EditorWire.Set(hud, "coinsText", coinsText);
            EditorWire.Set(hud, "buildingText", buildingText);
            EditorWire.Set(hud, "hintText", hintText);
            EditorWire.Set(hud, "victoryText", victoryText);
            EditorWire.Set(hud, "hammerButton", hammerButton);
            EditorWire.Set(hud, "hammerLabel", hammerLabel);
            EditorWire.Set(hud, "workerButton", workerButton);
            EditorWire.Set(hud, "workerLabel", workerLabel);
            return hud;
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
            img.pixelsPerUnitMultiplier = 1.6f;   // tighter corner radius
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

        static Button UpgradeButton(Transform parent, string name, Sprite sprite, TMP_FontAsset font, Vector2 pos, Color tint, out TMP_Text label)
        {
            var rt = Rect(parent, name, new Vector2(0.5f, 0f), pos, new Vector2(520f, 140f));
            rt.pivot = new Vector2(0.5f, 0f);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 1.6f;
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

            label = Label(rt, "Label", font, null, 34, InkColor, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(500f, 120f));
            label.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            label.textWrappingMode = TextWrappingModes.Normal;
            return button;
        }
    }
}
