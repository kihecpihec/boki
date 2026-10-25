using System;

namespace Boki.Ending
{
    // Grooming strokes for the ending's cat gag: the professor licks his forearm again and again. All are pure
    // functions of the seconds since the licking began, so any moment can be shown directly (skip, frame capture).
    public static class EndingMotion
    {
        // Fraction of the current stroke, 0 up to just under 1. Zero until the licking begins.
        public static float StrokePhase(float seconds, float period)
        {
            RequirePeriod(period);
            if (!(seconds > 0f)) return 0f;
            double cycles = seconds / period;
            return (float)(cycles - Math.Floor(cycles));
        }

        // How far the tongue is out: 0 between strokes, 1 through the middle of each. Zero until the licking begins.
        public static float Reach(float seconds, float period)
        {
            RequirePeriod(period);
            if (!(seconds > 0f)) return 0f;
            float phase = StrokePhase(seconds, period);
            return Smooth(0f, 0.25f, phase) * (1f - Smooth(0.75f, 1f, phase));
        }

        // Where on the forearm the tongue is: 0 at the wrist, 1 at the elbow, sweeping once per stroke.
        public static float Along(float seconds, float period)
        {
            RequirePeriod(period);
            if (!(seconds > 0f)) return 0f;
            return Smooth(0.15f, 0.85f, StrokePhase(seconds, period));
        }

        private static void RequirePeriod(float period)
        {
            if (!(period > 0f) || float.IsInfinity(period)) throw new ArgumentOutOfRangeException(nameof(period));
        }

        private static float Smooth(float from, float to, float value)
        {
            float t = Math.Min(1f, Math.Max(0f, (value - from) / (to - from)));
            return t * t * (3f - 2f * t);
        }
    }
}
