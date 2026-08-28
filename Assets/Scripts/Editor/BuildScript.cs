#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace SanMonica.EditorTools
{
    /// <summary>
    /// Command line and menu driven Android builds. Produces either an APK for
    /// side-loading or an AAB for Google Play, with keystore support.
    ///
    /// Usage:
    ///   Unity -batchmode -quit -projectPath . -executeMethod SanMonica.EditorTools.BuildScript.BuildApk
    ///   Unity -batchmode -quit -projectPath . -executeMethod SanMonica.EditorTools.BuildScript.BuildAab
    /// Optional arguments: -outputPath &lt;file&gt; -development -keystore &lt;path&gt;
    ///   -keystorePass &lt;pw&gt; -keyalias &lt;alias&gt; -keyaliasPass &lt;pw&gt; -versionCode &lt;n&gt;
    /// </summary>
    public static class BuildScript
    {
        [MenuItem("San Monica/Build/Android APK", priority = 20)]
        public static void BuildApkMenu() => Build(false, null, false);

        [MenuItem("San Monica/Build/Android App Bundle (AAB)", priority = 21)]
        public static void BuildAabMenu() => Build(true, null, false);

        [MenuItem("San Monica/Build/Android APK (Development)", priority = 22)]
        public static void BuildApkDevMenu() => Build(false, null, true);

        public static void BuildApk() => BuildFromCommandLine(false);
        public static void BuildAab() => BuildFromCommandLine(true);

        private static void BuildFromCommandLine(bool appBundle)
        {
            string output = GetArgument("-outputPath");
            bool development = HasFlag("-development");
            ApplyKeystoreFromArguments();
            string versionCode = GetArgument("-versionCode");
            if (!string.IsNullOrEmpty(versionCode) && int.TryParse(versionCode, out int code))
                PlayerSettings.Android.bundleVersionCode = code;
            Build(appBundle, output, development);
        }

        public static void Build(bool appBundle, string outputPath, bool development)
        {
            ProjectAutoSetup.RunSetup(false);

            var scenes = GetScenes();
            if (scenes.Length == 0)
            {
                Debug.LogError("[San Monica] No scenes in the build settings. Run San Monica/Setup Project first.");
                EditorApplication.Exit(1);
                return;
            }

            if (string.IsNullOrEmpty(outputPath))
            {
                string directory = Path.Combine(Directory.GetCurrentDirectory(), "Builds");
                Directory.CreateDirectory(directory);
                outputPath = Path.Combine(directory, "SanMonica" + (appBundle ? ".aab" : ".apk"));
            }
            else
            {
                string directory = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            }

            EditorUserBuildSettings.buildAppBundle = appBundle;
            EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.ASTC;
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = development
                    ? BuildOptions.Development | BuildOptions.AllowDebugging
                    : BuildOptions.None
            };

            Debug.Log("[San Monica] Building " + (appBundle ? "AAB" : "APK") + " to " + outputPath);
            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[San Monica] Build succeeded: {summary.outputPath} ({summary.totalSize / (1024 * 1024)} MB, {summary.totalTime})");
                if (IsBatchMode()) EditorApplication.Exit(0);
            }
            else
            {
                Debug.LogError($"[San Monica] Build failed: {summary.result} with {summary.totalErrors} errors");
                if (IsBatchMode()) EditorApplication.Exit(1);
            }
        }

        private static string[] GetScenes()
        {
            var list = new List<string>();
            foreach (var scene in EditorBuildSettings.scenes)
                if (scene.enabled && !string.IsNullOrEmpty(scene.path)) list.Add(scene.path);
            return list.ToArray();
        }

        private static void ApplyKeystoreFromArguments()
        {
            string keystore = GetArgument("-keystore");
            if (string.IsNullOrEmpty(keystore)) return;

            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystoreName = keystore;
            PlayerSettings.Android.keystorePass = GetArgument("-keystorePass");
            PlayerSettings.Android.keyaliasName = GetArgument("-keyalias");
            PlayerSettings.Android.keyaliasPass = GetArgument("-keyaliasPass");
            Debug.Log("[San Monica] Signing with keystore " + keystore);
        }

        private static string GetArgument(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
            return null;
        }

        private static bool HasFlag(string name)
        {
            var args = Environment.GetCommandLineArgs();
            foreach (var arg in args)
                if (string.Equals(arg, name, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static bool IsBatchMode()
        {
            return Application.isBatchMode;
        }
    }
}
#endif
