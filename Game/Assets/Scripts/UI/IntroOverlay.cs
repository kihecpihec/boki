using UnityEngine;
using UnityEngine.UI;

namespace Boki.UI
{
    // Presentation only: IntroCutscene decides what to show and when.
    [DisallowMultipleComponent]
    public sealed class IntroOverlay : MonoBehaviour
    {
        [SerializeField] private Image fade;
        [SerializeField] private GameObject letterbox;
        [SerializeField] private Text subtitle;
        [SerializeField] private CanvasGroup watchReadout;
        [SerializeField] private GameObject objective;
        [SerializeField] private GameObject skipHint;

        private void OnEnable()
        {
            if (fade == null || letterbox == null || subtitle == null || watchReadout == null
                || objective == null || skipHint == null)
            {
                Debug.LogError("Assign every IntroOverlay reference.", this);
                enabled = false;
            }
        }

        public void SetFade(float alpha)
        {
            Color color = fade.color;
            color.a = Mathf.Clamp01(alpha);
            fade.color = color;
        }

        public void SetLetterbox(bool visible) => letterbox.SetActive(visible);

        public void SetSubtitle(string line, Color color)
        {
            subtitle.text = line;
            subtitle.color = color;
        }

        public void SetWatchReadout(float alpha) => watchReadout.alpha = Mathf.Clamp01(alpha);
        public void SetObjective(bool visible) => objective.SetActive(visible);
        public void SetSkipHint(bool visible) => skipHint.SetActive(visible);
    }
}
