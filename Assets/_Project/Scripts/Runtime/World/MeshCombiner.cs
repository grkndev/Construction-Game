using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// Merges many small primitives into one mesh per material, so a building made of ~70 cubes and cylinders costs a handful of
    /// renderers instead of 70 (less culling, fewer shadow casters, fewer draw calls). Objects that carry colliders, or that the caller
    /// wants to keep moving on their own (a flag, turning blades), are left alone. Parts that do not cast shadows are merged separately from
    /// those that do, so the merged mesh casts exactly what its parts did.
    /// </summary>
    public static class MeshCombiner
    {
        /// <summary>
        /// Combines every mesh under <paramref name="root"/> (not root itself); <paramref name="keep"/> marks subtrees to leave untouched.
        /// Returns the merged renderers with the material each one draws.
        /// </summary>
        public static List<(Material material, MeshRenderer renderer)> Combine(Transform root, Func<Transform, bool> keep = null)
        {
            var groups = new Dictionary<(Material, bool), List<CombineInstance>>();
            var merged = new List<Transform>();
            var result = new List<(Material, MeshRenderer)>();
            Collect(root, root, keep, groups, merged);

            foreach (var pair in groups)
            {
                var (material, castsShadow) = pair.Key;
                var combined = MeshMerge.Combine(pair.Value, $"Merged {material.name}");

                var go = new GameObject($"Merged {material.name}", typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(root, false);
                go.GetComponent<MeshFilter>().sharedMesh = combined;
                var meshRenderer = go.GetComponent<MeshRenderer>();
                meshRenderer.sharedMaterial = material;
                if (!castsShadow) meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                result.Add((material, meshRenderer));
            }

            foreach (var node in merged) Retire(node);
            return result;
        }

        static void Collect(Transform root, Transform node, Func<Transform, bool> keep,
            Dictionary<(Material, bool), List<CombineInstance>> groups, List<Transform> merged)
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
                    var key = (meshRenderer.sharedMaterial, meshRenderer.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.Off);
                    if (!groups.TryGetValue(key, out var list)) groups[key] = list = new List<CombineInstance>();
                    list.Add(new CombineInstance { mesh = filter.sharedMesh, transform = root.worldToLocalMatrix * child.localToWorldMatrix });
                    merged.Add(child);
                }
                Collect(root, child, keep, groups, merged);
            }
        }

        // Removes what was merged. The renderer and filter go at once (not at the end of the frame): a second merge in the same frame, such as a
        // finished building fusing right after it was built, must not see the originals still standing next to their merged copy.
        static void Retire(Transform node)
        {
            UnityEngine.Object.DestroyImmediate(node.GetComponent<MeshRenderer>());
            UnityEngine.Object.DestroyImmediate(node.GetComponent<MeshFilter>());
            if (node.childCount == 0) Destroy(node.gameObject);   // nothing else hangs under it: the empty object goes too
        }

        static void Destroy(UnityEngine.Object o)
        {
            if (Application.isPlaying) UnityEngine.Object.Destroy(o);
            else UnityEngine.Object.DestroyImmediate(o);
        }
    }
}
