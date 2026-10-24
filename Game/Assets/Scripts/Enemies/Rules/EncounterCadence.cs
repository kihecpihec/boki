using System;

namespace Boki.Enemies
{
    // Gameplay §5: use the run-owned, per-level combat clock shared with Guilt.
    public static class EncounterCadence
    {
        public static float Evaluate(float initialSeconds, float combatSeconds)
        {
            if (initialSeconds <= 0f || float.IsNaN(initialSeconds) || float.IsInfinity(initialSeconds))
                throw new ArgumentOutOfRangeException(nameof(initialSeconds));
            if (float.IsNaN(combatSeconds)) throw new ArgumentOutOfRangeException(nameof(combatSeconds));
            float progress = Math.Max(0f, Math.Min(1f, combatSeconds / 120f));
            return initialSeconds * (1f - 0.5f * progress);
        }
    }
}
