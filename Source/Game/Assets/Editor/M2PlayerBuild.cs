using UnityEditor;

namespace Boki.Editor
{
    public static class M2PlayerBuild
    {
        [MenuItem("Boki/Build M2 Linux Test Player")]
        public static void Linux() => Build(BuildTarget.StandaloneLinux64, "Boki-M2.x86_64");

        [MenuItem("Boki/Build M2 Windows Test Player")]
        public static void Windows() => Build(BuildTarget.StandaloneWindows64, "Boki-M2.exe");

        private static void Build(BuildTarget target, string filename)
        {
            PlayerBuild.Build(target, filename, "BOKI_M2_BUILD_DIRECTORY", "M2",
                new[] { M2SceneBuilder.ScenePath }, BuildOptions.Development);
        }
    }
}
