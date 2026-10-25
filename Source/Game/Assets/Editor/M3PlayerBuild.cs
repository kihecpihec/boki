using UnityEditor;

namespace Boki.Editor
{
    public static class M3PlayerBuild
    {
        [MenuItem("Boki/Build M3 Linux Test Player")]
        public static void Linux() => Build(BuildTarget.StandaloneLinux64, "Boki-M3.x86_64");

        [MenuItem("Boki/Build M3 Windows Test Player")]
        public static void Windows() => Build(BuildTarget.StandaloneWindows64, "Boki-M3.exe");

        private static void Build(BuildTarget target, string filename)
        {
            PlayerBuild.Build(target, filename, "BOKI_M3_BUILD_DIRECTORY", "M3",
                new[] { Boki.Core.RunSession.ForestScene, Boki.Core.RunSession.CityScene }, BuildOptions.Development);
        }
    }
}
