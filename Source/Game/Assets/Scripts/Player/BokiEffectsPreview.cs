using UnityEngine;
using UnityEngine.InputSystem;

namespace Boki.Player
{
    /// <summary>Preview motion only; this is not the scooter gameplay controller.</summary>
    [RequireComponent(typeof(BokiPlayerEffects))]
    public sealed class BokiEffectsPreview : MonoBehaviour
    {
        private BokiPlayerEffects effects;
        private float elapsed;
        private float angle;
        private int lastHitCycle;

        private void Awake()
        {
            effects = GetComponent<BokiPlayerEffects>();
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            Keyboard keyboard = Keyboard.current;
            bool boosting = elapsed % 4f < 0.6f || (keyboard != null && keyboard.leftShiftKey.isPressed);
            effects.SetBoostActive(boosting);

            angle += Time.deltaTime * (boosting ? 2.2f : 1.4f);
            transform.position = new Vector3(Mathf.Sin(angle) * 5f, 0f, Mathf.Cos(angle) * 5f);
            transform.rotation = Quaternion.LookRotation(new Vector3(Mathf.Cos(angle), 0f, -Mathf.Sin(angle)));

            int hitCycle = Mathf.FloorToInt(elapsed / 3f);
            if (hitCycle != lastHitCycle || (keyboard != null && keyboard.hKey.wasPressedThisFrame))
                effects.PlayDamageFlash();
            lastHitCycle = hitCycle;
        }

        private void OnDisable()
        {
            if (effects != null)
                effects.SetBoostActive(false);
        }
    }
}
