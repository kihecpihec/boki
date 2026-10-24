using System;

namespace Boki.Progression
{
    public sealed class GuiltMovementAdvantage
    {
        private float remaining, cooldown;
        public float Bonus => remaining > 0f ? 0.25f : 0f;
        public void Tick(float seconds, bool nearby)
        {
            if (seconds <= 0f || float.IsNaN(seconds) || float.IsInfinity(seconds)) return;
            remaining = Math.Max(0f, remaining - seconds);
            cooldown = Math.Max(0f, cooldown - seconds);
            if (nearby && cooldown <= 0f) { remaining = 2f; cooldown = 5f; }
        }
    }
}
