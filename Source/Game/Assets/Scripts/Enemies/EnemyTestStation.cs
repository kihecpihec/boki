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
        [SerializeField] private string description;
        private GameObject enemy;
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
            enemy.SetActive(true);
            var guilt = enemy.GetComponent<GuiltChaser>();
            if (guilt != null) guilt.SetPressureActive(true);
        }

        public void End()
        {
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
