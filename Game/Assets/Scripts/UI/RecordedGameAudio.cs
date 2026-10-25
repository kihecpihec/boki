using Boki.Core;
using Boki.Player;
using Boki.Routes;
using UnityEngine;
using UnityEngine.UI;

namespace Boki.UI
{
    // Scene-owned voices survive an enemy's destruction, with a fixed six-source budget.
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public sealed class RecordedGameAudio : MonoBehaviour
    {
        [SerializeField] private PlayerHealth player;
        [SerializeField] private V1GameFlow flow;
        [SerializeField] private RouteManager routes;
        [SerializeField] private Text subtitle;
        [SerializeField] private AudioSource[] enemySources;
        [SerializeField] private AudioSource dialogueSource;
        [SerializeField] private AudioClip[] enemyDamage;
        [SerializeField] private AudioClip[] enemyDeath;
        [SerializeField] private AudioClip enemyGreeting;
        [SerializeField] private AudioClip[] teleport;
        [SerializeField, Min(0f)] private float portalVoiceDistance = 8f;
        [SerializeField] private AudioClip professorQuestion;
        [SerializeField] private AudioClip bokiReply;
        [SerializeField] private AudioClip professorSaturday;
        [SerializeField] private AudioClip[] bokiReaction;
        private bool focused, worldPaused, dialoguePaused, greeted, ownsPause;
        private int lastDamage = -1, lastDeath = -1, lastTeleport = -1, lastReaction = -1;
        private int dialogueStage;
        private float remaining;
        private RunSession sequenceSession;
        private string previousSubtitle;
        private LevelPortal[] portals;
        private BoxCollider[] portalColliders;
        private bool[] portalVoicesPlayed;

        private void OnEnable()
        {
            if (player == null || dialogueSource == null || enemySources == null || enemySources.Length != 6
                || System.Array.IndexOf(enemySources, null) >= 0 || enemyDamage == null || enemyDamage.Length == 0
                || enemyDeath == null || enemyDeath.Length == 0)
            {
                Debug.LogError("Assign player, six enemy sources, dialogue source and recorded enemy clips.", this);
                enabled = false;
                return;
            }
            focused = Application.isFocused;
            worldPaused = dialoguePaused = greeted = false;
        }

        private void Start()
        {
            portals = FindObjectsByType<LevelPortal>(FindObjectsSortMode.None);
            portalColliders = new BoxCollider[portals.Length];
            portalVoicesPlayed = new bool[portals.Length];
            for (int i = 0; i < portals.Length; i++)
                portalColliders[i] = portals[i].GetComponent<BoxCollider>();
        }

        private void PlayNearbyPortalVoice()
        {
            if (!CanPlayWorld || dialogueStage != 0 || dialogueSource.isPlaying || portals == null) return;
            for (int i = 0; i < portals.Length; i++)
            {
                if (portalVoicesPlayed[i] || portals[i] == null || !portals[i].isActiveAndEnabled
                    || portals[i].gameObject.scene != gameObject.scene
                    || portalColliders[i] == null || !portalColliders[i].enabled) continue;
                Vector3 nearest = portalColliders[i].ClosestPoint(player.transform.position);
                if ((nearest - player.transform.position).sqrMagnitude > portalVoiceDistance * portalVoiceDistance) continue;
                AudioClip clip = AudioVariations.Next(null, teleport, ref lastTeleport);
                if (clip == null) return;
                portalVoicesPlayed[i] = true;
                PlayDialogue(clip);
                return;
            }
        }

        private void OnApplicationFocus(bool value) => focused = value;
        private bool CanPlayWorld => isActiveAndEnabled && focused && player.IsAlive
            && (flow == null || !flow.IsTerminal)
            && (RunSession.Instance == null || RunSession.Instance.IsGameplayActive);

        public void PlayEnemyGreeting(Vector3 position)
        {
            if (greeted || enemyGreeting == null || !CanPlayWorld) return;
            greeted = PlayWorld(enemyGreeting, position, false);
        }
        public void PlayEnemyDamage(Vector3 position)
        {
            if (CanPlayWorld) PlayWorld(AudioVariations.Next(null, enemyDamage, ref lastDamage), position, false);
        }
        public void PlayEnemyDeath(Vector3 position)
        {
            if (CanPlayWorld) PlayWorld(AudioVariations.Next(null, enemyDeath, ref lastDeath), position, true);
        }

        private bool PlayWorld(AudioClip clip, Vector3 position, bool important)
        {
            if (clip == null) return false;
            int slot = -1;
            for (int i = 0; i < enemySources.Length; i++)
                if (!enemySources[i].isPlaying) { slot = i; break; }
            if (slot < 0 && important) slot = 0;
            if (slot < 0) return false;
            AudioSource source = enemySources[slot];
            source.Stop();
            source.transform.position = position;
            source.clip = clip;
            source.Play();
            return true;
        }

        public void PlayBossOpening()
        {
            if (!isActiveAndEnabled || dialogueStage != 0 || professorQuestion == null || bokiReply == null) return;
            sequenceSession = RunSession.Ensure();
            ownsPause = !sequenceSession.State.Pause.Contains(PauseReason.ScriptedSequence);
            if (ownsPause) sequenceSession.SetPaused(PauseReason.ScriptedSequence, true);
            previousSubtitle = subtitle != null ? subtitle.text : null;
            dialogueStage = 1;
            ShowLine(professorQuestion, "»Boki. Koliko je ura?«");
        }

        public void PlayBossVictory()
        {
            if (!isActiveAndEnabled || professorSaturday == null) return;
            EndSequence();
            dialogueStage = 3;
            ShowLine(professorSaturday, "»Boki ... danes je sobota.«");
        }

        private void ShowLine(AudioClip clip, string text)
        {
            if (subtitle != null) subtitle.text = text;
            PlayDialogue(clip);
            remaining = clip != null ? clip.length + 0.2f : 0.2f;
        }
        private void PlayDialogue(AudioClip clip)
        {
            if (clip == null) return;
            dialogueSource.Stop();
            dialogueSource.clip = clip;
            dialogueSource.Play();
        }

        private void Update()
        {
            PlayNearbyPortalVoice();
            bool terminal = !player.IsAlive || (flow != null && flow.IsTerminal);
            bool pauseWorld = !focused || terminal
                || (RunSession.Instance != null && !RunSession.Instance.IsGameplayActive);
            if (terminal)
            {
                foreach (AudioSource source in enemySources) source.Stop();
            }
            else if (pauseWorld != worldPaused)
            {
                foreach (AudioSource source in enemySources)
                    if (pauseWorld) source.Pause(); else source.UnPause();
            }
            worldPaused = pauseWorld;
            // Scripted speech and the ending use unscaled time; a menu/focus loss still pauses them.
            bool pauseDialogue = !focused || (dialogueStage == 0 && pauseWorld)
                || ((flow == null || !flow.IsTerminal) && RunSession.Instance != null
                    && RunSession.Instance.State.Pause.Contains(PauseReason.Menu));
            if (pauseDialogue != dialoguePaused)
            {
                if (pauseDialogue) dialogueSource.Pause(); else dialogueSource.UnPause();
                dialoguePaused = pauseDialogue;
            }
            if (dialogueStage == 0 || pauseDialogue) return;
            if (!player.IsAlive) { dialogueSource.Stop(); EndSequence(); return; }
            remaining -= Time.unscaledDeltaTime;
            if (remaining > 0f) return;
            switch (dialogueStage)
            {
                case 1: dialogueStage = 2; ShowLine(bokiReply, "»Saj sem skoraj pravočasno ...«"); break;
                case 3:
                    dialogueStage = 4;
                    ShowLine(AudioVariations.Next(null, bokiReaction, ref lastReaction), "»... A?«");
                    break;
                default: EndSequence(); break;
            }
        }

        private void EndSequence()
        {
            if (ownsPause && sequenceSession != null)
                sequenceSession.SetPaused(PauseReason.ScriptedSequence, false);
            ownsPause = false;
            if (subtitle != null && previousSubtitle != null) subtitle.text = previousSubtitle;
            previousSubtitle = null;
            dialogueStage = 0;
        }
        private void OnDisable()
        {
            EndSequence();
            if (dialogueSource != null) dialogueSource.Stop();
            if (enemySources != null)
                foreach (AudioSource source in enemySources) if (source != null) source.Stop();
        }
    }
}
