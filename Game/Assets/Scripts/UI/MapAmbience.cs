using Boki.Core;
using Boki.Player;
using UnityEngine;

namespace Boki.UI
{
    [DisallowMultipleComponent]
    public sealed class MapAmbience : MonoBehaviour
    {
        [SerializeField] private PlayerHealth player;
        [SerializeField] private V1GameFlow flow;
        [SerializeField] private RecordedGameAudio dialogue;
        [SerializeField] private AudioSource bedSource;
        [SerializeField] private AudioSource detailSource;
        [SerializeField] private AudioClip[] details;
        [SerializeField, Range(0f, 1f)] private float bedVolume = 0.16f;
        [SerializeField, Range(0f, 1f)] private float detailVolume = 0.12f;
        [SerializeField, Min(1f)] private float minimumInterval = 12f;
        [SerializeField, Min(1f)] private float maximumInterval = 24f;
        [SerializeField, Min(0.1f)] private float fadeSeconds = 2f;
        [SerializeField] private bool silenceInBossArena;
        private bool focused, paused;
        private float remaining;
        private int lastDetail = -1;

        private void OnEnable()
        {
            if (player == null || dialogue == null || bedSource == null || bedSource.clip == null
                || detailSource == null || detailSource == bedSource || details == null || details.Length == 0
                || System.Array.IndexOf(details, null) >= 0)
            {
                Debug.LogError("Assign map ambience player, dialogue, two sources, bed clip and detail clips.", this);
                enabled = false;
                return;
            }
            focused = Application.isFocused;
            paused = false;
            lastDetail = -1;
            remaining = NextInterval();
            bedSource.Stop();
            detailSource.Stop();
            bedSource.loop = true;
            bedSource.volume = 0f;
            detailSource.loop = false;
        }

        private void OnApplicationFocus(bool value) => focused = value;
        private float NextInterval() => Random.Range(minimumInterval, Mathf.Max(minimumInterval, maximumInterval));

        private void Update()
        {
            RunSession session = RunSession.Instance;
            // Story §6 keeps the boss approach quiet; the arena has its own music and ending.
            bool stopped = !player.IsAlive || (flow != null && flow.IsTerminal)
                || (silenceInBossArena && session != null && session.HasEnteredBossArena);
            if (stopped)
            {
                bedSource.Stop();
                detailSource.Stop();
                bedSource.volume = 0f;
                paused = false;
                return;
            }
            bool suspend = !focused || (session != null && !session.IsGameplayActive);
            if (suspend)
            {
                if (!paused) { bedSource.Pause(); detailSource.Pause(); paused = true; }
                return;
            }
            if (paused) { bedSource.UnPause(); detailSource.UnPause(); paused = false; }
            if (!bedSource.isPlaying) bedSource.Play();
            bool speaking = dialogue.IsDialoguePlaying;
            float target = bedVolume * (speaking ? 0.35f : 1f);
            bedSource.volume = Mathf.MoveTowards(bedSource.volume, target,
                bedVolume / fadeSeconds * Time.deltaTime);
            if (speaking)
            {
                // Leave room for portal dialogue as well as the scripted professor lines.
                detailSource.volume = Mathf.MoveTowards(detailSource.volume, 0f, Time.deltaTime);
                return;
            }
            remaining -= Time.deltaTime;
            if (remaining > 0f || detailSource.isPlaying) return;
            detailSource.clip = AudioVariations.Next(null, details, ref lastDetail);
            detailSource.volume = detailVolume * Random.Range(0.7f, 1f);
            detailSource.panStereo = Random.Range(-0.45f, 0.45f);
            detailSource.pitch = Random.Range(0.96f, 1.04f);
            detailSource.Play();
            remaining = detailSource.clip.length / detailSource.pitch + NextInterval();
        }

        private void OnDisable()
        {
            if (bedSource != null) { bedSource.Stop(); bedSource.volume = 0f; }
            if (detailSource != null) detailSource.Stop();
        }
    }
}
