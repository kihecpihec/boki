using Boki.UI;
using Boki.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Boki.Intro
{
    // Story §3 PROLOGUE, minimum slice: shot 1 (wake-up), shot 2 (watch, 07:58, the lecture) and shot 4 (scooter),
    // played for laughs: Boki's eyes pop out twice, then he scrambles onto the scooter.
    // Shot 3 (VHS glitch and the black figure) is not built. Skipping jumps to the same end state as playing through.
    [DisallowMultipleComponent]
    public sealed class IntroCutscene : MonoBehaviour
    {
        private static readonly Color Paper = new Color(0.91f, 0.89f, 0.86f);
        private static readonly Color Lcd = new Color(0.62f, 0.72f, 0.3f);
        private const string PhoneMessage = "»PREDAVANJE — 08:00 / FERI«";
        private const string BokiLine = "»Fak ... ne spet.«";

        [Header("Shots")]
        [SerializeField] private Camera shotCamera;
        [Tooltip("Ground point of the wake-up. Boki lies along its Z axis with his head toward -Z; +Z faces the city.")]
        [SerializeField] private Transform stage;
        [SerializeField] private IntroOverlay overlay;
        [SerializeField] private GameObject wakeActors;
        [SerializeField] private IntroBoki boki;
        [SerializeField] private Transform fallenScooter;
        [SerializeField] private Transform bottle;

        [Header("Handoff to gameplay")]
        [SerializeField] private GameObject rider;
        [SerializeField] private Transform riderRoot;
        [Tooltip("Same pivot as ThirdPersonCamera, so the last shot ends on its resting pose.")]
        [SerializeField] private Transform followPivot;
        [SerializeField, Min(0.5f)] private float followDistance = 5f;
        [SerializeField] private Behaviour headlight;
        [SerializeField] private Behaviour[] controls;
        [SerializeField] private Canvas[] gameplayCanvases = System.Array.Empty<Canvas>();
        private bool[] canvasStates;
        [SerializeField, Min(0f)] private float objectiveSeconds = 6f;

        [Header("Audio and input")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip phoneBeep;
        [SerializeField] private AudioClip eyePop;
        [SerializeField] private AudioClip dashWhoosh;
        [SerializeField] private AudioClip bokiLine;
        [SerializeField, Range(0f, 1f)] private float effectsGain = 0.35f;
        [SerializeField, Range(0f, 1f)] private float dialogueGain = 0.65f;
        [Header("Ambience (optional; silent when unassigned)")]
        [Tooltip("Story §3 PROLOGUE: distant techno bass gives way to wind on Pohorje. Lasts as long as the intro.")]
        [SerializeField] private AudioClip ambience;
        [SerializeField] private AudioClip wakeUp;
        [SerializeField] private AudioClip armRustle;
        [SerializeField] private AudioClip watchCheck;
        [Tooltip("Starts with the Watch shot: a pulse that races from 07:58 on, jolts on the eye pops and ends in the sprint.")]
        [SerializeField] private AudioClip heartbeat;
        [Tooltip("The Scramble shot: gasp, bottle kick, footfalls, skid, scooter clank and the leap.")]
        [SerializeField] private AudioClip scramble;
        [SerializeField, Range(0f, 1f)] private float ambienceGain = 0.6f;
        [SerializeField, Range(0f, 1f)] private float foleyGain = 0.5f;
        [SerializeField, Range(0f, 1f)] private float heartbeatGain = 0.45f;
        [SerializeField, Min(0.05f)] private float ambienceFadeSeconds = 0.5f;
        [SerializeField] private InputActionReference skipAction;
        [SerializeField] private InputActionReference replayAction;
        [SerializeField] private bool playOnStart = true;

        private RunSession session;
        private bool ownsSequencePause;
        private IntroTimeline timeline;
        private bool restCaptured;
        private float baseFieldOfView;
        private Vector3 scooterRestPosition;
        private Quaternion scooterRestRotation;
        private Vector3 bottleRestPosition;
        private Quaternion bottleRestRotation;
        private float lastElapsed;
        private float objectiveRemaining;
        private bool ownsSkip;
        private bool ownsReplay;
        private AudioSource ambienceSource;
        private bool ambienceFading;

        public static int ShotCount => IntroActing.ShotSeconds.Length;

        private void OnEnable()
        {
            if (shotCamera == null || stage == null || overlay == null || wakeActors == null || boki == null
                || !boki.IsWired || fallenScooter == null || bottle == null || rider == null
                || riderRoot == null || followPivot == null || headlight == null || controls == null
                || audioSource == null || phoneBeep == null || eyePop == null || dashWhoosh == null || bokiLine == null
                || skipAction == null || skipAction.action == null
                || System.Array.IndexOf(controls, null) >= 0)
            {
                Debug.LogError("Assign every IntroCutscene reference.", this);
                enabled = false;
                return;
            }
            ownsSkip = EnableAction(skipAction);
            if (replayAction != null && replayAction.action != null) ownsReplay = EnableAction(replayAction);
        }

        private void OnDisable()
        {
            if (audioSource != null) audioSource.Stop();
            if (ambienceSource != null) ambienceSource.Stop();
            ambienceFading = false;
            if (timeline != null) Unsubscribe(timeline);
            ReleasePause();
            RestoreCanvases();
            if (ownsSkip) skipAction.action.Disable();
            if (ownsReplay) replayAction.action.Disable();
            ownsSkip = ownsReplay = false;
        }

        private void Start()
        {
            if (playOnStart) Play();
            else
            {
                wakeActors.SetActive(false);
                overlay.gameObject.SetActive(false);
            }
        }

        private static bool EnableAction(InputActionReference reference)
        {
            bool owned = !reference.action.enabled;
            if (owned) reference.action.Enable();
            return owned;
        }

        public void Play()
        {
            if (timeline != null && !timeline.IsFinished) return;
            if (timeline != null) Unsubscribe(timeline);
            overlay.gameObject.SetActive(true);
            session = RunSession.Ensure();
            ownsSequencePause = !session.State.Pause.Contains(PauseReason.ScriptedSequence);
            if (ownsSequencePause) session.SetPaused(PauseReason.ScriptedSequence, true);
            foreach (Behaviour control in controls) control.enabled = false;
            headlight.enabled = false;
            canvasStates = new bool[gameplayCanvases.Length];
            for (int i = 0; i < gameplayCanvases.Length; i++)
            {
                if (gameplayCanvases[i] == null) continue;
                canvasStates[i] = gameplayCanvases[i].enabled;
                gameplayCanvases[i].enabled = false;
            }
            objectiveRemaining = 0f;
            lastElapsed = 0f;
            timeline = new IntroTimeline(IntroActing.ShotSeconds);
            timeline.ShotStarted += OnShotStarted;
            timeline.Finished += OnFinished;
            StartAmbience();
            timeline.Begin();
            ShowFrame(IntroActing.Wake, 0f);
        }

        private void StartAmbience()
        {
            ambienceFading = false;
            if (ambience == null) return;
            if (ambienceSource == null)
            {
                ambienceSource = gameObject.AddComponent<AudioSource>();
                ambienceSource.playOnAwake = false;
                ambienceSource.loop = false;
                ambienceSource.spatialBlend = 0f;
            }
            ambienceSource.clip = ambience;
            ambienceSource.volume = ambienceGain;
            ambienceSource.Play();
        }

        // Skipping or finishing lets the bed die away instead of cutting it. Unscaled time: the gameplay pause is released by then.
        private void FadeAmbience()
        {
            if (!ambienceFading || ambienceSource == null) return;
            ambienceSource.volume = Mathf.MoveTowards(ambienceSource.volume, 0f,
                ambienceGain / ambienceFadeSeconds * Time.unscaledDeltaTime);
            if (ambienceSource.volume > 0f) return;
            ambienceSource.Stop();
            ambienceFading = false;
        }

        private void PlayFoley(AudioClip clip)
        {
            if (clip != null) audioSource.PlayOneShot(clip, foleyGain);
        }

        private void Unsubscribe(IntroTimeline target)
        {
            target.ShotStarted -= OnShotStarted;
            target.Finished -= OnFinished;
        }

        private void Update()
        {
            FadeAmbience();
            if (timeline == null || timeline.IsFinished)
            {
                if (replayAction != null && replayAction.action != null && replayAction.action.WasPressedThisFrame())
                    Play();
                else if (timeline != null) TickObjective();
                return;
            }
            if (skipAction.action.WasPressedThisFrame()) timeline.TrySkip();
            else
            {
                // A long first frame after a scene load must not swallow the opening shot.
                timeline.Advance(Mathf.Min(Time.unscaledDeltaTime, 0.1f));
                // Finishing already showed the end state; another frame would bring the letterbox back.
                if (timeline.IsFinished) return;
                PlayCues(lastElapsed, timeline.Elapsed);
                lastElapsed = timeline.Elapsed;
                ShowFrame(timeline.ShotIndex, timeline.ShotProgress);
            }
        }

        private void TickObjective()
        {
            if (objectiveRemaining <= 0f) return;
            objectiveRemaining -= Time.deltaTime;
            if (objectiveRemaining <= 0f) overlay.SetObjective(false);
        }

        private void OnShotStarted(int shot)
        {
            if (shot == IntroActing.Wake) PlayFoley(wakeUp);
            if (shot == IntroActing.Stir) PlayFoley(armRustle);
            if (shot == IntroActing.Watch)
            {
                PlayFoley(watchCheck);
                if (heartbeat != null) audioSource.PlayOneShot(heartbeat, heartbeatGain);
            }
            if (shot == IntroActing.Scramble) PlayFoley(scramble);
            if (shot == IntroActing.Message) audioSource.PlayOneShot(phoneBeep, effectsGain);
            if (shot == IntroActing.Line) audioSource.PlayOneShot(bokiLine, dialogueGain * 2.5f);
            if (shot == IntroActing.Mount) headlight.enabled = true;
        }

        // Sounds that fall inside a shot are placed on the same clock as the poses in IntroActing.
        private void PlayCues(float from, float to)
        {
            if (Crossed(from, to, IntroActing.StartOf(IntroActing.DoubleTake) + IntroActing.FirstPopAt))
                audioSource.PlayOneShot(eyePop, effectsGain * 0.8f);
            if (Crossed(from, to, IntroActing.StartOf(IntroActing.Message) + IntroActing.SecondPopAt))
                audioSource.PlayOneShot(eyePop, effectsGain);
            if (Crossed(from, to, IntroActing.StartOf(IntroActing.Scramble) + IntroActing.DashAt))
                audioSource.PlayOneShot(dashWhoosh, effectsGain * 0.8f);
        }

        private static bool Crossed(float from, float to, float at) => from < at && at <= to;

        private void ReleasePause()
        {
            if (ownsSequencePause && session != null) session.SetPaused(PauseReason.ScriptedSequence, false);
            ownsSequencePause = false;
        }

        private void RestoreCanvases()
        {
            if (canvasStates == null) return;
            for (int i = 0; i < gameplayCanvases.Length; i++)
                if (gameplayCanvases[i] != null) gameplayCanvases[i].enabled = canvasStates[i];
            canvasStates = null;
        }

        private void OnFinished()
        {
            ReleasePause();
            RestoreCanvases();
            audioSource.Stop();
            ambienceFading = ambienceSource != null && ambienceSource.isPlaying;
            ShowFrame(IntroActing.Mount, 1f);
            overlay.SetFade(0f);
            overlay.SetFlash(0f);
            overlay.SetLetterbox(false);
            overlay.SetSkipHint(false);
            overlay.SetObjective(true);
            headlight.enabled = true;
            objectiveRemaining = objectiveSeconds;
            // The camera already sits on the follow pose, so ThirdPersonCamera adopts it without a jump.
            foreach (Behaviour control in controls) control.enabled = true;
            var playerAudio = riderRoot.GetComponent<Boki.Player.PlayerAudio>();
            if (playerAudio != null) playerAudio.QueueDepartureLine();
        }

        // Pure pose for one moment of the sequence. Update calls it every frame; the Editor capture tool reuses it.
        public void ShowFrame(int shot, float progress)
        {
            CaptureRest();
            shot = Mathf.Clamp(shot, 0, IntroActing.ShotSeconds.Length - 1);
            float p = Mathf.Clamp01(progress);
            float seconds = p * IntroActing.ShotSeconds[shot];
            float e = Mathf.SmoothStep(0f, 1f, p);
            bool riding = shot == IntroActing.Mount;
            wakeActors.SetActive(!riding);
            rider.SetActive(riding);

            IntroActing.Beat beat = IntroActing.Sample(shot, seconds);
            PoseActors(beat);
            PlaceCamera(shot, seconds, p, e, beat);
            boki.ShowCues(beat.Pose, shotCamera.transform.rotation);

            float fade = shot == IntroActing.Wake ? 1f - Ramp(p, 0f, 0.2f)
                : shot == IntroActing.Line ? Ramp(p, 0.78f, 1f)
                : shot == IntroActing.Scramble ? 1f - Ramp(seconds, 0f, 0.25f)
                : 0f;
            float flash = riding ? 0.7f * IntroMotion.Impact(seconds, 12f) : beat.Flash;
            float readout = shot == IntroActing.Watch ? Ramp(p, 0.15f, 0.9f) : 0f;
            overlay.SetFade(fade);
            overlay.SetFlash(flash);
            overlay.SetWatchReadout(readout);
            overlay.SetLetterbox(true);
            overlay.SetSkipHint(true);
            overlay.SetObjective(riding);
            if (shot == IntroActing.Message) overlay.SetSubtitle(PhoneMessage, Lcd);
            else if (shot == IntroActing.Line) overlay.SetSubtitle(BokiLine, Paper);
            else overlay.SetSubtitle(string.Empty, Paper);
        }

        private void PoseActors(IntroActing.Beat beat)
        {
            boki.transform.SetPositionAndRotation(stage.TransformPoint(beat.Position),
                stage.rotation * Quaternion.Euler(-90f * beat.Lie, beat.Yaw, 0f));
            boki.Apply(beat.Pose);

            float upright = beat.ScooterUpright;
            Vector3 standing = new Vector3(IntroActing.ScooterSpot.x, 0f, IntroActing.ScooterSpot.z);
            fallenScooter.localPosition = Vector3.LerpUnclamped(scooterRestPosition, standing, upright);
            fallenScooter.localRotation = Quaternion.SlerpUnclamped(scooterRestRotation, Quaternion.identity, upright);

            float kick = beat.BottleKick;
            if (kick <= 0f)
            {
                bottle.SetLocalPositionAndRotation(bottleRestPosition, bottleRestRotation);
                return;
            }
            // Ballistic hop away from the sit-up, then it rolls to rest on the ground.
            Vector3 velocity = new Vector3(2.2f, 3.4f, 0.8f);
            float t = Mathf.Min(kick, 1.4f);
            Vector3 position = bottleRestPosition + velocity * t + Vector3.down * (4.9f * t * t);
            position.y = Mathf.Max(position.y, bottleRestPosition.y);
            bottle.SetLocalPositionAndRotation(position, bottleRestRotation * Quaternion.Euler(0f, 540f * t, 300f * t));
        }

        private void PlaceCamera(int shot, float seconds, float p, float e, IntroActing.Beat beat)
        {
            Vector3 face = boki.Face.position;
            Vector3 hand = boki.Hand.position;
            Vector3 position;
            Vector3 target;
            switch (shot)
            {
                case IntroActing.Wake:
                    // Starts low at Boki's side, in the grass, then rises and pulls back to the skyline.
                    position = Vector3.Lerp(Local(1.5f, 0.2f, -0.6f), Local(1.25f, 1.1f, -2.8f), e);
                    target = Vector3.Lerp(Local(-0.2f, 0.3f, 0.5f), Local(0f, 0.55f, 8f), e);
                    break;
                case IntroActing.Stir:
                    position = Vector3.Lerp(Local(1.25f, 1.1f, -2.8f), face + Axes(-1f, 0.65f, 0.6f), e);
                    // The view drops from the skyline to Boki early, so the arm raise is on screen.
                    target = Vector3.Lerp(Local(0f, 0.55f, 8f), Vector3.Lerp(face, hand, 0.5f), Ramp(p, 0f, 0.5f));
                    break;
                case IntroActing.Watch:
                    position = Vector3.Lerp(face + Axes(-1f, 0.65f, 0.6f), hand + Axes(-0.3f, 0.38f, 0.1f), e);
                    target = Vector3.Lerp(Vector3.Lerp(face, hand, 0.5f), hand, e);
                    break;
                case IntroActing.DoubleTake:
                    FaceShot(face, Mathf.Lerp(0.78f, 0.66f, e), out position, out target);
                    break;
                case IntroActing.Message:
                    FaceShot(face, Mathf.Lerp(0.66f, 0.56f, e), out position, out target);
                    break;
                case IntroActing.Line:
                    FaceShot(face, Mathf.Lerp(0.56f, 0.42f, e), out position, out target);
                    break;
                case IntroActing.Scramble:
                    position = Local(3.6f, 1.3f, 0.2f);
                    target = Vector3.Lerp(Local(-0.2f, 0.9f, 1.3f), stage.TransformPoint(beat.Position + Vector3.up * 0.9f), 0.55f);
                    break;
                default:
                    PlaceOrbit(e);
                    ApplyPunch(0f, seconds);
                    return;
            }
            shotCamera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(target - position, Vector3.up));
            ApplyPunch(beat.Punch, seconds);
        }

        // Boki lies face up, so the face is seen from above and from the feet side, the right way up on screen.
        private void FaceShot(Vector3 face, float distance, out Vector3 position, out Vector3 target)
        {
            position = face + stage.forward * (0.5f * distance) + Vector3.up * (0.8f * distance);
            target = face - stage.forward * 0.1f + Vector3.up * 0.03f;
        }

        // A hit squeezes the view in and rattles it, then both die away.
        private void ApplyPunch(float punch, float seconds)
        {
            Transform view = shotCamera.transform;
            if (punch > 0f)
            {
                float rattle = 0.012f * punch;
                view.position += view.right * (rattle * Mathf.Sin(seconds * 71f)) + view.up * (rattle * Mathf.Sin(seconds * 83f));
            }
            shotCamera.fieldOfView = baseFieldOfView * (1f - 0.22f * punch);
        }

        private Vector3 Local(float x, float y, float z) => stage.TransformPoint(x, y, z);
        private Vector3 Axes(float x, float y, float z) => stage.TransformVector(x, y, z);

        private static float Ramp(float value, float from, float to)
            => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(from, to, value));

        // Swing from in front of Boki to the follow pose behind him, ending exactly on ThirdPersonCamera's rest pose.
        private void PlaceOrbit(float e)
        {
            Quaternion look = Quaternion.Euler(Mathf.Lerp(8f, 12f, e), riderRoot.eulerAngles.y + Mathf.Lerp(150f, 0f, e), 0f);
            shotCamera.transform.SetPositionAndRotation(
                followPivot.position + look * Vector3.back * Mathf.Lerp(3.4f, followDistance, e), look);
        }

        // Rest poses are read once, while the actors still hold their authored placement.
        private void CaptureRest()
        {
            if (restCaptured) return;
            restCaptured = true;
            baseFieldOfView = shotCamera.fieldOfView;
            scooterRestPosition = fallenScooter.localPosition;
            scooterRestRotation = fallenScooter.localRotation;
            bottleRestPosition = bottle.localPosition;
            bottleRestRotation = bottle.localRotation;
        }
    }
}
