using Boki.Player;
using Boki.UI;
using UnityEngine;

namespace Boki.Combat
{
    // Added by AutoLaser, like EnemyHealth adds EnemyFeedback, so every scene with a laser gets the combo.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AutoLaser))]
    public sealed class ComboTracker : MonoBehaviour
    {
        private AutoLaser laser;
        private PlayerHealth health;
        private int lastHealth;
        public ComboCounter Counter { get; } = new ComboCounter();

        private void Awake()
        {
            laser = GetComponent<AutoLaser>();
            health = laser.Health;
            if (!TryGetComponent<ComboView>(out _)) gameObject.AddComponent<ComboView>();
        }

        private void OnEnable()
        {
            if (health == null)
            {
                Debug.LogError("ComboTracker needs the AutoLaser's PlayerHealth.", this);
                enabled = false;
                return;
            }
            Counter.Reset();
            lastHealth = health.Current;
            laser.Hit += OnHit;
            health.Changed += OnHealthChanged;
            health.Died += Counter.Reset;
        }

        private void OnDisable()
        {
            if (laser != null) laser.Hit -= OnHit;
            if (health == null) return;
            health.Changed -= OnHealthChanged;
            health.Died -= Counter.Reset;
        }

        private void OnHit() => Counter.RegisterHit();

        private void OnHealthChanged()
        {
            // Healing and a fresh run raise health; only a drop breaks the streak.
            if (health.Current < lastHealth) Counter.Reset();
            lastHealth = health.Current;
        }
    }
}
