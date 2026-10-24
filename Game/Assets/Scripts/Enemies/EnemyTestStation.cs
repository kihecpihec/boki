using Boki.Combat;
using Boki.Core;
using Boki.Player;
using UnityEngine;

namespace Boki.Enemies
{
    [DisallowMultipleComponent]
    public sealed class EnemyTestStation : MonoBehaviour
    {
        [SerializeField] private PlayerTestArena arena;
        [SerializeField] private BoxCollider volume;
        [SerializeField] private Transform entry;
        [SerializeField] private GameObject enemyTemplate;
        [SerializeField] private ArenaXpPickup xpTemplate;
        [SerializeField] private Transform pickupRoot;
        [SerializeField] private string description;
        private GameObject enemy;
        private EnemyHealth health;
        public Transform Entry => entry;
        public GameObject Enemy => enemy;
        public string Description => description;
        public bool Contains(Vector3 position) => volume.bounds.Contains(position);
        public void Visit() => arena.Visit(this);

        public void Begin()
        {
            End();
            enemy = Instantiate(enemyTemplate, enemyTemplate.transform.position,
                enemyTemplate.transform.rotation, transform);
            enemy.name = enemyTemplate.name + " (Active)";
            health = enemy.GetComponent<EnemyHealth>();
            if (health != null) health.Died += OnEnemyDied;
            enemy.SetActive(true);
            var guilt = enemy.GetComponent<GuiltChaser>();
            if (guilt != null) guilt.SetPressureActive(true);
        }

        private void OnEnemyDied()
        {
            var state = RunSession.Ensure().State;
            state.KillCount++;
            // Story §5: only regular enemies leave one pack; kills never recharge battery.
            if (enemy.GetComponent<ProfessorBoss>() != null || state.Upgrades.Count >= 6) return;
            Vector3 position = enemy.transform.position;
            position.y = 0.65f;
            var pack = Instantiate(xpTemplate, position, Quaternion.identity, pickupRoot);
            pack.gameObject.SetActive(true);
        }

        public void End()
        {
            if (health != null) health.Died -= OnEnemyDied;
            health = null;
            if (enemy != null)
            {
                enemy.SetActive(false);
                Destroy(enemy);
            }
            enemy = null;
        }

        private void OnDisable() => End();
    }
}
