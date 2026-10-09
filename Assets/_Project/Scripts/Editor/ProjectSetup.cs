using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// One-shot entry points, also callable from batchmode:
    /// <c>Unity -batchmode -quit -executeMethod Game.Editor.ProjectSetup.RunAll</c> (idempotent: creates what is missing)
    /// and <c>...RebuildAll</c> (regenerates the scene from scratch).
    /// </summary>
    public static class ProjectSetup
    {
        [MenuItem("Tools/Project/Setup Build Settings")]
        public static void SetupBuildSettings()
        {
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(GameSceneBuilder.ScenePath, true) };
            Debug.Log($"[Setup] Build Settings scenes: {GameSceneBuilder.ScenePath}");
        }

        /// <summary>
        /// Phase 1. TMP Essentials are imported asynchronously (the editor must go through an update loop),
        /// so this has to be its own editor session before RunAll can create fonts. scripts/setup.sh does both.
        /// </summary>
        [MenuItem("Tools/Project/Import TMP Essentials")]
        public static void ImportTmp()
        {
            ArtBuilder.EnsureTmpEssentials();
            if (!Application.isBatchMode) return;

            // Run WITHOUT -quit: keep the editor alive until the queued package import has actually finished.
            var deadline = EditorApplication.timeSinceStartup + 180.0;
            void WaitForImport()
            {
                var done = ArtBuilder.HasTmpEssentials && !EditorApplication.isUpdating && !EditorApplication.isCompiling;
                if (!done && EditorApplication.timeSinceStartup < deadline) return;
                EditorApplication.update -= WaitForImport;
                if (!done) Debug.LogError("[Setup] Timed out waiting for the TMP Essentials import.");
                EditorApplication.Exit(done ? 0 : 1);
            }
            EditorApplication.update += WaitForImport;
        }

        [MenuItem("Tools/Project/Run All Setup")]
        public static void RunAll()
        {
            RequireTmp();
            ProjectSettingsApplier.Apply();
            GameSceneBuilder.Ensure();
            SetupBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        [MenuItem("Tools/Project/Rebuild Everything")]
        public static void RebuildAll()
        {
            RequireTmp();
            ProjectSettingsApplier.Apply();
            GameSceneBuilder.Rebuild();
            SetupBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        static void RequireTmp()
        {
            if (!ArtBuilder.HasTmpEssentials)
                throw new System.InvalidOperationException("TMP Essentials missing: run ProjectSetup.ImportTmp first (scripts/setup.sh does this).");
        }
    }
}
