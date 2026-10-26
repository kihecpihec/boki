using UnityEngine;

namespace Boki.Core
{
    internal static class NativeDisplayResolution
    {
#if UNITY_STANDALONE && !UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Apply()
        {
            var display = Screen.mainWindowDisplayInfo;
            if (display.width <= 0 || display.height <= 0) return;

            // Replace any lower resolution retained from a previous player launch.
            Screen.SetResolution(display.width, display.height, FullScreenMode.FullScreenWindow);
        }
#endif
    }
}
