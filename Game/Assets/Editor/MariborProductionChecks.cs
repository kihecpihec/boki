using UnityEditor;

namespace Boki.Editor
{
    public static class MariborProductionChecks
    {
        [MenuItem("Boki/Maribor Production/Validate Collision and Traversal")]
        public static void Run() => MapProductionChecks.Run(MariborProductionTools.Delivery);
    }
}
