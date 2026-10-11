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
            ApplyShadows();
            ApplyMobile();
            ApplySplash();

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

        // URP measures the shadow distance from the camera (not from the player), and the follow camera sits ~48 units out with the far
        // half of the screen reaching ~100 units, so the default 50 cut the shadows off before the middle of the screen. The pipeline
        // assets, not QualitySettings, are what URP reads.
        public const float ShadowDistance = 150f;
        static readonly string[] PipelineAssets = { "Assets/Settings/PC_RPAsset.asset", "Assets/Settings/Mobile_RPAsset.asset" };

        static void ApplyShadows()
        {
            foreach (var path in PipelineAssets)
            {
                var asset = AssetDatabase.LoadMainAssetAtPath(path);
                if (asset == null) continue;
                var so = new SerializedObject(asset);
                var mobile = path.Contains("Mobile");
                var changed = SetFloat(so, "m_ShadowDistance", ShadowDistance);
                // The toon look has no bloom or exposure effects, so HDR only costs fill rate and memory (a half-float colour buffer).
                changed |= SetInt(so, "m_SupportsHDR", 0);
                // No shader reads the camera's depth or opaque texture, so URP would only spend a copy pass on them (the PC asset had both on).
                changed |= SetInt(so, "m_RequireDepthTexture", 0);
                changed |= SetInt(so, "m_RequireOpaqueTexture", 0);
                // Spread over a longer distance the maps must stay sharp: PC keeps its 4 cascades, mobile gets one at a higher resolution
                // (measured against 2 cascades: same picture, a seventh fewer draws and one shadow pass less).
                if (mobile)
                {
                    changed |= SetInt(so, "m_ShadowCascadeCount", 1);
                    changed |= SetInt(so, "m_MainLightShadowmapResolution", 2048);
                }
                if (!changed) continue;
                so.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssets();
                Debug.Log($"[Settings] {path}: shadow distance {ShadowDistance}");
            }
        }

        // The game is landscape only; on a phone the status bar and the home indicator stay out of the way (the first swipe from the bottom edge
        // only reveals the indicator, so a thumb sliding over the joystick does not leave the app).
        public const string IosBundleId = "com.gweep.construction";
        public const string AndroidPackageId = "com.gweep.construction";

        static void ApplyMobile()
        {
            var changed = false;
            if (PlayerSettings.defaultInterfaceOrientation != UIOrientation.AutoRotation) { PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation; changed = true; }
            if (PlayerSettings.allowedAutorotateToPortrait || PlayerSettings.allowedAutorotateToPortraitUpsideDown
                || !PlayerSettings.allowedAutorotateToLandscapeLeft || !PlayerSettings.allowedAutorotateToLandscapeRight)
            {
                PlayerSettings.allowedAutorotateToPortrait = false;
                PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
                PlayerSettings.allowedAutorotateToLandscapeLeft = true;
                PlayerSettings.allowedAutorotateToLandscapeRight = true;
                changed = true;
            }
            if (!PlayerSettings.statusBarHidden) { PlayerSettings.statusBarHidden = true; changed = true; }
            if (!PlayerSettings.iOS.hideHomeButton) { PlayerSettings.iOS.hideHomeButton = true; changed = true; }
            if (PlayerSettings.iOS.deferSystemGesturesMode != UnityEngine.iOS.SystemGestureDeferMode.BottomEdge) { PlayerSettings.iOS.deferSystemGesturesMode = UnityEngine.iOS.SystemGestureDeferMode.BottomEdge; changed = true; }
            if (PlayerSettings.GetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.iOS) != IosBundleId)
            {
                PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.iOS, IosBundleId);
                changed = true;
            }
            if (PlayerSettings.GetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android) != AndroidPackageId)
            {
                PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android, AndroidPackageId);
                changed = true;
            }
            if (!changed) return;
            AssetDatabase.SaveAssets();
            Debug.Log("[Settings] Mobile: landscape only, status bar and home indicator hidden, iOS bundle id " + IosBundleId);
        }

        // "Made with Unity" at startup: allowed to switch off on the Education licence (Unity 6 also allows it on Personal).
        static void ApplySplash()
        {
            if (PlayerSettings.SplashScreen.show)
            {
                PlayerSettings.SplashScreen.show = false;
                AssetDatabase.SaveAssets();
                Debug.Log("[Settings] Unity splash screen -> off");
            }
            // Our own page template (Assets/WebGLTemplates/Construction): the default one carries the Unity logo twice, in the loading screen and the footer.
            if (PlayerSettings.WebGL.template != WebGlTemplate)
            {
                PlayerSettings.WebGL.template = WebGlTemplate;
                AssetDatabase.SaveAssets();
                Debug.Log("[Settings] WebGL template -> " + WebGlTemplate);
            }
        }

        public const string WebGlTemplate = "PROJECT:Construction";

        static bool SetFloat(SerializedObject so, string name, float value)
        {
            var property = so.FindProperty(name);
            if (property == null || Mathf.Approximately(property.floatValue, value)) return false;
            property.floatValue = value;
            return true;
        }

        static bool SetInt(SerializedObject so, string name, int value)
        {
            var property = so.FindProperty(name);
            if (property == null || property.intValue == value) return false;
            property.intValue = value;
            return true;
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
