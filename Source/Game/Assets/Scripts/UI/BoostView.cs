using Boki.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Boki.UI
{
    [DisallowMultipleComponent]
    public sealed class BoostView : MonoBehaviour
    {
        [SerializeField] private ScooterController movement;
        [SerializeField] private Text statusText;
        [SerializeField] private Image progress;

        private void OnEnable()
        {
            if (movement != null && statusText != null && progress != null) return;
            Debug.LogError("Assign movement, status text and the progress image on BoostView.", this);
            enabled = false;
        }

        private void Update()
        {
            bool active = movement.BoostRemaining > 0f;
            bool recharging = movement.CooldownRemaining > 0f;
            string status = active ? "BOOST ACTIVE" : recharging ? "BOOST RECHARGING" : movement.PanicReady ? "PANIC READY · LEFT SHIFT" : "BOOST READY · LEFT SHIFT";
            if (statusText.text != status) statusText.text = status;
            progress.fillAmount = active
                ? movement.BoostRemaining / Mathf.Max(0.001f, movement.BoostDuration)
                : recharging ? 1f - movement.CooldownRemaining / Mathf.Max(0.001f, movement.BoostCooldown) : 1f;
        }
    }
}
