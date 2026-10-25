using Boki.Combat;
using UnityEngine;
using UnityEngine.UI;

namespace Boki.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ComboTracker))]
    public sealed class ComboView : MonoBehaviour
    {
        [SerializeField] private bool animatedEffects = true;
        [SerializeField] private PlayerHud playerHud;
        private bool AnimateEffects => animatedEffects && (playerHud == null || playerHud.ScreenEffectsEnabled);
        [SerializeField, Range(0f, 1f)] private float cueVolume = 0.18f;
        private const float PopSeconds = 0.14f;
        private const float LostSeconds = 0.6f;
        private const float CalloutSeconds = 0.85f;
        private static readonly Color Cream = new Color(0.94f, 0.87f, 0.72f);
        private static readonly Color Hot = new Color(1f, 0.56f, 0.24f);
        private static readonly Color Lost = new Color(0.85f, 0.12f, 0.16f);
        private ComboTracker tracker;
        private Canvas canvas;
        private Text count;
        private Text label;
        private Text callout;
        private Text cracks;
        private AudioSource source;
        private AudioClip[] cues;
        private int lastCount;
        private float popRemaining;
        private float lostRemaining;
        private float calloutRemaining;
        private bool audioPaused;

        private void Awake()
        {
            tracker = GetComponent<ComboTracker>();
            if (playerHud == null) playerHud = FindFirstObjectByType<PlayerHud>();
            var root = new GameObject("Kill Streak HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            root.transform.SetParent(transform, false);
            canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 25;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            count = Label("Count", root.transform, new Vector2(-50f, 30f), new Vector2(320f, 100f), 84);
            label = Label("Label", root.transform, new Vector2(-50f, -34f), new Vector2(320f, 40f), 25);
            cracks = Label("Cracks", root.transform, new Vector2(-50f, 30f), new Vector2(110f, 100f), 80);
            cracks.text = "/ /";
            cracks.color = new Color(0.08f, 0.04f, 0.06f);
            cracks.gameObject.SetActive(false);
            callout = Label("Milestone", root.transform, Vector2.zero, new Vector2(370f, 65f), 38);
            callout.alignment = TextAnchor.MiddleCenter;
            callout.rectTransform.anchorMin = callout.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            callout.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            callout.gameObject.SetActive(false);
            source = root.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.priority = 180;
            cues = new[] { CreateCue(0), CreateCue(1), CreateCue(2), CreateCue(3) };
            canvas.enabled = false;
        }

        private void OnEnable()
        {
            tracker.Counter.Changed += Refresh;
            lastCount = 0;
            popRemaining = lostRemaining = calloutRemaining = 0f;
            Refresh();
        }

        private void OnDisable()
        {
            tracker.Counter.Changed -= Refresh;
            if (canvas != null) canvas.enabled = false;
            if (source != null) source.Stop();
            audioPaused = false;
        }

        private void OnDestroy()
        {
            if (cues == null) return;
            foreach (AudioClip cue in cues) Destroy(cue);
        }

        private void Refresh()
        {
            int now = tracker.Counter.Count;
            if (now > lastCount)
            {
                popRemaining = AnimateEffects ? PopSeconds : 0f;
                lostRemaining = 0f;
                cracks.gameObject.SetActive(false);
                count.rectTransform.anchoredPosition = new Vector2(-50f, 30f);
                count.rectTransform.localRotation = Quaternion.identity;
                count.rectTransform.localScale = Vector3.one;
                label.text = "KILL STREAK";
                count.text = now.ToString();
                count.color = label.color = now >= 10 ? Hot : Cream;
                canvas.enabled = now >= 2;
                if (now == 5 || now == 10 || now == 20) ShowMilestone(now);
            }
            else if (now == 0)
            {
                calloutRemaining = 0f;
                callout.gameObject.SetActive(false);
                lostRemaining = lastCount >= 2 ? LostSeconds : 0f;
                canvas.enabled = lostRemaining > 0f;
                popRemaining = 0f;
                count.rectTransform.localScale = Vector3.one;
                count.color = label.color = Lost;
                label.text = "STREAK BROKEN";
                cracks.color = new Color(0.08f, 0.04f, 0.06f);
                cracks.gameObject.SetActive(AnimateEffects && lostRemaining > 0f);
                if (lostRemaining > 0f) PlayCue(3);
            }
            lastCount = now;
        }

        private void ShowMilestone(int kills)
        {
            int tier = kills == 5 ? 0 : kills == 10 ? 1 : 2;
            callout.text = tier == 0 ? "LOCKED IN" : tier == 1 ? "RAVE MODE" : "UNSTOPPABLE";
            // Fixed peripheral slots leave the riding area and boss bar clear.
            int slot = AnimateEffects ? Random.Range(0, 3) : 1;
            callout.rectTransform.anchoredPosition = slot == 0 ? new Vector2(-400f, 130f)
                : slot == 1 ? new Vector2(390f, 155f) : new Vector2(-400f, -140f);
            callout.rectTransform.localRotation = Quaternion.Euler(0f, 0f, AnimateEffects ? Random.Range(-6f, 6f) : 0f);
            callout.color = tier == 0 ? Cream : Hot;
            calloutRemaining = CalloutSeconds;
            callout.gameObject.SetActive(true);
            PlayCue(tier);
        }

        private void PlayCue(int index)
        {
            source.Stop();
            source.clip = cues[index];
            source.volume = cueVolume;
            source.Play();
            audioPaused = false;
        }

        private void Update()
        {
            bool active = EnemyFeedback.CanAnimate;
            if (!active)
            {
                if (!audioPaused) { source.Pause(); audioPaused = true; }
                return;
            }
            if (audioPaused) { source.UnPause(); audioPaused = false; }
            float dt = Time.deltaTime;
            if (popRemaining > 0f)
            {
                popRemaining = Mathf.Max(0f, popRemaining - dt);
                count.rectTransform.localScale = Vector3.one * (1f + 0.3f * popRemaining / PopSeconds);
            }
            if (calloutRemaining > 0f)
            {
                calloutRemaining = Mathf.Max(0f, calloutRemaining - dt);
                Color color = callout.color;
                color.a = Mathf.Clamp01(calloutRemaining / 0.2f);
                callout.color = color;
                float punch = AnimateEffects ? Mathf.Clamp01((calloutRemaining - 0.65f) / 0.2f) * 0.2f : 0f;
                callout.rectTransform.localScale = Vector3.one * (1f + punch);
                if (calloutRemaining <= 0f) callout.gameObject.SetActive(false);
            }
            if (lostRemaining <= 0f) return;
            lostRemaining = Mathf.Max(0f, lostRemaining - dt);
            float fade = lostRemaining / LostSeconds;
            Color lostColor = Lost;
            lostColor.a = fade;
            count.color = label.color = lostColor;
            if (AnimateEffects)
            {
                count.rectTransform.localRotation = Quaternion.Euler(0f, 0f, (1f - fade) * -9f);
                count.rectTransform.anchoredPosition = new Vector2(-50f + (1f - fade) * 15f, 30f - (1f - fade) * 18f);
                Color crackColor = cracks.color;
                crackColor.a = fade;
                cracks.color = crackColor;
            }
            if (lostRemaining <= 0f) canvas.enabled = false;
        }

        private static AudioClip CreateCue(int tier)
        {
            const int rate = 22050;
            float duration = tier == 3 ? 0.22f : 0.16f + tier * 0.055f;
            var samples = new float[Mathf.CeilToInt(rate * duration)];
            float phase = 0f;
            for (int i = 0; i < samples.Length; i++)
            {
                float t = (float)i / rate;
                float progress = t / duration;
                float frequency = tier == 3 ? Mathf.Lerp(230f, 65f, progress)
                    : (330f + tier * 110f) * Mathf.Pow(1.259921f, Mathf.Min(tier + 1, Mathf.FloorToInt(t / 0.065f)));
                phase += 2f * Mathf.PI * frequency / rate;
                float envelope = Mathf.Min(1f, t / 0.008f) * (1f - progress) * (1f - progress);
                samples[i] = (Mathf.Sin(phase) + 0.18f * Mathf.Sin(phase * 2f)) * envelope * 0.5f;
            }
            AudioClip clip = AudioClip.Create("Streak cue " + tier, samples.Length, 1, rate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static Text Label(string name, Transform parent, Vector2 position, Vector2 size, int fontSize)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(Shadow));
            var rect = (RectTransform)obj.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var text = obj.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleRight;
            text.raycastTarget = false;
            return text;
        }
    }
}
