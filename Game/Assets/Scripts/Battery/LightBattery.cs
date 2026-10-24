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
        [SerializeField, Range(0f, 45f)] private float headlightDownwardAngle = 24f;
        [SerializeField, Min(1f)] private float headlightRange = 8f;
        [SerializeField, Range(1f, 179f)] private float headlightAngle = 125f;
        [SerializeField, Min(0f)] private float fillIntensity = 0.55f;
        [SerializeField, Min(0.1f)] private float fillRange = 2.5f;
        [SerializeField, Min(1f)] private float headlightBrightnessMultiplier = 4f;
        [SerializeField, Min(1f)] private float nearbyBrightnessMultiplier = 3f;
        [SerializeField, Min(1f)] private float nearbyRangeMultiplier = 1.4f;
        [SerializeField, Min(0f)] private float headlightHeightOffset = 1.2f;
        [SerializeField] private Vector3 riderFillPosition = new Vector3(0f, 1.2f, -0.35f);
        [SerializeField, Min(0f)] private float emptyFillIntensity = 0.12f;
        [SerializeField, Min(0.1f)] private float emptyFillRange = 2.2f;
        public float ChargeFraction => Mathf.Clamp01(State.Current / State.Capacity);
        public float WarningStrength { get; set; }
        public float Visibility { get; private set; }
        private static readonly Color LampColor = Color.white;
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
            surroundingLight.color = LampColor;
            surroundingLight.shadows = LightShadows.None;
            if (nearbyFill == null)
            {
                var fillObject = new GameObject("Scooter Nearby Fill");
                nearbyFill = fillObject.AddComponent<Light>();
            }
            nearbyFill.transform.SetParent(transform, false);
            // A small point fill reaches the rider's back and scooter as well as the nearby ground.
            nearbyFill.transform.localPosition = riderFillPosition;
            nearbyFill.type = LightType.Point;
            nearbyFill.color = surroundingLight.color;
            nearbyFill.range = fillRange;
            nearbyFill.intensity = fillIntensity;
            nearbyFill.shadows = LightShadows.None;
            Visibility = Mathf.SmoothStep(0f, 1f, ChargeFraction);
            Refresh();
        }

        private void LateUpdate() => Refresh();
        private void Refresh()
        {
            // Imported model sockets can have a rotated basis; aim in the riding frame.
            surroundingLight.transform.rotation = transform.rotation * Quaternion.Euler(headlightDownwardAngle, 0f, 0f);
            Vector3 lampOrigin = headlightMount != null ? headlightMount.position : transform.TransformPoint(headlightPosition);
            surroundingLight.transform.position = lampOrigin + transform.up * headlightHeightOffset;
            surroundingLight.enabled = player.IsAlive && State.Current > 0f;
            if (IsGameplayActive) warningTime = Mathf.Repeat(warningTime + Time.deltaTime, 1f);
            float fraction = ChargeFraction;
            float targetVisibility = Mathf.SmoothStep(0f, 1f, fraction);
            Visibility = Mathf.Lerp(Visibility, targetVisibility, 1f - Mathf.Exp(-Time.deltaTime * 5f));
            if (fraction <= 0f) Visibility = 0f;
            float visibility = Visibility;
            float warning = Mathf.Clamp01(WarningStrength);
            surroundingLight.color = nearbyFill.color = LampColor;
            // Warnings modulate brightness gently without changing the road's color.
            float warningDip = strongGuiltWarning ? 0.18f : 0.1f;
            float flicker = 1f - warning * warningDip
                * (0.5f + 0.5f * Mathf.Sin(warningTime * Mathf.PI * 4f));
            // Fade reach as well as brightness so low charge leaves only the immediate riding area.
            surroundingLight.range = headlightRange * Mathf.Lerp(0.25f, 1f, visibility);
            nearbyFill.range = Mathf.Lerp(emptyFillRange, fillRange * nearbyRangeMultiplier, visibility);
            surroundingLight.intensity = intensity * headlightBrightnessMultiplier * visibility * flicker;
            nearbyFill.intensity = Mathf.Lerp(emptyFillIntensity, fillIntensity * nearbyBrightnessMultiplier, visibility) * flicker;
            nearbyFill.enabled = player.IsAlive;
        }

        private void OnDisable()
        {
            if (session != null) session.UnbindLight(this);
            if (surroundingLight != null) surroundingLight.enabled = false;
            if (nearbyFill != null) nearbyFill.enabled = false;
        }
    }
}
