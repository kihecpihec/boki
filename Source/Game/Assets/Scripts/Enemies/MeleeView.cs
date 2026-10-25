using Boki.Combat;
using UnityEngine;

namespace Boki.Enemies
{
    // Presentation only: plays Melee_* clips from MeleeEnemy/EnemyHealth events.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeleeEnemy), typeof(EnemyHealth))]
    public sealed class MeleeView : MonoBehaviour
    {
        private static readonly int Idle = Animator.StringToHash("Melee_Idle");
        private static readonly int Pursuit = Animator.StringToHash("Melee_Pursuit");
        private static readonly int ContactState = Animator.StringToHash("Melee_Contact");
        private static readonly int DeathState = Animator.StringToHash("Melee_Death");
        [SerializeField] private Animator animator;
        [SerializeField, Min(0.1f)] private float contactSeconds = 0.5f;
        [SerializeField, Min(0.1f)] private float deathSeconds = 1.5f;
        private MeleeEnemy melee;
        private EnemyHealth health;
        private float contactUntil;
        private int current;

        private void OnEnable()
        {
            melee = GetComponent<MeleeEnemy>();
            health = GetComponent<EnemyHealth>();
            if (animator == null)
            {
                Debug.LogError("Assign the Animator on MeleeView.", this);
                enabled = false;
                return;
            }
            melee.Attacked += OnAttacked;
            health.Died += OnDied;
        }

        private void OnDisable()
        {
            melee.Attacked -= OnAttacked;
            health.Died -= OnDied;
        }

        private void Update()
        {
            if (Time.time < contactUntil) return;
            Play(melee.IsMoving ? Pursuit : Idle);
        }

        private void OnAttacked()
        {
            contactUntil = Time.time + contactSeconds;
            Play(ContactState);
        }

        // EnemyHealth destroys the root right after Died, so the model detaches to finish its clip.
        private void OnDied()
        {
            animator.transform.SetParent(null, true);
            animator.CrossFadeInFixedTime(DeathState, 0.05f);
            Destroy(animator.gameObject, deathSeconds);
        }

        private void Play(int state)
        {
            if (state == current) return;
            current = state;
            animator.CrossFadeInFixedTime(state, 0.1f);
        }
    }
}
