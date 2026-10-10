using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Plain 2D geometry for the country map: provinces are the cells of a Voronoi diagram clipped to the country's outline, with
    /// every edge cut into short pieces and nudged by a smooth noise so borders and coast look hand-drawn. Neighbouring provinces get
    /// bit-for-bit identical vertices along their shared border (edges are cut from a canonical end), so the map has no gaps or overlaps.
    /// Polygons are counter-clockwise.
    /// </summary>
    public static class MapGeometry
    {
        public const float MaxSegment = 4.5f;

        public static float Area(IReadOnlyList<Vector2> poly)
        {
            var sum = 0f;
            for (var i = 0; i < poly.Count; i++)
            {
                var a = poly[i];
                var b = poly[(i + 1) % poly.Count];
                sum += a.x * b.y - b.x * a.y;
            }
            return sum * 0.5f;
        }

        public static Vector2 Centroid(IReadOnlyList<Vector2> poly)
        {
            double cx = 0, cy = 0, area = 0;
            for (var i = 0; i < poly.Count; i++)
            {
                var a = poly[i];
                var b = poly[(i + 1) % poly.Count];
                var cross = a.x * (double)b.y - b.x * (double)a.y;
                cx += (a.x + b.x) * cross;
                cy += (a.y + b.y) * cross;
                area += cross;
            }
            area *= 0.5;
            return new Vector2((float)(cx / (6 * area)), (float)(cy / (6 * area)));
        }

        public static bool Contains(IReadOnlyList<Vector2> poly, Vector2 p)
        {
            var inside = false;
            for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
                if ((poly[i].y > p.y) != (poly[j].y > p.y) &&
                    p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
                    inside = !inside;
            return inside;
        }

        public static float DistanceToEdges(IReadOnlyList<Vector2> poly, Vector2 p)
        {
            var best = float.MaxValue;
            for (var i = 0; i < poly.Count; i++)
            {
                var a = poly[i];
                var b = poly[(i + 1) % poly.Count];
                var ab = b - a;
                var t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
                best = Mathf.Min(best, Vector2.Distance(p, a + ab * t));
            }
            return best;
        }

        // ---------- Voronoi ----------

        /// <summary>The part of the convex <paramref name="outline"/> closer to <paramref name="seeds"/>[index] than to any other seed.</summary>
        public static List<Vector2> Cell(Vector2[] seeds, int index, IReadOnlyList<Vector2> outline)
        {
            var poly = new List<Vector2>(outline);
            for (var j = 0; j < seeds.Length; j++)
            {
                if (j == index) continue;
                // keep points p with (p - mid) . (seed_j - seed_i) <= 0
                var normal = seeds[j] - seeds[index];
                var offset = Vector2.Dot((seeds[j] + seeds[index]) * 0.5f, normal);
                poly = ClipHalfPlane(poly, normal, offset);
            }
            return poly;
        }

        /// <summary>Keeps the part of the polygon where <c>normal . p &lt;= offset</c> (Sutherland-Hodgman).</summary>
        public static List<Vector2> ClipHalfPlane(List<Vector2> poly, Vector2 normal, float offset)
        {
            var result = new List<Vector2>();
            for (var i = 0; i < poly.Count; i++)
            {
                var a = poly[i];
                var b = poly[(i + 1) % poly.Count];
                var da = Vector2.Dot(normal, a) - offset;
                var db = Vector2.Dot(normal, b) - offset;
                if (da <= 0f) result.Add(a);
                if ((da <= 0f) != (db <= 0f)) result.Add(a + (b - a) * (da / (da - db)));
            }
            return result;
        }

        // ---------- organic edges ----------

        public static Vector2 Quantize(Vector2 p) => new Vector2(Mathf.Round(p.x * 100f) / 100f, Mathf.Round(p.y * 100f) / 100f);

        /// <summary>A smooth, slowly varying offset (a function of position only, so shared vertices move together).</summary>
        public static Vector2 Wobble(Vector2 p) => new Vector2(
            1.7f * Mathf.Sin(0.13f * p.y + 0.9f) + 0.9f * Mathf.Sin(0.21f * p.x + 0.17f * p.y),
            1.7f * Mathf.Sin(0.12f * p.x + 2.1f) + 0.9f * Mathf.Sin(0.2f * p.y - 0.15f * p.x));

        static bool Before(Vector2 a, Vector2 b) => a.x < b.x || (a.x == b.x && a.y < b.y);

        /// <summary>Cuts every edge into pieces of at most <see cref="MaxSegment"/> (always measured from the lexicographically smaller end, so a shared edge is cut identically from both sides), then displaces every vertex.</summary>
        public static List<Vector2> Organic(IReadOnlyList<Vector2> poly, bool wobble = true)
        {
            var result = new List<Vector2>();
            for (var i = 0; i < poly.Count; i++)
            {
                var a = Quantize(poly[i]);
                var b = Quantize(poly[(i + 1) % poly.Count]);
                var forward = Before(a, b);
                var from = forward ? a : b;
                var to = forward ? b : a;
                var n = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(from, to) / MaxSegment));
                var pieces = new List<Vector2>();
                if (forward)
                    for (var k = 0; k < n; k++) pieces.Add(Vector2.Lerp(from, to, k / (float)n));   // a ... up to (excluding) b
                else
                    for (var k = n; k >= 1; k--) pieces.Add(Vector2.Lerp(from, to, k / (float)n));  // a (= to) ... down to (excluding) b (= from)
                foreach (var piece in pieces) result.Add(wobble ? piece + Wobble(piece) : piece);
            }
            return result;
        }

        // ---------- triangulation ----------

        /// <summary>Ear clipping for a simple counter-clockwise polygon; returns indices into <paramref name="poly"/>, three per triangle.</summary>
        public static List<int> Triangulate(IReadOnlyList<Vector2> poly)
        {
            var indices = new List<int>();
            var remaining = new List<int>();
            for (var i = 0; i < poly.Count; i++) remaining.Add(i);

            var guard = poly.Count * poly.Count + 10;
            while (remaining.Count > 3 && guard-- > 0)
            {
                var clipped = false;
                for (var k = 0; k < remaining.Count; k++)
                {
                    var ia = remaining[(k + remaining.Count - 1) % remaining.Count];
                    var ib = remaining[k];
                    var ic = remaining[(k + 1) % remaining.Count];
                    var a = poly[ia];
                    var b = poly[ib];
                    var c = poly[ic];
                    if (Cross(b - a, c - b) <= 1e-6f) continue;   // reflex or flat corner
                    var ear = true;
                    foreach (var other in remaining)
                    {
                        if (other == ia || other == ib || other == ic) continue;
                        if (InTriangle(poly[other], a, b, c)) { ear = false; break; }
                    }
                    if (!ear) continue;
                    indices.Add(ia); indices.Add(ib); indices.Add(ic);
                    remaining.RemoveAt(k);
                    clipped = true;
                    break;
                }
                if (!clipped) break;   // degenerate input: stop rather than loop forever
            }
            if (remaining.Count == 3) { indices.Add(remaining[0]); indices.Add(remaining[1]); indices.Add(remaining[2]); }
            return indices;
        }

        static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        static bool InTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            var d1 = Cross(b - a, p - a);
            var d2 = Cross(c - b, p - b);
            var d3 = Cross(a - c, p - c);
            return d1 >= -1e-6f && d2 >= -1e-6f && d3 >= -1e-6f;
        }

        // ---------- the country ----------

        /// <summary>A slightly irregular, convex outline: an ellipse with a gentle radial ripple.</summary>
        public static List<Vector2> CountryOutline(float radiusU, float radiusV, int points)
        {
            var outline = new List<Vector2>();
            for (var i = 0; i < points; i++)
            {
                var a = i * Mathf.PI * 2f / points;
                var ripple = 1f + 0.035f * Mathf.Sin(3f * a + 0.5f) + 0.025f * Mathf.Sin(5f * a + 1.3f);
                outline.Add(new Vector2(Mathf.Cos(a) * radiusU, Mathf.Sin(a) * radiusV) * ripple);
            }
            return outline;
        }

        /// <summary>One organic polygon per seed, tiling the outline without gaps.</summary>
        public static List<Vector2>[] Provinces(Vector2[] seeds, IReadOnlyList<Vector2> outline)
        {
            var result = new List<Vector2>[seeds.Length];
            for (var i = 0; i < seeds.Length; i++) result[i] = Organic(Cell(seeds, i, outline));
            return result;
        }

        /// <summary>The key of an undirected edge, for matching a border between two provinces.</summary>
        public static (Vector2, Vector2) EdgeKey(Vector2 a, Vector2 b) => Before(a, b) ? (a, b) : (b, a);
    }
}
