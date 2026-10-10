using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>Prints how heavy the main scene is, per root object (objects, renderers, colliders, triangles, materials). Batchmode: -executeMethod Game.Editor.SceneStats.Print.</summary>
    public static class SceneStats
    {
        [MenuItem("Tools/Project/Print Scene Stats")]
        public static void Print()
        {
            var scene = EditorSceneManager.OpenScene(GameSceneBuilder.ScenePath, OpenSceneMode.Single);
            var total = new Totals();
            foreach (var root in scene.GetRootGameObjects().OrderBy(r => r.name))
            {
                var t = Measure(root);
                total.Add(t);
                if (t.Objects >= 20) Debug.Log($"[Stats] {root.name,-34} objects {t.Objects,6}  renderers {t.Renderers,5}  colliders {t.Colliders,4}  casters {t.Casters,5}  tris {t.Triangles,8}  materials {t.Materials}");
            }
            Debug.Log($"[Stats] TOTAL objects {total.Objects}  renderers {total.Renderers}  colliders {total.Colliders}  casters {total.Casters}  tris {total.Triangles}");
        }

        struct Totals
        {
            public int Objects, Renderers, Colliders, Casters, Materials;
            public long Triangles;
            public void Add(Totals o) { Objects += o.Objects; Renderers += o.Renderers; Colliders += o.Colliders; Casters += o.Casters; Triangles += o.Triangles; }
        }

        static Totals Measure(GameObject root)
        {
            var t = new Totals { Objects = root.GetComponentsInChildren<Transform>(true).Length };
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            t.Renderers = renderers.Length;
            t.Casters = renderers.Count(r => r.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.Off);
            t.Colliders = root.GetComponentsInChildren<Collider>(true).Length;
            t.Materials = renderers.SelectMany(r => r.sharedMaterials).Distinct().Count();
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
                if (filter.sharedMesh != null) t.Triangles += filter.sharedMesh.triangles.Length / 3;
            return t;
        }
    }
}
