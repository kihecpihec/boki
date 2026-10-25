using Boki.Combat;
using Boki.Core;
using Boki.Ending;
using Boki.Enemies;
using Boki.UI;
using Boki.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Boki.Routes
{
    public sealed class V1BossArena : MonoBehaviour
    {
        [SerializeField] private RouteManager manager;
        [SerializeField] private V1GameFlow flow;
        [SerializeField] private ProfessorBoss professor;
        [SerializeField] private EnemyHealth bossHealth;
        [SerializeField] private GameObject entranceSeal;
        [SerializeField] private GameObject[] approachEncounters;
        [SerializeField] private GameObject[] reinforcementTemplates;
        [SerializeField] private Text bossStatus;
        [SerializeField] private RecordedGameAudio recordedAudio;
        [SerializeField] private AudioSource bossMusic;
        [SerializeField, Range(0f, 1f)] private float musicVolume = 0.22f;
        [SerializeField, Min(0.1f)] private float musicFadeSeconds = 2f;
        [Tooltip("Optional. Plays the Story §3 victory ending, voices included, before the results panel.")]
        [SerializeField] private EndingCutscene ending;
        private ThirdPersonCamera followCamera;
        private PlayerHealth player;
        private bool entered, reinforced;
        private bool focused;

        private void OnApplicationFocus(bool value) => focused = value;

        private void Start()
        {
            focused = Application.isFocused;
            followCamera = FindFirstObjectByType<ThirdPersonCamera>();
            player = FindFirstObjectByType<PlayerHealth>();
            if (bossMusic == null) return;
            bossMusic.playOnAwake = false;
            bossMusic.loop = true;
            bossMusic.spatialBlend = 0f;
            bossMusic.volume = 0f;
            bossMusic.Stop();
        }

        private void Update()
        {
            bool opening = entered && recordedAudio != null && recordedAudio.IsBossOpening;
            if (followCamera != null) followCamera.SetDialogueTarget(opening ? professor.transform : null);
            if (bossMusic == null) return;
            bool live = entered && bossHealth.IsAlive && player != null && player.IsAlive && !flow.IsTerminal;
            if (!live) { bossMusic.Stop(); bossMusic.volume = 0f; return; }
            bool playing = !opening && focused && RunSession.Instance.IsGameplayActive;
            if (!playing) { bossMusic.Pause(); return; }
            if (!bossMusic.isPlaying)
            {
                bossMusic.UnPause();
                if (!bossMusic.isPlaying) bossMusic.Play();
            }
            bossMusic.volume = Mathf.MoveTowards(bossMusic.volume, musicVolume,
                musicVolume / musicFadeSeconds * Time.unscaledDeltaTime);
        }
        private void OnEnable()
        {
            if (manager == null || flow == null || professor == null || bossHealth == null || entranceSeal == null || bossStatus == null)
            { Debug.LogError("Assign the V1 boss arena references.", this); enabled = false; return; }
            bossHealth.Died += Won;
            bossHealth.Damaged += Damaged;
        }
        private void OnDisable()
        {
            if (followCamera != null) followCamera.SetDialogueTarget(null);
            if (bossMusic != null) bossMusic.Stop();
            if (bossHealth == null) return;
            bossHealth.Died -= Won;
            bossHealth.Damaged -= Damaged;
        }
        private void OnTriggerEnter(Collider other) => Enter(other);
        private void OnTriggerStay(Collider other) => Enter(other);
        private void Enter(Collider other)
        {
            if (entered || !manager.IsPlayer(other) || !manager.IsReady || !RunSession.Instance.EnterBossArena()) return;
            entered = true;
            foreach (var encounter in approachEncounters) encounter.SetActive(false);
            entranceSeal.SetActive(true);
            professor.gameObject.SetActive(true);
            if (RunSession.Instance.State.PowerOff) bossHealth.TakeDamage(Mathf.RoundToInt(bossHealth.Maximum * 0.15f));
            professor.BeginFight();
            flow.SetObjective("Defeat the professor. Dodge the papers and leave the warning circle.");
            if (followCamera != null)
            {
                Vector3 direction = professor.transform.position - other.transform.position;
                followCamera.SnapHeading(Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg);
            }
            if (recordedAudio != null) recordedAudio.PlayBossOpening();
            Damaged();
        }
        private void Damaged()
        {
            // ProfessorHealthBar owns the boss display; retain the scene reference for older scenes.
            bossStatus.text = "";
            if (!entered || reinforced || bossHealth.Current > bossHealth.Maximum / 2
                || RunSession.Instance.State.Choice(RouteLevel.Feri) != RouteChoice.B) return;
            reinforced = true;
            foreach (var template in reinforcementTemplates)
            {
                var enemy = Instantiate(template, template.transform.position, template.transform.rotation, transform.parent);
                enemy.SetActive(true);
            }
        }
        private void Won()
        {
            bossStatus.text = "";
            // The ending speaks its own lines; without one the recorded victory lines play over the results panel.
            // A fallen player already has the ABSENT panel, which Victory leaves alone, so no ending plays over it.
            if (ending != null && !flow.IsTerminal)
            {
                ending.Play(flow.Victory);
                return;
            }
            flow.Victory();
            if (recordedAudio != null) recordedAudio.PlayBossVictory();
        }
    }
}
