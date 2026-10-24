using UnityEngine;

namespace Boki.Intro
{
    // What the intro's Boki does and when (Story §3 PROLOGUE). A pure function of (shot, seconds into the shot),
    // so playing, skipping and the Editor frame capture all show the same pose. Positions are stage-local:
    // origin on the ground at the wake-up spot, +Z toward the city, Boki lying with his head toward -Z.
    public static class IntroActing
    {
        public const int Wake = 0;
        public const int Stir = 1;
        public const int Watch = 2;
        public const int DoubleTake = 3;
        public const int Message = 4;
        public const int Line = 5;
        public const int Scramble = 6;
        public const int Mount = 7;

        public static readonly float[] ShotSeconds = { 4.5f, 1.8f, 1.6f, 1.7f, 2.4f, 3.1f, 2.4f, 3f };

        // Seconds into their shot. The pop sounds and the pop curves share these.
        public const float FirstPopAt = 0.45f;
        public const float SecondPopAt = 0.55f;
        public const float DashAt = 0.72f;

        // Where Boki lies, and where he stands to mount: the player's start, where the fallen scooter ends up upright.
        public static readonly Vector3 LyingSpot = new Vector3(0f, 0.14f, 0.86f);
        public static readonly Vector3 ScooterSpot = new Vector3(-1.15f, 0.05f, 0.3f);
        private static readonly Vector3 FeetSpot = new Vector3(0f, 0f, 0.86f);
        private static readonly Vector3 TurnSpot = new Vector3(0.15f, 0f, 2.55f);

        private const float SitSeconds = 0.32f;
        private const float SpringSeconds = 0.4f;
        private const float RunOneEnd = 1.17f;
        private const float SkidEnd = 1.37f;
        private const float RunTwoEnd = 1.95f;
        private const float StrideHertz = 3.4f;
        private const float Upright = 92f;

        private static readonly Vector3 RelaxUpperL = new Vector3(8f, 0f, 16f);
        private static readonly Vector3 RelaxUpperR = new Vector3(8f, 0f, -16f);
        private static readonly Vector3 RelaxLower = new Vector3(-14f, 0f, 0f);
        private static readonly Vector3 WatchUpper = new Vector3(-75f, 0f, 28f);
        private static readonly Vector3 WatchLower = new Vector3(-100f, 0f, 0f);
        private static readonly Vector3 PanicUpperL = new Vector3(-145f, 0f, -25f);
        private static readonly Vector3 PanicUpperR = new Vector3(-145f, 0f, 25f);
        private static readonly Vector3 PanicLower = new Vector3(-30f, 0f, 0f);

        private static readonly float[] ShotStarts = BuildStarts();

        public struct Beat
        {
            public IntroBoki.Pose Pose;
            public Vector3 Position;       // Boki's feet
            public float Yaw;              // degrees
            public float Lie;              // 1 lying face up .. 0 upright
            public float ScooterUpright;   // 0 fallen .. 1 standing, may overshoot
            public float BottleKick;       // seconds since the bottle was hit; negative = untouched
            public float Punch;            // camera punch-in, 0..1
            public float Flash;            // white flash alpha, 0..1
        }

        public static float StartOf(int shot) => ShotStarts[Mathf.Clamp(shot, 0, ShotStarts.Length - 1)];

        public static Beat Sample(int shot, float seconds)
        {
            shot = Mathf.Clamp(shot, 0, ShotSeconds.Length - 1);
            seconds = Mathf.Clamp(seconds, 0f, ShotSeconds[shot]);
            float clock = ShotStarts[shot] + seconds;
            float p = seconds / ShotSeconds[shot];
            var beat = new Beat
            {
                Position = LyingSpot,
                Lie = 1f,
                BottleKick = -1f,
                Pose = new IntroBoki.Pose
                {
                    UpperArmL = RelaxUpperL, LowerArmL = RelaxLower, UpperArmR = RelaxUpperR, LowerArmR = RelaxLower,
                    Lids = 0f, Brows = -0.4f, Mouth = 0.08f, SweatTime = -1f, Clock = clock,
                },
            };
            float breath = Mathf.Sin(clock * 2.2f);

            switch (shot)
            {
                case Wake:
                    beat.Pose.Torso = new Vector3(0.9f * breath, 0f, 0f);
                    beat.Pose.Head = new Vector3(0f, 0f, -5f);
                    beat.Pose.Mouth = 0.1f + 0.07f * Mathf.Max(0f, breath);
                    beat.Pose.Lids = 0.22f * Smooth(0.78f, 1f, p);
                    beat.Pose.Brows = Mathf.Lerp(-0.5f, -0.2f, Smooth(0.8f, 1f, p));
                    break;

                case Stir:
                    RaiseWatchArm(ref beat.Pose, Smooth(0f, 1f, p), Smooth(0.15f, 1f, p));
                    beat.Pose.Torso = new Vector3(0.9f * breath, 0f, 0f);
                    beat.Pose.Head = new Vector3(0f, 0f, Mathf.Lerp(-5f, 0f, Smooth(0f, 1f, p)));
                    beat.Pose.Lids = Mathf.Lerp(0.22f, 0.4f, p);
                    beat.Pose.Brows = Mathf.Lerp(-0.2f, -0.3f, p);
                    beat.Pose.Mouth = 0.06f;
                    beat.Pose.Gaze = new Vector2(-0.6f, -0.3f);
                    break;

                case Watch:
                    RaiseWatchArm(ref beat.Pose, 1f, 1f);
                    beat.Pose.UpperArmL.x += 1.5f * Mathf.Sin(clock * 9f);
                    beat.Pose.Lids = Mathf.Lerp(0.4f, 0.5f, p);
                    beat.Pose.Brows = Mathf.Lerp(-0.3f, 0f, p);
                    beat.Pose.Mouth = 0.04f;
                    beat.Pose.Gaze = new Vector2(-0.6f, -0.3f);
                    break;

                case DoubleTake:
                    SampleDoubleTake(ref beat, seconds);
                    break;

                case Message:
                    SampleMessage(ref beat, seconds);
                    break;

                case Line:
                    SampleLine(ref beat, p);
                    break;

                case Scramble:
                    SampleScramble(ref beat, seconds);
                    break;
            }
            return beat;
        }

        // The time on the watch lands: a blank stare, then the first eye pop.
        private static void SampleDoubleTake(ref Beat beat, float s)
        {
            ref IntroBoki.Pose pose = ref beat.Pose;
            float hit = s - FirstPopAt;
            float pop = IntroMotion.Pop(hit, 0.45f);
            float arm = 1f - Smooth(FirstPopAt, FirstPopAt + 0.35f, s);
            RaiseWatchArm(ref pose, arm, arm);
            pose.Pop = 0.62f * pop * (1f - 0.15f * Smooth(1.1f, 1.7f, s));
            pose.Lids = Mathf.Lerp(0.5f, 1f, pop);
            pose.Brows = Mathf.Lerp(0f, 0.8f, pop);
            pose.BrowTilt = Mathf.Lerp(0f, 10f, pop);
            pose.Mouth = Mathf.Lerp(0.04f, 0.45f, pop);
            pose.Shake = 0.8f * IntroMotion.Impact(hit, 2.5f);
            pose.Head = new Vector3(-9f * IntroMotion.Impact(hit, 6f), 0f, 3f * IntroMotion.Tremor(hit, 13f, 3f));
            pose.Gaze = Vector2.Lerp(new Vector2(-0.6f, -0.3f), Vector2.zero, pop);
            pose.Alert = 0.9f * IntroMotion.Pop(hit - 0.08f, 0.3f);
            pose.SweatTime = hit - 0.05f;
            beat.Punch = 0.8f * IntroMotion.Impact(hit, 7f);
            beat.Flash = 0.5f * IntroMotion.Impact(hit, 20f);
        }

        // The phone beeps, the lecture sinks in, and the eyes really leave: the bigger second pop.
        private static void SampleMessage(ref Beat beat, float s)
        {
            ref IntroBoki.Pose pose = ref beat.Pose;
            float hit = s - SecondPopAt;
            float pop = IntroMotion.Pop(hit, 0.55f);
            pose.Pop = Mathf.Lerp(0.52f, 1f, pop);
            pose.Lids = 1f;
            pose.Brows = Mathf.Lerp(0.8f, 1f, pop);
            pose.BrowTilt = Mathf.Lerp(10f, 16f, pop);
            pose.Mouth = Mathf.Lerp(0.45f, 1f, pop);
            pose.Shake = 0.25f + 0.9f * IntroMotion.Impact(hit, 2.2f);
            pose.Head = new Vector3(-7f * IntroMotion.Impact(hit, 5f), 0f, 5f * IntroMotion.Tremor(hit, 14f, 2.5f));
            float dart = IntroMotion.Impact(hit, 1.2f);
            pose.Gaze = new Vector2(0.3f * Mathf.Sin(pose.Clock * 9f), 0.2f * Mathf.Sin(pose.Clock * 7f)) * dart;
            pose.Alert = Mathf.Lerp(0.9f, 1.5f, pop);
            pose.SweatTime = hit;
            beat.Punch = IntroMotion.Impact(hit, 5f);
            beat.Flash = 0.8f * IntroMotion.Impact(hit, 16f);
        }

        // "Fak ... ne spet.": the eyes sag back, the mouth thins, the head droops. Then the screen goes dark.
        private static void SampleLine(ref Beat beat, float p)
        {
            ref IntroBoki.Pose pose = ref beat.Pose;
            pose.Pop = Mathf.Lerp(1f, 0.25f, Smooth(0.15f, 0.9f, p));
            pose.Lids = Mathf.Lerp(1f, 0.55f, Smooth(0.2f, 0.95f, p));
            pose.Brows = Mathf.Lerp(1f, -0.2f, Smooth(0.1f, 0.8f, p));
            pose.BrowTilt = Mathf.Lerp(16f, 26f, Smooth(0.1f, 0.8f, p));
            pose.Mouth = Mathf.Lerp(1f, 0.18f, Smooth(0.1f, 0.5f, p));
            pose.Shake = 0.2f * (1f - p);
            pose.Alert = Mathf.Lerp(1.5f, 0f, Smooth(0f, 0.35f, p));
            pose.Head = new Vector3(Mathf.Lerp(0f, 14f, Smooth(0.3f, 1f, p)), 0f, 0f);
            pose.Gaze = new Vector2(0f, Mathf.Lerp(0f, -0.5f, Smooth(0.4f, 1f, p)));
        }

        // Boki bolts upright, kicks the bottle away, overruns, turns and leaps onto the scooter.
        private static void SampleScramble(ref Beat beat, float s)
        {
            ref IntroBoki.Pose pose = ref beat.Pose;
            float panic = Smooth(0f, 0.3f, s);
            pose.UpperArmL = Vector3.Lerp(RelaxUpperL, PanicUpperL, panic);
            pose.UpperArmR = Vector3.Lerp(RelaxUpperR, PanicUpperR, panic);
            pose.LowerArmL = pose.LowerArmR = Vector3.Lerp(RelaxLower, PanicLower, panic);
            pose.Pop = Mathf.Lerp(0.45f, 0.18f, Smooth(0f, 1.2f, s));
            pose.Lids = 1f;
            pose.Brows = 0.7f;
            pose.BrowTilt = 14f;
            pose.Mouth = 0.9f;
            pose.Shake = 0.3f;
            pose.Gaze = Vector2.zero;
            pose.SweatTime = s - 0.2f;
            beat.BottleKick = s - 0.3f;
            beat.ScooterUpright = IntroMotion.Pop(s - 1.2f, 0.5f);

            // Sit up, spring to the feet, run at the city, skid, run back, leap.
            Vector3 position = LyingSpot;
            float yaw = 0f;
            float lie = 1f;
            float runAmount = 0f;
            float tuck = 0f;
            float torso = Upright * IntroMotion.Pop(s, SitSeconds);

            if (s >= SitSeconds)
            {
                float v = Smooth(SitSeconds, SitSeconds + SpringSeconds, s);
                lie = 1f - v;
                torso = Upright * lie;
                position = Vector3.Lerp(LyingSpot, FeetSpot, v) + Vector3.up * (2f * v * (1f - v));
                tuck = Mathf.Sin(Mathf.PI * v);
            }
            if (s >= DashAt)
            {
                Vector3 toRun = TurnSpot - FeetSpot;
                float yawOne = Mathf.Atan2(toRun.x, toRun.z) * Mathf.Rad2Deg;
                Vector3 skidTo = TurnSpot + toRun.normalized * 0.25f;
                Vector3 toScooter = ScooterSpot - skidTo;
                toScooter.y = 0f;
                Vector3 mountFrom = ScooterSpot - toScooter.normalized * 0.9f;
                float yawTwo = Mathf.Atan2(toScooter.x, toScooter.z) * Mathf.Rad2Deg;
                torso = 0f;
                lie = 0f;
                tuck = 0f;

                if (s < RunOneEnd)
                {
                    position = Vector3.Lerp(FeetSpot, TurnSpot, Mathf.InverseLerp(DashAt, RunOneEnd, s));
                    yaw = yawOne;
                    runAmount = 1f;
                }
                else if (s < SkidEnd)
                {
                    float u = Mathf.InverseLerp(RunOneEnd, SkidEnd, s);
                    position = Vector3.Lerp(TurnSpot, skidTo, Smooth(0f, 1f, u));
                    yaw = Mathf.LerpAngle(yawOne, yawTwo, Smooth(0.1f, 0.9f, u));
                    runAmount = 1f - u;
                    torso = -14f * Mathf.Sin(Mathf.PI * u);
                }
                else if (s < RunTwoEnd)
                {
                    position = Vector3.Lerp(skidTo, mountFrom, Mathf.InverseLerp(SkidEnd, RunTwoEnd, s));
                    yaw = yawTwo;
                    runAmount = 1f;
                }
                else
                {
                    float w = Mathf.InverseLerp(RunTwoEnd, ShotSeconds[Scramble], s);
                    position = Vector3.Lerp(mountFrom, ScooterSpot, w) + Vector3.up * (0.75f * 4f * w * (1f - w));
                    yaw = Mathf.LerpAngle(yawTwo, 0f, Smooth(0f, 1f, w));
                    tuck = Mathf.Sin(Mathf.PI * w);
                }
            }

            pose.Torso = new Vector3(torso, 0f, 0f);
            if (runAmount > 0f) ApplyRun(ref pose, beat.Pose.Clock, runAmount, ref position);
            if (tuck > 0f)
            {
                pose.ThighL = pose.ThighR = new Vector3(-50f * tuck, 0f, 0f);
                pose.ShinL = pose.ShinR = new Vector3(70f * tuck, 0f, 0f);
            }
            beat.Position = position;
            beat.Yaw = yaw;
            beat.Lie = lie;
            beat.Punch = 0.5f * IntroMotion.Impact(s - SitSeconds, 8f);
        }

        // Left arm up in front of the face, in two stages so the forearm trails the shoulder.
        private static void RaiseWatchArm(ref IntroBoki.Pose pose, float upper, float lower)
        {
            pose.UpperArmL = Vector3.Lerp(RelaxUpperL, WatchUpper, upper);
            pose.LowerArmL = Vector3.Lerp(RelaxLower, WatchLower, lower);
        }

        private static void ApplyRun(ref IntroBoki.Pose pose, float clock, float amount, ref Vector3 position)
        {
            float phase = clock * 2f * Mathf.PI * StrideHertz;
            float swing = Mathf.Sin(phase);
            float other = Mathf.Sin(phase + Mathf.PI);
            pose.ThighL = new Vector3(-45f * swing * amount, 0f, 0f);
            pose.ThighR = new Vector3(-45f * other * amount, 0f, 0f);
            pose.ShinL = new Vector3((15f + 55f * Mathf.Max(0f, Mathf.Sin(phase + 0.9f))) * amount, 0f, 0f);
            pose.ShinR = new Vector3((15f + 55f * Mathf.Max(0f, Mathf.Sin(phase + 0.9f + Mathf.PI))) * amount, 0f, 0f);
            pose.UpperArmL = new Vector3(-50f * other * amount, 0f, 12f);
            pose.UpperArmR = new Vector3(-50f * swing * amount, 0f, -12f);
            pose.LowerArmL = pose.LowerArmR = new Vector3(-70f * amount, 0f, 0f);
            pose.Torso = new Vector3(pose.Torso.x + 12f * amount, 0f, 0f);
            position += Vector3.up * (0.07f * amount * Mathf.Abs(swing));
        }

        private static float Smooth(float from, float to, float value)
            => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(from, to, value));

        private static float[] BuildStarts()
        {
            var starts = new float[ShotSeconds.Length];
            for (int i = 1; i < starts.Length; i++) starts[i] = starts[i - 1] + ShotSeconds[i - 1];
            return starts;
        }
    }
}
