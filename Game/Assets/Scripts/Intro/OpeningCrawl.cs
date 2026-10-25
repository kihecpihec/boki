using Boki.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Boki.Intro
{
    // Story §3 OPENING CRAWL: black screen, a one-line preface, the title receding, then the story so far
    // tilting away into the dark. Plays once per New Run from the main menu; Retry skips it. Enter skips.
    // Finishing and skipping both hand over to the V1 run, whose Bellevue prologue then fades in from black.
    [DisallowMultipleComponent]
    public sealed class OpeningCrawl : MonoBehaviour
    {
        [Header("Preface")]
        [SerializeField] private CanvasGroup preface;
        [SerializeField, Min(1f)] private float prefaceSeconds = 4f;

        [Header("Title")]
        [SerializeField] private RectTransform title;
        [SerializeField] private CanvasGroup titleGroup;
        [SerializeField, Min(1f)] private float titleSeconds = 5.5f;
        [Tooltip("The title sits still this long before it starts to recede into the dark.")]
        [SerializeField, Min(0f)] private float titleHoldSeconds = 1.8f;
        [SerializeField, Range(0.01f, 1f)] private float titleEndScale = 0.05f;

        [Header("Crawl")]
        [SerializeField] private RectTransform crawlTilt;
        [SerializeField] private Text crawlText;
        [Tooltip("Canvas units from the near edge of the tilted plane to its far end.")]
        [SerializeField, Min(100f)] private float travelHeight = 1900f;
        [Tooltip("Canvas units per second along the plane.")]
        [SerializeField, Min(10f)] private float scrollSpeed = 120f;

        [Header("Finish")]
        [SerializeField] private Image blackout;
        [SerializeField] private GameObject skipHint;
        [SerializeField, Min(0.1f)] private float outroSeconds = 1.5f;

        [Header("Audio and input")]
        [SerializeField] private AudioSource bed;
        [SerializeField] private AudioSource effects;
        [SerializeField] private AudioClip titleStinger;
        [SerializeField] private AudioClip heartbeat;
        [SerializeField, Range(0f, 1f)] private float bedGain = 0.5f;
        [SerializeField, Range(0f, 1f)] private float stingerGain = 0.7f;
        [SerializeField, Range(0f, 1f)] private float heartbeatGain = 0.45f;
        [Tooltip("The heartbeat starts this far through the crawl, so it is racing as the last line arrives.")]
        [SerializeField, Range(0.1f, 0.95f)] private float heartbeatAt = 0.55f;
        [SerializeField] private InputActionReference skipAction;

        private IntroTimeline timeline;
        private float crawlSeconds;
        private bool heartbeatPlayed;
        private bool ownsSkip;

        private void OnEnable()
        {
            if (preface == null || title == null || titleGroup == null || crawlTilt == null || crawlText == null
                || blackout == null || skipHint == null || bed == null || effects == null
                || skipAction == null || skipAction.action == null)
            {
                Debug.LogError("Assign every OpeningCrawl reference.", this);
                enabled = false;
                return;
            }
            ownsSkip = !skipAction.action.enabled;
            if (ownsSkip) skipAction.action.Enable();
            Cursor.visible = false;
        }

        private void OnDisable()
        {
            if (timeline != null)
            {
                timeline.ShotStarted -= OnPhaseStarted;
                timeline.Finished -= OnFinished;
            }
            if (bed != null) bed.Stop();
            if (ownsSkip && skipAction != null && skipAction.action != null) skipAction.action.Disable();
            ownsSkip = false;
        }

        private void Start()
        {
            crawlSeconds = OpeningCrawlRules.CrawlSeconds(crawlText.preferredHeight, travelHeight, scrollSpeed);
            timeline = new IntroTimeline(new[] { prefaceSeconds, titleSeconds, crawlSeconds, outroSeconds });
            timeline.ShotStarted += OnPhaseStarted;
            timeline.Finished += OnFinished;
            blackout.gameObject.SetActive(false);
            skipHint.SetActive(true);
            bed.loop = true;
            bed.volume = 0f;
            bed.Play();
            timeline.Begin();
            Show(OpeningCrawlRules.Preface, 0f);
        }

        private void Update()
        {
            if (timeline == null || timeline.IsFinished) return;
            if (skipAction.action.WasPressedThisFrame())
            {
                timeline.TrySkip();
                return;
            }
            // A long first frame after a scene load must not swallow the preface.
            timeline.Advance(Mathf.Min(Time.unscaledDeltaTime, 0.1f));
            if (timeline.IsFinished) return;
            float seconds = timeline.ShotProgress * PhaseSeconds(timeline.ShotIndex);
            Show(timeline.ShotIndex, seconds);
            bed.volume = bedGain * OpeningCrawlRules.Envelope(timeline.Elapsed, timeline.TotalSeconds, 2f, outroSeconds);
            if (timeline.ShotIndex == OpeningCrawlRules.Crawl && !heartbeatPlayed && heartbeat != null
                && timeline.ShotProgress >= heartbeatAt)
            {
                heartbeatPlayed = true;
                effects.PlayOneShot(heartbeat, heartbeatGain);
            }
        }

        private float PhaseSeconds(int phase) => phase switch
        {
            OpeningCrawlRules.Preface => prefaceSeconds,
            OpeningCrawlRules.Title => titleSeconds,
            OpeningCrawlRules.Crawl => crawlSeconds,
            _ => outroSeconds,
        };

        private void OnPhaseStarted(int phase)
        {
            if (phase == OpeningCrawlRules.Title && titleStinger != null) effects.PlayOneShot(titleStinger, stingerGain);
        }

        // Pure pose for one moment of the sequence, so playing in order is the only path through it.
        private void Show(int phase, float seconds)
        {
            preface.alpha = phase == OpeningCrawlRules.Preface
                ? OpeningCrawlRules.Envelope(seconds, prefaceSeconds, 1.2f, 1.2f) : 0f;

            bool onTitle = phase == OpeningCrawlRules.Title;
            titleGroup.alpha = onTitle ? OpeningCrawlRules.Envelope(seconds, titleSeconds, 0.6f, titleSeconds - titleHoldSeconds) : 0f;
            float recede = onTitle ? OpeningCrawlRules.Ramp(seconds, titleHoldSeconds, titleSeconds) : 0f;
            title.localScale = Vector3.one * Mathf.Lerp(1f, titleEndScale, recede);

            bool onCrawl = phase == OpeningCrawlRules.Crawl;
            crawlTilt.gameObject.SetActive(onCrawl);
            if (onCrawl)
                crawlText.rectTransform.anchoredPosition = new Vector2(0f, OpeningCrawlRules.ScrollOffset(seconds, scrollSpeed));
        }

        // Both the end of the outro and a skip land here: black screen, then the run begins.
        private void OnFinished()
        {
            preface.alpha = 0f;
            titleGroup.alpha = 0f;
            crawlTilt.gameObject.SetActive(false);
            skipHint.SetActive(false);
            blackout.gameObject.SetActive(true);
            bed.Stop();
            if (!RunSession.Ensure().BeginV1Run()) Debug.LogError("The opening crawl could not start the V1 run.", this);
        }
    }
}
