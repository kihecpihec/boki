using Boki.Core;
using Boki.Player;
using UnityEngine;
using UnityEngine.Rendering.Universal;

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
        [SerializeField, Min(1f)] private float headlightRange = 28f;
        [SerializeField, Min(0f)] private float fillIntensity = 0.55f;
        [SerializeField, Min(0.1f)] private float fillRange = 2.5f;
        [SerializeField, Min(1f)] private float headlightBrightnessMultiplier = 12f;
        [SerializeField, Min(1f)] private float nearbyBrightnessMultiplier = 1.3f;
        [SerializeField, Min(1f)] private float nearbyRangeMultiplier = 1.4f;
        [SerializeField, Min(0f)] private float headlightHeightOffset = 0.1f;
        [SerializeField] private Vector3 riderFillPosition = new Vector3(0f, 1.6f, -0.9f);
        [SerializeField, Min(0f)] private float emptyFillIntensity = 0.12f;
        [SerializeField, Min(0.1f)] private float emptyFillRange = 2.2f;
        public float ChargeFraction => Mathf.Clamp01(State.Current / State.Capacity);
        public float WarningStrength { get; set; }
        public float Visibility { get; private set; }
        private static readonly Color LampColor = Color.white;
        private float warningTime;
        private const uint WorldRenderingLayer = 1u;
        private const uint RiderRenderingLayer = 2u;
        private Renderer[] riderRenderers;
        private uint[] originalRenderingLayers;
        private Light nearbyFill;
        private RunSession session;
        public LightBatteryState State => (session != null ? session : RunSession.Ensure()).State.LightBattery;
        public bool IsGameplayActive => isActiveAndEnabled && player != null
            && player.isActiveAndEnabled && player.IsAlive && session != null && session.IsGameplayActive;
        public float EnemySpeedMultiplier => State.EnemySpeedMultiplier;

        private void Awake()
        {
            riderRenderers = GetComponentsInChildren<Renderer>(true);
            originalRenderingLayers = new uint[riderRenderers.Length];
            for (int i = 0; i < riderRenderers.Length; i++)
                originalRenderingLayers[i] = riderRenderers[i].renderingLayerMask;
        }

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
            surroundingLight.type = LightType.Point;
            surroundingLight.GetUniversalAdditionalLightData().renderingLayers = WorldRenderingLayer;
            // Keep the broad scooter spill off the rider; his own fill stays restrained.
            for (int i = 0; i < riderRenderers.Length; i++)
                if (riderRenderers[i] != null) riderRenderers[i].renderingLayerMask = RiderRenderingLayer;
            surroundingLight.range = headlightRange;
            surroundingLight.color = LampColor;
            surroundingLight.useColorTemperature = false;
            surroundingLight.shadows = LightShadows.None;
            if (nearbyFill == null)
            {
                var fillObject = new GameObject("Scooter Nearby Fill");
                nearbyFill = fillObject.AddComponent<Light>();
            }
            nearbyFill.transform.SetParent(transform, false);
            // The second light reveals only the rider and scooter, without a ground hotspot.
            nearbyFill.transform.localPosition = riderFillPosition;
            nearbyFill.type = LightType.Point;
            nearbyFill.GetUniversalAdditionalLightData().renderingLayers = RiderRenderingLayer;
            nearbyFill.color = LampColor;
            nearbyFill.useColorTemperature = false;
            nearbyFill.range = fillRange;
            nearbyFill.intensity = fillIntensity;
            nearbyFill.shadows = LightShadows.None;
            Visibility = Mathf.SmoothStep(0f, 1f, ChargeFraction);
            Refresh();
        }

        private void LateUpdate() => Refresh();
        private void Refresh()
        {
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
            surroundingLight.range = headlightRange * Mathf.Lerp(0.1f, 1f, visibility);
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
            for (int i = 0; i < riderRenderers.Length; i++)
                if (riderRenderers[i] != null) riderRenderers[i].renderingLayerMask = originalRenderingLayers[i];
        }
    }
}
