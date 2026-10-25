using System;
using System.Collections.Generic;
using UnityEngine;
using Boki.Core;
using Boki.Upgrades;

namespace Boki.Combat
{
    [DisallowMultipleComponent]
    public sealed class EnemyHealth : MonoBehaviour
    {
        private static readonly List<EnemyHealth> active = new List<EnemyHealth>();
        public static IReadOnlyList<EnemyHealth> Active => active;
        [SerializeField, Min(1)] private int maximumHealth = 60;
        [SerializeField] private Transform aimPoint;
        [SerializeField] private bool showCombatFeedback = true;
        private HealthState state;
        public int Current => State.Current;
        public int Maximum => State.Maximum;
        public bool IsAlive => State.IsAlive;
        public Vector3 AimPosition => aimPoint != null ? aimPoint.position : transform.position + Vector3.up;
        public event Action Died;
        public event Action Damaged;

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
        private void Awake()
        {
            _ = State;
            if (showCombatFeedback && GetComponent<EnemyFeedback>() == null)
                gameObject.AddComponent<EnemyFeedback>();
        }
        private void OnEnable() { if (IsAlive && !active.Contains(this)) active.Add(this); }
        private void OnDisable() => active.Remove(this);
        private void Die()
        {
            var session = RunSession.Instance;
            if (session != null && session.State.Health.IsAlive)
            {
                session.State.KillCount++;
                if (!session.State.Progression.IsMaxLevel && GetComponent<Boki.Enemies.ProfessorBoss>() == null)
                    XpPickup.Spawn(transform.position, gameObject.scene);
            }
            active.Remove(this);
            Died?.Invoke();
            Destroy(gameObject);
        }
        public void ConfigureEncounterHealth(float multiplier)
        {
            // Encounter templates are configured while inactive, before their first activation.
            if (gameObject.activeInHierarchy) throw new InvalidOperationException("Configure encounter health before activation.");
            if (state != null) state.Died -= Die;
            state = new HealthState(Mathf.Max(1, Mathf.RoundToInt(maximumHealth * multiplier)));
            state.Died += Die;
        }

        public void TakeDamage(int damage)
        {
            if (!isActiveAndEnabled || !IsAlive || damage <= 0) return;
            int previous = Current;
            State.TakeDamage(damage);
            if (Current < previous) Damaged?.Invoke();
        }
    }
}
