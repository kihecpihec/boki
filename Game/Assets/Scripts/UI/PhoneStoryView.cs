using Boki.Core;
using Boki.Enemies;
using Boki.Routes;
using UnityEngine;
using UnityEngine.UI;

namespace Boki.UI
{
    [DisallowMultipleComponent]
    public sealed class PhoneStoryView : MonoBehaviour
    {
        [SerializeField] private CanvasGroup card;
        [SerializeField] private Text message;
        [SerializeField] private AudioSource source;
        [SerializeField] private AudioClip notification;
        [SerializeField] private AudioClip reaction;
        [SerializeField, Min(1f)] private float displaySeconds = 6f;
        private RunSession session;
        private float remaining;
        private bool audioPaused;
        private bool reactionPending;
        private bool guiltTailArmed;
        private bool warningTail;
        private RouteLevel shownLevel;
        private RouteChoice initialChoice;
        private int initialHealth;
        private const string ForestMessage = "OLD CONVERSATION · FRIEND\nBoki: One drink. I'm leaving early.\nFriend: You said that last time.";
        private const string CityMessage = "DELAYED MESSAGE · FRIEND\nDid you make it home?";
        private const string FeriMessage = "PERSONAL REMINDER\nJust show up this time.";

        private void OnEnable()
        {
            if (card == null || message == null || source == null || notification == null)
            {
                Debug.LogError("Assign PhoneStoryView card, message, source and notification.", this);
                enabled = false;
                return;
            }
            session = RunSession.Ensure();
            session.State.Guilt.PhaseChanged += OnGuiltPhaseChanged;
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
            source.priority = 200;
            Hide();
        }

        public void TryShow(RouteLevel level)
        {
            if (!isActiveAndEnabled || !session.IsGameplayActive
                || !session.State.TryConsumePhoneStory(level)) return;
            // The trigger owns the safe window: an occupied arena consumes and skips the beat.
            if (session.State.Guilt.IsActive || session.State.Guilt.ScriptedSequenceActive) return;
            shownLevel = level;
            guiltTailArmed = level == RouteLevel.Feri;
            initialChoice = session.State.Choice(level);
            initialHealth = session.State.Health.Current;
            message.text = level == RouteLevel.Forest ? ForestMessage : level == RouteLevel.City ? CityMessage : FeriMessage;
            remaining = displaySeconds;
            card.alpha = 1f;
            source.pitch = level == RouteLevel.Feri ? 0.65f : 1f;
            source.clip = notification;
            source.Play();
            reactionPending = reaction != null;
        }

        private void Update()
        {
            if (remaining <= 0f && !warningTail) return;
            if (!session.State.Health.IsAlive || session.IsLoading || session.HasEnteredBossArena
                || session.State.ActiveLevel != shownLevel
                || session.State.Choice(shownLevel) != initialChoice || session.State.Health.Current < initialHealth
                || (!warningTail && (session.State.Guilt.IsActive
                    || session.State.Guilt.ScriptedSequenceActive)))
            {
                Hide();
                return;
            }
            bool pause = !session.IsGameplayActive;
            if (pause != audioPaused)
            {
                if (pause) source.Pause(); else source.UnPause();
                audioPaused = pause;
            }
            if (pause) return;
            if (warningTail)
            {
                if (!source.isPlaying) Hide();
                return;
            }
            remaining -= Time.deltaTime;
            // FERI's familiar beep slowly drops in pitch; existing Guilt warnings retain priority.
            if (shownLevel == RouteLevel.Feri) source.pitch = Mathf.Lerp(0.65f, 0.25f, Mathf.Clamp01((displaySeconds - remaining) / 1.5f));
            if (reactionPending && !source.isPlaying)
            {
                reactionPending = false;
                source.pitch = 1f;
                source.clip = reaction;
                source.Play();
            }
            if (remaining <= 0f) Hide();
        }

        private void OnGuiltPhaseChanged(GuiltPhase phase)
        {
            if (!guiltTailArmed || phase != GuiltPhase.Warning || !session.IsGameplayActive
                || session.State.ActiveLevel != RouteLevel.Feri || session.HasEnteredBossArena) return;
            guiltTailArmed = false;
            Hide();
            warningTail = true;
            initialChoice = session.State.Choice(RouteLevel.Feri);
            initialHealth = session.State.Health.Current;
            source.clip = notification;
            source.pitch = 0.25f;
            source.Play();
        }

        private void Hide()
        {
            remaining = 0f;
            reactionPending = audioPaused = warningTail = false;
            if (card != null) card.alpha = 0f;
            if (source != null) source.Stop();
        }
        private void OnDisable()
        {
            if (session != null) session.State.Guilt.PhaseChanged -= OnGuiltPhaseChanged;
            guiltTailArmed = false;
            Hide();
        }
    }
}
