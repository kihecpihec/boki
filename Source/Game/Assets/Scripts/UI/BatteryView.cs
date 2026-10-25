using Boki.Battery;
using UnityEngine;
using UnityEngine.UI;

namespace Boki.UI
{
    public sealed class BatteryView : MonoBehaviour
    {
        [SerializeField] private LightBattery battery;
        [SerializeField] private Text statusText;
        private int lastCharge = -1;
        private int lastCapacity = -1;
        private void OnEnable()
        {
            if (battery != null && statusText != null) return;
            Debug.LogError("Assign Battery and Status Text on BatteryView.", this);
            enabled = false;
        }
        private void Update()
        {
            int charge = Mathf.CeilToInt(battery.State.Current);
            int capacity = Mathf.CeilToInt(battery.State.Capacity);
            if (charge == lastCharge && capacity == lastCapacity) return;
            lastCharge = charge;
            lastCapacity = capacity;
            statusText.text = charge == 0 ? "LIGHT EMPTY · ENEMIES +30% SPEED" : $"LIGHT {charge} / {capacity}";
            statusText.color = charge <= 20 ? new Color(1f, 0.45f, 0.25f) : new Color(0.65f, 1f, 0.9f);
        }
    }
}
