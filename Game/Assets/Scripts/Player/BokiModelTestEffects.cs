using Boki.Combat;
using Boki.Battery;
using Boki.Core;
using Boki.Enemies;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Boki.Player
{
    // Scene-local bridge: shared controller, health, and player prefabs stay unchanged.
    [DisallowMultipleComponent]
    public sealed class BokiModelTestEffects : MonoBehaviour
    {
        [SerializeField] private ScooterController movement;
        [SerializeField] private PlayerHealth health;
        [SerializeField] private BokiPlayerEffects effects;
        [SerializeField] private Material targetMaterial;
        [SerializeField] private LightBattery battery;
        [SerializeField] private ScooterPresentation presentation;
        [SerializeField] private BatteryCharge[] charges = new BatteryCharge[0];
        private RunSession session;
        private int previousXP;
        private float warningPreview;
        private bool subscribed;
        private int previousHealth;
        private EnemyHealth target;

        private void OnEnable()
        {
            if (movement == null || health == null || effects == null || battery == null || presentation == null)
            {
                Debug.LogError("Assign movement, health, effects, battery and presentation on BokiModelTestEffects.", this);
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
            warningPreview = Mathf.Max(0f, warningPreview - Time.deltaTime);
            var guilt = session.State.Guilt;
            float warning = warningPreview > 0f || (guilt.IsActive && !guilt.AwaitingPlacement
                && (guilt.Phase == GuiltPhase.Warning || guilt.Phase == GuiltPhase.Chase)) ? 1f : 0f;
            battery.WarningStrength = presentation.GuiltWarningStrength = warning;
            Keyboard keyboard = Keyboard.current;
            if (!Application.isFocused || Cursor.lockState != CursorLockMode.Locked || keyboard == null) return;
            if (keyboard.hKey.wasPressedThisFrame) health.TakeDamage(5);
            if (keyboard.tKey.wasPressedThisFrame) SpawnLaserTarget();
            if (keyboard.gKey.wasPressedThisFrame) warningPreview = 3f;
            if (keyboard.xKey.wasPressedThisFrame) effects.PlayPickupGlow(false);
            if (keyboard.bKey.wasPressedThisFrame) battery.State.Current = Mathf.Max(0f, battery.State.Current - 25f);
        }

        public void SpawnLaserTarget()
        {
            if (target != null) Destroy(target.gameObject);
            GameObject dummy = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            dummy.name = "Model Test Laser Target";
            // Ignore Raycast keeps the dummy out of the environment-only obstruction mask.
            dummy.layer = 2;
            dummy.transform.position = transform.position + transform.forward * 5f + Vector3.up;
            dummy.GetComponent<Renderer>().sharedMaterial = targetMaterial;
            target = dummy.AddComponent<EnemyHealth>();
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
            warningPreview = 0f;
            if (battery != null) battery.WarningStrength = 0f;
            if (presentation != null) presentation.GuiltWarningStrength = 0f;
            if (effects == null) return;
            effects.SetBoostActive(false);
            effects.ClearTrails();
            if (target != null) Destroy(target.gameObject);
        }
    }
}
