using System;

namespace Boki.Intro
{
    // Cartoon timing curves for the prologue's gags. All are pure functions of the seconds since a
    // trigger, so any moment can be shown directly and a skip lands on exactly the same pose.
    public static class IntroMotion
    {
        private const float SpringDecay = 5f;
        private const float SpringCycles = 1.5f;

        // 0 until the trigger, then rushes to 1, overshoots, and settles on exactly 1 after duration.
        public static float Pop(float seconds, float duration)
        {
            if (!(duration > 0f) || float.IsInfinity(duration)) throw new ArgumentOutOfRangeException(nameof(duration));
            if (!(seconds > 0f)) return 0f;
            if (seconds >= duration) return 1f;
            float t = seconds / duration;
            float spring = 1f - (float)(Math.Exp(-SpringDecay * t) * Math.Cos(2.0 * Math.PI * SpringCycles * t));
            float land = Smooth(0.8f, 1f, t);
            return spring + (1f - spring) * land;
        }

        // 1 at the trigger, falling to 0; 0 before it. Used for camera punches and flashes.
        public static float Impact(float seconds, float decay)
        {
            if (!(decay > 0f)) throw new ArgumentOutOfRangeException(nameof(decay));
            return seconds < 0f ? 0f : (float)Math.Exp(-decay * seconds);
        }

        // Shake that starts at the trigger and dies away: |value| never exceeds Impact(seconds, decay).
        public static float Tremor(float seconds, float frequency, float decay)
        {
            if (!(frequency > 0f)) throw new ArgumentOutOfRangeException(nameof(frequency));
            if (!(seconds > 0f)) return 0f;
            return Impact(seconds, decay) * (float)Math.Sin(2.0 * Math.PI * frequency * seconds);
        }

        private static float Smooth(float from, float to, float value)
        {
            float t = Math.Min(1f, Math.Max(0f, (value - from) / (to - from)));
            return t * t * (3f - 2f * t);
        }
    }
}
