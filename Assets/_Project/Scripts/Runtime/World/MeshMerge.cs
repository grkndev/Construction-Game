using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.World
{
    /// <summary>
    /// Merges meshes into one (what <c>Mesh.CombineMeshes</c> does) and, on top of that, remembers for every vertex the direction it had "away from the
    /// middle of its own primitive" in UV1 (xyz, with w = 2 as the marker: primitives can have a UV1 of their own). The toon outline pushes vertices along that direction (see Toon.shader); once primitives are merged
    /// they no longer have a middle of their own, and without UV1 the outline would turn lopsided or vanish. A mesh that was merged before carries
    /// its directions into the next merge. Only the first submesh of each source is used (every mesh here has one).
    /// </summary>
    public static class MeshMerge
    {
        public const float Marker = 2f;

        public static Mesh Combine(IList<CombineInstance> instances, string name)
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var directions = new List<Vector4>();
            var triangles = new List<int>();
            var carried = new List<Vector4>();

            foreach (var instance in instances)
            {
                var source = instance.mesh;
                var matrix = instance.transform;
                var normalMatrix = matrix.inverse.transpose;
                var flipped = matrix.determinant < 0f;   // a mirrored part turns inside out unless its triangles are reversed
                var sourceVertices = source.vertices;
                var sourceNormals = source.normals;
                var hasDirections = source.HasVertexAttribute(VertexAttribute.TexCoord1) && source.GetVertexAttributeDimension(VertexAttribute.TexCoord1) == 4;
                if (hasDirections) source.GetUVs(1, carried);

                var start = vertices.Count;
                for (var i = 0; i < sourceVertices.Length; i++)
                {
                    vertices.Add(matrix.MultiplyPoint3x4(sourceVertices[i]));
                    var normal = sourceNormals.Length == sourceVertices.Length ? normalMatrix.MultiplyVector(sourceNormals[i]) : Vector3.up;
                    normals.Add(normal.sqrMagnitude > 1e-8f ? normal.normalized : Vector3.up);
                    var direction = matrix.MultiplyVector(hasDirections ? (Vector3)carried[i] : sourceVertices[i].normalized);
                    direction = direction.sqrMagnitude > 1e-8f ? direction.normalized : Vector3.up;
                    directions.Add(new Vector4(direction.x, direction.y, direction.z, Marker));
                }

                var indices = source.GetTriangles(0);
                for (var i = 0; i + 2 < indices.Length; i += 3)
                {
                    triangles.Add(start + indices[i]);
                    triangles.Add(start + indices[flipped ? i + 2 : i + 1]);
                    triangles.Add(start + indices[flipped ? i + 1 : i + 2]);
                }
            }

            var mesh = new Mesh { name = name };
            if (vertices.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(1, directions);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
