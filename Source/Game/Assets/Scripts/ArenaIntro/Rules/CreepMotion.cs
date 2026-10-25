using System;

namespace Boki.ArenaIntro
{
    // The creeping enemy's stop-and-go approach (Story §3 LEVEL 1): short lurches toward Boki with stillness between them,
    // ending still. Pure functions of the seconds since the creep began, so any moment can be shown directly (skip, capture).
    public sealed class CreepMotion
    {
        public const int FootfallsPerLurch = 2;
        private const float FirstStepAt = 0.25f;
        private const float SecondStepAt = 0.75f;

        private readonly float duration;
        private readonly float cycle;
        private readonly float lurchSeconds;

        // moveFraction: the share of each of the equal cycles spent moving; the rest is stillness.
        public CreepMotion(float duration, int lurches, float moveFraction)
        {
            if (!(duration > 0f) || float.IsInfinity(duration)) throw new ArgumentOutOfRangeException(nameof(duration));
            if (lurches < 1) throw new ArgumentOutOfRangeException(nameof(lurches));
            if (!(moveFraction > 0f && moveFraction < 1f)) throw new ArgumentOutOfRangeException(nameof(moveFraction));
            this.duration = duration;
            Lurches = lurches;
            cycle = duration / lurches;
            lurchSeconds = cycle * moveFraction;
        }

        public int Lurches { get; }
        public float Duration => duration;

        // Share of the whole approach covered, 0 to 1. Each lurch snaps forward and settles, so a lurch always
        // ends on a whole number of lurches covered.
        public float Progress(float seconds)
        {
            if (!(seconds > 0f)) return 0f;
            if (seconds >= duration) return 1f;
            int index = LurchIndex(seconds);
            float inside = Math.Min(1f, (seconds - index * cycle) / lurchSeconds);
            float eased = 1f - (1f - inside) * (1f - inside);
            return (index + eased) / Lurches;
        }

        public bool IsMoving(float seconds)
        {
            if (!(seconds > 0f) || seconds >= duration) return false;
            return seconds - LurchIndex(seconds) * cycle < lurchSeconds;
        }

        // Footsteps heard by this time: two per lurch, a quarter and three quarters of the way through it.
        public int Footfalls(float seconds)
        {
            int count = 0;
            for (int i = 0; i < Lurches; i++)
            {
                float start = i * cycle;
                if (seconds >= start + FirstStepAt * lurchSeconds) count++;
                if (seconds >= start + SecondStepAt * lurchSeconds) count++;
            }
            return count;
        }

        private int LurchIndex(float seconds) => Math.Min(Lurches - 1, (int)(seconds / cycle));
    }
}
