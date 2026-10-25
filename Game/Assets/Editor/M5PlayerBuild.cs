using UnityEditor;

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
            PlayerBuild.Build(target, filename, "BOKI_M5_BUILD_DIRECTORY", "M5",
                new[] { Boki.Core.RunSession.PohorjeScene, Boki.Core.RunSession.MariborScene, Boki.Core.RunSession.FeriScene }, BuildOptions.Development);
        }
    }
}
