using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Game.Core;

namespace Game.Editor
{
    /// <summary>
    /// Generates scenes from code instead of hand-editing .unity YAML.
    /// <c>Ensure*</c> only creates a scene that doesn't exist yet: Unity assigns random fileIDs on creation,
    /// so regenerating an existing scene would churn the git diff and overwrite hand edits.
    /// <c>Rebuild*</c> regenerates from scratch and saves over the same path (the .meta GUID is kept).
    /// </summary>
    public static class SceneBuilders
    {
        public const string ScenesDir = "Assets/_Project/Scenes";
        public const string MaterialsDir = "Assets/_Project/Art/Materials";
        public const string BootstrapPath = ScenesDir + "/Bootstrap.unity";
        public const string PlaygroundPath = ScenesDir + "/Playground.unity";

        public static void EnsureBootstrap()
        {
            if (!File.Exists(BootstrapPath)) RebuildBootstrap();
        }

        public static void EnsurePlayground()
        {
            if (!File.Exists(PlaygroundPath)) RebuildPlayground();
        }

        [MenuItem("Tools/Project/Rebuild Bootstrap Scene")]
        public static void RebuildBootstrap()
        {
            EnsureFolder(ScenesDir);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            new GameObject("Bootstrap").AddComponent<Bootstrap>();

            Save(scene, BootstrapPath);
        }

        [MenuItem("Tools/Project/Rebuild Playground Scene")]
        public static void RebuildPlayground()
        {
            EnsureFolder(ScenesDir);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var floorMat = GetOrCreateMaterial("Floor", new Color(0.62f, 0.64f, 0.60f));
            var playerMat = GetOrCreateMaterial("PlayerPlaceholder", new Color(0.95f, 0.65f, 0.15f));

            // Camera: temporary top-down angle; Cinemachine replaces it in M1.
            var cam = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cam.tag = "MainCamera";
            cam.transform.SetPositionAndRotation(new Vector3(0f, 12f, -12f), Quaternion.Euler(45f, 0f, 0f));

            var lightGo = new GameObject("Directional Light");
            var dirLight = lightGo.AddComponent<Light>();
            dirLight.type = LightType.Directional;
            dirLight.shadows = LightShadows.Soft;
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // Unity's Plane primitive is 10x10 units; scale 5 -> 50x50 floor.
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.localScale = new Vector3(5f, 1f, 5f);
            floor.GetComponent<MeshRenderer>().sharedMaterial = floorMat;

            // Capsule primitive is 2 units tall, origin at its center -> y = 1 puts feet on the floor.
            var player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = "Player";
            player.transform.position = new Vector3(0f, 1f, 0f);
            player.GetComponent<MeshRenderer>().sharedMaterial = playerMat;

            Save(scene, PlaygroundPath);
        }

        static void Save(UnityEngine.SceneManagement.Scene scene, string path)
        {
            if (!EditorSceneManager.SaveScene(scene, path))
                throw new IOException($"Could not save scene to {path}");
            Debug.Log($"[Scenes] Saved {path}");
        }

        // Flat-colour, slightly glossy URP/Lit material. Updated in place when it already exists
        // so references to the asset (and its GUID) survive re-runs.
        static Material GetOrCreateMaterial(string name, Color color)
        {
            EnsureFolder(MaterialsDir);
            var path = $"{MaterialsDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", 0.55f);
            mat.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        public static void EnsureFolder(string assetPath)
        {
            if (AssetDatabase.IsValidFolder(assetPath)) return;
            var parent = Path.GetDirectoryName(assetPath).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(assetPath));
        }
    }
}
