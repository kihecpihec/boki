using Boki.Battery;
using UnityEngine;
using UnityEngine.UI;

namespace Boki.UI
{
    // A soft, switchable overlay keeps the world readable without extra shadow lights or volumetrics.
    public sealed class ScooterDarkness : MonoBehaviour
    {
        [SerializeField] private LightBattery battery;
        [SerializeField] private Camera worldCamera;
        [SerializeField] private RawImage overlay;
        [SerializeField] private Shader darknessShader;
        [SerializeField] private bool darknessEnabled = true;
        [SerializeField, Min(0.1f)] private float lightRadius = 7f;
        [SerializeField, Range(0f, 0.8f)] private float darknessOpacity = 0.65f;
        [SerializeField, Min(0f)] private float forwardOffset = 0f;
        [SerializeField, Range(0.1f, 1f)] private float radiusScale = 0.75f;
        [SerializeField, Range(0f, 0.2f)] private float extraDarkness = 0.08f;
        [SerializeField, Min(1f)] private float chargedRadiusMultiplier = 3f;
        [SerializeField, Range(0f, 1f)] private float chargedOpacityMultiplier = 0.65f;
        [SerializeField, Range(0.4f, 1f)] private float emptyRadiusMultiplier = 1f;
        [SerializeField, Range(0f, 0.8f)] private float maximumEmptyOpacity = 0.5f;
        private Material material;
        private static readonly int Center = Shader.PropertyToID("_Center");
        private static readonly int Radius = Shader.PropertyToID("_Radius");
        private static readonly int Aspect = Shader.PropertyToID("_Aspect");
        private static readonly int Opacity = Shader.PropertyToID("_Opacity");
        public bool DarknessEnabled { get => darknessEnabled; set => darknessEnabled = value; }

        private void OnEnable()
        {
            if (battery == null || worldCamera == null || overlay == null || darknessShader == null)
            {
                Debug.LogError("Assign Battery, World Camera, Overlay and Darkness Shader on ScooterDarkness.", this);
                enabled = false;
                return;
            }
            material = new Material(darknessShader);
            overlay.material = material;
            overlay.raycastTarget = false;
        }
        private void LateUpdate()
        {
            overlay.enabled = darknessEnabled;
            if (!darknessEnabled) return;
            Vector3 lightCenter = battery.transform.position + Vector3.up
                + battery.transform.forward * (forwardOffset * battery.Visibility);
            Vector3 center = worldCamera.WorldToViewportPoint(lightCenter);
            Vector3 edge = worldCamera.WorldToViewportPoint(lightCenter + worldCamera.transform.up * (lightRadius * radiusScale));
            float radius = Mathf.Clamp(Mathf.Abs(edge.y - center.y), 0.15f, 1.5f);
            material.SetVector(Center, new Vector4(center.x, center.y, 0f, 0f));
            float visibility = battery.Visibility;
            material.SetFloat(Radius, radius * Mathf.Lerp(emptyRadiusMultiplier, chargedRadiusMultiplier, visibility));
            material.SetFloat(Aspect, (float)worldCamera.pixelWidth / Mathf.Max(1, worldCamera.pixelHeight));
            float authoredOpacity = Mathf.Min(0.8f, darknessOpacity + extraDarkness);
            material.SetFloat(Opacity, Mathf.Lerp(Mathf.Min(maximumEmptyOpacity, authoredOpacity),
                authoredOpacity * chargedOpacityMultiplier, visibility));
        }
        private void OnDisable()
        {
            if (overlay != null) { overlay.enabled = false; overlay.material = null; }
            if (material != null) Destroy(material);
        }
    }
}
