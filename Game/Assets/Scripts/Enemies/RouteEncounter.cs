using Boki.Battery;
using Boki.Combat;
using Boki.Core;
using Boki.Player;
using UnityEngine;

namespace Boki.Enemies
{
    public sealed class RouteEncounter : MonoBehaviour
    {
        [SerializeField] private BatteryCombatSpace space;
        [SerializeField] private PlayerHealth player;
        [SerializeField] private Camera viewCamera;
        [SerializeField] private Transform[] spawnPoints;
        [SerializeField] private GameObject[] templates;
        [SerializeField] private int desiredCount = 4;
        [SerializeField] private int maximumCount = 8;
        [SerializeField] private float cadence = 4f;
        private readonly GameObject[] spawned = new GameObject[12];
        private float timer;
        private int nextPoint, nextTemplate;

        private void OnEnable()
        {
            if (space != null && player != null && viewCamera != null && spawnPoints != null
                && spawnPoints.Length > 0 && templates != null && templates.Length > 0) return;
            Debug.LogError("Assign combat space, player, camera, spawn points and inactive templates on RouteEncounter.", this);
            enabled = false;
        }
        private void Update()
        {
            var session = RunSession.Instance;
            if (session == null || !session.IsGameplayActive || session.HasEnteredBossArena || !space.State.Occupied) return;
            timer -= Time.deltaTime;
            if (timer > 0f) return;
            timer = cadence;
            int active = 0;
            foreach (var enemy in EnemyHealth.Active)
                if (enemy != null && enemy.IsAlive && enemy.GetComponent<ProfessorBoss>() == null) active++;
            if (active >= desiredCount || active >= maximumCount) return;
            int slot = -1;
            for (int i = 0; i < spawned.Length; i++) if (spawned[i] == null) { slot = i; break; }
            if (slot < 0) return;
            for (int i = 0; i < spawnPoints.Length; i++)
            {
                Vector3 point = spawnPoints[nextPoint++ % spawnPoints.Length].position;
                if ((point - player.transform.position).sqrMagnitude < 64f) continue;
                Vector3 viewport = viewCamera.WorldToViewportPoint(point + Vector3.up);
                if (viewport.z > 0f && viewport.x > -0.1f && viewport.x < 1.1f && viewport.y > -0.1f && viewport.y < 1.1f) continue;
                if (!Physics.Raycast(point + Vector3.up * 3f, Vector3.down, out var hit, 8f, 1, QueryTriggerInteraction.Ignore)) continue;
                point = hit.point + Vector3.up * 0.06f;
                if (Physics.CheckCapsule(point + Vector3.up * 0.5f, point + Vector3.up * 1.4f, 0.4f, 1, QueryTriggerInteraction.Ignore)) continue;
                var template = templates[nextTemplate++ % templates.Length];
                if (template.GetComponent<RangedEnemy>() != null) point += Vector3.up * 1.7f;
                var instance = Instantiate(template, point, Quaternion.identity, transform);
                spawned[slot] = instance;
                instance.SetActive(true);
                return;
            }
        }
        private void OnDisable()
        {
            foreach (var enemy in spawned) if (enemy != null) Destroy(enemy);
        }
    }
}
