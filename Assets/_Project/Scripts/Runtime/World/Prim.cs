using UnityEngine;

namespace Game.World
{
    /// <summary>Helpers to assemble props out of Unity primitives (our placeholder-art approach).</summary>
    public static class Prim
    {
        static Mesh lowPolySphere, lowPolyCylinder;

        /// <summary>Swaps the built-in sphere and cylinder meshes for the palette's low-poly ones in everything made afterwards (null = keep the built-ins).</summary>
        public static void UseMeshes(Mesh sphere, Mesh cylinder)
        {
            lowPolySphere = sphere;
            lowPolyCylinder = cylinder;
        }

        public static GameObject Make(PrimitiveType type, Transform parent, string name, Vector3 localPos,
            Vector3 localScale, Material material, bool keepCollider = false)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            var t = go.transform;
            t.SetParent(parent, false);
            t.localPosition = localPos;
            t.localScale = localScale;
            go.GetComponent<Renderer>().sharedMaterial = material;
            var swap = type == PrimitiveType.Sphere ? lowPolySphere : type == PrimitiveType.Cylinder ? lowPolyCylinder : null;
            if (swap != null) go.GetComponent<MeshFilter>().sharedMesh = swap;
            if (!keepCollider)
            {
                var col = go.GetComponent<Collider>();
                if (Application.isPlaying) Object.Destroy(col);
                else Object.DestroyImmediate(col);
            }
            return go;
        }

        public static GameObject Cube(Transform parent, string name, Vector3 pos, Vector3 scale, Material m, bool collider = false) =>
            Make(PrimitiveType.Cube, parent, name, pos, scale, m, collider);

        public static GameObject Sphere(Transform parent, string name, Vector3 pos, Vector3 scale, Material m) =>
            Make(PrimitiveType.Sphere, parent, name, pos, scale, m);

        public static GameObject Cylinder(Transform parent, string name, Vector3 pos, Vector3 scale, Material m, bool collider = false) =>
            Make(PrimitiveType.Cylinder, parent, name, pos, scale, m, collider);

        public static GameObject Capsule(Transform parent, string name, Vector3 pos, Vector3 scale, Material m) =>
            Make(PrimitiveType.Capsule, parent, name, pos, scale, m);
    }
}
