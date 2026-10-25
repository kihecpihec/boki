using Boki.Combat;
using Boki.Core;
using Boki.Ending;
using Boki.Enemies;
using Boki.UI;
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
        [Tooltip("Optional. Plays the Story §3 victory ending, voices included, before the results panel.")]
        [SerializeField] private EndingCutscene ending;
        private bool entered, reinforced;
        private void OnEnable()
        {
            if (manager == null || flow == null || professor == null || bossHealth == null || entranceSeal == null || bossStatus == null)
            { Debug.LogError("Assign the V1 boss arena references.", this); enabled = false; return; }
            bossHealth.Died += Won;
            bossHealth.Damaged += Damaged;
        }
        private void OnDisable()
        {
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
