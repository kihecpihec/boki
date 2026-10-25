using UnityEngine;

namespace Boki.UI
{
    // Plays the looping menu track while the main menu (not the pause menu) is visible.
    [DisallowMultipleComponent]
    public sealed class MenuMusic : MonoBehaviour
    {
        [SerializeField] private MenuScreen screen;
        [SerializeField] private AudioSource source;
        [SerializeField, Range(0f, 1f)] private float volume = 0.6f;
        [SerializeField, Min(0.05f)] private float fadeSeconds = 0.75f;

        private void OnEnable()
        {
            if (screen == null || source == null || source.clip == null)
            {
                Debug.LogError("Assign MenuMusic screen and an AudioSource with a clip.", this);
                enabled = false;
                return;
            }
            source.loop = true;
            source.playOnAwake = false;
            source.volume = 0f;
        }

        private void OnDisable()
        {
            if (source != null) source.Stop();
        }

        private void Update()
        {
            float target = screen.IsMainMenuVisible ? volume : 0f;
            if (target > 0f && !source.isPlaying) source.Play();
            // Unscaled time keeps the fade working while the game is paused.
            source.volume = Mathf.MoveTowards(source.volume, target, volume / fadeSeconds * Time.unscaledDeltaTime);
            if (target == 0f && source.volume == 0f && source.isPlaying) source.Stop();
        }
    }
}
