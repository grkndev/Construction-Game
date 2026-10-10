using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.World;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// The picture shown on the dashboard: Çekiçköy as a country, its five districts as provinces that fit together without gaps. It is only
    /// scenery (the districts you play in are separate); each province gets its landmark, some trees and an anchor for its card.
    /// The map is drawn on the camera's screen axes: u to the right, v up the screen (away from the camera).
    /// </summary>
    public static class MapBuilder
    {
        // Seeds of the provinces in district order: Sahil, Merkez, Liman, Tepe, Sanayi. Sahil (south) and Liman (east) are on the coast;
        // Merkez sits in the middle, surrounded by the other four.
        public static readonly Vector2[] Seeds =
        {
            new Vector2(-4f, -40f), new Vector2(0f, 4f), new Vector2(46f, -6f), new Vector2(-6f, 40f), new Vector2(-44f, 0f),
        };

        const float RadiusU = 62f, RadiusV = 58f;

        public sealed class Result
        {
            public GameObject Root;
            public Transform[] Anchors;
        }

        public static List<Vector2> Outline() => MapGeometry.CountryOutline(RadiusU, RadiusV, 22);

        /// <summary>The provinces in map (u, v) coordinates, wobbled and ready to draw.</summary>
        public static List<Vector2>[] ProvincesUv() => MapGeometry.Provinces(Seeds, Outline());

        /// <summary>Map (u right, v up the screen) to world x/z: the camera looks along (1, 0, 1), so u runs along (1, 0, -1).</summary>
        public static Vector2 ToWorld(Vector2 uv) => new Vector2((uv.x + uv.y) * 0.7071068f, (uv.y - uv.x) * 0.7071068f);

        public static List<Vector2> ToWorld(IEnumerable<Vector2> uv) => uv.Select(ToWorld).ToList();

        public static Result Build(ToonPalette p)
        {
            var root = new GameObject("Map").transform;
            var provinces = ProvincesUv().Select(ToWorld).ToArray();
            var coast = MapGeometry.Organic(Outline()).Select(ToWorld).ToList();

            // Dirt under everything, a sand rim around the coast, then each province in its own tint.
            MeshPart(root, "Base", IslandMeshBuilder.Prism("MapBase", Scaled(coast, 0.97f), -2.6f, -0.5f), p.dirt);
            MeshPart(root, "Beach", IslandMeshBuilder.Prism("MapBeach", Scaled(coast, 1.05f), -1.2f, -0.12f), p.sand);
            for (var i = 0; i < provinces.Length; i++)
                MeshPart(root, $"Province {Balance.Districts[i].Name}", IslandMeshBuilder.Prism($"MapProvince{i + 1}", provinces[i], -0.5f, 0f), p.districtGround[i]);
            MeshPart(root, "Borders", BorderMesh(provinces), p.white);

            var anchors = new Transform[provinces.Length];
            var random = new System.Random(5);
            for (var i = 0; i < provinces.Length; i++)
            {
                var centre = MapGeometry.Centroid(provinces[i]);
                // The card floats on the upper part of the province and the landmark stands below it, so neither hides the other.
                var up = ToWorld(new Vector2(0f, 1f));
                var cardSpot = centre + up * 8f;
                var landmarkSpot = centre - up * 9f;
                var anchor = new GameObject($"Anchor {Balance.Districts[i].Name}").transform;
                anchor.SetParent(root, false);
                anchor.localPosition = new Vector3(cardSpot.x, 0f, cardSpot.y);
                anchors[i] = anchor;

                var outward = Mathf.Atan2(centre.y, centre.x) * Mathf.Rad2Deg;
                GameSceneBuilder.BuildLandmark(root, p, i, new Vector3(landmarkSpot.x, 0f, landmarkSpot.y), outward, 1.9f, onLand: true);
                Scatter(root, p, provinces[i], new[] { landmarkSpot, cardSpot }, random, 26);
            }
            return new Result { Root = root.gameObject, Anchors = anchors };
        }

        // Trees at random spots well inside a province, away from its landmark and its card.
        static void Scatter(Transform root, ToonPalette p, List<Vector2> province, Vector2[] keepClear, System.Random random, int count)
        {
            float minX = province.Min(v => v.x), maxX = province.Max(v => v.x), minY = province.Min(v => v.y), maxY = province.Max(v => v.y);
            var placed = 0;
            for (var attempt = 0; attempt < 600 && placed < count; attempt++)
            {
                var spot = new Vector2(Mathf.Lerp(minX, maxX, (float)random.NextDouble()), Mathf.Lerp(minY, maxY, (float)random.NextDouble()));
                if (!MapGeometry.Contains(province, spot)) continue;
                if (MapGeometry.DistanceToEdges(province, spot) < 3.2f) continue;
                if (keepClear.Any(c => Vector2.Distance(spot, c) < 7.5f)) continue;
                GameSceneBuilder.BuildTree(root, new Vector3(spot.x, 0f, spot.y), 1.15f + (float)random.NextDouble() * 0.5f, p, collider: false);
                placed++;
            }
        }

        static List<Vector2> Scaled(List<Vector2> poly, float factor) => poly.Select(v => v * factor).ToList();

        static GameObject MeshPart(Transform parent, string name, Mesh mesh, Material material)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            return go;
        }

        /// <summary>Thin flat strips along every edge two provinces share (the borders on the map).</summary>
        static Mesh BorderMesh(List<Vector2>[] provinces)
        {
            var seen = new Dictionary<(Vector2, Vector2), int>();
            foreach (var province in provinces)
                for (var i = 0; i < province.Count; i++)
                {
                    var key = MapGeometry.EdgeKey(province[i], province[(i + 1) % province.Count]);
                    seen[key] = seen.TryGetValue(key, out var n) ? n + 1 : 1;
                }

            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var triangles = new List<int>();
            const float half = 0.28f, y = 0.04f;
            foreach (var edge in seen.Where(e => e.Value > 1).Select(e => e.Key))
            {
                var a = edge.Item1;
                var b = edge.Item2;
                var side = new Vector2(-(b - a).y, (b - a).x).normalized * half;
                var start = vertices.Count;
                vertices.Add(new Vector3(a.x - side.x, y, a.y - side.y));
                vertices.Add(new Vector3(a.x + side.x, y, a.y + side.y));
                vertices.Add(new Vector3(b.x + side.x, y, b.y + side.y));
                vertices.Add(new Vector3(b.x - side.x, y, b.y - side.y));
                for (var k = 0; k < 4; k++) normals.Add(Vector3.up);
                // Both windings, so the strip shows whichever way the edge happened to be stored.
                triangles.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3, start, start + 2, start + 1, start, start + 3, start + 2 });
            }
            return IslandMeshBuilder.Save("MapBorders", vertices, normals, triangles);
        }
    }
}
