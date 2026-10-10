using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Batchmode build entry points used by scripts/build.sh.
    /// Optional args: <c>-outputPath &lt;path&gt;</c>, <c>-mono</c> (macOS quick build), <c>-release</c> (WebGL: compressed and stripped). The process exit code reflects the build result.
    /// </summary>
    public static class BuildScript
    {
        [MenuItem("Tools/Build/macOS (IL2CPP, Universal)")]
        public static void BuildMac() => Run(BuildTarget.StandaloneOSX, "Builds/macOS/Construction.app");

        [MenuItem("Tools/Build/WebGL")]
        public static void BuildWeb() => Run(BuildTarget.WebGL, "Builds/WebGL");

        public static void BuildWindows() => Run(BuildTarget.StandaloneWindows64, "Builds/Windows/Construction.exe");

        static void Run(BuildTarget target, string defaultOutput)
        {
            var exitCode = 1;
            try
            {
                exitCode = Build(target, GetArg("-outputPath") ?? defaultOutput);
            }
            catch (Exception e)
            {
                Debug.LogError($"[Build] {e}");
            }
            // Only force the process to exit in batchmode; from the menu just report.
            if (Application.isBatchMode) EditorApplication.Exit(exitCode);
        }

        static int Build(BuildTarget target, string output)
        {
            var group = BuildPipeline.GetBuildTargetGroup(target);
            if (!BuildPipeline.IsBuildTargetSupported(group, target))
            {
                Debug.LogError($"[Build] Platform module for {target} is not installed in this editor.");
                return 1;
            }

            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0)
            {
                Debug.LogError("[Build] No enabled scenes in Build Settings. Run Tools/Project/Run All Setup.");
                return 1;
            }

            // Release settings are applied for this build only: they would otherwise stay in ProjectSettings and leak into every later dev build.
            var restore = target == BuildTarget.WebGL ? SnapshotWebGlSettings() : null;
            try
            {
                ConfigurePlatform(target);
                return BuildPlayer(target, group, scenes, output);
            }
            finally
            {
                restore?.Invoke();
            }
        }

        static int BuildPlayer(BuildTarget target, BuildTargetGroup group, string[] scenes, string output)
        {
            var dir = target == BuildTarget.WebGL ? output : Path.GetDirectoryName(output);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = output,
                target = target,
                targetGroup = group,
                options = BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            Debug.Log($"[Build] {target}: {summary.result}, {summary.totalErrors} errors, {summary.totalSize / (1024 * 1024)} MB, {summary.totalTime.TotalSeconds:F0}s -> {summary.outputPath}");
            return summary.result == BuildResult.Succeeded ? 0 : 1;
        }

        // Returns an action that puts the WebGL player settings back as they are now.
        static Action SnapshotWebGlSettings()
        {
            var compression = PlayerSettings.WebGL.compressionFormat;
            var fallback = PlayerSettings.WebGL.decompressionFallback;
            var stripping = PlayerSettings.GetManagedStrippingLevel(NamedBuildTarget.WebGL);
            var exceptions = PlayerSettings.WebGL.exceptionSupport;
            var codeGeneration = PlayerSettings.GetIl2CppCodeGeneration(NamedBuildTarget.WebGL);
            return () =>
            {
                PlayerSettings.WebGL.compressionFormat = compression;
                PlayerSettings.WebGL.decompressionFallback = fallback;
                PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.WebGL, stripping);
                PlayerSettings.WebGL.exceptionSupport = exceptions;
                PlayerSettings.SetIl2CppCodeGeneration(NamedBuildTarget.WebGL, codeGeneration);
                AssetDatabase.SaveAssets();
            };
        }

        static void ConfigurePlatform(BuildTarget target)
        {
            switch (target)
            {
                case BuildTarget.StandaloneOSX:
                    // macOS: IL2CPP, Universal (Intel + Apple Silicon). "-mono" is a ~5x faster build for quick
                    // visual checks only; release builds must not pass it.
                    var fast = Array.IndexOf(Environment.GetCommandLineArgs(), "-mono") >= 0;
                    PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, fast ? ScriptingImplementation.Mono2x : ScriptingImplementation.IL2CPP);
                    UnityEditor.OSXStandalone.UserBuildSettings.architecture = OSArchitecture.x64ARM64;
                    break;
                case BuildTarget.StandaloneWindows64:
                    // Windows: Mono, because IL2CPP can't cross-compile for Windows from a Mac host.
                    PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
                    break;
                case BuildTarget.WebGL:
                    if (Array.IndexOf(Environment.GetCommandLineArgs(), "-release") >= 0)
                    {
                        // Release: Brotli (with the JS fallback for hosts that do not send Content-Encoding), high code stripping, no
                        // exception stack data and size-optimised IL2CPP code. Several times smaller to download.
                        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
                        PlayerSettings.WebGL.decompressionFallback = true;
                        PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.WebGL, ManagedStrippingLevel.High);
                        PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
                        PlayerSettings.SetIl2CppCodeGeneration(NamedBuildTarget.WebGL, Il2CppCodeGeneration.OptimizeSize);
                    }
                    else
                    {
                        // Dev: uncompressed, so it works on any static host / `python -m http.server` without Content-Encoding headers.
                        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
                    }
                    break;
            }
        }

        static string GetArg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            var i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }
    }
}
