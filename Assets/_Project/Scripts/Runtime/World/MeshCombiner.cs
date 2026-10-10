using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// Merges many small primitives into one mesh per material, so a building made of ~70 cubes and cylinders costs a handful of
    /// renderers instead of 70 (less culling, fewer shadow casters, fewer draw calls). Objects that carry colliders, or that the caller
    /// wants to keep moving on their own (a flag, turning blades), are left alone.
    /// </summary>
    public static class MeshCombiner
    {
        /// <summary>Combines every mesh under <paramref name="root"/> (not root itself); <paramref name="keep"/> marks subtrees to leave untouched.</summary>
        public static void Combine(Transform root, Func<Transform, bool> keep = null)
        {
            var groups = new Dictionary<Material, List<CombineInstance>>();
            var merged = new List<Transform>();
            Collect(root, root, keep, groups, merged);

            foreach (var pair in groups)
            {
                var combined = new Mesh { name = $"Merged {pair.Key.name}" };
                var vertexCount = 0;
                foreach (var instance in pair.Value) vertexCount += instance.mesh.vertexCount;
                if (vertexCount > 65000) combined.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                combined.CombineMeshes(pair.Value.ToArray(), mergeSubMeshes: true, useMatrices: true);

                var go = new GameObject($"Merged {pair.Key.name}", typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(root, false);
                go.GetComponent<MeshFilter>().sharedMesh = combined;
                go.GetComponent<MeshRenderer>().sharedMaterial = pair.Key;
            }

            foreach (var node in merged) Retire(node);
        }

        static void Collect(Transform root, Transform node, Func<Transform, bool> keep,
            Dictionary<Material, List<CombineInstance>> groups, List<Transform> merged)
        {
            for (var i = 0; i < node.childCount; i++)
            {
                var child = node.GetChild(i);
                if (keep != null && keep(child)) continue;
                var filter = child.GetComponent<MeshFilter>();
                var meshRenderer = child.GetComponent<MeshRenderer>();
                if (filter != null && meshRenderer != null && filter.sharedMesh != null && meshRenderer.sharedMaterial != null
                    && child.GetComponent<Collider>() == null)
                {
                    var material = meshRenderer.sharedMaterial;
                    if (!groups.TryGetValue(material, out var list)) groups[material] = list = new List<CombineInstance>();
                    list.Add(new CombineInstance { mesh = filter.sharedMesh, transform = root.worldToLocalMatrix * child.localToWorldMatrix });
                    merged.Add(child);
                }
                Collect(root, child, keep, groups, merged);
            }
        }

        // Removes what was merged: the whole object when nothing else hangs under it, otherwise just its renderer.
        static void Retire(Transform node)
        {
            if (node.childCount == 0) { Destroy(node.gameObject); return; }
            Destroy(node.GetComponent<MeshRenderer>());
            Destroy(node.GetComponent<MeshFilter>());
        }

        static void Destroy(UnityEngine.Object o)
        {
            if (Application.isPlaying) UnityEngine.Object.Destroy(o);
            else UnityEngine.Object.DestroyImmediate(o);
        }
    }
}
