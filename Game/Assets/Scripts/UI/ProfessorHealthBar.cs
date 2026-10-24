using Boki.Combat;
using Boki.Core;
using Boki.Enemies;
using Boki.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Boki.UI
{
    [DisallowMultipleComponent]
    public sealed class ProfessorHealthBar : MonoBehaviour
    {
        private ProfessorBoss boss;
        private EnemyHealth health;
        private PlayerHealth player;
        private Canvas canvas;
        private Image fill;
        private Image damageTrail;
        private Text value;
        private Text phase;
        private int lastHealth = -1;
        private int lastMaximum = -1;
        private float trailFraction = 1f;
        private readonly Color red = new Color(0.85f, 0.12f, 0.16f);
        private readonly Color cream = new Color(0.94f, 0.87f, 0.72f);

        public void Bind(ProfessorBoss professor, EnemyHealth bossHealth, PlayerHealth target)
        {
            boss = professor;
            health = bossHealth;
            player = target;
            lastHealth = lastMaximum = -1;
            trailFraction = health.Maximum > 0 ? (float)health.Current / health.Maximum : 0f;
        }

        private void Awake()
        {
            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            var panel = Box("Deadline Panel", transform, new Vector2(0.5f, 1f),
                new Vector2(0f, -235f), new Vector2(760f, 110f), new Color(0.035f, 0.025f, 0.045f, 0.96f));
            Box("Warning Stripe", panel, new Vector2(0f, 0.5f), new Vector2(5f, 0f), new Vector2(6f, 102f), red);
            Label("Title", panel, new Vector2(0f, 1f), new Vector2(24f, -14f),
                new Vector2(480f, 28f), "THE PROFESSOR // THE DEADLINE", 22, cream, TextAnchor.MiddleLeft);
            phase = Label("Phase", panel, new Vector2(1f, 1f), new Vector2(-24f, -14f),
                new Vector2(230f, 28f), "07:59 / FINAL LECTURE", 17, cream, TextAnchor.MiddleRight);
            var frame = Box("Health Frame", panel, new Vector2(0.5f, 0.5f), new Vector2(0f, -3f),
                new Vector2(712f, 23f), cream);
            var track = Box("Health Track", frame, new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(706f, 17f), new Color(0.12f, 0.045f, 0.065f));
            damageTrail = Bar("Recent Damage", track, new Color(1f, 0.56f, 0.24f));
            fill = Bar("Health", track, red);
            for (int i = 1; i < 20; i++)
                Box("Segment", track, new Vector2(i / 20f, 0.5f), Vector2.zero,
                    new Vector2(2f, 17f), new Color(0.035f, 0.025f, 0.045f, 0.65f));
            value = Label("Health Value", panel, new Vector2(1f, 0f), new Vector2(-24f, 12f),
                new Vector2(230f, 20f), "", 15, cream, TextAnchor.MiddleRight);
            Label("Signal", panel, new Vector2(0f, 0f), new Vector2(24f, 12f),
                new Vector2(420f, 20f), "REC  >  FERI / EXAM IN PROGRESS", 14, cream, TextAnchor.MiddleLeft);
            canvas.enabled = false;
        }

        private void LateUpdate()
        {
            bool visible = boss != null && boss.isActiveAndEnabled && boss.CanAttack
                && health != null && health.IsAlive && player != null && player.IsAlive;
            canvas.enabled = visible;
            if (!visible) return;
            float fraction = (float)health.Current / Mathf.Max(1, health.Maximum);
            fill.rectTransform.localScale = new Vector3(fraction, 1f, 1f);
            if (RunSession.Instance == null || RunSession.Instance.IsGameplayActive)
                trailFraction = Mathf.MoveTowards(trailFraction, fraction, Time.deltaTime * 0.3f);
            damageTrail.rectTransform.localScale = new Vector3(Mathf.Max(fraction, trailFraction), 1f, 1f);
            if (lastHealth == health.Current && lastMaximum == health.Maximum) return;
            lastHealth = health.Current;
            lastMaximum = health.Maximum;
            value.text = $"HP {lastHealth:0000} / {lastMaximum:0000}";
            bool enraged = fraction <= 0.5f;
            phase.text = enraged ? "08:00 / DEADLINE" : "07:59 / FINAL LECTURE";
            phase.color = enraged ? red : cream;
        }

        private void OnDisable()
        {
            if (canvas != null) canvas.enabled = false;
        }

        private static RectTransform Box(string name, Transform parent, Vector2 anchor, Vector2 position,
            Vector2 size, Color color)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            obj.transform.SetParent(parent, false);
            var rect = (RectTransform)obj.transform;
            rect.anchorMin = rect.anchorMax = anchor;
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            var image = obj.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return rect;
        }

        private static Image Bar(string name, RectTransform parent, Color color)
        {
            var rect = Box(name, parent, new Vector2(0.5f, 0.5f), Vector2.zero, parent.sizeDelta, color);
            var image = rect.GetComponent<Image>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            return image;
        }

        private static Text Label(string name, Transform parent, Vector2 anchor, Vector2 position,
            Vector2 size, string content, int fontSize, Color color, TextAnchor alignment)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            obj.transform.SetParent(parent, false);
            var rect = (RectTransform)obj.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            var text = obj.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = alignment;
            text.text = content;
            text.raycastTarget = false;
            return text;
        }
    }
}
