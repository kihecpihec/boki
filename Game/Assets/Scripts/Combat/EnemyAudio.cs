using Boki.UI;
using UnityEngine;

namespace Boki.Combat
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyHealth))]
    public sealed class EnemyAudio : MonoBehaviour
    {
        [SerializeField] private RecordedGameAudio output;
        [SerializeField] private bool professor;
        private EnemyHealth health;
        private float nextHit;

        private void Awake() => health = GetComponent<EnemyHealth>();
        private void OnEnable()
        {
            if (output == null)
            {
                Debug.LogError("Assign the scene's recorded audio output on EnemyAudio.", this);
                enabled = false;
                return;
            }
            nextHit = 0f;
            health.Damaged += Damaged;
            health.Died += Died;
            if (!professor) output.PlayEnemyGreeting(health.AimPosition);
        }
        private void OnDisable()
        {
            if (health == null) return;
            health.Damaged -= Damaged;
            health.Died -= Died;
        }
        private void Damaged()
        {
            if (!health.IsAlive || Time.time < nextHit) return;
            nextHit = Time.time + 0.15f;
            output.PlayEnemyDamage(health.AimPosition);
        }
        private void Died()
        {
            if (!professor) output.PlayEnemyDeath(health.AimPosition);
        }
    }
}
