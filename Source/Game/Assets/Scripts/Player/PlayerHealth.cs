using System;
using Boki.Combat;
using Boki.Core;
using UnityEngine;

namespace Boki.Player
{
    [DisallowMultipleComponent]
    public sealed class PlayerHealth : MonoBehaviour
    {
        [SerializeField] private bool useRunSession;
        [SerializeField, Min(1)] private int maximumHealth = 100;
        private HealthState state;
        public bool UsesRunSession => useRunSession;
        public int Current => State.Current;
        public int Maximum => State.Maximum;
        public bool IsAlive => State.IsAlive;
        public event Action Changed;
        public event Action Died;

        private HealthState State
        {
            get
            {
                if (state == null)
                {
                    Bind(useRunSession ? RunSession.Ensure().State.Health
                        : new HealthState(Mathf.Max(1, maximumHealth)));
                }
                return state;
            }
        }

        private void Awake() { _ = State; }
        private void OnEnable()
        {
            if (useRunSession)
            {
                RunSession.Ensure().State.Changed += RebindRunState;
                Bind(RunSession.Instance.State.Health);
            }
            else Bind(State);
        }
        private void RebindRunState()
        {
            if (state != RunSession.Instance.State.Health) Bind(RunSession.Instance.State.Health);
        }
        private void OnDisable()
        {
            if (useRunSession && RunSession.Instance != null) RunSession.Instance.State.Changed -= RebindRunState;
            if (state != null) { state.Changed -= ForwardChanged; state.Died -= ForwardDeath; }
        }
        private void Bind(HealthState value)
        {
            if (state != null) { state.Changed -= ForwardChanged; state.Died -= ForwardDeath; }
            state = value;
            state.Changed += ForwardChanged;
            state.Died += ForwardDeath;
        }
        private void ForwardChanged() => Changed?.Invoke();
        private void ForwardDeath() => Died?.Invoke();
        private void OnDestroy()
        {
            if (state != null) { state.Changed -= ForwardChanged; state.Died -= ForwardDeath; }
        }
        public void TakeDamage(int damage)
        {
            if (isActiveAndEnabled) State.TakeDamage(damage);
        }
    }
}
