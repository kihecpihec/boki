using Boki.Combat;
using Boki.Battery;
using Boki.Core;
using Boki.Enemies;
using UnityEngine;

namespace Boki.Player
{
    // Production feedback bridge; contains no test controls or gameplay rules.
    [DisallowMultipleComponent]
    public sealed class PlayerGameplayFeedback : MonoBehaviour
    {
        [SerializeField] private ScooterController movement;
        [SerializeField] private PlayerHealth health;
        [SerializeField] private BokiPlayerEffects effects;
        [SerializeField] private LightBattery battery;
        [SerializeField] private ScooterPresentation presentation;
        [SerializeField] private BatteryCharge[] charges = new BatteryCharge[0];
        private RunSession session;
        private int previousXP;
        private bool subscribed;
        private int previousHealth;

        private void OnEnable()
        {
            if (movement == null || health == null || effects == null || battery == null || presentation == null)
            {
                Debug.LogError("Assign movement, health, effects, battery and presentation on PlayerGameplayFeedback.", this);
                enabled = false;
                return;
            }
            session = RunSession.Ensure();
            previousXP = session.State.XP;
            previousHealth = health.Current;
            health.Changed += OnHealthChanged;
            foreach (BatteryCharge charge in charges)
                if (charge != null) charge.Collected += OnBatteryCollected;
            subscribed = true;
        }

        private void OnHealthChanged()
        {
            if (health.Current < previousHealth) effects.PlayDamageFlash();
            previousHealth = health.Current;
        }

        private void OnBatteryCollected() => effects.PlayPickupGlow(true);

        private void Update()
        {
            effects.SetBoostActive(movement.isActiveAndEnabled && health.IsAlive && movement.BoostRemaining > 0f);
            if (!session.IsGameplayActive || Time.deltaTime <= 0f || !health.IsAlive) return;
            if (session.State.XP > previousXP) effects.PlayPickupGlow(false);
            previousXP = session.State.XP;
            var guilt = session.State.Guilt;
            float warning = (guilt.IsActive && !guilt.AwaitingPlacement
                && (guilt.Phase == GuiltPhase.Warning || guilt.Phase == GuiltPhase.Chase)) ? 1f : 0f;
            battery.WarningStrength = presentation.GuiltWarningStrength = warning;
        }

        private void OnDisable()
        {
            if (subscribed)
            {
                health.Changed -= OnHealthChanged;
                foreach (BatteryCharge charge in charges)
                    if (charge != null) charge.Collected -= OnBatteryCollected;
            }
            subscribed = false;
            if (battery != null) battery.WarningStrength = 0f;
            if (presentation != null) presentation.GuiltWarningStrength = 0f;
            if (effects == null) return;
            effects.SetBoostActive(false);
            effects.ClearTrails();
        }
    }
}
