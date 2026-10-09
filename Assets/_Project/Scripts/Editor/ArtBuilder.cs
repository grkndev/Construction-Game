using System.IO;
using Game.Core;
using Game.World;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace Game.Editor
{
    /// <summary>
    /// Generates the placeholder art: toon materials, the palette asset, the TMP font and the prefabs.
    /// Materials/palette/font are updated in place on every run (stable GUIDs, no diff churn);
    /// prefabs are only created when missing (or on force) because Unity gives them random fileIDs on creation.
    /// </summary>
    public static class ArtBuilder
    {
        const string MaterialsDir = "Assets/_Project/Art/Materials";
        const string PrefabsDir = "Assets/_Project/Prefabs";
        const string TmpSettingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";
        public const string FontSourcePath = "Assets/_Project/UI/Fonts/PaytoneOne.ttf";
        public const string FontAssetPath = "Assets/_Project/UI/Fonts/PaytoneOne SDF.asset";
        public const string OutlineMaterialPath = "Assets/_Project/UI/Fonts/PaytoneOne Outline.mat";
        public const string PalettePath = "Assets/_Project/Data/ToonPalette.asset";
        public const string WorkerPrefabPath = PrefabsDir + "/Worker.prefab";
        public const string AutoWorkerPrefabPath = PrefabsDir + "/AutoWorker.prefab";
        public const string FloatingTextPrefabPath = PrefabsDir + "/FloatingText.prefab";

        // ---------- TextMeshPro ----------

        public static bool HasTmpEssentials => File.Exists(TmpSettingsPath);

        public static void EnsureTmpEssentials()
        {
            if (HasTmpEssentials) return;
            Debug.Log("[Art] Importing TMP Essential Resources");
            TMP_PackageResourceImporter.ImportResources(true, false, false);
            AssetDatabase.Refresh();
        }

        public static TMP_FontAsset EnsureFont()
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            if (font == null)
            {
                var source = AssetDatabase.LoadAssetAtPath<Font>(FontSourcePath)
                             ?? throw new FileNotFoundException(FontSourcePath);
                font = TMP_FontAsset.CreateFontAsset(source, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
                AssetDatabase.CreateAsset(font, FontAssetPath);
                font.material.name = "PaytoneOne SDF Material";
                AssetDatabase.AddObjectToAsset(font.material, font);
                font.atlasTextures[0].name = "PaytoneOne SDF Atlas";
                AssetDatabase.AddObjectToAsset(font.atlasTextures[0], font);

                // Bake every glyph the UI can show (incl. Turkish letters) so builds never depend on runtime generation.
                const string chars = " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~" +
                                     "ÇçĞğİıÖöŞşÜü";
                if (!font.TryAddCharacters(chars, out var missing))
                    Debug.LogWarning($"[Art] Font is missing glyphs: '{missing}'");
                EditorUtility.SetDirty(font);
                AssetDatabase.SaveAssets();
            }
            return font;
        }

        /// <summary>Shared TMP material with a dark outline: white/yellow text stays readable on any background.</summary>
        public static Material EnsureOutlineMaterial(TMP_FontAsset font)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(OutlineMaterialPath);
            if (mat == null)
            {
                mat = new Material(font.material) { name = "PaytoneOne Outline" };
                AssetDatabase.CreateAsset(mat, OutlineMaterialPath);
            }
            mat.EnableKeyword("OUTLINE_ON");
            mat.SetColor("_OutlineColor", new Color(0.17f, 0.13f, 0.2f, 1f));
            mat.SetFloat("_OutlineWidth", 0.22f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // ---------- UI sprite ----------

        public const string RoundedSpritePath = "Assets/_Project/UI/RoundedRect.png";

        /// <summary>9-sliced rounded rectangle for panels and buttons (generated, no external art needed).</summary>
        public static Sprite EnsureRoundedSprite()
        {
            if (!File.Exists(RoundedSpritePath))
            {
                const int size = 96, radius = 36;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                for (var y = 0; y < size; y++)
                for (var x = 0; x < size; x++)
                {
                    // Distance from the nearest corner-circle centre; inside the radius = opaque, with 1px antialiasing.
                    var cx = Mathf.Clamp(x + 0.5f, radius, size - radius);
                    var cy = Mathf.Clamp(y + 0.5f, radius, size - radius);
                    var d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(radius - d + 0.5f)));
                }
                File.WriteAllBytes(RoundedSpritePath, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(RoundedSpritePath);

                var importer = (TextureImporter)AssetImporter.GetAtPath(RoundedSpritePath);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteBorder = new Vector4(radius, radius, radius, radius);
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(RoundedSpritePath);
        }

        // ---------- Materials & palette ----------

        static Material Mat(string name, string hex, float outline = 0.0045f, float shadeStrength = 1f)
        {
            var path = $"{MaterialsDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Construction/Toon"));
                AssetDatabase.CreateAsset(mat, path);
            }
            ColorUtility.TryParseHtmlString(hex, out var color);
            mat.SetColor("_BaseColor", color);
            // Shadows are the base colour pushed towards a cool purple; shadeStrength 0 = no shading (flat).
            mat.SetColor("_ShadeColor", Color.Lerp(Color.white, new Color(0.58f, 0.52f, 0.8f), shadeStrength));
            mat.SetFloat("_OutlineWidth", outline);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        public static ToonPalette BuildPalette()
        {
            EditorWire.EnsureFolder(MaterialsDir);
            EditorWire.EnsureFolder("Assets/_Project/Data");
            const float thin = 0.0022f;

            var palette = AssetDatabase.LoadAssetAtPath<ToonPalette>(PalettePath);
            if (palette == null)
            {
                palette = ScriptableObject.CreateInstance<ToonPalette>();
                AssetDatabase.CreateAsset(palette, PalettePath);
            }

            palette.grass = Mat("Grass", "#86DB5B", 0.003f);
            palette.dirt = Mat("Dirt", "#D79C66", 0f);
            palette.sea = Mat("Sea", "#52B7EA", 0f, 0.3f);
            palette.trunk = Mat("Trunk", "#9A6240");
            palette.leaves = Mat("Leaves", "#43BF63");
            palette.crate = Mat("Crate", "#E0A867");
            palette.cone = Mat("Cone", "#FF7F32");
            palette.stone = Mat("Stone", "#C3C5D4");

            palette.walls = new[]
            {
                Mat("WallPink", "#FF93A8"), Mat("WallYellow", "#FFD56B"), Mat("WallBlue", "#74C4EA"),
                Mat("WallPurple", "#B795EB"), Mat("WallOrange", "#FFA46E"),
            };
            palette.roof = Mat("Roof", "#E5574B");
            palette.window = Mat("Window", "#C9EEFF", thin);
            palette.door = Mat("Door", "#85522E", thin);
            palette.scaffold = Mat("Scaffold", "#DDAE6A", thin);
            palette.flag = Mat("Flag", "#FF4D4D", thin);

            palette.skin = Mat("Skin", "#FFD6AE");
            palette.hat = Mat("Hat", "#FFD62A");
            palette.shirt = Mat("Shirt", "#3F8CFF");
            palette.shirtHelper = Mat("ShirtHelper", "#FF6F6F");
            palette.hammerHandle = Mat("HammerHandle", "#9A6240", thin);
            palette.hammerHead = Mat("HammerHead", "#A4ACC0", thin);
            palette.eye = Mat("Eye", "#2B2233", 0f, 0f);

            palette.barBack = Mat("BarBack", "#2B2233", thin, 0f);
            palette.barFill = Mat("BarFill", "#FFD62A", 0f, 0f);
            palette.marker = Mat("Marker", "#FFD62A");
            palette.energyFill = Mat("EnergyFill", "#5BE37D", 0f, 0f);
            palette.energyLow = Mat("EnergyLow", "#FF5A4F", 0f, 0f);

            EditorUtility.SetDirty(palette);
            AssetDatabase.SaveAssets();
            return palette;
        }

        // ---------- Prefabs ----------

        public static void EnsurePrefabs(ToonPalette palette, TMP_FontAsset font, Material outlineMat, bool force)
        {
            EditorWire.EnsureFolder(PrefabsDir);
            if (force || !File.Exists(WorkerPrefabPath)) SavePrefab(WorkerPrefabPath, BuildWorker(palette, "Worker", palette.shirt, CharacterKind.Player));
            if (force || !File.Exists(AutoWorkerPrefabPath))
            {
                var root = BuildWorker(palette, "AutoWorker", palette.shirtHelper, CharacterKind.Worker);
                var auto = root.AddComponent<AutoWorker>();
                EditorWire.Set(auto, "visual", root.GetComponent<WorkerVisual>());
                SavePrefab(AutoWorkerPrefabPath, root);
            }
            if (force || !File.Exists(FloatingTextPrefabPath)) SavePrefab(FloatingTextPrefabPath, BuildFloatingText(font, outlineMat));
        }

        static void SavePrefab(string path, GameObject root)
        {
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            Debug.Log($"[Art] Saved {path}");
        }

        /// <summary>Cartoon construction worker: capsule body, round head, yellow hard hat, hammer on a pivot.</summary>
        static GameObject BuildWorker(ToonPalette p, string name, Material shirt, CharacterKind kind)
        {
            var root = new GameObject(name);
            root.transform.localScale = Vector3.one * 1.4f;   // chunky proportions read better from the iso camera
            var body = new GameObject("Body").transform;
            body.SetParent(root.transform, false);

            Prim.Capsule(body, "Torso", new Vector3(0f, 0.6f, 0f), new Vector3(0.85f, 0.6f, 0.85f), shirt);
            Prim.Sphere(body, "Head", new Vector3(0f, 1.38f, 0f), new Vector3(0.78f, 0.74f, 0.78f), p.skin);
            Prim.Sphere(body, "Hat", new Vector3(0f, 1.63f, 0f), new Vector3(0.84f, 0.5f, 0.84f), p.hat);
            Prim.Cylinder(body, "HatBrim", new Vector3(0f, 1.5f, 0.05f), new Vector3(0.95f, 0.035f, 0.95f), p.hat);
            Prim.Sphere(body, "EyeL", new Vector3(-0.15f, 1.4f, 0.34f), new Vector3(0.1f, 0.13f, 0.08f), p.eye);
            Prim.Sphere(body, "EyeR", new Vector3(0.15f, 1.4f, 0.34f), new Vector3(0.1f, 0.13f, 0.08f), p.eye);
            Prim.Sphere(body, "FootL", new Vector3(-0.2f, 0.08f, 0.1f), new Vector3(0.3f, 0.18f, 0.42f), p.hammerHandle);
            Prim.Sphere(body, "FootR", new Vector3(0.2f, 0.08f, 0.1f), new Vector3(0.3f, 0.18f, 0.42f), p.hammerHandle);

            var pivot = new GameObject("HammerPivot").transform;
            pivot.SetParent(root.transform, false);
            pivot.localPosition = new Vector3(0.55f, 0.85f, 0.15f);
            Prim.Cylinder(pivot, "Handle", new Vector3(0f, 0.42f, 0f), new Vector3(0.09f, 0.42f, 0.09f), p.hammerHandle);
            // The head's long axis points along the swing direction (local Z): a mallet strikes with its end face,
            // and at impact (handle ~horizontal) the head stands upright instead of lying flat.
            Prim.Cube(pivot, "Head", new Vector3(0f, 0.9f, 0f), new Vector3(0.28f, 0.3f, 0.58f), p.hammerHead);

            var visual = root.AddComponent<WorkerVisual>();
            EditorWire.Set(visual, "body", body);
            EditorWire.Set(visual, "hammerPivot", pivot);

            // Energy bar above the head (hidden while full); the Character component drives it.
            var bar = MeterBar.Create(root.transform, "EnergyBar", new Vector3(0f, 2.3f, 0f), 1.0f, 0.16f,
                p.barBack, p.energyFill, p.energyLow, hideWhenFull: true);
            var character = root.AddComponent<Character>();
            EditorWire.SetInt(character, "kind", (int)kind);
            EditorWire.Set(character, "visual", visual);
            EditorWire.Set(character, "energyBar", bar);
            return root;
        }

        static GameObject BuildFloatingText(TMP_FontAsset font, Material outlineMat)
        {
            var go = new GameObject("FloatingText");
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.font = font;
            tmp.fontSharedMaterial = outlineMat;
            tmp.fontSize = 6f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.rectTransform.sizeDelta = new Vector2(12f, 2f);
            tmp.text = "+1";
            go.AddComponent<Billboard>();
            var ft = go.AddComponent<FloatingText>();
            EditorWire.Set(ft, "label", tmp);
            return go;
        }
    }
}
