using System.Linq;
using Game.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Tests
{
    public class RenderMergeTests
    {
        GameObject root;

        [TearDown]
        public void TearDown()
        {
            if (root != null) Object.DestroyImmediate(root);
        }

        static Material NewMaterial(string name) => new Material(Shader.Find("Hidden/InternalErrorShader")) { name = name };

        [Test]
        public void Combine_MakesOneRendererPerMaterial_AndReturnsThem()
        {
            root = new GameObject("Root");
            var red = NewMaterial("Red");
            var blue = NewMaterial("Blue");
            Prim.Cube(root.transform, "A", Vector3.zero, Vector3.one, red);
            Prim.Cube(root.transform, "B", Vector3.right * 2f, Vector3.one, red);
            Prim.Cube(root.transform, "C", Vector3.left * 2f, Vector3.one, blue);

            var merged = MeshCombiner.Combine(root.transform);

            Assert.AreEqual(2, merged.Count);
            var renderers = root.GetComponentsInChildren<MeshRenderer>();
            Assert.AreEqual(2, renderers.Length, "the originals must be gone");
            Assert.AreEqual(red, merged.Single(m => m.renderer.sharedMaterial == red).material);
            Assert.AreEqual(24 * 2, merged.Single(m => m.material == red).renderer.GetComponent<MeshFilter>().sharedMesh.vertexCount);
        }

        [Test]
        public void Combine_KeepsShadowlessPartsApart_SoShadowsStayAsTheyWere()
        {
            root = new GameObject("Root");
            var wall = NewMaterial("Wall");
            Prim.Cube(root.transform, "Casts", Vector3.zero, Vector3.one, wall);
            Prim.NoShadow(Prim.Cube(root.transform, "Quiet", Vector3.right * 2f, Vector3.one, wall));

            var merged = MeshCombiner.Combine(root.transform);

            Assert.AreEqual(2, merged.Count, "same colour, different shadow setting: two meshes");
            Assert.AreEqual(1, merged.Count(m => m.renderer.shadowCastingMode == ShadowCastingMode.Off));
            Assert.AreEqual(1, merged.Count(m => m.renderer.shadowCastingMode == ShadowCastingMode.On));
        }

        [Test]
        public void Combine_LeavesSolidParts_AndKeptSubtrees()
        {
            root = new GameObject("Root");
            var wall = NewMaterial("Wall");
            Prim.Cube(root.transform, "Solid", Vector3.zero, Vector3.one, wall, collider: true);
            var flag = Prim.Cube(root.transform, "Flag", Vector3.up, Vector3.one, wall);
            Prim.Cube(root.transform, "Plain", Vector3.right * 2f, Vector3.one, wall);

            MeshCombiner.Combine(root.transform, t => t == flag.transform);

            Assert.IsNotNull(root.transform.Find("Solid")?.GetComponent<Collider>());
            Assert.IsNotNull(root.transform.Find("Flag")?.GetComponent<MeshRenderer>());
            Assert.IsNull(root.transform.Find("Plain"));
        }

        [Test]
        public void SplitCollider_KeepsTheSolidShape_AndLetsThePartMerge()
        {
            root = new GameObject("Root");
            var wall = NewMaterial("Wall");
            var post = Prim.Cylinder(root.transform, "Post", new Vector3(1f, 0.7f, 2f), new Vector3(0.4f, 0.7f, 0.4f), wall, true);
            post.transform.Rotate(0f, 30f, 0f);

            Prim.SplitCollider(post);

            Assert.IsNull(post.GetComponent<Collider>(), "the visible part is no longer solid");
            var twin = root.transform.Find("Post Collider").GetComponent<Collider>();
            Assert.AreEqual(1f, twin.bounds.center.x, 0.001f);
            Assert.AreEqual(0.7f, twin.bounds.center.y, 0.001f);
            Assert.AreEqual(1.4f, twin.bounds.size.y, 0.001f);   // the built-in cylinder is 2 high, scaled by 0.7

            Assert.AreEqual(1, MeshCombiner.Combine(root.transform).Count);
            Assert.IsNotNull(root.transform.Find("Post Collider"), "the collision survives the merge");
        }

        [Test]
        public void Combine_KeepsOutlineDirections_PointingAwayFromEachPartsOwnCentre_EvenWhenMergedAgain()
        {
            root = new GameObject("Root");
            var wall = NewMaterial("Wall");
            Prim.Cube(root.transform, "Far", new Vector3(50f, 0f, 0f), Vector3.one, wall);
            Prim.Cube(root.transform, "Far2", new Vector3(-50f, 0f, 0f), Vector3.one, wall);

            var first = MeshCombiner.Combine(root.transform).Single().renderer.GetComponent<MeshFilter>().sharedMesh;
            AssertDirectionsPointOutOfTheirCubes(first);

            // Merge the merged mesh into a bigger one (a finished building fusing its stages).
            var outer = new GameObject("Outer").transform;
            outer.SetParent(root.transform, false);
            outer.localPosition = new Vector3(0f, 0f, 30f);
            root.transform.Find("Merged Wall").SetParent(outer, true);
            var second = MeshCombiner.Combine(root.transform).Single().renderer.GetComponent<MeshFilter>().sharedMesh;
            AssertDirectionsPointOutOfTheirCubes(second);
        }

        static void AssertDirectionsPointOutOfTheirCubes(Mesh mesh)
        {
            var directions = new System.Collections.Generic.List<Vector4>();
            mesh.GetUVs(1, directions);
            var vertices = mesh.vertices;
            Assert.AreEqual(vertices.Length, directions.Count);
            for (var i = 0; i < vertices.Length; i++)
            {
                // Each cube is 1 wide, so the corner's offset from its cube's centre is (+-0.5, +-0.5, +-0.5) whatever the cube's position.
                var centreX = Mathf.Abs(vertices[i].x) > 25f ? Mathf.Sign(vertices[i].x) * 50f : 0f;
                var offset = new Vector3(vertices[i].x - centreX, vertices[i].y, vertices[i].z - (Mathf.Abs(vertices[i].z) > 15f ? 30f : 0f));
                Assert.AreEqual(MeshMerge.Marker, directions[i].w, $"vertex {i} lacks the marker");
                Assert.Greater(Vector3.Dot(((Vector3)directions[i]).normalized, offset.normalized), 0.99f, $"vertex {i}");
            }
        }

        [Test]
        public void MeshMerge_KeepsEveryVertexAndTriangle_OfLowPolyMeshes()
        {
            var sphere = LowPolyMeshes.CreateSphere();
            var instances = new[]
            {
                new CombineInstance { mesh = sphere, transform = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one) },
                new CombineInstance { mesh = sphere, transform = Matrix4x4.TRS(Vector3.right * 3f, Quaternion.identity, Vector3.one * 2f) },
            };

            var merged = MeshMerge.Combine(instances, "Test");

            Assert.AreEqual(sphere.vertexCount * 2, merged.vertexCount);
            Assert.AreEqual(sphere.triangles.Length * 2, merged.triangles.Length);
            var directions = new System.Collections.Generic.List<Vector4>();
            merged.GetUVs(1, directions);
            Assert.AreEqual(merged.vertexCount, directions.Count);
            Object.DestroyImmediate(merged);
            Object.DestroyImmediate(sphere);
        }

        [Test]
        public void MeshMerge_MirroredPart_KeepsTheSameWindingAsThePlainOne()
        {
            var sphere = LowPolyMeshes.CreateSphere();
            var plain = MeshMerge.Combine(new[] { new CombineInstance { mesh = sphere, transform = Matrix4x4.identity } }, "Plain");
            var mirrored = MeshMerge.Combine(new[] { new CombineInstance { mesh = sphere, transform = Matrix4x4.Scale(new Vector3(-1f, 1f, 1f)) } }, "Mirror");

            // For a sphere centred on the origin, the sign of Cross(b-a, c-a) . centroid says whether a triangle faces out or in.
            static float Side(Mesh mesh, int triangle)
            {
                var v = mesh.vertices;
                var i = mesh.triangles;
                var a = v[i[triangle * 3]];
                var b = v[i[triangle * 3 + 1]];
                var c = v[i[triangle * 3 + 2]];
                return Vector3.Dot(Vector3.Cross(b - a, c - a), (a + b + c) / 3f);
            }
            for (var t = 0; t < plain.triangles.Length / 3; t++)
            {
                var p = Side(plain, t);
                if (Mathf.Abs(p) < 1e-6f) continue;
                Assert.AreEqual(Mathf.Sign(p), Mathf.Sign(Side(mirrored, t)), $"triangle {t} turned inside out by the mirror");
            }
            Object.DestroyImmediate(plain);
            Object.DestroyImmediate(mirrored);
            Object.DestroyImmediate(sphere);
        }

        [Test]
        public void LowPolyCapsule_MatchesTheBuiltInSize_WithFarFewerTriangles()
        {
            var mesh = LowPolyMeshes.CreateCapsule();
            Assert.AreEqual(new Vector3(1f, 2f, 1f).x, mesh.bounds.size.x, 0.05f);
            Assert.AreEqual(2f, mesh.bounds.size.y, 0.001f);
            Assert.AreEqual(1f, mesh.bounds.size.z, 0.05f);
            Assert.Less(mesh.triangles.Length / 3, 300);
            Assert.Greater(mesh.triangles.Length / 3, 100);
            Object.DestroyImmediate(mesh);
        }
    }
}
