using Boki.Intro;
using UnityEngine;

namespace Boki.Ending
{
    // What the ending's actors and camera do and when (Story §3 FINAL BOSS victory). A pure function of
    // (shot, seconds into the shot), so playing, skipping and the Editor frame capture all show the same frame.
    // Positions are stage-local: origin on the floor under the professor at his lectern, +Z toward Boki and the camera.
    // Seen from the class the stage's +X is on the screen's left.
    public static class EndingActing
    {
        public const int Loom = 0;
        public const int Glasses = 1;
        public const int Saturday = 2;
        public const int Huh = 3;
        public const int Point = 4;
        public const int ZoomIn = 5;
        public const int ZoomOut = 6;
        public const int Meow = 7;
        public const int Both = 8;
        public const int EndCard = 9;

        public static readonly float[] ShotSeconds = { 2.6f, 2f, 2.8f, 2.6f, 2f, 2.4f, 1.8f, 2.2f, 3.6f, 2.4f };

        // Seconds into their shot. The sounds and the poses share these.
        public const float ShrinkAt = 0.9f;
        public const float ZoomCueAt = 0.15f;
        public const float ThumpAt = 1.75f;
        public const float MeowAt = 0.45f;
        public const float BokiPopAt = 0.75f;
        public const float LickCueAt = 0.02f;
        // The recorded lines (professor_saturday.wav, boki_reaction_0*.wav) start here in their shots. The professor's
        // speech sits at 0.1-0.6 s and 0.9-1.9 s of his clip, Boki's at 0.1-2 s of his.
        public const float ProfessorVoiceAt = 0.25f;
        public const float BokiVoiceAt = 0.3f;

        // Seconds per lick stroke. Matches LICK_PERIOD in music/generate_ending_sfx.py.
        public const float LickSeconds = 0.5f;

        public const int NoLine = 0;
        public const int ProfessorLine = 1;
        public const int BokiLine = 2;

        // The professor looms and talks behind his lectern. The V1 classroom's lectern is a desk 2 m wide and 1.4 m high that
        // hides everything below his shoulders, so from the Boki reaction on he stands beside it, where the cut hides the move.
        public static readonly Vector3 ProfessorBehind = Vector3.zero;
        public static readonly Vector3 ProfessorBeside = new Vector3(1.65f, 0f, 0.35f);
        // Where the calendar hangs unless the host scene says otherwise: on the wall behind the professor, to his right.
        public static readonly Vector3 DefaultCalendar = new Vector3(3.4f, 2.15f, -1.62f);
        public static readonly Vector3 BokiSpot = new Vector3(-1.3f, 0f, 3.3f);
        private static readonly Vector3 BokiBothSpot = new Vector3(2.9f, 0f, 0.6f);

        // Where the pointing shot ends; the zoom starts from there.
        private static readonly Vector3 PointCameraEnd = new Vector3(4.1f, 1.75f, 1.6f);
        private static readonly Vector3 PointTarget = new Vector3(2.5f, 1.7f, -0.6f);
        private const float PointFov = 42f;
        // The cat is first seen from here; the zoom out ends and the meow starts on it.
        private static readonly Vector3 CatCamera = ProfessorBeside + new Vector3(-0.4f, 1.7f, 2.4f);

        private const float ShoulderHeight = 1.42f;
        private const float FaceHeight = 1.56f;

        // Boki's arms hang with these; his rig names the +X arm L, so it swings in with negative Z.
        private static readonly Vector3 BokiHangL = new Vector3(0f, 0f, -28f);
        private static readonly Vector3 BokiHangR = new Vector3(0f, 0f, 28f);
        private static readonly Vector3 BokiHangLower = new Vector3(-12f, 0f, 0f);

        // The professor's arm poses were solved against his rig's bone positions: hands on the lectern top, the
        // right hand at the glasses, the left wrist at the mouth with the elbow low.
        private static readonly Vector3 LecternUpperL = new Vector3(-10f, 0f, 27f);
        private static readonly Vector3 LecternUpperR = new Vector3(-10f, 0f, -27f);
        private static readonly Vector3 LecternLower = new Vector3(-81f, 0f, 0f);
        private static readonly Vector3 LoomUpperL = new Vector3(-15f, 0f, -78f);
        private static readonly Vector3 LoomUpperR = new Vector3(-15f, 0f, 78f);
        private static readonly Vector3 LoomLower = new Vector3(-35f, 0f, 0f);
        private static readonly Vector3 GlassesUpperR = new Vector3(-80f, 28f, -49f);
        private static readonly Vector3 GlassesLowerR = new Vector3(-111f, -67f, 0f);
        private static readonly Vector3 LickUpperL = new Vector3(-40f, 30f, 50f);
        private static readonly Vector3 LickLowerL = new Vector3(-139f, 57f, 0f);
        private static readonly Vector3 LickHead = new Vector3(22f, -8f, -6f);

        private static readonly float[] ShotStarts = BuildStarts();

        public struct Beat
        {
            public EndingProfessor.Pose Professor;
            public Vector3 ProfessorPosition;  // his feet
            public float ProfessorYaw;         // degrees
            public IntroBoki.Pose Boki;
            public Vector3 BokiPosition;       // Boki's feet
            public float BokiYaw;              // degrees
            public Vector3 Camera;
            public Vector3 Target;
            public float Fov;                  // vertical degrees
            public float Punch;                // camera punch-in, 0..1
            public float Flash;                // white flash alpha
            public float Fade;                 // black fade alpha
            public float RedLight;             // the boss's red light, 1 on .. 0 out
            public float MarkerPop;            // the ring around the date, 0 none .. 1 drawn, may overshoot
            public float Meow;                 // size of the MEOW burst, 0 none, may overshoot
            public float EndCard;              // alpha of the closing card
            public int Subtitle;
            public bool Letterbox;
        }

        public static float StartOf(int shot) => ShotStarts[Mathf.Clamp(shot, 0, ShotStarts.Length - 1)];

        // calendar: stage-local center of the wall calendar. The professor turns and the zoom aims there.
        public static Beat Sample(int shot, float seconds, Vector3 calendar)
        {
            shot = Mathf.Clamp(shot, 0, ShotSeconds.Length - 1);
            seconds = Mathf.Clamp(seconds, 0f, ShotSeconds[shot]);
            float clock = ShotStarts[shot] + seconds;
            float p = seconds / ShotSeconds[shot];
            float e = Smooth(0f, 1f, p);
            Vector3 professor = shot >= Huh ? ProfessorBeside : ProfessorBehind;
            var beat = new Beat
            {
                Professor = Lectern(clock),
                ProfessorPosition = professor,
                ProfessorYaw = Facing(professor, BokiSpot),
                Boki = BokiStanding(clock),
                BokiPosition = BokiSpot,
                BokiYaw = Facing(BokiSpot, professor),
                Fov = 40f,
                MarkerPop = shot > ZoomIn ? 1f : 0f,
                Letterbox = true,
            };

            switch (shot)
            {
                case Loom: SampleLoom(ref beat, seconds, e); break;
                case Glasses: SampleGlasses(ref beat, seconds, e); break;
                case Saturday: SampleSaturday(ref beat, seconds, e); break;
                case Huh: SampleHuh(ref beat, seconds, e); break;
                case Point: SamplePoint(ref beat, seconds, e, calendar); break;
                case ZoomIn: SampleZoomIn(ref beat, seconds, p, calendar); break;
                case ZoomOut: SampleZoomOut(ref beat, seconds, p, calendar); break;
                case Meow: SampleMeow(ref beat, seconds, e); break;
                case Both: SampleBoth(ref beat, seconds, e); break;
                default:
                    beat = Sample(Both, ShotSeconds[Both], calendar);
                    beat.Fade = 1f;
                    beat.EndCard = Smooth(0.15f, 0.6f, seconds);
                    beat.Letterbox = false;
                    beat.Flash = 0f;
                    break;
            }
            return beat;
        }

        // The boss goes down. Its red light gutters out and the looming giant deflates to a tired man at a lectern.
        private static void SampleLoom(ref Beat beat, float t, float e)
        {
            float shrink = IntroMotion.Pop(t - ShrinkAt, 0.7f);
            float loom = 1f - Smooth(ShrinkAt - 0.05f, ShrinkAt + 0.45f, t);
            ref EndingProfessor.Pose pose = ref beat.Professor;
            pose.Scale = 1f + 0.7f * (1f - shrink);
            pose.UpperArmL = Vector3.Lerp(pose.UpperArmL, LoomUpperL, loom);
            pose.UpperArmR = Vector3.Lerp(pose.UpperArmR, LoomUpperR, loom);
            pose.LowerArmL = Vector3.Lerp(pose.LowerArmL, LoomLower, loom);
            pose.LowerArmR = Vector3.Lerp(pose.LowerArmR, LoomLower, loom);
            pose.Head = Vector3.Lerp(pose.Head, new Vector3(-14f, 0f, 0f), loom);
            pose.Torso = Vector3.Lerp(pose.Torso, new Vector3(-8f, 0f, 0f), loom);
            pose.Torso.z += 3.5f * loom * Mathf.Sin(t * 41f);
            // Deflating: the head droops a beat after the body has shrunk.
            pose.Head.x += 12f * IntroMotion.Impact(t - ShrinkAt - 0.25f, 3f);

            beat.RedLight = (1f - Smooth(0.2f, 1.1f, t)) * (0.8f + 0.2f * Mathf.Sin(t * 53f));
            beat.Flash = 0.9f * IntroMotion.Impact(t, 7f);
            beat.Punch = 0.7f * IntroMotion.Impact(t - ShrinkAt, 7f);

            // Boki braces against the giant, then sags with relief.
            ref IntroBoki.Pose boki = ref beat.Boki;
            boki.Lids = Mathf.Lerp(boki.Lids, 1f, loom);
            boki.Brows = Mathf.Lerp(boki.Brows, 0.6f, loom);
            boki.BrowTilt = 12f * loom;
            boki.Mouth = Mathf.Lerp(boki.Mouth, 0.5f, loom);
            boki.Shake = 0.3f * loom;

            // From the class, over the lectern; Boki is a foreground silhouette at the edge.
            beat.Camera = Vector3.Lerp(new Vector3(0.4f, 1.6f, 4.3f), new Vector3(0.2f, 1.8f, 2.5f), e);
            beat.Target = new Vector3(0f, Mathf.Lerp(2.1f, 1.7f, Smooth(ShrinkAt - 0.1f, ShrinkAt + 0.7f, t)), 0f);
            beat.Fov = Mathf.Lerp(52f, 44f, e);
        }

        // He pushes his glasses up with the air of a man who has done this before.
        private static void SampleGlasses(ref Beat beat, float t, float e)
        {
            float up = Smooth(0f, 0.4f, t) * (1f - Smooth(1.45f, 1.85f, t));
            float tap = Mathf.Max(0f, Mathf.Sin((t - 0.55f) * 20f)) * Smooth(0.5f, 0.6f, t) * (1f - Smooth(1.1f, 1.2f, t));
            ref EndingProfessor.Pose pose = ref beat.Professor;
            pose.UpperArmR = Vector3.Lerp(LecternUpperR, GlassesUpperR, up);
            pose.LowerArmR = Vector3.Lerp(LecternLower, GlassesLowerR, up);
            pose.LowerArmR.x += 7f * tap;
            pose.Head = new Vector3(4f + 5f * up + 3f * tap, 0f, 0f);

            beat.Camera = Vector3.Lerp(new Vector3(-0.7f, 1.9f, 1.55f), new Vector3(-0.45f, 1.85f, 1.3f), e);
            beat.Target = new Vector3(0f, 1.62f, 0f);
            beat.Fov = 38f;
        }

        // »Boki ... danes je sobota.« Jaw bobbing per syllable, a pause for the ellipsis, a nod on the last word.
        private static void SampleSaturday(ref Beat beat, float t, float e)
        {
            float speaking = Window(t, ProfessorVoiceAt + 0.1f, ProfessorVoiceAt + 0.6f) + Window(t, ProfessorVoiceAt + 0.9f, ProfessorVoiceAt + 1.9f);
            ref EndingProfessor.Pose pose = ref beat.Professor;
            pose.Mouth = speaking * (0.3f + 0.5f * Mathf.Abs(Mathf.Sin(t * 14f)));
            pose.Head = new Vector3(4f + 4f * speaking * Mathf.Sin(t * 7f), 0f, 0f);
            float gesture = Window(t, 1.55f, 2.4f);
            pose.UpperArmR = Vector3.Lerp(LecternUpperR, new Vector3(-62f, 0f, -18f), gesture);
            pose.LowerArmR = Vector3.Lerp(LecternLower, new Vector3(-60f, 0f, 0f), gesture);

            beat.Subtitle = t >= 0.2f ? ProfessorLine : NoLine;
            beat.Camera = Vector3.Lerp(new Vector3(0.35f, 1.85f, 1.7f), new Vector3(0.15f, 1.8f, 1.15f), e);
            beat.Target = new Vector3(0f, 1.6f, 0f);
            beat.Fov = Mathf.Lerp(34f, 30f, e);
        }

        // »... A?« A blank stare, then the head tilts like a confused dog's.
        private static void SampleHuh(ref Beat beat, float t, float e)
        {
            ref IntroBoki.Pose boki = ref beat.Boki;
            boki.Lids = Mathf.Lerp(0.5f, 0.78f, Smooth(0f, 0.3f, t));
            boki.Brows = Mathf.Lerp(-0.2f, 0.35f, Smooth(0.5f, 1f, t));
            boki.BrowTilt = 12f * Smooth(0.6f, 1.1f, t);
            boki.Mouth = 0.08f + 0.55f * Window(t, BokiVoiceAt + 0.1f, BokiVoiceAt + 1.8f) * (0.6f + 0.4f * Mathf.Sin(t * 11f));
            boki.Head = new Vector3(0f, 0f, 14f * Smooth(0.7f, 1.3f, t));
            boki.Gaze = new Vector2(0.4f, 0.1f);
            boki.SweatTime = t - 1.3f;

            beat.Subtitle = t >= BokiVoiceAt ? BokiLine : NoLine;
            Vector3 face = BokiSpot + Vector3.up * FaceHeight;
            Vector3 forward = new Vector3(Mathf.Sin(beat.BokiYaw * Mathf.Deg2Rad), 0f, Mathf.Cos(beat.BokiYaw * Mathf.Deg2Rad));
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            beat.Camera = face + forward * Mathf.Lerp(1.15f, 0.85f, e) + right * 0.1f + Vector3.up * 0.04f;
            beat.Target = face - forward * 0.02f;
            beat.Fov = Mathf.Lerp(34f, 30f, e);
        }

        // He turns on the spot and points at the wall calendar without a word.
        private static void SamplePoint(ref Beat beat, float t, float e, Vector3 calendar)
        {
            Vector3 toCalendar = calendar - beat.ProfessorPosition;
            float turn = IntroMotion.Pop(t - 0.1f, 0.5f);
            float yaw = Mathf.Atan2(toCalendar.x, toCalendar.z) * Mathf.Rad2Deg;
            beat.ProfessorYaw = Mathf.LerpUnclamped(beat.ProfessorYaw, yaw, turn);
            float arm = Smooth(0.3f, 0.75f, t);
            ref EndingProfessor.Pose pose = ref beat.Professor;
            pose.UpperArmR = Vector3.Lerp(LecternUpperR, PointUpper(toCalendar), arm);
            pose.LowerArmR = Vector3.Lerp(LecternLower, new Vector3(-4f, 0f, 0f), arm);
            pose.UpperArmR.x += 1.5f * Smooth(1f, 1.2f, t) * Mathf.Sin(t * 7f);
            pose.Head = new Vector3(4f - 8f * arm, 0f, 0f);
            pose.Torso = new Vector3(3f, 0f, 0f);

            beat.Camera = Vector3.Lerp(new Vector3(4.4f, 1.75f, 1.9f), PointCameraEnd, e);
            beat.Target = PointTarget;
            beat.Fov = PointFov;
        }

        // Zoom in on the calendar until the date fills the screen. Behind the full-screen card the professor
        // quietly becomes a cat, so the reveal belongs to the zoom out.
        private static void SampleZoomIn(ref Beat beat, float t, float p, Vector3 calendar)
        {
            Vector3 toCalendar = calendar - beat.ProfessorPosition;
            float yaw = Mathf.Atan2(toCalendar.x, toCalendar.z) * Mathf.Rad2Deg;
            ref EndingProfessor.Pose pose = ref beat.Professor;
            pose.UpperArmR = PointUpper(toCalendar);
            pose.LowerArmR = new Vector3(-4f, 0f, 0f);
            pose.Head = new Vector3(-4f, 0f, 0f);
            beat.ProfessorYaw = yaw;

            float change = Smooth(1.9f, 2.3f, t);
            if (change > 0f)
            {
                AsCat(ref beat, change, 0f);
                pose.Cat = IntroMotion.Pop(t - 1.95f, 0.3f);
                beat.ProfessorYaw = Mathf.LerpAngle(yaw, Facing(beat.ProfessorPosition, CatCamera), change);
            }

            beat.MarkerPop = IntroMotion.Pop(t - ThumpAt, 0.35f);
            beat.Punch = 0.5f * IntroMotion.Impact(t - ThumpAt, 6f);
            beat.Flash = 0.2f * IntroMotion.Impact(t - ThumpAt, 16f);

            float zoom = Smooth(0f, 1f, p);
            Vector3 date = DateSpot(calendar);
            beat.Camera = Vector3.Lerp(PointCameraEnd, date + Vector3.forward * 1f, zoom);
            beat.Target = Vector3.Lerp(PointTarget, date, zoom);
            beat.Fov = Mathf.Lerp(PointFov, 26f, zoom);
        }

        // Pull back from the date, swing across the room, and there he is: ears, whiskers, licking his arm.
        private static void SampleZoomOut(ref Beat beat, float t, float p, Vector3 calendar)
        {
            AsCat(ref beat, 1f, t);
            beat.ProfessorYaw = Facing(beat.ProfessorPosition, CatCamera);

            float pull = Smooth(0f, 1f, p);
            Vector3 date = DateSpot(calendar);
            beat.Camera = Bezier(date + Vector3.forward * 1f, new Vector3(3f, 1.9f, 3.6f), CatCamera, pull);
            beat.Target = Vector3.Lerp(date, beat.ProfessorPosition + new Vector3(0.1f, 1.5f, 0f), Smooth(0.3f, 0.85f, p));
            beat.Fov = Mathf.Lerp(26f, 42f, pull);
        }

        // He stops licking, looks up and meows: a burst, a punch and a flash.
        private static void SampleMeow(ref Beat beat, float t, float e)
        {
            float look = Smooth(0.25f, 0.5f, t) * (1f - Smooth(1.5f, 1.85f, t));
            float tongue = Mathf.Clamp01(1f - Smooth(0.25f, 0.42f, t) + Smooth(1.75f, 1.9f, t));
            float lick = t < 1.7f ? ShotSeconds[ZoomOut] + t : t - 1.7f;
            AsCat(ref beat, 1f - 0.6f * look, lick);
            beat.ProfessorYaw = Facing(beat.ProfessorPosition, CatCamera);
            ref EndingProfessor.Pose pose = ref beat.Professor;
            pose.Tongue *= tongue;

            float open = Smooth(MeowAt, MeowAt + 0.12f, t) * (1f - Smooth(1.2f, 1.45f, t));
            pose.Mouth = open;
            pose.Head = Vector3.Lerp(pose.Head, new Vector3(-16f, 0f, 0f), look);
            pose.Torso = Vector3.Lerp(pose.Torso, new Vector3(-5f, 0f, 0f), look);
            pose.Twitch = 0.4f + 0.6f * IntroMotion.Impact(t - MeowAt, 2f);

            beat.Meow = IntroMotion.Pop(t - MeowAt, 0.3f) * (1f - Smooth(1.5f, 1.9f, t));
            beat.Punch = 0.7f * IntroMotion.Impact(t - MeowAt, 6f);
            beat.Flash = 0.45f * IntroMotion.Impact(t - MeowAt, 18f);

            // Already close when the meow lands, then a slow push in.
            Vector3 feet = beat.ProfessorPosition;
            beat.Camera = Vector3.Lerp(CatCamera, feet + new Vector3(-0.1f, 1.66f, 1.7f), Smooth(0f, 0.5f, t))
                + Vector3.back * (0.2f * e);
            beat.Target = feet + new Vector3(0f, 1.6f, 0f);
            beat.Fov = Mathf.Lerp(40f, 33f, Smooth(0f, 0.5f, t));
        }

        // The two of them, side by side. The professor keeps washing; Boki looks at him, then slowly at us, eyes out on stalks.
        private static void SampleBoth(ref Beat beat, float t, float e)
        {
            AsCat(ref beat, 1f, t);
            beat.BokiPosition = BokiBothSpot;
            beat.BokiYaw = 0f;
            beat.ProfessorYaw = 8f;

            float pop = IntroMotion.Pop(t - BokiPopAt, 0.5f);
            ref IntroBoki.Pose boki = ref beat.Boki;
            // The professor stands on Boki's -X side.
            boki.Head = new Vector3(0f, -45f * (1f - Smooth(BokiPopAt - 0.1f, BokiPopAt + 0.25f, t)), 0f);
            boki.Pop = pop;
            boki.Lids = Mathf.Lerp(0.5f, 1f, Mathf.Clamp01(pop));
            boki.Brows = Mathf.Lerp(-0.2f, 0.9f, Mathf.Clamp01(pop));
            boki.BrowTilt = 14f * pop;
            boki.Mouth = Mathf.Lerp(0.08f, 0.7f, Mathf.Clamp01(pop));
            boki.Shake = 0.2f + 0.7f * IntroMotion.Impact(t - BokiPopAt, 2.5f);
            boki.Alert = 1.1f * pop;
            boki.SweatTime = t - BokiPopAt - 0.1f;
            boki.Gaze = Vector2.zero;
            beat.Punch = 0.5f * IntroMotion.Impact(t - BokiPopAt, 7f);
            beat.Flash = 0.35f * IntroMotion.Impact(t - BokiPopAt, 18f);

            beat.Fade = Smooth(3f, 3.6f, t);
            beat.Camera = Vector3.Lerp(new Vector3(2.4f, 1.45f, 4.8f), new Vector3(2.3f, 1.45f, 4.3f), e);
            beat.Target = new Vector3(2.3f, 1.2f, 0.5f);
            beat.Fov = 32f;
        }

        // The cat professor: ears and whiskers, the left forearm raised, the tongue working along it.
        // amount blends the pose in from whatever the beat already holds; lickSeconds is the licking clock.
        private static void AsCat(ref Beat beat, float amount, float lickSeconds)
        {
            ref EndingProfessor.Pose pose = ref beat.Professor;
            pose.UpperArmL = Vector3.Lerp(pose.UpperArmL, LickUpperL, amount);
            pose.LowerArmL = Vector3.Lerp(pose.LowerArmL, LickLowerL, amount);
            pose.Head = Vector3.Lerp(pose.Head, LickHead, amount);
            pose.Torso = Vector3.Lerp(pose.Torso, new Vector3(8f, 0f, 0f), amount);
            pose.Cat = 1f;
            pose.Twitch = 0.4f;
            pose.Tongue = EndingMotion.Reach(lickSeconds, LickSeconds) * amount;
            pose.LickAlong = EndingMotion.Along(lickSeconds, LickSeconds);
            // Licking bobs the head with each stroke.
            pose.Head.x += 3f * EndingMotion.Reach(lickSeconds, LickSeconds) * amount;
        }

        private static EndingProfessor.Pose Lectern(float clock) => new EndingProfessor.Pose
        {
            Scale = 1f,
            Torso = new Vector3(3f, 0f, 0f),
            Head = new Vector3(4f, 0f, 0f),
            UpperArmL = LecternUpperL,
            LowerArmL = LecternLower,
            UpperArmR = LecternUpperR,
            LowerArmR = LecternLower,
            Clock = clock,
        };

        private static IntroBoki.Pose BokiStanding(float clock) => new IntroBoki.Pose
        {
            Torso = new Vector3(0.8f * Mathf.Sin(clock * 2.2f), 0f, 0f),
            UpperArmL = BokiHangL,
            LowerArmL = BokiHangLower,
            UpperArmR = BokiHangR,
            LowerArmR = BokiHangLower,
            Lids = 0.5f,
            Brows = -0.2f,
            Mouth = 0.08f,
            SweatTime = -1f,
            Clock = clock,
        };

        // The right arm, straight, aimed at the calendar from the shoulder. The solved pose for 15 degrees of elevation is
        // (-104, -12, -6); the yaw and roll bring the hand in from the bind pose's outstretched A-pose.
        private static Vector3 PointUpper(Vector3 toCalendar)
        {
            float distance = new Vector2(toCalendar.x, toCalendar.z).magnitude;
            float elevation = Mathf.Atan2(toCalendar.y - ShoulderHeight, distance) * Mathf.Rad2Deg;
            return new Vector3(-(89f + elevation), -12f, -6f);
        }

        // The card faces +Z, so what is on its right is on the stage's -X.
        private static Vector3 DateSpot(Vector3 calendar)
        {
            Vector2 cell = EndingCalendarLayout.EndingDateCell();
            return calendar + new Vector3(-cell.x, cell.y, 0f);
        }

        // Yaw in degrees that turns an actor standing at from to look at to.
        private static float Facing(Vector3 from, Vector3 to) => Mathf.Atan2(to.x - from.x, to.z - from.z) * Mathf.Rad2Deg;

        private static Vector3 Bezier(Vector3 a, Vector3 b, Vector3 c, float t)
            => Vector3.Lerp(Vector3.Lerp(a, b, t), Vector3.Lerp(b, c, t), t);

        // 1 between a and b with short smooth edges, 0 elsewhere.
        private static float Window(float t, float a, float b)
            => Smooth(a, a + 0.05f, t) * (1f - Smooth(b - 0.05f, b, t));

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
