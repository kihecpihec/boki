using System;
using Boki.Core;
using Boki.Intro;
using Boki.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Boki.Ending
{
    // Story §3 FINAL BOSS victory: the red light goes out, the professor shrinks back to normal size, adjusts his glasses
    // and says it is Saturday; he points at a calendar showing 31 October and the zoom out reveals a cat. Then the two of
    // them, and the closing card. Skipping jumps to the same end state as playing through, and both call the callback once.
    [DisallowMultipleComponent]
    public sealed class EndingCutscene : MonoBehaviour
    {
        private static readonly Color Chalk = new Color(0.98f, 0.93f, 0.66f);
        private static readonly Color Paper = new Color(0.91f, 0.89f, 0.86f);
        private const string ProfessorSays = "»Boki ... danes je sobota.«";
        private const string BokiSays = "»... A?«";

        [Header("Stage")]
        [Tooltip("Floor point under the professor. +Z faces Boki and the camera.")]
        [SerializeField] private Transform stage;
        [SerializeField] private GameObject actors;
        [SerializeField] private EndingProfessor professor;
        [SerializeField] private IntroBoki boki;
        [SerializeField] private Transform calendar;
        [SerializeField] private Transform calendarMarker;
        [SerializeField] private GameObject stageLights;
        [SerializeField] private Light redLight;
        [SerializeField] private EndingOverlay overlay;

        [Header("Scene")]
        [SerializeField] private Camera shotCamera;
        [Tooltip("The rider and anything else the ending replaces with its own actors.")]
        [SerializeField] private GameObject[] hiddenDuringEnding = Array.Empty<GameObject>();
        [Tooltip("Disabled while the ending plays and left disabled afterwards.")]
        [SerializeField] private Behaviour[] controls = Array.Empty<Behaviour>();
        [SerializeField] private Canvas[] gameplayCanvases = Array.Empty<Canvas>();

        [Header("Audio and input")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip deflate;
        [SerializeField] private AudioClip zoom;
        [SerializeField] private AudioClip thump;
        [SerializeField] private AudioClip meow;
        [SerializeField] private AudioClip lick;
        [SerializeField] private AudioClip eyePop;
        [Tooltip("The recorded »Boki ... danes je sobota.« and Boki's recorded »... A?« takes; one is picked per playthrough.")]
        [SerializeField] private AudioClip professorVoice;
        [SerializeField] private AudioClip[] bokiVoices = Array.Empty<AudioClip>();
        [Header("Ambience and music (optional; silent when unassigned)")]
        [Tooltip("The empty classroom: a drone while the red light is on, then fluorescent hum, a wall clock and birds outside.")]
        [SerializeField] private AudioClip roomTone;
        [Tooltip("A short Saturday-morning tune that starts when the zoom out reveals the cat and resolves on the closing card.")]
        [SerializeField] private AudioClip reliefMusic;
        [SerializeField, Range(0f, 1f)] private float roomToneGain = 0.55f;
        [SerializeField, Range(0f, 1f)] private float musicGain = 0.6f;
        [SerializeField, Min(0.05f)] private float fadeOutSeconds = 0.8f;
        [SerializeField] private InputActionReference skipAction;
        [SerializeField] private InputActionReference replayAction;
        [SerializeField] private bool playOnStart;

        private RunSession session;
        private bool ownsSequencePause;
        private IntroTimeline timeline;
        private Action completed;
        private bool[] canvasStates;
        private bool restCaptured;
        private float baseFieldOfView;
        private float redRestIntensity;
        private Vector3 markerRestScale;
        private AudioClip bokiVoice;
        private float lastElapsed;
        private bool ownsSkip;
        private bool ownsReplay;
        private AudioSource roomSource;
        private AudioSource musicSource;
        private bool fadingOut;

        public bool IsPlaying => timeline != null && !timeline.IsFinished;

        private void OnEnable()
        {
            if (stage == null || actors == null || professor == null || !professor.IsWired || boki == null || !boki.IsWired
                || calendar == null || calendarMarker == null || stageLights == null || redLight == null || overlay == null
                || shotCamera == null || hiddenDuringEnding == null || controls == null || gameplayCanvases == null
                || audioSource == null || deflate == null || zoom == null || thump == null || meow == null || lick == null
                || eyePop == null || professorVoice == null || bokiVoices == null || bokiVoices.Length == 0
                || skipAction == null || skipAction.action == null || Array.IndexOf(bokiVoices, null) >= 0
                || Array.IndexOf(hiddenDuringEnding, null) >= 0 || Array.IndexOf(controls, null) >= 0
                || Array.IndexOf(gameplayCanvases, null) >= 0)
            {
                Debug.LogError("Assign every EndingCutscene reference.", this);
                enabled = false;
                return;
            }
            ownsSkip = EnableAction(skipAction);
            if (replayAction != null && replayAction.action != null) ownsReplay = EnableAction(replayAction);
        }

        private void OnDisable()
        {
            StopBed(roomSource);
            StopBed(musicSource);
            fadingOut = false;
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
            else Hide();
        }

        private static bool EnableAction(InputActionReference reference)
        {
            bool owned = !reference.action.enabled;
            if (owned) reference.action.Enable();
            return owned;
        }

        private void Hide()
        {
            actors.SetActive(false);
            stageLights.SetActive(false);
            overlay.gameObject.SetActive(false);
        }

        // onFinished runs once, after the ending has played or been skipped and the gameplay pause is released.
        // A cutscene that could not start (missing references) finishes at once so the run never stalls on it.
        public void Play(Action onFinished = null)
        {
            if (!isActiveAndEnabled)
            {
                onFinished?.Invoke();
                return;
            }
            if (IsPlaying) return;
            if (timeline != null) Unsubscribe(timeline);
            completed = onFinished;
            session = RunSession.Ensure();
            ownsSequencePause = !session.State.Pause.Contains(PauseReason.ScriptedSequence);
            if (ownsSequencePause) session.SetPaused(PauseReason.ScriptedSequence, true);
            foreach (Behaviour control in controls) control.enabled = false;
            foreach (GameObject hidden in hiddenDuringEnding) hidden.SetActive(false);
            canvasStates = new bool[gameplayCanvases.Length];
            for (int i = 0; i < gameplayCanvases.Length; i++)
            {
                canvasStates[i] = gameplayCanvases[i].enabled;
                gameplayCanvases[i].enabled = false;
            }
            actors.SetActive(true);
            stageLights.SetActive(true);
            overlay.gameObject.SetActive(true);
            lastElapsed = 0f;
            bokiVoice = bokiVoices[UnityEngine.Random.Range(0, bokiVoices.Length)];
            timeline = new IntroTimeline(EndingActing.ShotSeconds);
            timeline.Finished += OnFinished;
            fadingOut = false;
            StartBed(ref roomSource, roomTone, roomToneGain);
            StopBed(musicSource);
            timeline.Begin();
            ShowFrame(EndingActing.Loom, 0f);
        }

        // The beds are separate sources so the one-shots can be stopped, and the beds faded, independently.
        private void StartBed(ref AudioSource source, AudioClip clip, float gain)
        {
            if (clip == null) return;
            if (source == null)
            {
                source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = false;
                source.spatialBlend = 0f;
            }
            source.clip = clip;
            source.volume = gain;
            source.Play();
        }

        private static void StopBed(AudioSource source)
        {
            if (source != null) source.Stop();
        }

        // Unscaled time: the gameplay pause is released by the time the ending finishes.
        private void FadeBeds()
        {
            if (!fadingOut) return;
            float step = Time.unscaledDeltaTime / fadeOutSeconds;
            bool live = false;
            if (roomSource != null && roomSource.isPlaying)
            {
                roomSource.volume = Mathf.MoveTowards(roomSource.volume, 0f, roomToneGain * step);
                if (roomSource.volume <= 0f) roomSource.Stop();
                live |= roomSource.isPlaying;
            }
            if (musicSource != null && musicSource.isPlaying)
            {
                musicSource.volume = Mathf.MoveTowards(musicSource.volume, 0f, musicGain * step);
                if (musicSource.volume <= 0f) musicSource.Stop();
                live |= musicSource.isPlaying;
            }
            fadingOut = live;
        }

        public void Skip()
        {
            if (IsPlaying) timeline.TrySkip();
        }

        private void Unsubscribe(IntroTimeline target) => target.Finished -= OnFinished;

        private void Update()
        {
            FadeBeds();
            if (timeline == null || timeline.IsFinished)
            {
                if (replayAction != null && replayAction.action != null && replayAction.action.WasPressedThisFrame()) Play();
                return;
            }
            if (skipAction.action.WasPressedThisFrame()) timeline.TrySkip();
            else
            {
                // A long first frame after a scene load must not swallow the opening shot.
                timeline.Advance(Mathf.Min(Time.unscaledDeltaTime, 0.1f));
                // Finishing already showed the end state; another frame would undo it.
                if (timeline.IsFinished) return;
                PlayCues(lastElapsed, timeline.Elapsed);
                lastElapsed = timeline.Elapsed;
                ShowFrame(timeline.ShotIndex, timeline.ShotProgress);
            }
        }

        // Sounds that fall inside a shot are placed on the same clock as the poses in EndingActing.
        private void PlayCues(float from, float to)
        {
            if (Crossed(from, to, EndingActing.Loom, EndingActing.ShrinkAt)) audioSource.PlayOneShot(deflate);
            if (Crossed(from, to, EndingActing.Saturday, EndingActing.ProfessorVoiceAt)) audioSource.PlayOneShot(professorVoice);
            if (Crossed(from, to, EndingActing.Huh, EndingActing.BokiVoiceAt)) audioSource.PlayOneShot(bokiVoice);
            if (Crossed(from, to, EndingActing.ZoomIn, EndingActing.ZoomCueAt)) audioSource.PlayOneShot(zoom);
            if (Crossed(from, to, EndingActing.ZoomIn, EndingActing.ThumpAt)) audioSource.PlayOneShot(thump);
            if (Crossed(from, to, EndingActing.ZoomOut, 0.001f))
            {
                audioSource.PlayOneShot(zoom, 0.8f);
                StartBed(ref musicSource, reliefMusic, musicGain);
            }
            if (Crossed(from, to, EndingActing.ZoomOut, EndingActing.LickCueAt)) audioSource.PlayOneShot(lick);
            if (Crossed(from, to, EndingActing.Meow, EndingActing.MeowAt)) audioSource.PlayOneShot(meow);
            if (Crossed(from, to, EndingActing.Meow, 1.7f)) audioSource.PlayOneShot(lick, 0.8f);
            if (Crossed(from, to, EndingActing.Both, EndingActing.LickCueAt)) audioSource.PlayOneShot(lick, 0.8f);
            if (Crossed(from, to, EndingActing.Both, EndingActing.BokiPopAt)) audioSource.PlayOneShot(eyePop);
        }

        private static bool Crossed(float from, float to, int shot, float seconds)
        {
            float at = EndingActing.StartOf(shot) + seconds;
            return from < at && at <= to;
        }

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
            fadingOut = true;
            Action done = completed;
            completed = null;
            if (done == null)
            {
                // Stand-alone: rest on the closing card.
                ShowFrame(EndingActing.EndCard, 1f);
                overlay.SetSkipHint(false);
                return;
            }
            // In the game the results panel takes over from a clean frame.
            overlay.gameObject.SetActive(false);
            shotCamera.fieldOfView = baseFieldOfView;
            done();
        }

        // Pure pose for one moment of the sequence. Update calls it every frame; the Editor capture tool reuses it.
        public void ShowFrame(int shot, float progress)
        {
            CaptureRest();
            shot = Mathf.Clamp(shot, 0, EndingActing.ShotSeconds.Length - 1);
            float seconds = Mathf.Clamp01(progress) * EndingActing.ShotSeconds[shot];
            EndingActing.Beat beat = EndingActing.Sample(shot, seconds, stage.InverseTransformPoint(calendar.position));

            professor.transform.SetPositionAndRotation(stage.TransformPoint(beat.ProfessorPosition),
                stage.rotation * Quaternion.Euler(0f, beat.ProfessorYaw, 0f));
            professor.Apply(beat.Professor);
            boki.transform.SetPositionAndRotation(stage.TransformPoint(beat.BokiPosition),
                stage.rotation * Quaternion.Euler(0f, beat.BokiYaw, 0f));
            boki.Apply(beat.Boki);

            PlaceCamera(beat, seconds);
            boki.ShowCues(beat.Boki, shotCamera.transform.rotation);
            ShowStage(beat);
            ShowOverlay(shot, seconds, beat);
        }

        private void PlaceCamera(EndingActing.Beat beat, float seconds)
        {
            Vector3 position = stage.TransformPoint(beat.Camera);
            Vector3 target = stage.TransformPoint(beat.Target);
            Transform view = shotCamera.transform;
            view.SetPositionAndRotation(position, Quaternion.LookRotation(target - position, Vector3.up));
            // A hit squeezes the view in and rattles it, then both die away.
            if (beat.Punch > 0f)
            {
                float rattle = 0.012f * beat.Punch;
                view.position += view.right * (rattle * Mathf.Sin(seconds * 71f)) + view.up * (rattle * Mathf.Sin(seconds * 83f));
            }
            shotCamera.fieldOfView = beat.Fov * (1f - 0.22f * beat.Punch);
        }

        private void ShowStage(EndingActing.Beat beat)
        {
            redLight.intensity = redRestIntensity * Mathf.Max(0f, beat.RedLight);
            redLight.enabled = beat.RedLight > 0.01f;
            calendarMarker.gameObject.SetActive(beat.MarkerPop > 0.01f);
            calendarMarker.localScale = markerRestScale * Mathf.Max(0f, beat.MarkerPop);
        }

        private void ShowOverlay(int shot, float seconds, EndingActing.Beat beat)
        {
            overlay.SetFade(beat.Fade);
            overlay.SetFlash(beat.Flash);
            overlay.SetLetterbox(beat.Letterbox);
            overlay.SetSkipHint(shot < EndingActing.EndCard);
            overlay.SetMeow(beat.Meow, seconds);
            overlay.SetEndCard(beat.EndCard);
            switch (beat.Subtitle)
            {
                case EndingActing.ProfessorLine: overlay.SetSubtitle(ProfessorSays, Chalk); break;
                case EndingActing.BokiLine: overlay.SetSubtitle(BokiSays, Paper); break;
                default: overlay.SetSubtitle(string.Empty, Paper); break;
            }
        }

        // Rest values are read once, while the stage still holds its authored placement.
        private void CaptureRest()
        {
            if (restCaptured) return;
            restCaptured = true;
            baseFieldOfView = shotCamera.fieldOfView;
            redRestIntensity = redLight.intensity;
            markerRestScale = calendarMarker.localScale;
        }
    }
}
