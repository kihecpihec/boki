using Boki.Combat;
using Boki.Core;
using UnityEngine;

namespace Boki.Progression
{
    [RequireComponent(typeof(EnemyHealth))]
    public sealed class EnemyReward : MonoBehaviour
    {
        [SerializeField] private GameObject xpPrefab;
        private EnemyHealth health;
        private bool rewarded;
        private void OnEnable()
        {
            health = GetComponent<EnemyHealth>();
            if (xpPrefab == null) { Debug.LogError("Assign XP Prefab on EnemyReward.", this); enabled = false; return; }
            health.Died += Reward;
        }
        private void OnDisable() { if (health != null) health.Died -= Reward; }
        private void Reward()
        {
            if (rewarded || RunSession.Instance == null) return;
            rewarded = true;
            var state = RunSession.Instance.State;
            state.KillCount++;
            if (!state.Progression.MaximumLevel)
                Instantiate(xpPrefab, transform.position + Vector3.up * 0.55f, Quaternion.identity, transform.parent);
        }
    }
}
