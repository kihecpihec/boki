using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Boki.Editor
{
    public static class M5PlayerBuild
    {
        [MenuItem("Boki/Build M5 Linux Test Player")]
        public static void Linux() => Build(BuildTarget.StandaloneLinux64, "Boki-M5.x86_64");

        [MenuItem("Boki/Build M5 Windows Test Player")]
        public static void Windows() => Build(BuildTarget.StandaloneWindows64, "Boki-M5.exe");

        private static void Build(BuildTarget target, string filename)
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, target))
                throw new InvalidOperationException("Install the pinned Editor's build support for " + target + " first.");
            string directory = Environment.GetEnvironmentVariable("BOKI_M5_BUILD_DIRECTORY");
            if (string.IsNullOrEmpty(directory) && !Application.isBatchMode)
                directory = EditorUtility.OpenFolderPanel("M5 test player output", "", "");
            if (string.IsNullOrEmpty(directory))
            {
                if (Application.isBatchMode) throw new InvalidOperationException("Set BOKI_M5_BUILD_DIRECTORY for batch builds.");
                return;
            }
            Directory.CreateDirectory(directory);
            // Explicit scene list allows retry without modifying shared build profiles/settings.
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { Boki.Core.RunSession.PohorjeScene, Boki.Core.RunSession.MariborScene, Boki.Core.RunSession.FeriScene },
                locationPathName = Path.Combine(directory, filename),
                target = target,
                options = BuildOptions.Development
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("M5 player build failed: " + report.summary.result);
            Debug.Log("M5_PLAYER_BUILD_PASSED: " + target);
        }
    }
}
