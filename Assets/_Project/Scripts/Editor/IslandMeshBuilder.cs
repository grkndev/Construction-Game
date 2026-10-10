using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Builds the flat-shaded ground slabs of the districts and of the map (a polygon extruded between two heights) and stores them as mesh
    /// assets, so the scene only references them. An existing asset is refilled in place to keep its GUID stable.
    /// </summary>
    public static class IslandMeshBuilder
    {
        const string Dir = "Assets/_Project/Art/Meshes";

        /// <param name="outline">Simple counter-clockwise polygon in x/z (seen from +y).</param>
        public static Mesh Prism(string name, IReadOnlyList<Vector2> outline, float yBottom, float yTop)
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var triangles = new List<int>();

            // Top face: ear-clipped (the outline need not be convex). Unity wants clockwise winding for the visible side, so the order is reversed.
            var first = vertices.Count;
            foreach (var p in outline)
            {
                vertices.Add(new Vector3(p.x, yTop, p.y));
                normals.Add(Vector3.up);
            }
            var top = MapGeometry.Triangulate(outline);
            for (var i = 0; i < top.Count; i += 3)
            {
                triangles.Add(first + top[i]);
                triangles.Add(first + top[i + 2]);
                triangles.Add(first + top[i + 1]);
            }

            // Sides: one flat quad per outline edge.
            for (var i = 0; i < outline.Count; i++)
            {
                var a = outline[i];
                var b = outline[(i + 1) % outline.Count];
                var edge = new Vector3(b.x - a.x, 0f, b.y - a.y);
                var normal = new Vector3(edge.z, 0f, -edge.x).normalized;   // outward for a counter-clockwise outline
                var start = vertices.Count;
                vertices.Add(new Vector3(a.x, yBottom, a.y));
                vertices.Add(new Vector3(a.x, yTop, a.y));
                vertices.Add(new Vector3(b.x, yTop, b.y));
                vertices.Add(new Vector3(b.x, yBottom, b.y));
                for (var k = 0; k < 4; k++) normals.Add(normal);
                triangles.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
            }

            return Save(name, vertices, normals, triangles);
        }

        /// <summary>Stores raw mesh data as an asset (refilling an existing one in place).</summary>
        public static Mesh Save(string name, List<Vector3> vertices, List<Vector3> normals, List<int> triangles)
        {
            EditorWire.EnsureFolder(Dir);
            var path = $"{Dir}/{name}.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
            {
                mesh = new Mesh { name = name };
                AssetDatabase.CreateAsset(mesh, path);
            }
            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            return mesh;
        }
    }
}
