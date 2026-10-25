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
        private const float PopSeconds = 0.48f;
        private const float LostSeconds = 0.6f;
        private const float CalloutSeconds = 0.85f;
        private const float BadgeScale = 0.65f;
        private static readonly Color Cream = new Color(0.94f, 0.87f, 0.72f);
        private static readonly Color Hot = new Color(1f, 0.56f, 0.24f);
        private static readonly Color Lost = new Color(0.85f, 0.12f, 0.16f);
        private ComboTracker tracker;
        private Canvas canvas;
        private Text count;
        private Text label;
        private Text callout;
        private Text cracks;
        private RectTransform badge;
        private CanvasGroup badgeOpacity;
        private Image accent;
        private Image underline;
        private Text echo;
        private float impactDirection;
        private float impactStrength;
        private Vector2 badgePosition = new Vector2(-95f, 0f);
        private Vector2 calloutPosition;
        private float calloutAngle;
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
            var badgeObject = new GameObject("Streak Badge", typeof(RectTransform), typeof(CanvasGroup));
            badge = badgeObject.GetComponent<RectTransform>();
            badge.SetParent(root.transform, false);
            badge.anchorMin = badge.anchorMax = badge.pivot = new Vector2(1f, 0.5f);
            badge.anchoredPosition = badgePosition;
            badge.localScale = Vector3.one * BadgeScale;
            badge.sizeDelta = new Vector2(270f, 160f);
            badgeOpacity = badgeObject.GetComponent<CanvasGroup>();
            badgeOpacity.blocksRaycasts = false;
            badgeOpacity.interactable = false;
            Plate("Offset Ink", badge, new Vector2(-126f, -9f), new Vector2(258f, 141f),
                new Color(0.04f, 0.03f, 0.05f, 0.85f), -3f);
            accent = Plate("Orange Slip", badge, new Vector2(-135f, 5f), new Vector2(260f, 142f), Hot, 3f);
            Plate("Ink Face", badge, new Vector2(-135f, 7f), new Vector2(250f, 132f),
                new Color(0.09f, 0.065f, 0.085f, 0.97f), 0f);
            underline = Plate("Impact Stripe", badge, new Vector2(-134f, -54f), new Vector2(222f, 5f), Cream, 0f);
            echo = Label("Count Echo", badge, new Vector2(-22f, 24f), new Vector2(230f, 100f), 84);
            count = Label("Count", badge, new Vector2(-22f, 24f), new Vector2(230f, 100f), 84);
            label = Label("Label", badge, new Vector2(-24f, -30f), new Vector2(230f, 36f), 22);
            cracks = Label("Cracks", badge, new Vector2(-50f, 25f), new Vector2(160f, 100f), 80);
            cracks.text = "/ /";
            cracks.color = new Color(0.08f, 0.04f, 0.06f);
            cracks.gameObject.SetActive(false);
            callout = Label("Milestone", root.transform, Vector2.zero, new Vector2(270f, 55f), 28);
            callout.alignment = TextAnchor.MiddleCenter;
            callout.rectTransform.anchorMin = callout.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            callout.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            callout.gameObject.SetActive(false);
            source = root.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.priority = 180;
            cues = new[] { CreateCue(0), CreateCue(1), CreateCue(2), CreateCue(3) };
            Canvas.ForceUpdateCanvases();
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
                count.rectTransform.anchoredPosition = new Vector2(-22f, 24f);
                count.rectTransform.localRotation = Quaternion.identity;
                count.rectTransform.localScale = Vector3.one;
                label.text = "KILL STREAK";
                count.text = now.ToString();
                echo.text = count.text;
                impactDirection = now % 2 == 0 ? 1f : -1f;
                impactStrength = now >= 20 ? 1.35f : now >= 10 ? 1.15f : 0.9f;
                if (now == 5 || now == 10 || now == 20) impactStrength += 0.35f;
                badgeOpacity.alpha = 1f;
                accent.color = now >= 10 ? Hot : Cream;
                underline.color = now >= 10 ? Hot : Cream;
                if (now >= 2)
                    badgePosition = AnimateEffects ? RandomRightSidePosition(240f, 95f, 160f) : new Vector2(-95f, 0f);
                PoseBadge(AnimateEffects ? 1f : 0f);
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
                accent.color = underline.color = Lost;
                echo.color = Color.clear;
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
            calloutPosition = AnimateEffects ? RandomRightSidePosition(180f, 180f, 110f) : new Vector2(-180f, 100f);
            calloutAngle = AnimateEffects ? Random.Range(-9f, 9f) : 0f;
            callout.rectTransform.anchoredPosition = calloutPosition;
            callout.rectTransform.localRotation = Quaternion.Euler(0f, 0f, calloutAngle);
            callout.color = tier == 0 ? Cream : Hot;
            calloutRemaining = CalloutSeconds;
            callout.gameObject.SetActive(true);
            PlayCue(tier);
        }

        private Vector2 RandomRightSidePosition(float leftInset, float rightInset, float verticalInset)
        {
            Rect area = ((RectTransform)canvas.transform).rect;
            // Insets leave room for the pop, rotation and broken-streak fall.
            float left = Mathf.Min(-rightInset, -area.width * 0.5f + leftInset);
            float height = Mathf.Max(0f, area.height * 0.5f - verticalInset);
            return new Vector2(Random.Range(left, -rightInset), Random.Range(-height, height));
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
            popRemaining = Mathf.Max(0f, popRemaining - dt);
            if (lostRemaining <= 0f && canvas.enabled)
                PoseBadge(AnimateEffects ? popRemaining / PopSeconds : 0f);
            if (calloutRemaining > 0f)
            {
                calloutRemaining = Mathf.Max(0f, calloutRemaining - dt);
                Color color = callout.color;
                color.a = Mathf.Clamp01(calloutRemaining / 0.2f);
                callout.color = color;
                float punch = AnimateEffects ? Mathf.Clamp01((calloutRemaining - 0.65f) / 0.2f) * 0.2f : 0f;
                callout.rectTransform.localScale = Vector3.one * (1f + punch);
                callout.rectTransform.anchoredPosition = calloutPosition + new Vector2(punch * 100f, punch * -65f);
                callout.rectTransform.localRotation = Quaternion.Euler(0f, 0f, AnimateEffects ? calloutAngle + punch * 45f : 0f);
                if (calloutRemaining <= 0f) callout.gameObject.SetActive(false);
            }
            if (lostRemaining <= 0f) return;
            lostRemaining = Mathf.Max(0f, lostRemaining - dt);
            float fade = lostRemaining / LostSeconds;
            badgeOpacity.alpha = fade;
            Color lostColor = Lost;
            lostColor.a = fade;
            count.color = label.color = lostColor;
            if (!AnimateEffects) PoseBadge(0f);
            if (AnimateEffects)
            {
                float fall = 1f - fade;
                badge.localRotation = Quaternion.Euler(0f, 0f, -5f - fall * fall * 24f);
                badge.anchoredPosition = badgePosition + new Vector2(fall * 48f, -fall * fall * 95f);
                Color crackColor = cracks.color;
                crackColor.a = fade;
                cracks.color = crackColor;
            }
            if (lostRemaining <= 0f) canvas.enabled = false;
        }

        private void PoseBadge(float remaining)
        {
            float age = 1f - remaining;
            float kick = remaining * remaining * impactStrength;
            float twist = Mathf.Cos(age * Mathf.PI * 3f) * kick;
            badge.localRotation = Quaternion.Euler(0f, 0f, AnimateEffects ? -5f + twist * impactDirection * 13f : 0f);
            badge.anchoredPosition = badgePosition + new Vector2(kick * 30f, kick * 12f);
            badge.localScale = Vector3.one * (BadgeScale * (1f + kick * 0.16f));
            count.rectTransform.localScale = Vector3.one * (1f + kick * 0.22f);
            echo.rectTransform.anchoredPosition = new Vector2(-22f - age * 42f, 24f + age * 20f);
            echo.rectTransform.localScale = Vector3.one * (1f + age * 0.45f);
            Color echoColor = accent.color;
            echoColor.a = AnimateEffects ? remaining * remaining * 0.32f : 0f;
            echo.color = echoColor;
            underline.rectTransform.localScale = new Vector3(1f + kick * 0.08f, 1f + kick, 1f);
        }

        private static Image Plate(string name, Transform parent, Vector2 position, Vector2 size, Color color, float angle)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rect = (RectTransform)obj.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            rect.localRotation = Quaternion.Euler(0f, 0f, angle);
            var image = obj.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
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
            var shadow = obj.GetComponent<Shadow>();
            shadow.effectColor = new Color(0.025f, 0.015f, 0.035f, 0.9f);
            shadow.effectDistance = new Vector2(3f, -3f);
            return text;
        }
    }
}
