using System;

namespace Boki.Player
{
    // Gameplay §5 and Story §5: temporary riding bonus, separate from Panic Mode boosts.
    public sealed class GuiltMovementAdvantage
    {
        public float Remaining { get; private set; }
        public float Cooldown { get; private set; }
        public bool IsActive => Remaining > 0f;
        public void Tick(bool nearbyChase, float seconds)
        {
            if (seconds <= 0f || float.IsNaN(seconds) || float.IsInfinity(seconds)) return;
            Remaining = Math.Max(0f, Remaining - seconds);
            Cooldown = Math.Max(0f, Cooldown - seconds);
            if (!nearbyChase || Cooldown > 0f) return;
            Remaining = 2f;
            Cooldown = 5f;
        }
    }
}
