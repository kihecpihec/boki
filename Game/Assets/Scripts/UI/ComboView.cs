using Boki.Combat;
using UnityEngine;
using UnityEngine.UI;

namespace Boki.UI
{
    // Right-edge streak counter; hidden until the streak is worth showing.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ComboTracker))]
    public sealed class ComboView : MonoBehaviour
    {
        private const int MinimumShown = 2;
        private const int HotCount = 10;
        private const float PopSeconds = 0.14f;
        private const float LostSeconds = 0.6f;
        private static readonly Color Cream = new Color(0.94f, 0.87f, 0.72f);
        private static readonly Color Hot = new Color(1f, 0.56f, 0.24f);
        private static readonly Color Lost = new Color(0.85f, 0.12f, 0.16f);
        private ComboTracker tracker;
        private Canvas canvas;
        private Text count;
        private Text label;
        private int lastCount;
        private float popRemaining;
        private float lostRemaining;

        private void Awake()
        {
            tracker = GetComponent<ComboTracker>();
            var root = new GameObject("Combo HUD", typeof(RectTransform), typeof(CanvasScaler));
            root.transform.SetParent(transform, false);
            canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 25;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            count = Label("Count", root.transform, new Vector2(-50f, 30f), new Vector2(320f, 100f), 84);
            label = Label("Label", root.transform, new Vector2(-50f, -34f), new Vector2(320f, 40f), 30);
            label.text = "COMBO";
            canvas.enabled = false;
        }

        private void OnEnable()
        {
            tracker.Counter.Changed += Refresh;
            lastCount = 0;
            Refresh();
        }

        private void OnDisable()
        {
            tracker.Counter.Changed -= Refresh;
            if (canvas != null) canvas.enabled = false;
        }

        private void Refresh()
        {
            int now = tracker.Counter.Count;
            if (now > lastCount)
            {
                popRemaining = PopSeconds;
                lostRemaining = 0f;
                Color color = now >= HotCount ? Hot : Cream;
                count.text = "x" + now;
                count.color = label.color = color;
                canvas.enabled = now >= MinimumShown;
            }
            else if (now == 0)
            {
                if (lastCount >= MinimumShown) lostRemaining = LostSeconds;
                else canvas.enabled = false;
                popRemaining = 0f;
                count.rectTransform.localScale = Vector3.one;
                count.color = label.color = Lost;
            }
            lastCount = now;
        }

        private void Update()
        {
            if (!EnemyFeedback.CanAnimate) return;
            float dt = Time.deltaTime;
            if (popRemaining > 0f)
            {
                popRemaining = Mathf.Max(0f, popRemaining - dt);
                count.rectTransform.localScale = Vector3.one * (1f + 0.35f * popRemaining / PopSeconds);
            }
            if (lostRemaining <= 0f) return;
            lostRemaining = Mathf.Max(0f, lostRemaining - dt);
            Color color = Lost;
            color.a = lostRemaining / LostSeconds;
            count.color = label.color = color;
            if (lostRemaining <= 0f) canvas.enabled = false;
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
