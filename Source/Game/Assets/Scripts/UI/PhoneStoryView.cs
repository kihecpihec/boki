using System;
using Boki.Core;
using Boki.Enemies;
using Boki.Routes;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Boki.UI
{
    [DisallowMultipleComponent]
    public sealed class PhoneStoryView : MonoBehaviour
    {
        [Serializable]
        private struct Bubble
        {
            public CanvasGroup group;
            public Image background;
            public Text text;
        }

        private readonly struct Line
        {
            public readonly bool FromBoki;
            public readonly string Text;
            public Line(bool fromBoki, string text) { FromBoki = fromBoki; Text = text; }
        }

        private readonly struct Notification
        {
            public readonly string Header;
            public readonly Color Accent;
            public readonly Line[] Lines;
            public Notification(string header, Color accent, params Line[] lines) { Header = header; Accent = accent; Lines = lines; }
        }

        [SerializeField] private CanvasGroup card;
        [FormerlySerializedAs("message")] [SerializeField] private Text header;
        [SerializeField] private Image icon;
        [SerializeField] private Bubble[] bubbles;
        [SerializeField] private AudioSource source;
        [SerializeField] private AudioClip notification;
        [SerializeField] private AudioClip reaction;
        [SerializeField, Min(1f)] private float displaySeconds = 6f;

        // Story §3: no date, weekday or timestamp may appear. Indexed by RouteLevel.
        private static readonly Notification[] Script =
        {
            new Notification("OLD CONVERSATION · FRIEND", new Color(1f, 0.56f, 0.24f),
                new Line(true, "One drink. I'm leaving early."), new Line(false, "You said that last time.")),
            new Notification("DELAYED MESSAGE · FRIEND", new Color(0.45f, 0.62f, 0.78f),
                new Line(false, "Did you make it home?")),
            new Notification("PERSONAL REMINDER", new Color(0.85f, 0.12f, 0.16f),
                new Line(true, "Just show up this time.")),
        };

        // Top right, below the speed readout: the Bellevue intro's objective prompt owns the top left for six seconds.
        private static readonly Vector2 RestPosition = new Vector2(-30f, -120f);
        private static readonly Color CardColor = new Color(0.04f, 0.03f, 0.05f, 0.92f);
        private static readonly Color HeaderColor = new Color(0.66f, 0.62f, 0.55f);
        private static readonly Color IncomingColor = new Color(0.2f, 0.18f, 0.22f);
        private static readonly Color Cream = new Color(0.94f, 0.87f, 0.72f);
        private static readonly Color Ink = new Color(0.09f, 0.06f, 0.05f);
        private const float Padding = 14f;
        private const float HeaderHeight = 34f;
        private const float BubbleHeight = 40f;
        private const float BubbleStep = 48f;
        private const float BubblePadding = 16f;
        private const float MinCardWidth = 300f;
        private const float SlideSeconds = 0.35f;
        private const float SlideDistance = 70f;
        private const float LineDelaySeconds = 0.9f;
        private const float LineFadeSeconds = 0.25f;

        private RunSession session;
        private RectTransform cardRect;
        private Image cardImage;
        private Sprite roundedSprite;
        private float remaining;
        private bool audioPaused;
        private bool reactionPending;
        private bool guiltTailArmed;
        private bool warningTail;
        private int lineCount;
        private RouteLevel shownLevel;
        private int initialHealth;

        private void OnEnable()
        {
            if (!HasReferences())
            {
                Debug.LogError("Assign PhoneStoryView card, header, icon, both bubbles, source and notification.", this);
                enabled = false;
                return;
            }
            session = RunSession.Ensure();
            session.State.Guilt.PhaseChanged += OnGuiltPhaseChanged;
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
            source.priority = 200;
            ApplyStyle();
            Hide();
        }

        public void TryShow(RouteLevel level)
        {
            if (!isActiveAndEnabled || !session.IsGameplayActive
                || !session.State.TryConsumePhoneStory(level)) return;
            // The trigger owns the safe window: an occupied arena consumes and skips the beat.
            if (session.State.Guilt.IsActive || session.State.Guilt.ScriptedSequenceActive) return;
            shownLevel = level;
            guiltTailArmed = level == RouteLevel.Feri;
            initialHealth = session.State.Health.Current;
            Present(Script[(int)level]);
            remaining = displaySeconds;
            Animate();
            source.pitch = level == RouteLevel.Feri ? 0.65f : 1f;
            source.clip = notification;
            source.Play();
            reactionPending = reaction != null;
        }

        private void Update()
        {
            if (remaining <= 0f && !warningTail) return;
            if (!session.State.Health.IsAlive || session.IsLoading || session.HasEnteredBossArena
                || session.State.ActiveLevel != shownLevel || session.State.Health.Current < initialHealth
                || (!warningTail && session.State.Guilt.ScriptedSequenceActive))
            {
                Hide();
                return;
            }
            bool pause = !session.IsGameplayActive;
            if (pause != audioPaused)
            {
                if (pause) source.Pause(); else source.UnPause();
                audioPaused = pause;
            }
            if (pause) return;
            if (warningTail)
            {
                if (!source.isPlaying) Hide();
                return;
            }
            remaining -= Time.deltaTime;
            // FERI's familiar beep slowly drops in pitch; existing Guilt warnings retain priority.
            if (shownLevel == RouteLevel.Feri) source.pitch = Mathf.Lerp(0.65f, 0.25f, Mathf.Clamp01((displaySeconds - remaining) / 1.5f));
            if (reactionPending && !source.isPlaying)
            {
                reactionPending = false;
                source.pitch = 1f;
                source.clip = reaction;
                source.Play();
            }
            if (remaining <= 0f) Hide();
            else Animate();
        }

        private bool HasReferences()
        {
            if (card == null || !card.TryGetComponent(out cardImage) || header == null || icon == null
                || source == null || notification == null || bubbles == null || bubbles.Length < 2) return false;
            foreach (var bubble in bubbles)
                if (bubble.group == null || bubble.background == null || bubble.text == null) return false;
            return true;
        }

        // The view owns every visual number so a restyle never needs the four scenes re-saved.
        private void ApplyStyle()
        {
            if (roundedSprite == null) roundedSprite = CreateRoundedSprite();
            cardRect = (RectTransform)card.transform;
            cardRect.anchorMin = cardRect.anchorMax = cardRect.pivot = Vector2.one;
            Style(cardImage, CardColor);
            Style(icon, Color.white);
            Anchor((RectTransform)icon.transform, Vector2.up, new Vector2(Padding, -Padding - 1f), new Vector2(16f, 16f));
            header.font = header.font != null ? header.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            header.fontSize = 14;
            header.fontStyle = FontStyle.Bold;
            header.color = HeaderColor;
            header.alignment = TextAnchor.MiddleLeft;
            header.horizontalOverflow = HorizontalWrapMode.Overflow;
            header.verticalOverflow = VerticalWrapMode.Overflow;
            header.raycastTarget = false;
            Anchor(header.rectTransform, Vector2.up, new Vector2(Padding + 16f + 8f, -Padding + 3f), new Vector2(0f, 22f));
            foreach (var bubble in bubbles)
            {
                Style(bubble.background, Color.white);
                bubble.group.blocksRaycasts = bubble.group.interactable = false;
                var label = bubble.text;
                label.font = label.font != null ? label.font : header.font;
                label.fontSize = 20;
                label.color = Cream;
                label.alignment = TextAnchor.MiddleLeft;
                label.horizontalOverflow = HorizontalWrapMode.Overflow;
                label.verticalOverflow = VerticalWrapMode.Overflow;
                label.raycastTarget = false;
                label.rectTransform.anchorMin = Vector2.zero;
                label.rectTransform.anchorMax = Vector2.one;
                label.rectTransform.offsetMin = new Vector2(BubblePadding, 0f);
                label.rectTransform.offsetMax = new Vector2(-BubblePadding, 0f);
            }
        }

        private void Style(Image image, Color color)
        {
            image.sprite = roundedSprite;
            image.type = Image.Type.Sliced;
            image.color = color;
            image.raycastTarget = false;
        }

        private static void Anchor(RectTransform rect, Vector2 corner, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = corner;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        // Runs once per beat, never per frame.
        private void Present(Notification story)
        {
            header.text = story.Header;
            icon.color = story.Accent;
            lineCount = story.Lines.Length;
            float widest = 0f;
            for (int i = 0; i < bubbles.Length; i++)
            {
                bool used = i < lineCount;
                bubbles[i].group.gameObject.SetActive(used);
                if (!used) continue;
                bubbles[i].text.text = story.Lines[i].Text;
                widest = Mathf.Max(widest, bubbles[i].text.preferredWidth + BubblePadding * 2f);
            }
            float width = Mathf.Max(MinCardWidth, header.preferredWidth + Padding * 2f + 24f, widest + Padding * 2f);
            cardRect.sizeDelta = new Vector2(width, HeaderHeight + lineCount * BubbleStep + Padding - (BubbleStep - BubbleHeight));
            header.rectTransform.sizeDelta = new Vector2(width - Padding * 2f - 24f, 22f);
            for (int i = 0; i < lineCount; i++)
            {
                Line line = story.Lines[i];
                Vector2 corner = line.FromBoki ? Vector2.one : Vector2.up;
                var rect = (RectTransform)bubbles[i].group.transform;
                Anchor(rect, corner, new Vector2(line.FromBoki ? -Padding : Padding, -HeaderHeight - i * BubbleStep),
                    new Vector2(bubbles[i].text.preferredWidth + BubblePadding * 2f, BubbleHeight));
                bubbles[i].background.color = line.FromBoki ? story.Accent : IncomingColor;
                // Dark ink on the bright accent, cream on the dark one, so the red FERI bubble stays legible.
                bubbles[i].text.color = line.FromBoki && story.Accent.grayscale > 0.5f ? Ink : Cream;
            }
        }

        // Slides in from the right edge and fades in, then out for the final SlideSeconds; the second line lands a beat after the first.
        private void Animate()
        {
            float elapsed = displaySeconds - remaining;
            float visible = Mathf.SmoothStep(0f, 1f, Mathf.Min(elapsed, remaining) / SlideSeconds);
            card.alpha = visible;
            cardRect.anchoredPosition = RestPosition + Vector2.right * ((1f - visible) * SlideDistance);
            for (int i = 0; i < lineCount; i++)
                bubbles[i].group.alpha = Mathf.Clamp01((elapsed - i * LineDelaySeconds) / LineFadeSeconds);
        }

        private void OnGuiltPhaseChanged(GuiltPhase phase)
        {
            if (!guiltTailArmed || phase != GuiltPhase.Warning || !session.IsGameplayActive
                || session.State.ActiveLevel != RouteLevel.Feri || session.HasEnteredBossArena) return;
            guiltTailArmed = false;
            Hide();
            warningTail = true;
            initialHealth = session.State.Health.Current;
            source.clip = notification;
            source.pitch = 0.25f;
            source.Play();
        }

        private void Hide()
        {
            remaining = 0f;
            reactionPending = audioPaused = warningTail = false;
            if (card != null) card.alpha = 0f;
            if (source != null) source.Stop();
        }

        private void OnDisable()
        {
            if (session != null) session.State.Guilt.PhaseChanged -= OnGuiltPhaseChanged;
            guiltTailArmed = false;
            Hide();
        }

        private void OnDestroy()
        {
            if (roundedSprite == null) return;
            Destroy(roundedSprite.texture);
            Destroy(roundedSprite);
        }

        // A white 9-sliced rounded rectangle, tinted per Image; avoids shipping a sprite asset.
        private static Sprite CreateRoundedSprite()
        {
            const int size = 32;
            const float radius = 10f;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };
            var pixels = new Color32[size * size];
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // Signed distance to a rounded square centred in the texture.
                    float qx = Mathf.Abs(x + 0.5f - half) - (half - radius);
                    float qy = Mathf.Abs(y + 0.5f - half) - (half - radius);
                    float outside = Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) + Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f));
                    float distance = outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
                    byte alpha = (byte)(Mathf.Clamp01(0.5f - distance) * 255f);
                    pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
        }
    }
}
