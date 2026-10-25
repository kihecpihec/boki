using UnityEngine;
using UnityEngine.UI;

namespace Boki.UI
{
    // Presentation only: ArenaIntroCutscene decides what to show and when.
    [DisallowMultipleComponent]
    public sealed class ArenaIntroOverlay : MonoBehaviour
    {
        [SerializeField] private Image flash;
        [SerializeField] private GameObject letterbox;
        [SerializeField] private Text subtitle;
        [SerializeField] private GameObject subtitleBacking;
        [SerializeField] private GameObject skipHint;

        private void OnEnable()
        {
            if (flash == null || letterbox == null || subtitle == null || subtitleBacking == null || skipHint == null)
            {
                Debug.LogError("Assign every ArenaIntroOverlay reference.", this);
                enabled = false;
            }
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
            subtitle.text = line;
            subtitle.color = color;
            subtitleBacking.SetActive(!string.IsNullOrEmpty(line));
        }
    }
}
