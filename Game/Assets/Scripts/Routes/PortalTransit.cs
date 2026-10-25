using System;
using Boki.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Boki.Routes
{
    // Screen swirl and sounds that carry the rider through a portal. It lives on RunSession so it survives the
    // scene load: the swirl closes before the load and opens on the new world. Identical for every portal,
    // speed and destination, so it cannot hint at the Gameplay §1.1 shortcut.
    [DisallowMultipleComponent]
    public sealed class PortalTransit : MonoBehaviour
    {
        private enum Phase { Idle, Closing, Covered, Waiting, Holding, Opening }

        private const float MaximumStep = 0.05f;
        private const float ClosedSwirlScale = 0.02f;
        private const float ArrivalDiscSeconds = 1.9f;
        private const float ArrivalDiscPeak = 0.45f;
        private const float OpenOvershoot = 0.8f;

        [SerializeField, Min(0.1f)] private float openSeconds = 1.1f;
        [Tooltip("Keeps the new world covered for a moment so its first frames never show.")]
        [SerializeField, Min(0f)] private float holdSeconds = 0.2f;
        [SerializeField, Min(0f)] private float swirlSpinRate = 5f;
        [SerializeField, Range(0f, 1f)] private float soundGain = 0.7f;
        private Phase phase;
        private Action covered;
        private float elapsed, closeSeconds, spin, arrivalElapsed;
        private GameObject canvasObject;
        private RectTransform swirl;
        private Material swirlMaterial;
        private AudioSource audioSource;
        private AudioClip pullClip, arriveClip;
        private VortexDisc arrivalDisc;

        private static readonly int AlphaId = Shader.PropertyToID("_Alpha");
        private static readonly int SpinId = Shader.PropertyToID("_Spin");

        public bool IsPlaying => phase != Phase.Idle;

        // 'covered' runs once the swirl has filled the screen and been presented for a frame,
        // so a blocking scene load can never cut the effect short.
        public void PlayEnter(float seconds, Action covered)
        {
            if (!Build())
            {
                covered();
                return;
            }
            ClearArrival();
            this.covered = covered;
            closeSeconds = Mathf.Max(seconds, 0.01f);
            elapsed = 0f;
            phase = Phase.Closing;
            canvasObject.SetActive(true);
            Apply(0f, 0f);
            Play(pullClip);
        }

        public void PlayArrive()
        {
            if (phase != Phase.Waiting) return;
            elapsed = 0f;
            phase = Phase.Holding;
        }

        // A load that is not a portal trip (retry, menu) must not inherit a half-open swirl.
        public void Cancel()
        {
            phase = Phase.Idle;
            covered = null;
            if (canvasObject != null) canvasObject.SetActive(false);
            ClearArrival();
        }

        private bool Build()
        {
            if (canvasObject != null) return true;
            Material material = VortexDisc.CreateMaterial(true, false);
            if (material == null)
            {
                Debug.LogError("Missing Resources/PortalVortex shader; portals load without their effect.", this);
                return false;
            }
            swirlMaterial = material;
            pullClip = Resources.Load<AudioClip>("Portal/portal_pull");
            arriveClip = Resources.Load<AudioClip>("Portal/portal_arrive");
            canvasObject = new GameObject("Portal Transit", typeof(Canvas));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            var image = new GameObject("Swirl", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            swirl = image.rectTransform;
            swirl.SetParent(canvasObject.transform, false);
            swirl.anchorMin = swirl.anchorMax = swirl.pivot = Vector2.one * 0.5f;
            swirl.anchoredPosition = Vector2.zero;
            image.material = swirlMaterial;
            image.raycastTarget = false;
            canvasObject.SetActive(false);
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
            return true;
        }

        private void Update()
        {
            if (phase == Phase.Idle && arrivalDisc == null) return;
            float step = Mathf.Min(Time.unscaledDeltaTime, MaximumStep);
            switch (phase)
            {
                case Phase.Closing:
                    elapsed += step;
                    float closed = Mathf.Clamp01(elapsed / closeSeconds);
                    Apply(closed * closed, Mathf.Clamp01(closed * closed * 4f), step);
                    if (closed >= 1f) phase = Phase.Covered;
                    break;
                case Phase.Covered:
                    // The full swirl was drawn last frame; the load may block now. PlayArrive ends the wait.
                    var load = covered;
                    covered = null;
                    phase = Phase.Waiting;
                    load?.Invoke();
                    break;
                case Phase.Waiting:
                    Apply(1f, 1f, step);
                    break;
                case Phase.Holding:
                    Apply(1f, 1f, step);
                    elapsed += step;
                    if (elapsed >= holdSeconds) BeginOpening();
                    break;
                case Phase.Opening:
                    elapsed += step;
                    float open = Mathf.Clamp01(elapsed / openSeconds);
                    // The swirl keeps expanding past the screen edges while it fades, as if flying out of the vortex.
                    float eased = Mathf.SmoothStep(0f, 1f, open);
                    Apply(1f + OpenOvershoot * eased, 1f - Mathf.Clamp01((eased - 0.1f) / 0.9f), step);
                    if (open >= 1f)
                    {
                        phase = Phase.Idle;
                        canvasObject.SetActive(false);
                    }
                    break;
            }
            UpdateArrivalDisc(step);
        }

        private void BeginOpening()
        {
            phase = Phase.Opening;
            elapsed = 0f;
            Play(arriveClip);
            var rider = FindAnyObjectByType<PlayerHealth>();
            if (rider == null) return;
            arrivalDisc = VortexDisc.Create("Portal Arrival", null);
            if (arrivalDisc == null) return;
            arrivalDisc.Transform.SetPositionAndRotation(rider.transform.position + Vector3.up * 0.12f,
                Quaternion.Euler(90f, 0f, 0f));
            arrivalElapsed = 0f;
        }

        // scale 0 is a dot and 1 is fully closed; fade is the swirl's opacity.
        private void Apply(float scale, float fade, float step = 0f)
        {
            spin += step * swirlSpinRate * (1f + Mathf.Min(scale, 1f));
            // Fully closed means the opaque part of the disc reaches past the screen corners.
            float height = Screen.height;
            float fullScale = 1.15f * Mathf.Sqrt((float)Screen.width * Screen.width + height * height) / (0.8f * height);
            swirl.sizeDelta = Vector2.one * (height * Mathf.LerpUnclamped(ClosedSwirlScale, fullScale, scale));
            swirlMaterial.SetFloat(AlphaId, fade);
            swirlMaterial.SetFloat(SpinId, spin);
        }

        // A whirlpool on the ground where the rider landed blooms as the screen swirl clears the centre, then fades.
        private void UpdateArrivalDisc(float step)
        {
            if (arrivalDisc == null) return;
            arrivalElapsed += step;
            float t = arrivalElapsed / ArrivalDiscSeconds;
            if (t >= 1f) { ClearArrival(); return; }
            float level = t < ArrivalDiscPeak
                ? Mathf.SmoothStep(0f, 1f, t / ArrivalDiscPeak)
                : 1f - Mathf.SmoothStep(0f, 1f, (t - ArrivalDiscPeak) / (1f - ArrivalDiscPeak));
            float size = Mathf.Lerp(2.4f, 5.5f, Mathf.SmoothStep(0f, 1f, t));
            arrivalDisc.Transform.localScale = new Vector3(size, size, 1f);
            arrivalDisc.Show(1.4f * level, spin * 1.5f);
        }

        private void ClearArrival()
        {
            if (arrivalDisc != null) arrivalDisc.Destroy();
            arrivalDisc = null;
        }

        private void Play(AudioClip clip)
        {
            if (clip != null) audioSource.PlayOneShot(clip, soundGain);
        }

        private void OnDestroy()
        {
            ClearArrival();
            if (swirlMaterial != null) Destroy(swirlMaterial);
        }
    }
}
