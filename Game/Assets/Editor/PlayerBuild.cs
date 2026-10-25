using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Boki.Editor
{
    public static class PlayerBuild
    {
        [MenuItem("Boki/Build V2 Windows ARM64 Player")]
        public static void V2WindowsArm64() => BuildV2Windows(OSArchitecture.ARM64);

        [MenuItem("Boki/Build V2 Windows x86-64 Player")]
        public static void V2WindowsX64() => BuildV2Windows(OSArchitecture.x64);

        private static void BuildV2Windows(OSArchitecture architecture)
        {
            ValidateEditor();
            var previousArchitecture = UnityEditor.WindowsStandalone.UserBuildSettings.architecture;
            string previousVersion = PlayerSettings.bundleVersion;
            try
            {
                UnityEditor.WindowsStandalone.UserBuildSettings.architecture = architecture;
                PlayerSettings.bundleVersion = "2.0.0";
                Build(BuildTarget.StandaloneWindows64, "Boki-V2.exe", "BOKI_V2_BUILD_DIRECTORY", "V2",
                    new[] { "Assets/Scenes/MainMenu.unity", "Assets/Scenes/V1_Pohorje.unity",
                        "Assets/Scenes/V1_Maribor.unity", "Assets/Scenes/V1_FERI.unity" }, BuildOptions.None);
            }
            finally
            {
                UnityEditor.WindowsStandalone.UserBuildSettings.architecture = previousArchitecture;
                PlayerSettings.bundleVersion = previousVersion;
            }
        }

        internal static void Build(BuildTarget target, string filename, string environmentVariable,
            string label, string[] scenes, BuildOptions options)
        {
            ValidateEditor();
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, target))
                throw new InvalidOperationException("Install the pinned Editor's build support for " + target + " first.");
            string directory = Environment.GetEnvironmentVariable(environmentVariable);
            if (string.IsNullOrEmpty(directory) && !Application.isBatchMode)
                directory = EditorUtility.OpenFolderPanel(label + " player output", "", "");
            if (string.IsNullOrEmpty(directory))
            {
                if (Application.isBatchMode) throw new InvalidOperationException("Set " + environmentVariable + " for batch builds.");
                return;
            }
            Directory.CreateDirectory(directory);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = Path.Combine(directory, filename),
                target = target,
                options = options
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException(label + " player build failed: " + report.summary.result);
            Debug.Log(label + "_PLAYER_BUILD_PASSED: " + target);
        }
        private static void ValidateEditor()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before building a player.");
            for (int i = 0; i < EditorSceneManager.sceneCount; i++)
                if (EditorSceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save or discard dirty scenes before building a player.");
        }
    }
}
