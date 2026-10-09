using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Enforces the project-wide settings the team relies on. Idempotent: running it twice is a no-op.
    /// Why code and not a manual checklist: settings live in ProjectSettings/*.asset and silently drift
    /// when someone opens the project with other defaults; this makes the expected state reproducible.
    /// </summary>
    public static class ProjectSettingsApplier
    {
        // PlayerSettings "Active Input Handling": 0 = old Input Manager, 1 = Input System, 2 = both.
        public const int InputSystemOnly = 1;
        public const string VisibleMetaFiles = "Visible Meta Files";

        // Standalone window: 16:9 like the 1920x1080 UI reference, resizable, fits a 13" laptop screen.
        public const int WindowWidth = 1440;
        public const int WindowHeight = 810;

        [MenuItem("Tools/Project/Apply Settings")]
        public static void Apply()
        {
            if (EditorSettings.serializationMode != SerializationMode.ForceText)
            {
                EditorSettings.serializationMode = SerializationMode.ForceText;
                Debug.Log("[Settings] Asset serialization -> Force Text");
            }

            if (VersionControlSettings.mode != VisibleMetaFiles)
            {
                VersionControlSettings.mode = VisibleMetaFiles;
                Debug.Log("[Settings] Version control mode -> Visible Meta Files");
            }

            ApplyWindow();

            if (GetActiveInputHandler() != InputSystemOnly)
            {
                var so = LoadPlayerSettings();
                so.FindProperty("activeInputHandler").intValue = InputSystemOnly;
                so.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssets();
                // The Unity backend only switches handlers after a domain restart / editor restart.
                Debug.LogWarning("[Settings] Active Input Handling -> Input System Package. Restart the editor for it to take effect.");
            }
        }

        static void ApplyWindow()
        {
            if (PlayerSettings.defaultScreenWidth == WindowWidth && PlayerSettings.defaultScreenHeight == WindowHeight
                && PlayerSettings.resizableWindow && PlayerSettings.fullScreenMode == FullScreenMode.Windowed)
                return;

            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = WindowWidth;
            PlayerSettings.defaultScreenHeight = WindowHeight;
            PlayerSettings.resizableWindow = true;
            AssetDatabase.SaveAssets();
            Debug.Log($"[Settings] Standalone window -> {WindowWidth}x{WindowHeight}, windowed, resizable");
        }

        public static int GetActiveInputHandler() =>
            LoadPlayerSettings().FindProperty("activeInputHandler").intValue;

        static SerializedObject LoadPlayerSettings()
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            return new SerializedObject(assets[0]);
        }
    }
}
