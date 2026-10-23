using System;
using System.Collections.Generic;
using UnityEngine;

namespace Boki.Combat
{
    [DisallowMultipleComponent]
    public sealed class EnemyHealth : MonoBehaviour
    {
        private static readonly List<EnemyHealth> active = new List<EnemyHealth>();
        public static IReadOnlyList<EnemyHealth> Active => active;
        [SerializeField, Min(1)] private int maximumHealth = 60;
        [SerializeField] private Transform aimPoint;
        private HealthState state;
        public int Current => State.Current;
        public int Maximum => State.Maximum;
        public bool IsAlive => State.IsAlive;
        public Vector3 AimPosition => aimPoint != null ? aimPoint.position : transform.position + Vector3.up;
        public event Action Died;

        private HealthState State
        {
            get
            {
                if (state == null)
                {
                    state = new HealthState(Mathf.Max(1, maximumHealth));
                    state.Died += Die;
                }
                return state;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRegistry() => active.Clear();
        private void Awake() { _ = State; }
        private void OnEnable() { if (IsAlive && !active.Contains(this)) active.Add(this); }
        private void OnDisable() => active.Remove(this);
        private void Die()
        {
            active.Remove(this);
            // Listeners can award rewards later; M2 intentionally grants none.
            Died?.Invoke();
            Destroy(gameObject);
        }
        public void TakeDamage(int damage)
        {
            if (isActiveAndEnabled) State.TakeDamage(damage);
        }
    }
}
