using Boki.Combat;
using Boki.Player;
using UnityEngine;

namespace Boki.Enemies
{
    // Standalone demonstration only; production encounters use route-owned spawning.
    public sealed class EnemyCombatPreview : MonoBehaviour
    {
        [SerializeField] private PlayerHealth player;
        [SerializeField] private GameObject[] templates;
        private EnemyHealth[] enemies;
        private float retry;

        private void OnEnable()
        {
            if (player == null || templates == null || templates.Length == 0)
            {
                Debug.LogError("Assign the preview player and inactive enemy templates.", this);
                enabled = false;
                return;
            }
            enemies = new EnemyHealth[templates.Length];
            retry = 0f;
        }

        private void Update()
        {
            if (!player.IsAlive) return;
            retry -= Time.deltaTime;
            if (retry > 0f) return;
            retry = 4f;
            for (int i = 0; i < templates.Length; i++)
            {
                if (enemies[i] != null || templates[i] == null) continue;
                var template = templates[i];
                var enemy = Instantiate(template, template.transform.position, template.transform.rotation, transform);
                enemies[i] = enemy.GetComponent<EnemyHealth>();
                enemy.SetActive(true);
            }
        }

        private void OnDisable()
        {
            if (enemies == null) return;
            foreach (var enemy in enemies)
                if (enemy != null) Destroy(enemy.gameObject);
            enemies = null;
        }
    }
}
