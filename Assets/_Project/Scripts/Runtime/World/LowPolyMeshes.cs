using System.Collections.Generic;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// Smooth-shaded low-poly stand-ins for Unity's built-in sphere (768 triangles), capsule (832) and cylinder: with an outline pass, shadow cascades and
    /// hundreds of trees those triangles add up. Same dimensions as the built-ins (sphere diameter 1, cylinder diameter 1 and height 2, capsule diameter 1 and height 2), so
    /// <see cref="Prim"/> can swap them in without touching any layout.
    /// </summary>
    public static class LowPolyMeshes
    {
        public const int SphereSegments = 12, SphereRings = 7, CylinderSegments = 14;

        public static Mesh CreateSphere(string name = "LowPolySphere")
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var triangles = new List<int>();
            for (var ring = 0; ring <= SphereRings; ring++)
            {
                var theta = Mathf.PI * ring / SphereRings;
                for (var seg = 0; seg <= SphereSegments; seg++)
                {
                    var phi = 2f * Mathf.PI * seg / SphereSegments;
                    var n = new Vector3(Mathf.Sin(theta) * Mathf.Cos(phi), Mathf.Cos(theta), Mathf.Sin(theta) * Mathf.Sin(phi));
                    vertices.Add(n * 0.5f);
                    normals.Add(n);
                }
            }
            var stride = SphereSegments + 1;
            for (var ring = 0; ring < SphereRings; ring++)
            for (var seg = 0; seg < SphereSegments; seg++)
            {
                var a = ring * stride + seg;
                var b = a + stride;
                if (ring > 0) AddFacing(vertices, normals, triangles, a, b, a + 1);                      // not at the top pole
                if (ring < SphereRings - 1) AddFacing(vertices, normals, triangles, a + 1, b, b + 1);    // not at the bottom pole
            }
            return Build(name, vertices, normals, triangles);
        }

        public const int CapsuleCapRings = 4;

        public static Mesh CreateCapsule(string name = "LowPolyCapsule")
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var triangles = new List<int>();
            // Two hemispheres (radius 0.5) joined by a straight band of height 1; the equator ring appears twice, once per half.
            var rings = 0;
            foreach (var top in new[] { true, false })
            for (var i = 0; i <= CapsuleCapRings; i++)
            {
                var theta = Mathf.PI * 0.5f * (top ? i : CapsuleCapRings + i) / CapsuleCapRings;
                for (var seg = 0; seg <= SphereSegments; seg++)
                {
                    var phi = 2f * Mathf.PI * seg / SphereSegments;
                    var n = new Vector3(Mathf.Sin(theta) * Mathf.Cos(phi), Mathf.Cos(theta), Mathf.Sin(theta) * Mathf.Sin(phi));
                    vertices.Add(n * 0.5f + Vector3.up * (top ? 0.5f : -0.5f));
                    normals.Add(n);
                }
                rings++;
            }
            var stride = SphereSegments + 1;
            for (var ring = 0; ring < rings - 1; ring++)
            for (var seg = 0; seg < SphereSegments; seg++)
            {
                var a = ring * stride + seg;
                var b = a + stride;
                if (ring > 0) AddFacing(vertices, normals, triangles, a, b, a + 1);                      // not at the top pole
                if (ring < rings - 2) AddFacing(vertices, normals, triangles, a + 1, b, b + 1);          // not at the bottom pole
            }
            return Build(name, vertices, normals, triangles);
        }

        public static Mesh CreateCylinder(string name = "LowPolyCylinder")
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var triangles = new List<int>();

            // Side: smooth radial normals.
            for (var seg = 0; seg <= CylinderSegments; seg++)
            {
                var phi = 2f * Mathf.PI * seg / CylinderSegments;
                var n = new Vector3(Mathf.Cos(phi), 0f, Mathf.Sin(phi));
                vertices.Add(n * 0.5f + Vector3.down); normals.Add(n);
                vertices.Add(n * 0.5f + Vector3.up); normals.Add(n);
            }
            for (var seg = 0; seg < CylinderSegments; seg++)
            {
                var i = seg * 2;
                AddFacing(vertices, normals, triangles, i, i + 1, i + 2);
                AddFacing(vertices, normals, triangles, i + 1, i + 3, i + 2);
            }

            // Caps: a fan around a centre vertex, flat normals.
            foreach (var up in new[] { true, false })
            {
                var y = up ? 1f : -1f;
                var centre = vertices.Count;
                vertices.Add(new Vector3(0f, y, 0f)); normals.Add(up ? Vector3.up : Vector3.down);
                for (var seg = 0; seg <= CylinderSegments; seg++)
                {
                    var phi = 2f * Mathf.PI * seg / CylinderSegments;
                    vertices.Add(new Vector3(Mathf.Cos(phi) * 0.5f, y, Mathf.Sin(phi) * 0.5f));
                    normals.Add(up ? Vector3.up : Vector3.down);
                }
                for (var seg = 0; seg < CylinderSegments; seg++) AddFacing(vertices, normals, triangles, centre, centre + 1 + seg, centre + 2 + seg);
            }
            return Build(name, vertices, normals, triangles);
        }

        // Adds the triangle with whichever winding makes it face the way its vertex normals point (Unity: clockwise seen from the front).
        static void AddFacing(List<Vector3> v, List<Vector3> n, List<int> tris, int a, int b, int c)
        {
            var face = Vector3.Cross(v[b] - v[a], v[c] - v[a]);
            var normal = n[a] + n[b] + n[c];
            if (Vector3.Dot(face, normal) >= 0f) { tris.Add(a); tris.Add(b); tris.Add(c); }
            else { tris.Add(a); tris.Add(c); tris.Add(b); }
        }

        static Mesh Build(string name, List<Vector3> vertices, List<Vector3> normals, List<int> triangles)
        {
            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
