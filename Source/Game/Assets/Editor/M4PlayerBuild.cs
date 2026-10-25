using UnityEditor;

namespace Boki.Editor
{
    public static class M4PlayerBuild
    {
        [MenuItem("Boki/Build M4 Linux Test Player")]
        public static void Linux() => Build(BuildTarget.StandaloneLinux64, "Boki-M4.x86_64");

        [MenuItem("Boki/Build M4 Windows Test Player")]
        public static void Windows() => Build(BuildTarget.StandaloneWindows64, "Boki-M4.exe");

        private static void Build(BuildTarget target, string filename)
        {
            PlayerBuild.Build(target, filename, "BOKI_M4_BUILD_DIRECTORY", "M4",
                new[] { Boki.Core.RunSession.PohorjeScene, Boki.Core.RunSession.MariborScene, Boki.Core.RunSession.FeriScene }, BuildOptions.Development);
        }
    }
}
