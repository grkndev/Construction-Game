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
    /// Optional arg: <c>-outputPath &lt;path&gt;</c>. The process exit code reflects the build result.
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

            ConfigurePlatform(target);

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

        static void ConfigurePlatform(BuildTarget target)
        {
            switch (target)
            {
                case BuildTarget.StandaloneOSX:
                    // macOS: IL2CPP, Universal (Intel + Apple Silicon).
                    PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.IL2CPP);
                    UnityEditor.OSXStandalone.UserBuildSettings.architecture = OSArchitecture.x64ARM64;
                    break;
                case BuildTarget.StandaloneWindows64:
                    // Windows: Mono, because IL2CPP can't cross-compile for Windows from a Mac host.
                    PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
                    break;
                case BuildTarget.WebGL:
                    // Uncompressed: works on any static host / `python -m http.server` without
                    // Content-Encoding headers. Revisit for the final release build (M6).
                    PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
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
