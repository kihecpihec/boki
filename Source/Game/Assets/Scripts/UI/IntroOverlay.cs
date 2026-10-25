using UnityEngine;
using UnityEngine.UI;

namespace Boki.UI
{
    // Presentation only: IntroCutscene decides what to show and when.
    [DisallowMultipleComponent]
    public sealed class IntroOverlay : MonoBehaviour
    {
        [SerializeField] private Image fade;
        [SerializeField] private Image flash;
        [SerializeField] private GameObject letterbox;
        [SerializeField] private Text subtitle;
        [SerializeField] private GameObject subtitleBacking;
        [SerializeField] private CanvasGroup watchReadout;
        [SerializeField] private GameObject objective;
        [SerializeField] private GameObject skipHint;
        private SubtitlePresentation captions;

        private void OnEnable()
        {
            if (fade == null || flash == null || letterbox == null || subtitle == null || subtitleBacking == null || watchReadout == null
                || objective == null || skipHint == null)
            {
                Debug.LogError("Assign every IntroOverlay reference.", this);
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

        public void SetSubtitle(string line, Color color)
        {
            if (captions == null) captions = new SubtitlePresentation(subtitle, subtitleBacking, 130f);
            captions.Show(line, color);
        }

        public void SetWatchReadout(float alpha) => watchReadout.alpha = Mathf.Clamp01(alpha);
        public void SetObjective(bool visible) => objective.SetActive(visible);
        public void SetSkipHint(bool visible) => skipHint.SetActive(visible);
    }
}
