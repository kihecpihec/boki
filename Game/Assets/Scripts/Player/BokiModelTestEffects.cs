using Boki.Combat;
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
        private int previousHealth;
        private EnemyHealth target;

        private void OnEnable()
        {
            previousHealth = health.Current;
            health.Changed += OnHealthChanged;
        }

        private void OnHealthChanged()
        {
            if (health.Current < previousHealth) effects.PlayDamageFlash();
            previousHealth = health.Current;
        }

        private void LateUpdate()
        {
            effects.SetBoostActive(movement.isActiveAndEnabled && health.IsAlive && movement.BoostRemaining > 0f);
            Keyboard keyboard = Keyboard.current;
            if (!Application.isFocused || Cursor.lockState != CursorLockMode.Locked || keyboard == null) return;
            if (keyboard.hKey.wasPressedThisFrame) health.TakeDamage(5);
            if (keyboard.tKey.wasPressedThisFrame) SpawnLaserTarget();
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
            health.Changed -= OnHealthChanged;
            effects.SetBoostActive(false);
            effects.ClearTrails();
            if (target != null) Destroy(target.gameObject);
        }
    }
}
