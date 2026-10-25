using UnityEngine;
using UnityEngine.UI;

namespace Boki.UI
{
    // Shared presentation for authored cutscene captions and scene-owned spoken dialogue.
    public sealed class SubtitlePresentation
    {
        private readonly Text text;
        private readonly Image backing;
        private readonly Canvas displayCanvas;
        private readonly RectTransform canvas;
        private readonly float bottomInset;
        private Vector2 canvasSize;
        private Rect safeArea;

        public SubtitlePresentation(Text text, GameObject backing, float bottomInset)
        {
            this.text = text;
            this.backing = backing.GetComponent<Image>();
            this.bottomInset = bottomInset;
            displayCanvas = text.canvas;
            canvas = (RectTransform)displayCanvas.transform;
            text.fontSize = 32;
            text.fontStyle = FontStyle.Bold;
            text.lineSpacing = 1.12f;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.resizeTextForBestFit = false;
            text.supportRichText = false;
            text.raycastTarget = false;
            var outline = text.GetComponent<Outline>();
            if (outline == null) outline = text.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.7f);
            outline.effectDistance = new Vector2(1f, -1f);
            this.backing.color = new Color(0.025f, 0.02f, 0.03f, 0.88f);
            this.backing.raycastTarget = false;
            var rect = this.backing.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0f);
            text.rectTransform.anchorMin = text.rectTransform.anchorMax = text.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            Clear();
        }

        public static SubtitlePresentation Create(Transform owner)
        {
            var root = new GameObject("Dialogue Subtitles", typeof(Canvas), typeof(CanvasScaler));
            root.transform.SetParent(owner, false);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 40;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600f, 900f);
            scaler.matchWidthOrHeight = 0.5f;
            var backing = new GameObject("Subtitle Backing", typeof(RectTransform), typeof(Image));
            backing.transform.SetParent(root.transform, false);
            var label = new GameObject("Subtitle", typeof(RectTransform), typeof(Text));
            label.transform.SetParent(root.transform, false);
            var text = label.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return new SubtitlePresentation(text, backing, 64f);
        }

        public void Show(string line, Color color)
        {
            if (string.IsNullOrEmpty(line)) { Clear(); return; }
            bool changed = text.text != line || text.color != color || !text.enabled || !backing.gameObject.activeSelf;
            if (changed)
            {
                text.text = line;
                text.color = color;
                text.enabled = true;
                backing.gameObject.SetActive(true);
                Layout();
                float alpha = Application.isPlaying ? 0f : 1f;
                text.canvasRenderer.SetAlpha(alpha);
                backing.canvasRenderer.SetAlpha(alpha);
                text.CrossFadeAlpha(1f, Application.isPlaying ? 0.1f : 0f, true);
                backing.CrossFadeAlpha(1f, Application.isPlaying ? 0.1f : 0f, true);
            }
            RefreshLayout();
        }

        public void Clear()
        {
            text.text = string.Empty;
            text.enabled = false;
            backing.gameObject.SetActive(false);
        }

        public void SetSortingOrder(int order) => displayCanvas.sortingOrder = order;

        public void RefreshLayout()
        {
            if (backing.gameObject.activeSelf && (canvasSize != canvas.rect.size || safeArea != Screen.safeArea))
                Layout();
        }

        private void Layout()
        {
            canvasSize = canvas.rect.size;
            safeArea = Screen.safeArea;
            float scale = canvasSize.x / Mathf.Max(1, Screen.width);
            float available = Mathf.Max(96f, Mathf.Min(canvasSize.x, safeArea.width * scale) - 96f);
            float width = Mathf.Min(1120f, available, text.preferredWidth + 48f);
            text.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width - 48f);
            float height = text.preferredHeight + 32f;
            text.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height - 32f);
            backing.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            backing.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            backing.rectTransform.anchoredPosition = new Vector2(
                (safeArea.center.x - Screen.width * 0.5f) * scale, bottomInset + safeArea.yMin * scale);
            // Keep the authored prefab hierarchy intact so Editor frame previews work too.
            text.rectTransform.position = backing.rectTransform.TransformPoint(backing.rectTransform.rect.center);
        }
    }
}
