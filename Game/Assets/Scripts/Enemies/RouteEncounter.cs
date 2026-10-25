using System.Collections.Generic;
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
        [SerializeField, Min(1)] private int initialCount = 15;
        [SerializeField] private float cadence = 4f;
        private readonly List<GameObject> spawned = new List<GameObject>();
        private int initialSpawned;
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
            if (session == null || !session.IsGameplayActive || session.HasEnteredBossArena || session.State.Guilt.ScriptedSequenceActive || !space.State.Occupied) return;
            bool initial = initialSpawned < initialCount;
            if (!initial)
            {
                timer -= Time.deltaTime;
                if (timer > 0f) return;
                timer = EncounterCadence.Evaluate(cadence, session.State.Guilt.AccumulatedCombatSeconds);
            }
            spawned.RemoveAll(enemy => enemy == null);
            int attempts = initial ? initialCount - initialSpawned : 1;
            for (int attempt = 0; attempt < attempts; attempt++)
                if (TrySpawn(initial, session.State.Guilt.AccumulatedCombatSeconds) && initial)
                {
                    initialSpawned++;
                    timer = EncounterCadence.Evaluate(cadence, session.State.Guilt.AccumulatedCombatSeconds);
                }
        }

        private bool TrySpawn(bool initial, float combatSeconds)
        {
            for (int i = 0; i < spawnPoints.Length; i++)
            {
                int index = nextPoint++;
                Vector3 point = spawnPoints[index % spawnPoints.Length].position;
                // Spread the opening group around authored markers instead of stacking capsules.
                if (initial)
                {
                    float angle = index * 2.399963f;
                    point += new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * (1.5f + index % 4);
                }
                if ((point - player.transform.position).sqrMagnitude < 64f) continue;
                Vector3 viewport = viewCamera.WorldToViewportPoint(point + Vector3.up);
                if (!initial && viewport.z > 0f && viewport.x > -0.1f && viewport.x < 1.1f && viewport.y > -0.1f && viewport.y < 1.1f) continue;
                if (!Physics.Raycast(point + Vector3.up * 3f, Vector3.down, out var hit, 8f, 1, QueryTriggerInteraction.Ignore)) continue;
                point = hit.point + Vector3.up * 0.06f;
                if (Physics.CheckCapsule(point + Vector3.up * 0.5f, point + Vector3.up * 1.4f, 0.4f, 1, QueryTriggerInteraction.Ignore)) continue;
                bool crowded = false;
                foreach (var enemy in spawned)
                    if (enemy != null && (enemy.transform.position - point).sqrMagnitude < 2.25f)
                    {
                        crowded = true;
                        break;
                    }
                if (crowded) continue;
                var template = templates[nextTemplate++ % templates.Length];
                if (template.GetComponent<RangedEnemy>() != null) point += Vector3.up * 1.7f;
                var instance = Instantiate(template, point, Quaternion.identity, transform);
                spawned.Add(instance);
                instance.GetComponent<EnemyHealth>().ConfigureEncounterHealth(
                    EncounterCadence.HealthMultiplier(combatSeconds));
                if (instance.TryGetComponent<MeleeEnemy>(out var melee))
                    melee.SetDamageMultiplier(EncounterCadence.DamageMultiplier(combatSeconds));
                if (instance.TryGetComponent<RangedEnemy>(out var ranged))
                    ranged.SetDamageMultiplier(EncounterCadence.DamageMultiplier(combatSeconds));
                instance.SetActive(true);
                return true;
            }
            return false;
        }
        private void OnDisable()
        {
            foreach (var enemy in spawned) if (enemy != null) Destroy(enemy);
            spawned.Clear();
            initialSpawned = 0;
            timer = 0f;
        }
    }
}
