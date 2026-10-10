using UnityEngine;

namespace Game.World
{
    /// <summary>Merges the primitives under this object into a few meshes when it first wakes up (a fence of 100 posts becomes two renderers).</summary>
    public sealed class MergeChildren : MonoBehaviour
    {
        void Awake() => MeshCombiner.Combine(transform);
    }
}
