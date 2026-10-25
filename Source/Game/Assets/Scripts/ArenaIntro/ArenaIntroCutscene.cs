using System;
using Boki.Battery;
using Boki.Core;
using Boki.Intro;
using Boki.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Boki.ArenaIntro
{
    // Story §3 LEVEL 1, the first arena: when Boki first rides into a combat space he hears something in the dark and
    // freezes (»Kaj je to blo?«), and an enemy creeps out of the dark toward him; then the arena floods as usual. The
    // players' introduction to the enemies. It holds PauseReason.ScriptedSequence, so RouteEncounter places nothing
    // until it ends. Skipping jumps to the same end state as playing through: the scene exactly as it was, resumed.
    [DefaultExecutionOrder(-50)]
    [DisallowMultipleComponent]
    public sealed class ArenaIntroCutscene : MonoBehaviour
    {
        private static readonly Color Paper = new Color(0.91f, 0.89f, 0.86f);
        private static readonly int PursuitState = Animator.StringToHash("Melee_Pursuit");
        private static readonly float[] LaneLengths = { 22f, 17f, 13f };
        private static readonly float[] LaneTurns = { 0f, 20f, -20f, 40f, -40f, 60f, -60f, 90f, -90f, 135f, -135f, 180f };
        private const string BokiSays = "»Kaj je to blo?«";
        private const uint RiderRenderingLayer = 2u;
        private const float LaneChestHeight = 1.2f;
        private const float LaneRadius = 0.4f;
        private const float FlashFadeSeconds = 0.35f;

        [Header("Trigger")]
        [Tooltip("The combat spaces; the first one Boki occupies starts the scene.")]
        [SerializeField] private BatteryCombatSpace[] arenas = Array.Empty<BatteryCombatSpace>();
        [Tooltip("Environment layers the enemy's lane and the shot camera must stay clear of; exclude the player layer.")]
        [SerializeField] private LayerMask obstructionMask = 1;

        [Header("Stage")]
        [Tooltip("Floor point under Boki. +Z is the lane the enemy comes down; it is turned toward clear ground when the scene starts.")]
        [SerializeField] private Transform stage;
        [SerializeField] private GameObject actors;
        [SerializeField] private IntroBoki boki;
        [SerializeField] private Light stageLight;
        [SerializeField] private Transform creeper;
        [SerializeField] private Animator creeperAnimator;
        [SerializeField] private AudioSource creeperVoice;
        [SerializeField] private AudioSource creeperSteps;
        [SerializeField] private ArenaIntroOverlay overlay;

        [Header("Scene")]
        [SerializeField] private Camera shotCamera;
        [SerializeField] private Transform riderRoot;
        [Tooltip("The rider's visuals, replaced by the close-up Boki for the face shots.")]
        [SerializeField] private GameObject rider;
        [Tooltip("Disabled while the scene plays and enabled again afterwards.")]
        [SerializeField] private Behaviour[] controls = Array.Empty<Behaviour>();
        [SerializeField] private Canvas[] gameplayCanvases = Array.Empty<Canvas>();
        [Tooltip("Optional: spends the enemy greeting the scene plays, so the first real enemy does not repeat it.")]
        [SerializeField] private RecordedGameAudio recordedAudio;

        [Header("Audio and input")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip rustle;
        [SerializeField] private AudioClip eyePop;
        [SerializeField] private AudioClip bokiLine;
        [SerializeField] private AudioClip greeting;
        [SerializeField] private AudioClip step;
        [SerializeField] private AudioClip stinger;
        [SerializeField, Range(0f, 1f)] private float foleyGain = 0.8f;
        [SerializeField, Range(0f, 1f)] private float effectsGain = 0.35f;
        [SerializeField, Range(0f, 1f)] private float dialogueGain = 0.65f;
        [SerializeField, Range(0f, 1f)] private float greetingGain = 0.7f;
        [SerializeField, Range(0f, 1f)] private float stepGain = 0.8f;
        [SerializeField, Range(0f, 1f)] private float stingerGain = 0.9f;
        [SerializeField] private InputActionReference skipAction;
        [Tooltip("Optional replay for the preview scene; a game scene leaves it empty so it plays once, on first entry.")]
        [SerializeField] private InputActionReference replayAction;

        private RunSession session;
        private bool ownsSequencePause;
        private IntroTimeline timeline;
        private bool played;
        private bool[] canvasStates;
        private float lastElapsed;
        private float laneStart;
        private float flashAlpha;
        private Vector3 restCameraPosition;
        private Quaternion restCameraRotation;
        private Quaternion restRiderRotation;
        private float baseFieldOfView;
        private bool ownsSkip;
        private bool ownsReplay;
        private bool puppetLit;

        public bool IsPlaying => timeline != null && !timeline.IsFinished;

        private void OnEnable()
        {
            if (arenas == null || arenas.Length == 0 || Array.IndexOf(arenas, null) >= 0 || stage == null || actors == null
                || boki == null || !boki.IsWired || stageLight == null || creeper == null || creeperAnimator == null
                || creeperVoice == null || creeperSteps == null || overlay == null || shotCamera == null
                || riderRoot == null || rider == null || controls == null || Array.IndexOf(controls, null) >= 0
                || gameplayCanvases == null || audioSource == null || rustle == null || eyePop == null || bokiLine == null
                || greeting == null || step == null || stinger == null || skipAction == null || skipAction.action == null)
            {
                Debug.LogError("Assign every ArenaIntroCutscene reference.", this);
                enabled = false;
                return;
            }
            ownsSkip = EnableAction(skipAction);
            if (replayAction != null && replayAction.action != null) ownsReplay = EnableAction(replayAction);
        }

        private void OnDisable()
        {
            if (timeline != null)
            {
                timeline.Finished -= OnFinished;
                if (!timeline.IsFinished) Restore();
            }
            if (ownsSkip) skipAction.action.Disable();
            if (ownsReplay) replayAction.action.Disable();
            ownsSkip = ownsReplay = false;
        }

        private void Start()
        {
            actors.SetActive(false);
            overlay.gameObject.SetActive(false);
        }

        private static bool EnableAction(InputActionReference reference)
        {
            bool owned = !reference.action.enabled;
            if (owned) reference.action.Enable();
            return owned;
        }

        private void Update()
        {
            if (timeline == null || timeline.IsFinished)
            {
                FadeFlash();
                if (replayAction != null && replayAction.action != null && replayAction.action.WasPressedThisFrame()) Play();
                else if (!played) TryTrigger();
                return;
            }
            if (skipAction.action.WasPressedThisFrame()) timeline.TrySkip();
            else
            {
                // A long first frame after a scene load must not swallow the opening shot.
                timeline.Advance(Mathf.Min(Time.unscaledDeltaTime, 0.1f));
                // Finishing already restored the scene; another frame would undo it.
                if (timeline.IsFinished) return;
                PlayCues(lastElapsed, timeline.Elapsed);
                lastElapsed = timeline.Elapsed;
                ShowFrame(timeline.ShotIndex, timeline.ShotProgress);
            }
        }

        public void Skip()
        {
            if (IsPlaying) timeline.TrySkip();
        }

        // Runs before RouteEncounter (order 0): the scene holds the pause before the opening group's first enemy is placed.
        private void TryTrigger()
        {
            RunSession active = RunSession.Instance;
            if (active == null || !active.IsGameplayActive || active.HasEnteredBossArena) return;
            foreach (BatteryCombatSpace arena in arenas)
                if (arena.isActiveAndEnabled && arena.State.Occupied)
                {
                    Play();
                    return;
                }
        }

        // Starts the scene. A scene with no clear ground for the enemy to come down finishes at once rather than stall the run.
        public void Play()
        {
            if (IsPlaying) return;
            played = true;
            if (!PrepareStage()) return;
            restCameraPosition = shotCamera.transform.position;
            restCameraRotation = shotCamera.transform.rotation;
            baseFieldOfView = shotCamera.fieldOfView;
            restRiderRotation = riderRoot.rotation;
            LightPuppet();

            session = RunSession.Ensure();
            ownsSequencePause = !session.State.Pause.Contains(PauseReason.ScriptedSequence);
            if (ownsSequencePause) session.SetPaused(PauseReason.ScriptedSequence, true);
            foreach (Behaviour control in controls) control.enabled = false;
            canvasStates = new bool[gameplayCanvases.Length];
            for (int i = 0; i < gameplayCanvases.Length; i++)
            {
                if (gameplayCanvases[i] == null) continue;
                canvasStates[i] = gameplayCanvases[i].enabled;
                gameplayCanvases[i].enabled = false;
            }
            actors.SetActive(true);
            overlay.gameObject.SetActive(true);
            lastElapsed = flashAlpha = 0f;
            if (timeline != null) timeline.Finished -= OnFinished;
            timeline = new IntroTimeline(ArenaIntroActing.ShotSeconds);
            timeline.Finished += OnFinished;
            timeline.Begin();
            ShowFrame(ArenaIntroActing.Hush, 0f);
        }

        // The stage faces the first clear lane found, preferring the way the player is looking and the longest approach.
        private bool PrepareStage()
        {
            Vector3 origin = riderRoot.position;
            if (GroundAt(origin, out float ground)) origin.y = ground;
            Vector3 facing = Vector3.ProjectOnPlane(shotCamera.transform.forward, Vector3.up);
            if (facing.sqrMagnitude < 0.01f) facing = Vector3.ProjectOnPlane(riderRoot.forward, Vector3.up);
            facing.Normalize();
            foreach (float length in LaneLengths)
                foreach (float turn in LaneTurns)
                {
                    Vector3 direction = Quaternion.AngleAxis(turn, Vector3.up) * facing;
                    if (!LaneIsClear(origin, direction, length)) continue;
                    stage.SetPositionAndRotation(origin, Quaternion.LookRotation(direction, Vector3.up));
                    laneStart = length;
                    return true;
                }
            Debug.LogWarning("ArenaIntroCutscene found no clear lane for the enemy and was skipped.", this);
            return false;
        }

        private bool LaneIsClear(Vector3 origin, Vector3 direction, float length)
        {
            // Start past Boki's own body, then require open space along the lane and ground under the enemy's whole approach.
            const float clearance = 0.7f;
            Vector3 chest = origin + direction * clearance + Vector3.up * LaneChestHeight;
            if (Physics.SphereCast(chest, LaneRadius, direction, out _, length - clearance, obstructionMask, QueryTriggerInteraction.Ignore))
                return false;
            for (float distance = ArenaIntroActing.StopDistance; distance <= length + 0.01f; distance += 4f)
                if (!GroundAt(origin + direction * distance, out float height) || Mathf.Abs(height - origin.y) > 2.5f) return false;
            return true;
        }

        private bool GroundAt(Vector3 point, out float height)
        {
            bool found = Physics.Raycast(point + Vector3.up * 4f, Vector3.down, out RaycastHit hit, 12f, obstructionMask,
                QueryTriggerInteraction.Ignore);
            height = found ? hit.point.y : point.y;
            return found;
        }

        // The close-up Boki is lit like the rider: only by the rider-layer lights, not the broad headlight spill.
        private void LightPuppet()
        {
            if (puppetLit) return;
            puppetLit = true;
            foreach (Renderer renderer in boki.GetComponentsInChildren<Renderer>(true))
                renderer.renderingLayerMask = RiderRenderingLayer;
        }

        // Sounds that fall inside a shot are placed on the same clock as the poses in ArenaIntroActing.
        private void PlayCues(float from, float to)
        {
            if (Crossed(from, to, ArenaIntroActing.StartOf(ArenaIntroActing.Hush) + ArenaIntroActing.RustleAt))
                audioSource.PlayOneShot(rustle, foleyGain);
            if (Crossed(from, to, ArenaIntroActing.StartOf(ArenaIntroActing.Startle) + ArenaIntroActing.EyePopAt))
                audioSource.PlayOneShot(eyePop, effectsGain);
            if (Crossed(from, to, ArenaIntroActing.StartOf(ArenaIntroActing.Line) + ArenaIntroActing.BokiVoiceAt))
                audioSource.PlayOneShot(bokiLine, dialogueGain);
            if (ArenaIntroActing.Approach.Footfalls(ArenaIntroActing.CreepSeconds(to))
                > ArenaIntroActing.Approach.Footfalls(ArenaIntroActing.CreepSeconds(from)))
                creeperSteps.PlayOneShot(step, stepGain * UnityEngine.Random.Range(0.75f, 1f));
            if (Crossed(from, to, ArenaIntroActing.StartOf(ArenaIntroActing.Look) + ArenaIntroActing.CreeperVoiceAt))
            {
                creeperVoice.PlayOneShot(greeting, greetingGain);
                if (recordedAudio != null) recordedAudio.MarkEnemyGreetingPlayed();
            }
            if (Crossed(from, to, ArenaIntroActing.StartOf(ArenaIntroActing.Lunge) + ArenaIntroActing.StingerAt))
                audioSource.PlayOneShot(stinger, stingerGain);
        }

        private static bool Crossed(float from, float to, float at) => from < at && at <= to;

        private void OnFinished()
        {
            bool skipped = timeline.WasSkipped;
            Restore();
            if (skipped) audioSource.Stop();
            overlay.SetLetterbox(false);
            overlay.SetSkipHint(false);
            overlay.SetSubtitle(string.Empty, Paper);
            // The white frame of the lunge carries over the cut into the fight and fades there. A skip has none.
            flashAlpha = skipped ? 0f : ArenaIntroActing.Sample(ArenaIntroActing.Lunge, ArenaIntroActing.ShotSeconds[ArenaIntroActing.Lunge], laneStart).Flash;
            overlay.SetFlash(flashAlpha);
            overlay.gameObject.SetActive(flashAlpha > 0f);
        }

        private void FadeFlash()
        {
            if (flashAlpha <= 0f) return;
            flashAlpha = Mathf.MoveTowards(flashAlpha, 0f, Time.unscaledDeltaTime / FlashFadeSeconds);
            overlay.SetFlash(flashAlpha);
            if (flashAlpha <= 0f) overlay.gameObject.SetActive(false);
        }

        // The scene exactly as it was before the sequence, with the controls back on and gameplay resumed.
        private void Restore()
        {
            if (actors != null) actors.SetActive(false);
            if (rider != null) rider.SetActive(true);
            if (riderRoot != null) riderRoot.rotation = restRiderRotation;
            if (shotCamera != null)
            {
                shotCamera.transform.SetPositionAndRotation(restCameraPosition, restCameraRotation);
                shotCamera.fieldOfView = baseFieldOfView;
            }
            foreach (Behaviour control in controls)
                if (control != null) control.enabled = true;
            if (canvasStates != null)
                for (int i = 0; i < gameplayCanvases.Length; i++)
                    if (gameplayCanvases[i] != null) gameplayCanvases[i].enabled = canvasStates[i];
            canvasStates = null;
            if (ownsSequencePause && session != null) session.SetPaused(PauseReason.ScriptedSequence, false);
            ownsSequencePause = false;
        }

        // Pure pose for one moment of the sequence. Update calls it every frame; the Editor capture tool reuses it.
        public void ShowFrame(int shot, float progress)
        {
            shot = Mathf.Clamp(shot, 0, ArenaIntroActing.ShotSeconds.Length - 1);
            float seconds = Mathf.Clamp01(progress) * ArenaIntroActing.ShotSeconds[shot];
            ArenaIntroActing.Beat beat = ArenaIntroActing.Sample(shot, seconds, laneStart);

            rider.SetActive(!beat.Puppet);
            boki.gameObject.SetActive(beat.Puppet);
            stageLight.enabled = beat.Puppet;
            riderRoot.rotation = beat.RiderFacesLane ? stage.rotation : restRiderRotation;
            PlaceCreeper(beat);
            PlaceCamera(beat, seconds);
            if (beat.Puppet)
            {
                boki.transform.SetPositionAndRotation(stage.position, stage.rotation);
                boki.Apply(beat.Boki);
                boki.ShowCues(beat.Boki, shotCamera.transform.rotation);
            }

            overlay.SetLetterbox(true);
            overlay.SetSkipHint(true);
            overlay.SetFlash(beat.Flash);
            overlay.SetSubtitle(beat.Subtitle ? BokiSays : string.Empty, Paper);
        }

        // Faces Boki and walks the pursuit cycle by distance covered, so each lurch is one stride and every rest is the same pose.
        private void PlaceCreeper(in ArenaIntroActing.Beat beat)
        {
            creeper.gameObject.SetActive(beat.CreeperVisible);
            if (!beat.CreeperVisible) return;
            Vector3 position = stage.TransformPoint(0f, 0f, beat.CreeperDistance);
            if (GroundAt(position, out float height)) position.y = height;
            creeper.SetPositionAndRotation(position, stage.rotation * Quaternion.Euler(0f, 180f, 0f));
            creeperAnimator.Play(PursuitState, 0, Mathf.Repeat(beat.CreeperStride, 1f));
            creeperAnimator.Update(0f);
        }

        private void PlaceCamera(in ArenaIntroActing.Beat beat, float seconds)
        {
            Vector3 position = Unobstructed(stage.TransformPoint(0f, 1.5f, 0f), stage.TransformPoint(beat.Camera));
            Vector3 target = stage.TransformPoint(beat.Target);
            Quaternion rotation = Quaternion.LookRotation(target - position, Vector3.up);
            // The first shot starts on the gameplay camera, so the cut-in has no jump.
            position = Vector3.Lerp(position, restCameraPosition, beat.GameplayBlend);
            rotation = Quaternion.Slerp(rotation, restCameraRotation, beat.GameplayBlend);
            Transform view = shotCamera.transform;
            view.SetPositionAndRotation(position, rotation);
            // A hit squeezes the view in and rattles it, then both die away.
            if (beat.Punch > 0f)
            {
                float rattle = 0.012f * beat.Punch;
                view.position += view.right * (rattle * Mathf.Sin(seconds * 71f)) + view.up * (rattle * Mathf.Sin(seconds * 83f));
            }
            shotCamera.fieldOfView = Mathf.Lerp(beat.Fov, baseFieldOfView, beat.GameplayBlend) * (1f - 0.22f * beat.Punch);
        }

        // Keeps the shot camera out of trees and walls, as the follow camera does.
        private Vector3 Unobstructed(Vector3 from, Vector3 desired)
        {
            Vector3 offset = desired - from;
            float distance = offset.magnitude;
            if (distance < 0.01f) return desired;
            Vector3 direction = offset / distance;
            if (Physics.SphereCast(from, 0.2f, direction, out RaycastHit hit, distance, obstructionMask, QueryTriggerInteraction.Ignore))
                return from + direction * Mathf.Max(0.3f, hit.distance - 0.1f);
            return desired;
        }
    }
}
