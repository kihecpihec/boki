using System;

namespace Boki.Intro
{
    // Ordered shot timer for the skippable Bellevue prologue (Story §3 PROLOGUE).
    // Playing to the end and skipping both finish through Finished, exactly once.
    public sealed class IntroTimeline
    {
        private readonly float[] shotSeconds;
        private float shotElapsed;

        public int ShotCount => shotSeconds.Length;
        public float TotalSeconds { get; }
        public int ShotIndex { get; private set; }
        public float ShotProgress => IsFinished ? 1f : shotElapsed / shotSeconds[ShotIndex];
        public float Elapsed => IsFinished ? TotalSeconds : ShotStart(ShotIndex) + shotElapsed;
        public bool HasStarted { get; private set; }
        public bool IsFinished { get; private set; }
        public bool WasSkipped { get; private set; }

        public event Action<int> ShotStarted;
        public event Action Finished;

        public IntroTimeline(float[] shotSeconds)
        {
            if (shotSeconds == null || shotSeconds.Length == 0)
                throw new ArgumentException("At least one shot is required.", nameof(shotSeconds));
            foreach (float seconds in shotSeconds)
                if (!(seconds > 0f) || float.IsInfinity(seconds))
                    throw new ArgumentOutOfRangeException(nameof(shotSeconds), "Each shot needs a finite positive duration.");
            this.shotSeconds = (float[])shotSeconds.Clone();
            foreach (float seconds in this.shotSeconds) TotalSeconds += seconds;
        }

        // Sequence time at which a shot begins, so cues inside a shot can be placed on one clock.
        public float ShotStart(int shot)
        {
            if (shot < 0 || shot >= shotSeconds.Length) throw new ArgumentOutOfRangeException(nameof(shot));
            float start = 0f;
            for (int i = 0; i < shot; i++) start += shotSeconds[i];
            return start;
        }

        public bool Begin()
        {
            if (HasStarted) return false;
            HasStarted = true;
            ShotStarted?.Invoke(0);
            return true;
        }

        public void Advance(float deltaSeconds)
        {
            if (!HasStarted || IsFinished || !(deltaSeconds > 0f)) return;
            float remaining = deltaSeconds;
            while (!IsFinished)
            {
                float left = shotSeconds[ShotIndex] - shotElapsed;
                if (remaining < left)
                {
                    shotElapsed += remaining;
                    return;
                }
                remaining -= left;
                if (ShotIndex == ShotCount - 1)
                {
                    Finish();
                    return;
                }
                ShotIndex++;
                shotElapsed = 0f;
                ShotStarted?.Invoke(ShotIndex);
            }
        }

        public bool TrySkip()
        {
            if (!HasStarted || IsFinished) return false;
            WasSkipped = true;
            Finish();
            return true;
        }

        private void Finish()
        {
            IsFinished = true;
            Finished?.Invoke();
        }
    }
}
