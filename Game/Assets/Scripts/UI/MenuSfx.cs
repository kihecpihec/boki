using UnityEngine;

namespace Boki.UI
{
    // One-shot menu feedback. Keeps working while paused because AudioSource ignores Time.timeScale.
    [DisallowMultipleComponent]
    public sealed class MenuSfx : MonoBehaviour
    {
        [SerializeField] private MenuScreen screen;
        [SerializeField] private AudioSource source;
        [SerializeField] private AudioClip select;
        [SerializeField] private AudioClip click;
        [SerializeField] private AudioClip back;
        [SerializeField] private AudioClip pause;
        [SerializeField, Range(0f, 1f)] private float volume = 0.7f;

        private void OnEnable()
        {
            if (screen == null || source == null || select == null || click == null || back == null || pause == null)
            {
                Debug.LogError("Assign MenuSfx screen, an AudioSource and all four clips.", this);
                enabled = false;
                return;
            }
            source.playOnAwake = false;
            source.loop = false;
            screen.Selected += PlaySelect;
            screen.Confirmed += PlayClick;
            screen.Backed += PlayBack;
            screen.PauseShown += PlayPause;
        }

        private void OnDisable()
        {
            if (screen == null) return;
            screen.Selected -= PlaySelect;
            screen.Confirmed -= PlayClick;
            screen.Backed -= PlayBack;
            screen.PauseShown -= PlayPause;
        }

        private void PlaySelect() => source.PlayOneShot(select, volume);
        private void PlayClick() => source.PlayOneShot(click, volume);
        private void PlayBack() => source.PlayOneShot(back, volume);
        private void PlayPause() => source.PlayOneShot(pause, volume);
    }
}
