using System;

namespace Boki.Enemies
{
    // Shared repeat gate for contact hits and ranged shots; time is passed in by the adapter.
    public sealed class CooldownTimer
    {
        public float Interval { get; }
        public float Remaining { get; private set; }
        public bool IsReady => Remaining <= 0f;

        public CooldownTimer(float interval, float initialDelay = 0f)
        {
            if (interval <= 0f) throw new ArgumentOutOfRangeException(nameof(interval));
            Interval = interval;
            Remaining = Math.Max(0f, initialDelay);
        }

        public void Tick(float deltaTime)
        {
            if (deltaTime > 0f) Remaining = Math.Max(0f, Remaining - deltaTime);
        }

        // Starts the interval only when ready, so a blocked or ignored attempt costs nothing.
        public bool TryTrigger()
        {
            if (!IsReady) return false;
            Remaining = Interval;
            return true;
        }
    }
}
