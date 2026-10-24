using Boki.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Boki.Intro
{
    // Story §3 PROLOGUE, minimum slice: shot 1 (wake-up), shot 2 (watch, 07:58) and shot 4 (scooter).
    // Shot 3 (VHS glitch and the black figure) is not built. Skipping jumps to the same end state as playing through.
    [DisallowMultipleComponent]
    public sealed class IntroCutscene : MonoBehaviour
    {
        private const int Wake = 0;
        private const int ArmRaise = 1;
        private const int WatchClose = 2;
        private const int Message = 3;
        private const int Line = 4;
        private const int Mount = 5;
        private static readonly float[] ShotSeconds = { 5f, 1.8f, 1.7f, 2.2f, 2f, 3f };
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
        [SerializeField] private Transform head;
        [SerializeField] private Transform leftUpperArm;
        [SerializeField] private Transform leftLowerArm;
        [SerializeField] private Transform leftHand;

        [Header("Handoff to gameplay")]
        [SerializeField] private GameObject rider;
        [SerializeField] private Transform riderRoot;
        [Tooltip("Same pivot as ThirdPersonCamera, so the last shot ends on its resting pose.")]
        [SerializeField] private Transform followPivot;
        [SerializeField, Min(0.5f)] private float followDistance = 5f;
        [SerializeField] private Behaviour headlight;
        [SerializeField] private Behaviour[] controls;
        [SerializeField, Min(0f)] private float objectiveSeconds = 6f;

        [Header("Audio and input")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip phoneBeep;
        [SerializeField] private InputActionReference skipAction;
        [SerializeField] private InputActionReference replayAction;
        [SerializeField] private bool playOnStart = true;

        private IntroTimeline timeline;
        private bool restCaptured;
        private Quaternion upperRest;
        private Quaternion lowerRest;
        private Quaternion upperRaise;
        private Quaternion lowerRaise;
        private Vector3 side;
        private Vector3 armCameraPosition;
        private Vector3 armCameraTarget;
        private float objectiveRemaining;
        private bool ownsSkip;
        private bool ownsReplay;

        public static int ShotCount => ShotSeconds.Length;

        private void OnEnable()
        {
            if (shotCamera == null || stage == null || overlay == null || wakeActors == null || head == null
                || leftUpperArm == null || leftLowerArm == null || leftHand == null || rider == null
                || riderRoot == null || followPivot == null || headlight == null || controls == null
                || audioSource == null || phoneBeep == null || skipAction == null || skipAction.action == null
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
            if (timeline != null) Unsubscribe(timeline);
            if (ownsSkip) skipAction.action.Disable();
            if (ownsReplay) replayAction.action.Disable();
            ownsSkip = ownsReplay = false;
        }

        private void Start()
        {
            if (playOnStart) Play();
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
            foreach (Behaviour control in controls) control.enabled = false;
            headlight.enabled = false;
            objectiveRemaining = 0f;
            timeline = new IntroTimeline(ShotSeconds);
            timeline.ShotStarted += OnShotStarted;
            timeline.Finished += OnFinished;
            timeline.Begin();
            ShowFrame(Wake, 0f);
        }

        private void Unsubscribe(IntroTimeline target)
        {
            target.ShotStarted -= OnShotStarted;
            target.Finished -= OnFinished;
        }

        private void Update()
        {
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
                timeline.Advance(Mathf.Min(Time.deltaTime, 0.1f));
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
            if (shot == Message) audioSource.PlayOneShot(phoneBeep);
            if (shot == Mount) headlight.enabled = true;
        }

        private void OnFinished()
        {
            audioSource.Stop();
            ShowFrame(Mount, 1f);
            overlay.SetFade(0f);
            overlay.SetLetterbox(false);
            overlay.SetSkipHint(false);
            overlay.SetObjective(true);
            headlight.enabled = true;
            objectiveRemaining = objectiveSeconds;
            // The camera already sits on the follow pose, so ThirdPersonCamera adopts it without a jump.
            foreach (Behaviour control in controls) control.enabled = true;
        }

        // Pure pose for one moment of the sequence. Update calls it every frame; the Editor capture tool reuses it.
        public void ShowFrame(int shot, float progress)
        {
            CaptureRest();
            shot = Mathf.Clamp(shot, 0, ShotSeconds.Length - 1);
            float p = Mathf.Clamp01(progress);
            float e = Mathf.SmoothStep(0f, 1f, p);
            bool riding = shot >= Mount;
            wakeActors.SetActive(!riding);
            rider.SetActive(riding);
            RaiseArm(shot < ArmRaise ? 0f : shot == ArmRaise ? e : 1f);

            Vector3 close = leftHand.position + Vector3.up * 0.4f - side * 0.1f;
            switch (shot)
            {
                case Wake:
                    PlaceCamera(Vector3.Lerp(Local(0.45f, 0.16f, -1.9f), Local(0.9f, 1.5f, -3.3f), e),
                        Vector3.Lerp(Local(0f, 0.35f, 0.9f), Local(0f, 0.7f, 14f), e));
                    break;
                case ArmRaise:
                    PlaceCamera(armCameraPosition, armCameraTarget);
                    break;
                case WatchClose:
                    PlaceCamera(Vector3.Lerp(armCameraPosition, close, e), Vector3.Lerp(armCameraTarget, leftHand.position, e));
                    break;
                case Message:
                    PlaceCamera(close, leftHand.position);
                    break;
                case Line:
                    PlaceCamera(Vector3.Lerp(close, head.position + Vector3.up * 0.85f + stage.forward * 0.25f, e),
                        Vector3.Lerp(leftHand.position, head.position, e));
                    break;
                default:
                    PlaceOrbit(e);
                    break;
            }

            float fade = shot == Wake ? 1f - Ramp(p, 0f, 0.25f)
                : shot == Line ? Ramp(p, 0.75f, 1f)
                : shot == Mount ? 1f - Ramp(p, 0f, 0.25f)
                : 0f;
            float readout = shot == WatchClose ? Ramp(p, 0.15f, 0.9f)
                : shot == Message ? 1f
                : shot == Line ? 1f - Ramp(p, 0f, 0.35f)
                : 0f;
            overlay.SetFade(fade);
            overlay.SetWatchReadout(readout);
            overlay.SetLetterbox(true);
            overlay.SetSkipHint(true);
            overlay.SetObjective(riding);
            if (shot == Message) overlay.SetSubtitle(PhoneMessage, Lcd);
            else if (shot == Line) overlay.SetSubtitle(BokiLine, Paper);
            else overlay.SetSubtitle(string.Empty, Paper);
        }

        private Vector3 Local(float x, float y, float z) => stage.TransformPoint(x, y, z);

        private static float Ramp(float value, float from, float to)
            => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(from, to, value));

        private void PlaceCamera(Vector3 position, Vector3 lookAt)
            => shotCamera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(lookAt - position, Vector3.up));

        // Swing from in front of Boki to the follow pose behind him, ending exactly on ThirdPersonCamera's rest pose.
        private void PlaceOrbit(float e)
        {
            Quaternion look = Quaternion.Euler(Mathf.Lerp(8f, 12f, e), riderRoot.eulerAngles.y + Mathf.Lerp(150f, 0f, e), 0f);
            shotCamera.transform.SetPositionAndRotation(
                followPivot.position + look * Vector3.back * Mathf.Lerp(3.4f, followDistance, e), look);
        }

        // Rest rotations are read once, while the actors still hold their authored pose.
        private void CaptureRest()
        {
            if (restCaptured) return;
            restCaptured = true;
            upperRest = leftUpperArm.rotation;
            lowerRest = leftLowerArm.rotation;
            Vector3 shoulder = leftUpperArm.position;
            Vector3 elbow = leftLowerArm.position;
            Vector3 wrist = leftHand.position;

            // Sideways direction from the body axis to the watch arm.
            Vector3 offset = shoulder - Local(0f, 0.15f, 0f);
            offset = Vector3.ProjectOnPlane(offset, stage.forward);
            offset.y = 0f;
            side = offset.sqrMagnitude > 1e-4f ? offset.normalized : stage.right;

            // World-space targets: elbow up and slightly out, forearm across the face (Boki lies face up).
            Vector3 towardHead = -stage.forward;
            Vector3 upperTarget = (Vector3.up * 0.85f + towardHead * 0.2f + side * 0.45f).normalized;
            Vector3 forearmTarget = (towardHead * 0.55f + Vector3.up * 0.35f - side * 0.75f).normalized;
            upperRaise = Quaternion.FromToRotation((elbow - shoulder).normalized, upperTarget);
            lowerRaise = Quaternion.FromToRotation(upperRaise * (wrist - elbow).normalized, forearmTarget);

            armCameraTarget = head.position + stage.forward * 0.3f + Vector3.up * 0.1f;
            armCameraPosition = armCameraTarget + side * 1.7f + Vector3.up * 0.6f + stage.forward * 0.5f;
        }

        private void RaiseArm(float amount)
        {
            Quaternion upper = Quaternion.Slerp(Quaternion.identity, upperRaise, amount);
            Quaternion lower = Quaternion.Slerp(Quaternion.identity, lowerRaise, amount);
            leftUpperArm.rotation = upper * upperRest;
            leftLowerArm.rotation = lower * upper * lowerRest;
        }
    }
}
