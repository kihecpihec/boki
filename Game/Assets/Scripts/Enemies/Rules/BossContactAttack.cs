using System;

namespace Boki.Enemies
{
    public sealed class BossContactAttack
    {
        private readonly float interval;
        private float remaining;

        public BossContactAttack(float interval)
        {
            if (interval <= 0f || float.IsNaN(interval) || float.IsInfinity(interval))
                throw new ArgumentOutOfRangeException(nameof(interval));
            this.interval = interval;
        }

        public bool Tick(float activeSeconds, bool touching)
        {
            if (activeSeconds <= 0f) return false;
            remaining = Math.Max(0f, remaining - activeSeconds);
            if (!touching || remaining > 0f) return false;
            remaining = interval;
            return true;
        }
    }
}
