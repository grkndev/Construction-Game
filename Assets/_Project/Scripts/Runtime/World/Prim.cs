using UnityEngine;

namespace Game.World
{
    /// <summary>Helpers to assemble props out of Unity primitives (our placeholder-art approach).</summary>
    public static class Prim
    {
        static Mesh lowPolySphere, lowPolyCylinder, lowPolyCapsule;

        /// <summary>Swaps the built-in sphere, cylinder and capsule meshes for the palette's low-poly ones in everything made afterwards (null = keep the built-ins).</summary>
        public static void UseMeshes(Mesh sphere, Mesh cylinder, Mesh capsule = null)
        {
            lowPolySphere = sphere;
            lowPolyCylinder = cylinder;
            lowPolyCapsule = capsule;
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
            var swap = type == PrimitiveType.Sphere ? lowPolySphere : type == PrimitiveType.Cylinder ? lowPolyCylinder : type == PrimitiveType.Capsule ? lowPolyCapsule : null;
            if (swap != null) go.GetComponent<MeshFilter>().sharedMesh = swap;
            // Immediately, not at the end of the frame: until a deferred Destroy lands the part still reports a collider, and MeshCombiner leaves
            // anything with a collider alone (so buildings and decorations built while playing were never merged).
            if (!keepCollider) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        /// <summary>Stops a part from casting a shadow (tiny, flat or hidden pieces whose shadow nobody can see still cost a draw in every shadow cascade).</summary>
        public static GameObject NoShadow(GameObject part)
        {
            part.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return part;
        }

        /// <summary>
        /// Moves a primitive's collider onto an empty twin object (same place, rotation, size and shape) and returns the visible part. A solid prop
        /// is then just a mesh that <see cref="MeshCombiner"/> can merge with its neighbours, and the collision is exactly what it was.
        /// </summary>
        public static GameObject SplitCollider(GameObject part)
        {
            var original = part.GetComponent<Collider>();
            if (original == null) return part;
            var twin = new GameObject(part.name + " Collider");
            twin.transform.SetParent(part.transform.parent, false);
            twin.transform.localPosition = part.transform.localPosition;
            twin.transform.localRotation = part.transform.localRotation;
            twin.transform.localScale = part.transform.localScale;
            switch (original)
            {
                case BoxCollider box:
                    var newBox = twin.AddComponent<BoxCollider>();
                    newBox.center = box.center;
                    newBox.size = box.size;
                    break;
                case CapsuleCollider capsule:
                    var newCapsule = twin.AddComponent<CapsuleCollider>();
                    newCapsule.center = capsule.center;
                    newCapsule.radius = capsule.radius;
                    newCapsule.height = capsule.height;
                    newCapsule.direction = capsule.direction;
                    break;
                default:
                    Object.DestroyImmediate(twin);
                    return part;   // a shape we do not copy: leave it solid on the part
            }
            Object.DestroyImmediate(original);
            return part;
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
