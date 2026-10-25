using UnityEditor;

namespace Boki.Editor
{
    public static class PohorjeProductionChecks
    {
        [MenuItem("Boki/Pohorje Production/Validate Collision and Traversal")]
        public static void Run() => MapProductionChecks.Run(PohorjeProductionTools.Delivery);
    }
}
