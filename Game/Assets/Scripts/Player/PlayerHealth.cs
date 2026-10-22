using System;
using Boki.Combat;
using UnityEngine;

namespace Boki.Player
{
    [DisallowMultipleComponent]
    public sealed class PlayerHealth : MonoBehaviour
    {
        [SerializeField, Min(1)] private int maximumHealth = 100;
        private HealthState state;
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
                    state = new HealthState(Mathf.Max(1, maximumHealth));
                    state.Changed += () => Changed?.Invoke();
                    state.Died += () => Died?.Invoke();
                }
                return state;
            }
        }

        private void Awake() { _ = State; }
        public void TakeDamage(int damage)
        {
            if (isActiveAndEnabled) State.TakeDamage(damage);
        }
    }
}
