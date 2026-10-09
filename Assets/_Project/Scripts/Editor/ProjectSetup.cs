using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// One-shot entry point: <c>Unity -batchmode -quit -executeMethod Game.Editor.ProjectSetup.RunAll</c>.
    /// </summary>
    public static class ProjectSetup
    {
        static readonly string[] BuildScenes = { SceneBuilders.BootstrapPath, SceneBuilders.PlaygroundPath };

        [MenuItem("Tools/Project/Setup Build Settings")]
        public static void SetupBuildSettings()
        {
            // Order matters: index 0 is the scene a standalone build starts with.
            var scenes = new EditorBuildSettingsScene[BuildScenes.Length];
            for (var i = 0; i < BuildScenes.Length; i++)
                scenes[i] = new EditorBuildSettingsScene(BuildScenes[i], true);
            EditorBuildSettings.scenes = scenes;
            Debug.Log("[Setup] Build Settings scenes: " + string.Join(", ", BuildScenes));
        }

        [MenuItem("Tools/Project/Run All Setup")]
        public static void RunAll()
        {
            ProjectSettingsApplier.Apply();
            SceneBuilders.EnsureBootstrap();
            SceneBuilders.EnsurePlayground();
            SetupBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }
}
