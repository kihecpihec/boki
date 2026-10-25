namespace Boki.Routes
{
    // Distance is the only input on purpose: portal effects must look the same at every speed and route,
    // so they never hint at the Gameplay §1.1 shortcut.
    public static class PortalProximity
    {
        public static float Intensity(float distance, float nearDistance, float farDistance)
        {
            if (float.IsNaN(distance)) return 0f;
            if (distance <= nearDistance) return 1f;
            if (distance >= farDistance) return 0f;
            float t = (farDistance - distance) / (farDistance - nearDistance);
            return t * t * (3f - 2f * t);
        }
    }
}
