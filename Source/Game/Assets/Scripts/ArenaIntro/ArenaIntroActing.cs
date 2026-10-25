using Boki.Intro;
using UnityEngine;

namespace Boki.ArenaIntro
{
    // What the first-arena scare does and when (Story §3 LEVEL 1): Boki rides in, hears something in the dark and freezes,
    // and an enemy creeps out of the dark toward him. A pure function of (shot, seconds into the shot, lane length), so
    // playing, skipping and the frame capture show the same frame. Positions are stage-local: origin on the ground under
    // Boki, +Z along the lane the enemy comes down (the dark), +X to the right of it when looking down the lane.
    public static class ArenaIntroActing
    {
        public const int Hush = 0;
        public const int Startle = 1;
        public const int Line = 2;
        public const int Look = 3;
        public const int Creep = 4;
        public const int Lunge = 5;

        public static readonly float[] ShotSeconds = { 1.6f, 1.5f, 2.3f, 2.2f, 6f, 1f };

        // Seconds into their shot. The sounds and the poses share these.
        public const float RustleAt = 0.65f;
        public const float EyePopAt = 0.22f;
        // kaj_je_to_blo.wav is 1.74 s and speaks for nearly all of it.
        public const float BokiVoiceAt = 0.3f;
        public const float BokiVoiceEnd = 2.05f;
        public const float StingerAt = 0f;
        // Where the stinger clip lands its hit; the lunge, flash and camera punch share it.
        public const float HitAt = 0.3f;
        // Seconds since the creep began: the enemy's recorded greeting starts when it is first well in view.
        public const float CreeperVoiceAt = 3.6f;

        // How far the enemy stops from Boki. The opening group never spawns closer than 8 m (Gameplay §5).
        public const float StopDistance = 8.5f;
        private const float LungeDistance = 2.3f;
        private const float FaceHeight = 1.56f;
        private const float CreeperChest = 1.15f;

        // The enemy walks for the whole of Look and Creep, in stop-and-go lurches that each take one stride cycle.
        public static readonly CreepMotion Approach = new CreepMotion(ShotSeconds[Look] + ShotSeconds[Creep], 9, 0.6f);

        private static readonly Vector3 RelaxUpperL = new Vector3(8f, 0f, 16f);
        private static readonly Vector3 RelaxUpperR = new Vector3(8f, 0f, -16f);
        private static readonly Vector3 RelaxLower = new Vector3(-14f, 0f, 0f);

        private static readonly Vector3 HushCamera = new Vector3(-1f, 1.8f, -3.4f);
        private static readonly Vector3 HushTarget = new Vector3(0f, 1.35f, 3.5f);
        // Establishing: over his left shoulder from about 3 m, the lane opening beyond him.
        private static readonly Vector3 LookFrom = new Vector3(-1.3f, 1.65f, -3.3f);
        private static readonly Vector3 LookTo = new Vector3(-1.25f, 1.5f, -3f);
        // The enemy's approach: tight behind his left shoulder, his head a dark shape at the edge, the enemy in the middle.
        private static readonly Vector3 CreepFrom = new Vector3(-0.55f, 1.62f, -2.2f);
        private static readonly Vector3 CreepTo = new Vector3(-0.45f, 1.58f, -1.5f);
        private static readonly Vector3 DownLane = new Vector3(0.25f, 1.2f, 12f);
        private static readonly Vector3 BokiShoulder = new Vector3(0f, 1.42f, 0f);

        private static readonly float[] ShotStarts = BuildStarts();

        public struct Beat
        {
            public IntroBoki.Pose Boki;
            public bool Puppet;            // the close-up Boki stands in for the rider
            public bool RiderFacesLane;    // false only before he hears it
            public Vector3 Camera;
            public Vector3 Target;
            public float Fov;              // vertical degrees
            public float GameplayBlend;    // 1 the gameplay camera, 0 this shot's own
            public float Punch;            // camera punch-in, 0..1
            public float Flash;            // white flash alpha
            public bool CreeperVisible;
            public float CreeperDistance;  // along the lane, from Boki
            public float CreeperStride;    // stride cycles walked; the fraction picks the pose
            public bool Subtitle;
        }

        public static float StartOf(int shot) => ShotStarts[Mathf.Clamp(shot, 0, ShotStarts.Length - 1)];

        // Seconds the enemy has been creeping at this moment of the sequence (it starts with the Look shot).
        public static float CreepSeconds(float sequenceSeconds) => Mathf.Max(0f, sequenceSeconds - ShotStarts[Look]);

        // laneStart: how far down the lane the enemy begins, which depends on how much clear ground the arena gives.
        public static Beat Sample(int shot, float seconds, float laneStart)
        {
            shot = Mathf.Clamp(shot, 0, ShotSeconds.Length - 1);
            seconds = Mathf.Clamp(seconds, 0f, ShotSeconds[shot]);
            float clock = ShotStarts[shot] + seconds;
            float p = seconds / ShotSeconds[shot];
            float e = Smooth(0f, 1f, p);
            var beat = new Beat
            {
                Fov = 50f,
                RiderFacesLane = shot > Hush,
                Puppet = shot == Startle || shot == Line,
                CreeperVisible = shot >= Look,
                Boki = new IntroBoki.Pose
                {
                    UpperArmL = RelaxUpperL, LowerArmL = RelaxLower, UpperArmR = RelaxUpperR, LowerArmR = RelaxLower,
                    Lids = 0.55f, Brows = -0.1f, Mouth = 0.05f, SweatTime = -1f, Clock = clock,
                },
            };
            PlaceCreeper(ref beat, shot, seconds, laneStart);
            Vector3 creeperChest = new Vector3(0f, CreeperChest, beat.CreeperDistance);

            switch (shot)
            {
                case Hush:
                    beat.Camera = HushCamera;
                    beat.Target = HushTarget;
                    beat.Fov = 60f;
                    beat.GameplayBlend = 1f - Smooth(0f, 0.85f, p);
                    break;

                case Startle:
                    SampleStartle(ref beat, seconds);
                    FaceCamera(ref beat, Mathf.Lerp(1f, 0.82f, e), clock);
                    break;

                case Line:
                    SampleLine(ref beat, seconds, p);
                    FaceCamera(ref beat, Mathf.Lerp(0.82f, 0.6f, e), clock);
                    break;

                case Look:
                    beat.Camera = Vector3.Lerp(LookFrom, LookTo, e);
                    beat.Target = Vector3.Lerp(BokiShoulder, DownLane, Smooth(0.25f, 1f, p));
                    break;

                case Creep:
                    beat.Camera = Vector3.Lerp(CreepFrom, CreepTo, e);
                    beat.Target = Vector3.Lerp(DownLane, creeperChest, 0.9f);
                    beat.Fov = Mathf.Lerp(38f, 28f, e);
                    break;

                case Lunge:
                    // A beat of dread before it comes: the camera leans in, then the hit lands.
                    beat.Camera = Vector3.Lerp(CreepTo, CreepTo + new Vector3(0.05f, 0f, 0.35f), Smooth(0f, HitAt, seconds));
                    beat.Target = Vector3.Lerp(DownLane, creeperChest, 0.9f);
                    beat.Fov = 28f;
                    beat.Punch = IntroMotion.Impact(seconds - HitAt, 6f);
                    // A white hit as it lands, then a rising white that is still there at the cut, masking the swap to the real horde.
                    beat.Flash = Mathf.Clamp01((seconds < HitAt ? 0f : 0.8f * IntroMotion.Impact(seconds - HitAt, 14f))
                        + 0.5f * Smooth(0.82f, 0.98f, seconds));
                    break;
            }
            return beat;
        }

        // The enemy is alive from the start of Look. In the lunge it breaks into a run for the last stretch.
        private static void PlaceCreeper(ref Beat beat, int shot, float seconds, float laneStart)
        {
            float progress = Approach.Progress(CreepSeconds(ShotStarts[shot] + seconds));
            beat.CreeperDistance = Mathf.Lerp(laneStart, StopDistance, progress);
            beat.CreeperStride = progress * Approach.Lurches;
            if (shot != Lunge) return;
            float rush = Smooth(0.12f, 0.42f, seconds);
            beat.CreeperDistance = StopDistance - LungeDistance * (1f - (1f - rush) * (1f - rush));
            beat.CreeperStride += 3f * rush;
        }

        // The rider freezes at the sound: eyes out on stalks, a jolt, the "!" and sweat.
        private static void SampleStartle(ref Beat beat, float s)
        {
            ref IntroBoki.Pose pose = ref beat.Boki;
            float hit = s - EyePopAt;
            float pop = IntroMotion.Pop(hit, 0.45f);
            pose.Pop = 0.78f * pop;
            pose.Lids = Mathf.Lerp(0.55f, 1f, pop);
            pose.Brows = Mathf.Lerp(-0.1f, 1f, pop);
            pose.BrowTilt = Mathf.Lerp(0f, 14f, pop);
            pose.Mouth = Mathf.Lerp(0.05f, 0.5f, pop);
            pose.Shake = 0.9f * IntroMotion.Impact(hit, 2.4f);
            pose.Head = new Vector3(-8f * IntroMotion.Impact(hit, 6f), 0f, 3f * IntroMotion.Tremor(hit, 13f, 3f));
            pose.Alert = 0.9f * IntroMotion.Pop(hit - 0.08f, 0.3f);
            pose.SweatTime = hit - 0.05f;
            beat.Punch = 0.8f * IntroMotion.Impact(hit, 7f);
            beat.Flash = 0.4f * IntroMotion.Impact(hit, 20f);
        }

        // »Kaj je to blo?«: he scans left, then right, for whatever made the noise, shaking and sweating.
        private static void SampleLine(ref Beat beat, float s, float p)
        {
            ref IntroBoki.Pose pose = ref beat.Boki;
            float yaw = -22f * Smooth(0f, 0.3f, p) + 48f * Smooth(0.3f, 0.7f, p) - 26f * Smooth(0.7f, 1f, p);
            float talk = Smooth(BokiVoiceAt, BokiVoiceAt + 0.08f, s) * (1f - Smooth(BokiVoiceEnd - 0.1f, BokiVoiceEnd, s));
            pose.Pop = Mathf.Lerp(0.78f, 0.55f, Smooth(0.1f, 0.9f, p));
            pose.Lids = Mathf.Lerp(1f, 0.85f, p);
            pose.Brows = Mathf.Lerp(1f, 0.6f, p);
            pose.BrowTilt = Mathf.Lerp(14f, 20f, p);
            pose.Mouth = Mathf.Lerp(0.2f, 0.2f + 0.6f * Mathf.Abs(Mathf.Sin(s * 14.5f)), talk);
            pose.Shake = 0.35f;
            pose.Head = new Vector3(0f, yaw, 0f);
            pose.Gaze = new Vector2(Mathf.Clamp(yaw / 30f, -1f, 1f), 0f);
            pose.Alert = 0.9f * (1f - Smooth(0f, 0.4f, p));
            pose.SweatTime = Mathf.Repeat(s, 1.3f) - 0.1f;
            beat.Subtitle = s >= BokiVoiceAt && s <= BokiVoiceEnd;
        }

        // From just ahead of his face, so the dark he is peering into is behind the lens. Aimed above the eyes so the "!" clears the letterbox.
        private static void FaceCamera(ref Beat beat, float distance, float clock)
        {
            const float aim = FaceHeight + 0.1f;
            beat.Camera = new Vector3(0.28f * distance + 0.012f * Mathf.Sin(clock * 1.7f), aim + 0.1f * distance, distance);
            beat.Target = new Vector3(0f, aim, 0f);
            beat.Fov = 48f;
        }

        private static float Smooth(float from, float to, float value)
        {
            float t = Mathf.Clamp01((value - from) / (to - from));
            return t * t * (3f - 2f * t);
        }

        private static float[] BuildStarts()
        {
            var starts = new float[ShotSeconds.Length];
            for (int i = 1; i < starts.Length; i++) starts[i] = starts[i - 1] + ShotSeconds[i - 1];
            return starts;
        }
    }
}
