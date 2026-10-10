using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Draws the cartoon HUD kit (chunky outlined panels and buttons, glossy bars, coin/star/bolt icons) into
    /// Assets/_Project/UI/Kit as 9-slice-ready PNGs. No external art: everything is signed-distance maths, so there is
    /// nothing to license. Sprites are white-ish on purpose; the scene builder tints them per widget.
    /// </summary>
    public static class UiKitBuilder
    {
        const string Dir = "Assets/_Project/UI/Kit";
        static readonly Color Ink = new Color(0.13f, 0.09f, 0.2f);
        const int Samples = 3;   // 3x3 supersampling keeps edges smooth at these small sizes

        public static Sprite Panel => Load("Panel", 64, 64, 16, 20, p => Chunky(p, 64, 64, 16, 4, lip: 8, gloss: 14, bodyShade: 0.93f));
        public static Sprite Button => Load("Button", 64, 64, 16, 20, p => Chunky(p, 64, 64, 16, 4, lip: 10, gloss: 16, bodyShade: 0.97f));
        public static Sprite BarBack => Load("BarBack", 32, 32, 16, 15, p => Chunky(p, 32, 32, 16, 3, lip: 0, gloss: 0, bodyShade: 1f, inset: 6));
        public static Sprite BarFill => Load("BarFill", 32, 32, 16, 12, p => Fill(p, 32, 32, 16));
        public static Sprite Coin => Load("Coin", 96, 96, 0, 0, CoinPixel);
        public static Sprite Star => Load("Star", 96, 96, 0, 0, p => Poly(p, 96, StarPoints(), new Color(1f, 0.85f, 0.2f)));
        public static Sprite Bolt => Load("Bolt", 96, 96, 0, 0, p => Poly(p, 96, BoltPoints(), new Color(0.5f, 0.95f, 0.4f)));

        [MenuItem("Tools/Project/Rebuild UI Kit")]
        public static void Rebuild()
        {
            if (Directory.Exists(Dir)) foreach (var f in Directory.GetFiles(Dir, "*.png")) AssetDatabase.DeleteAsset(f.Replace('\\', '/'));
            _ = Panel; _ = Button; _ = BarBack; _ = BarFill; _ = Coin; _ = Star; _ = Bolt;
            AssetDatabase.SaveAssets();
        }

        // ---------- plumbing ----------

        static Sprite Load(string name, int w, int h, int radius, int border, Func<Vector2, Color> pixel)
        {
            var path = $"{Dir}/{name}.png";
            if (!File.Exists(path))
            {
                Directory.CreateDirectory(Dir);
                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                var pixels = new Color[w * h];
                for (var y = 0; y < h; y++)
                for (var x = 0; x < w; x++)
                {
                    float r = 0, g = 0, b = 0, a = 0;
                    for (var sy = 0; sy < Samples; sy++)
                    for (var sx = 0; sx < Samples; sx++)
                    {
                        var c = pixel(new Vector2(x + (sx + 0.5f) / Samples, y + (sy + 0.5f) / Samples));
                        r += c.r * c.a; g += c.g * c.a; b += c.b * c.a; a += c.a;   // premultiplied average, no dark fringes
                    }
                    var n = Samples * Samples;
                    pixels[y * w + x] = a <= 0f ? Color.clear : new Color(r / a, g / a, b / a, a / n);
                }
                tex.SetPixels(pixels);
                File.WriteAllBytes(path, tex.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(path);
            }

            // Checked every run: Multiple import mode silently ignores the border (see CLAUDE.md sprite gotcha).
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            var wanted = new Vector4(border, border, border, border);
            if (importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Single || importer.spriteBorder != wanted)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spriteBorder = wanted;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        // Signed distance to a rounded rectangle (negative inside).
        static float RoundRect(Vector2 p, float w, float h, float r)
        {
            var q = new Vector2(Mathf.Abs(p.x - w * 0.5f) - (w * 0.5f - r), Mathf.Abs(p.y - h * 0.5f) - (h * 0.5f - r));
            return new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - r;
        }

        // ---------- panels, buttons, bars ----------

        // Ink outline, flat body, brighter band on top (gloss), darker lip on the bottom. 'inset' darkens the top
        // instead, so a bar background reads as a groove.
        static Color Chunky(Vector2 p, float w, float h, float r, float outline, float lip, float gloss, float bodyShade, float inset = 0f)
        {
            var d = RoundRect(p, w, h, r);
            if (d > 0f) return Color.clear;
            if (d > -outline) return Ink;
            var shade = bodyShade;
            if (lip > 0f && p.y < outline + lip) shade *= 0.72f;
            else if (gloss > 0f && p.y > h - outline - gloss) shade = 1f;
            if (inset > 0f && p.y > h - outline - inset) shade *= 0.7f;
            return new Color(shade, shade, shade, 1f);
        }

        // Bar fill: soft vertical gradient with a bright stripe near the top, so any tint looks glossy.
        static Color Fill(Vector2 p, float w, float h, float r)
        {
            if (RoundRect(p, w, h, r) > 0f) return Color.clear;
            var t = p.y / h;
            var shade = Mathf.Lerp(0.78f, 1f, t);
            if (t > 0.58f && t < 0.84f) shade = Mathf.Min(1f, shade + 0.18f);
            return new Color(shade, shade, shade, 1f);
        }

        // ---------- icons ----------

        static Color CoinPixel(Vector2 p)
        {
            var c = new Vector2(48f, 48f);
            var dist = Vector2.Distance(p, c);
            if (dist > 44f) return Color.clear;
            if (dist > 39f) return Ink;
            if (dist > 31f && dist < 35f) return new Color(0.86f, 0.55f, 0.1f);   // stamped inner ring
            var body = new Color(1f, 0.82f, 0.2f);
            if (p.y > 50f && p.x < 40f && dist > 8f && Vector2.Distance(p, new Vector2(34f, 62f)) < 9f) return new Color(1f, 0.95f, 0.65f);   // shine
            if (InPolygon(p, Scale(StarPoints(), 0.36f, c))) return new Color(0.9f, 0.58f, 0.1f);   // embossed star
            return body;
        }

        // Two-tone polygon: the whole shape in ink, then the same shape shrunk about its centre in the fill colour.
        static Color Poly(Vector2 p, float size, Vector2[] shape, Color fill)
        {
            var c = new Vector2(size * 0.5f, size * 0.5f);
            if (!InPolygon(p, Scale(shape, 0.96f, c))) return Color.clear;
            if (!InPolygon(p, Scale(shape, 0.84f, c))) return Ink;
            return p.y > size * 0.55f ? Color.Lerp(fill, Color.white, 0.35f) : fill;
        }

        static Vector2[] StarPoints()
        {
            var pts = new Vector2[10];
            for (var i = 0; i < 10; i++)
            {
                var a = Mathf.PI / 2f + i * Mathf.PI / 5f;
                var rad = i % 2 == 0 ? 46f : 20f;
                pts[i] = new Vector2(48f + Mathf.Cos(a) * rad, 46f + Mathf.Sin(a) * rad);
            }
            return pts;
        }

        static Vector2[] BoltPoints() => new[]
        {
            new Vector2(66f, 94f), new Vector2(14f, 48f), new Vector2(42f, 48f),
            new Vector2(30f, 2f), new Vector2(84f, 56f), new Vector2(56f, 56f),
        };

        static Vector2[] Scale(Vector2[] pts, float s, Vector2 centre)
        {
            var res = new Vector2[pts.Length];
            for (var i = 0; i < pts.Length; i++) res[i] = centre + (pts[i] - centre) * s;
            return res;
        }

        static bool InPolygon(Vector2 p, Vector2[] poly)
        {
            var inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
                if ((poly[i].y > p.y) != (poly[j].y > p.y) &&
                    p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
                    inside = !inside;
            return inside;
        }
    }
}
