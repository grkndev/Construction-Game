using System.Collections.Generic;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// Every tree of a district (or of the map) as a few merged meshes instead of three objects per tree. The trees are stored as plain
    /// positions in the scene and merged on first use into square chunks, so the camera still culls the chunks it cannot see. Trees that
    /// need a collider are built as ordinary objects instead.
    /// </summary>
    public sealed class Forest : MonoBehaviour
    {
        const float ChunkSize = 24f;

        [SerializeField] Mesh sphereMesh;
        [SerializeField] Mesh cylinderMesh;
        [SerializeField] Material leaves;
        [SerializeField] Material trunk;
        [SerializeField] Vector3[] trees;   // x, z, scale

        void Awake() => Build();

        void Build()
        {
            if (trees == null || trees.Length == 0) return;
            var leafChunks = new Dictionary<Vector2Int, List<CombineInstance>>();
            var trunkChunks = new Dictionary<Vector2Int, List<CombineInstance>>();

            foreach (var tree in trees)
            {
                var key = new Vector2Int(Mathf.FloorToInt(tree.x / ChunkSize), Mathf.FloorToInt(tree.y / ChunkSize));
                var root = Matrix4x4.TRS(new Vector3(tree.x, 0f, tree.y), Quaternion.identity, Vector3.one * tree.z);   // same shapes as GameSceneBuilder.BuildTree
                Add(trunkChunks, key, cylinderMesh, root * Matrix4x4.TRS(new Vector3(0f, 0.7f, 0f), Quaternion.identity, new Vector3(0.4f, 0.7f, 0.4f)));
                Add(leafChunks, key, sphereMesh, root * Matrix4x4.TRS(new Vector3(0f, 1.9f, 0f), Quaternion.identity, Vector3.one * 2.3f));
                Add(leafChunks, key, sphereMesh, root * Matrix4x4.TRS(new Vector3(0.1f, 2.9f, 0.05f), Quaternion.identity, Vector3.one * 1.6f));
            }
            Emit(leafChunks, leaves, "Leaves");
            Emit(trunkChunks, trunk, "Trunks");
        }

        static void Add(Dictionary<Vector2Int, List<CombineInstance>> chunks, Vector2Int key, Mesh mesh, Matrix4x4 matrix)
        {
            if (!chunks.TryGetValue(key, out var list)) chunks[key] = list = new List<CombineInstance>();
            list.Add(new CombineInstance { mesh = mesh, transform = matrix });
        }

        void Emit(Dictionary<Vector2Int, List<CombineInstance>> chunks, Material material, string label)
        {
            foreach (var pair in chunks)
            {
                var mesh = new Mesh { name = $"Forest {label} {pair.Key.x},{pair.Key.y}" };
                mesh.CombineMeshes(pair.Value.ToArray(), mergeSubMeshes: true, useMatrices: true);
                var go = new GameObject(mesh.name, typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(transform, false);
                go.GetComponent<MeshFilter>().sharedMesh = mesh;
                go.GetComponent<MeshRenderer>().sharedMaterial = material;
            }
        }
    }
}
