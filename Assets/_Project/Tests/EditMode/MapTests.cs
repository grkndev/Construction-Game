using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Game.Editor;
using Game.World;

namespace Game.Tests
{
    /// <summary>The country map (dashboard picture), the ground sizing of a district and its play area.</summary>
    public class MapTests
    {
        static readonly string[] Names = { "Sahil", "Merkez", "Liman", "Tepe", "Sanayi" };

        static (Vector2, Vector2) Key(Vector2 a, Vector2 b) => MapGeometry.EdgeKey(a, b);

        static Dictionary<(Vector2, Vector2), List<int>> EdgeOwners(List<Vector2>[] provinces)
        {
            var owners = new Dictionary<(Vector2, Vector2), List<int>>();
            for (var i = 0; i < provinces.Length; i++)
                for (var k = 0; k < provinces[i].Count; k++)
                {
                    var key = Key(provinces[i][k], provinces[i][(k + 1) % provinces[i].Count]);
                    if (!owners.TryGetValue(key, out var list)) owners[key] = list = new List<int>();
                    list.Add(i);
                }
            return owners;
        }

        [Test]
        public void EveryProvince_IsASimpleCounterClockwisePolygon()
        {
            foreach (var (province, i) in MapBuilder.ProvincesUv().Select((p, i) => (p, i)))
            {
                Assert.Greater(MapGeometry.Area(province), 200f, Names[i]);
                var triangles = MapGeometry.Triangulate(province);
                Assert.AreEqual((province.Count - 2) * 3, triangles.Count, $"{Names[i]}: ear clipping must use every vertex");
                var areaOfTriangles = 0f;
                for (var t = 0; t < triangles.Count; t += 3)
                    areaOfTriangles += MapGeometry.Area(new[] { province[triangles[t]], province[triangles[t + 1]], province[triangles[t + 2]] });
                Assert.AreEqual(MapGeometry.Area(province), areaOfTriangles, 0.5f, Names[i]);
            }
        }

        [Test]
        public void Provinces_FitTogetherWithoutGapsOrOverlaps()
        {
            var provinces = MapBuilder.ProvincesUv();
            // Every edge belongs to one province (the coast) or to exactly two (a border), never more.
            foreach (var pair in EdgeOwners(provinces))
            {
                Assert.LessOrEqual(pair.Value.Count, 2, "an edge is shared by more than two provinces");
                if (pair.Value.Count == 2) Assert.AreNotEqual(pair.Value[0], pair.Value[1]);
            }
            // The five areas add up to the area inside the outer boundary (the edges used by one province only).
            var coastEdges = EdgeOwners(provinces).Where(e => e.Value.Count == 1).Select(e => e.Key).ToList();
            Assert.Greater(coastEdges.Count, 10);
            var sum = provinces.Sum(p => MapGeometry.Area(p));
            var outline = MapGeometry.Organic(MapBuilder.Outline());
            Assert.AreEqual(MapGeometry.Area(outline), sum, sum * 0.03f, "the provinces must fill the country");
        }

        [Test]
        public void EveryProvince_HasANeighbourToTheEdge_AndMerkezIsLandlocked()
        {
            var provinces = MapBuilder.ProvincesUv();
            var owners = EdgeOwners(provinces);
            for (var i = 0; i < provinces.Length; i++)
            {
                var neighbours = new HashSet<int>();
                var coast = 0;
                for (var k = 0; k < provinces[i].Count; k++)
                {
                    var list = owners[Key(provinces[i][k], provinces[i][(k + 1) % provinces[i].Count])];
                    if (list.Count == 1) coast++;
                    else neighbours.Add(list.First(o => o != i));
                }
                Assert.GreaterOrEqual(neighbours.Count, 2, $"{Names[i]} should border at least two provinces");
                if (i == 1) Assert.AreEqual(0, coast, "Merkez is the inland province");
                if (i == 0 || i == 2) Assert.Greater(coast, 0, $"{Names[i]} is on the sea");
            }
            // Merkez touches every other province.
            var merkez = new HashSet<int>();
            for (var k = 0; k < provinces[1].Count; k++)
                foreach (var o in owners[Key(provinces[1][k], provinces[1][(k + 1) % provinces[1].Count])]) merkez.Add(o);
            CollectionAssert.AreEquivalent(new[] { 0, 1, 2, 3, 4 }, merkez);
        }

        [Test]
        public void SharedBorders_UseIdenticalVertices_FromBothSides()
        {
            // Both provinces cut a shared edge from the same end, so their vertex lists match exactly (a hair of difference would show as a crack).
            var provinces = MapBuilder.ProvincesUv();
            var vertexSets = provinces.Select(p => new HashSet<Vector2>(p)).ToArray();
            foreach (var pair in EdgeOwners(provinces).Where(e => e.Value.Count == 2))
            {
                foreach (var owner in pair.Value)
                {
                    Assert.IsTrue(vertexSets[owner].Contains(pair.Key.Item1));
                    Assert.IsTrue(vertexSets[owner].Contains(pair.Key.Item2));
                }
            }
        }

        [Test]
        public void ViewFootprint_ReachesFartherUpTheScreenThanDownIt()
        {
            var view = ViewFootprint.Compute(new Vector3(-19f, 21f, -19f), 1.25f, 30f, 2.6f);
            // The camera sits at -x/-z, so the far (top of the screen) side shows more ground than the near side.
            Assert.Greater(view.MaxX, -view.MinX);
            Assert.Greater(view.MaxZ, -view.MinZ);
            Assert.Greater(view.MaxX, 5f);
        }

        [Test]
        public void WiderScreens_ShowMoreGround()
        {
            var narrow = ViewFootprint.Compute(new Vector3(-19f, 21f, -19f), 1.25f, 30f, 1.6f);
            var wide = ViewFootprint.Compute(new Vector3(-19f, 21f, -19f), 1.25f, 30f, 2.6f);
            Assert.GreaterOrEqual(wide.MaxX, narrow.MaxX);
            Assert.GreaterOrEqual(wide.MaxZ, narrow.MaxZ);
        }

        [Test]
        public void PlayArea_IsASquare()
        {
            Assert.AreEqual(new Vector2(10f, -7f), District.ClampToSquare(new Vector2(10f, -7f), 25f));
            Assert.AreEqual(new Vector2(25f, 25f), District.ClampToSquare(new Vector2(90f, 40f), 25f));
            Assert.AreEqual(new Vector2(-25f, 3f), District.ClampToSquare(new Vector2(-40f, 3f), 25f));
        }

        [Test]
        public void ShadowDistance_CoversEverythingTheDistrictCameraCanShow()
        {
            // URP measures from the camera: the far corner of a wide screen is ~100 units out, and the shadows must still be there.
            var depth = ViewFootprint.FarthestDepth(new Vector3(-19f, 21f, -19f), Game.Core.GameController.DistrictViewScale, 30f, 2.6f);
            Assert.Greater(depth, 60f);
            foreach (var path in new[] { "Assets/Settings/PC_RPAsset.asset", "Assets/Settings/Mobile_RPAsset.asset" })
            {
                var pipeline = UnityEditor.AssetDatabase.LoadMainAssetAtPath(path);
                Assert.IsNotNull(pipeline, path);
                var distance = new UnityEditor.SerializedObject(pipeline).FindProperty("m_ShadowDistance").floatValue;
                Assert.GreaterOrEqual(distance, depth + 10f, $"{path}: shadows stop before the top of the screen");
            }
        }
    }
}
