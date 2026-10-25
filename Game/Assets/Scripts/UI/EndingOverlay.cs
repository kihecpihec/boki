using UnityEngine;
using UnityEngine.UI;

namespace Boki.UI
{
    // Presentation only: EndingCutscene decides what to show and when.
    [DisallowMultipleComponent]
    public sealed class EndingOverlay : MonoBehaviour
    {
        [SerializeField] private Image fade;
        [SerializeField] private Image flash;
        [SerializeField] private GameObject letterbox;
        [SerializeField] private Text subtitle;
        [SerializeField] private GameObject subtitleBacking;
        [SerializeField] private GameObject skipHint;
        [SerializeField] private RectTransform meowBurst;
        [SerializeField] private CanvasGroup endCard;
        private SubtitlePresentation captions;

        private void OnEnable()
        {
            if (fade == null || flash == null || letterbox == null || subtitle == null || subtitleBacking == null
                || skipHint == null || meowBurst == null || endCard == null)
            {
                Debug.LogError("Assign every EndingOverlay reference.", this);
                enabled = false;
                return;
            }
            if (captions == null) captions = new SubtitlePresentation(subtitle, subtitleBacking, 130f);
            captions.Clear();
        }

        private void LateUpdate() => captions?.RefreshLayout();
        private void OnDisable() => captions?.Clear();

        public void SetFade(float alpha)
        {
            Color color = fade.color;
            color.a = Mathf.Clamp01(alpha);
            fade.color = color;
        }

        // Brief white frame on a hit; fully transparent when zero.
        public void SetFlash(float alpha)
        {
            Color color = flash.color;
            color.a = Mathf.Clamp01(alpha);
            flash.color = color;
        }

        public void SetLetterbox(bool visible) => letterbox.SetActive(visible);
        public void SetSkipHint(bool visible) => skipHint.SetActive(visible);

        public void SetSubtitle(string line, Color color)
        {
            if (captions == null) captions = new SubtitlePresentation(subtitle, subtitleBacking, 130f);
            captions.Show(line, color);
        }

        // The comic MEOW! burst: pops in with the amount (which may overshoot 1) and wobbles while it is up.
        public void SetMeow(float amount, float seconds)
        {
            bool visible = amount > 0.01f;
            meowBurst.gameObject.SetActive(visible);
            if (!visible) return;
            meowBurst.localScale = Vector3.one * amount;
            meowBurst.localRotation = Quaternion.Euler(0f, 0f, -7f + 3f * Mathf.Sin(seconds * 30f));
        }

        public void SetEndCard(float alpha) => endCard.alpha = Mathf.Clamp01(alpha);
    }
}
