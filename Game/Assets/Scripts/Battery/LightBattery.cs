using Boki.Core;
using Boki.Player;
using UnityEngine;

namespace Boki.Battery
{
    [DisallowMultipleComponent]
    public sealed class LightBattery : MonoBehaviour
    {
        [SerializeField] private PlayerHealth player;
        [SerializeField] private Light surroundingLight;
        [SerializeField] private Transform headlightMount;
        [SerializeField] private bool strongGuiltWarning;
        [SerializeField, Min(0f)] private float intensity = 5f;
        [SerializeField] private Vector3 headlightPosition = new Vector3(0f, 0.65f, 0.85f);
        [SerializeField, Range(0f, 45f)] private float headlightDownwardAngle = 15f;
        [SerializeField, Min(1f)] private float headlightRange = 15f;
        [SerializeField, Range(1f, 179f)] private float headlightAngle = 85f;
        [SerializeField, Min(0f)] private float fillIntensity = 0.55f;
        [SerializeField, Min(0.1f)] private float fillRange = 4.5f;
        [SerializeField, Min(1f)] private float headlightBrightnessMultiplier = 2f;
        [SerializeField, Min(1f)] private float nearbyBrightnessMultiplier = 6f;
        [SerializeField, Min(1f)] private float nearbyRangeMultiplier = 1.8f;
        public float ChargeFraction => Mathf.Clamp01(State.Current / State.Capacity);
        public float WarningStrength { get; set; }
        private float warningTime;
        private Light nearbyFill;
        private RunSession session;
        public LightBatteryState State => (session != null ? session : RunSession.Ensure()).State.LightBattery;
        public bool IsGameplayActive => isActiveAndEnabled && player != null
            && player.isActiveAndEnabled && player.IsAlive && session != null && session.IsGameplayActive;
        public float EnemySpeedMultiplier => State.EnemySpeedMultiplier;

        private void OnEnable()
        {
            if (player == null || surroundingLight == null)
            {
                Debug.LogError("Assign Player and Surrounding Light on LightBattery.", this);
                enabled = false;
                return;
            }
            session = RunSession.Ensure();
            if (!session.BindLight(this)) { enabled = false; return; }
            // Keep the existing serialized light reference so authored scenes need no rewiring.
            surroundingLight.transform.SetParent(headlightMount != null ? headlightMount : transform, false);
            surroundingLight.transform.localPosition = headlightMount != null ? Vector3.zero : headlightPosition;
            surroundingLight.transform.localRotation = Quaternion.Euler(headlightDownwardAngle, 0f, 0f);
            surroundingLight.type = LightType.Spot;
            surroundingLight.range = headlightRange;
            surroundingLight.spotAngle = headlightAngle;
            surroundingLight.innerSpotAngle = headlightAngle * 0.45f;
            surroundingLight.color = new Color(1f, 0.94f, 0.82f);
            surroundingLight.shadows = LightShadows.None;
            if (nearbyFill == null)
            {
                var fillObject = new GameObject("Scooter Nearby Fill");
                nearbyFill = fillObject.AddComponent<Light>();
            }
            nearbyFill.transform.SetParent(headlightMount != null ? headlightMount : transform, false);
            nearbyFill.transform.localPosition = headlightMount != null ? new Vector3(0f, -0.25f, -0.08f)
                : new Vector3(0f, 0.45f, 0.65f);
            nearbyFill.type = LightType.Point;
            nearbyFill.color = surroundingLight.color;
            nearbyFill.range = fillRange;
            nearbyFill.intensity = fillIntensity;
            nearbyFill.shadows = LightShadows.None;
            Refresh();
        }

        private void LateUpdate() => Refresh();
        private void Refresh()
        {
            surroundingLight.enabled = player.IsAlive && State.Current > 0f;
            if (IsGameplayActive) warningTime = Mathf.Repeat(warningTime + Time.deltaTime, 1f);
            float fraction = ChargeFraction;
            float visibility = Mathf.SmoothStep(0f, 1f, fraction);
            float warning = Mathf.Clamp01(WarningStrength);
            float pulse = warningTime < 0.16f || (warningTime > 0.28f && warningTime < 0.44f) ? 1f : 0f;
            float alarm = strongGuiltWarning ? warning : 0f;
            Color lampColor = Color.Lerp(new Color(1f, 0.94f, 0.82f),
                new Color(1f, 0.035f, 0.015f), alarm * (0.65f + pulse * 0.35f));
            surroundingLight.color = nearbyFill.color = lampColor;
            float flicker = 1f - Mathf.Clamp01(WarningStrength) * 0.45f
                * Mathf.Pow(Mathf.Sin(warningTime * Mathf.PI * 7f), 8f);
            if (strongGuiltWarning) flicker = Mathf.Lerp(1f, 0.25f + pulse * 1.75f, warning);
            // Fade reach as well as brightness so low charge leaves only the immediate riding area.
            surroundingLight.range = headlightRange * Mathf.Lerp(0.25f, 1f, visibility);
            nearbyFill.range = fillRange * nearbyRangeMultiplier * Mathf.Lerp(0.25f, 1f, visibility)
                * Mathf.Lerp(1f, 1.5f, alarm);
            surroundingLight.intensity = intensity * headlightBrightnessMultiplier * visibility * flicker;
            nearbyFill.intensity = fillIntensity * nearbyBrightnessMultiplier * visibility * flicker;
            nearbyFill.enabled = surroundingLight.enabled;
        }

        private void OnDisable()
        {
            if (session != null) session.UnbindLight(this);
            if (surroundingLight != null) surroundingLight.enabled = false;
            if (nearbyFill != null) nearbyFill.enabled = false;
        }
    }
}
