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
        [SerializeField, Min(0f)] private float forwardOffset = 2f;
        [SerializeField, Range(0.1f, 1f)] private float radiusScale = 0.75f;
        [SerializeField, Range(0f, 0.2f)] private float extraDarkness = 0.08f;
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
                + battery.transform.forward * forwardOffset;
            Vector3 center = worldCamera.WorldToViewportPoint(lightCenter);
            Vector3 edge = worldCamera.WorldToViewportPoint(lightCenter + worldCamera.transform.up * (lightRadius * radiusScale));
            float radius = Mathf.Clamp(Mathf.Abs(edge.y - center.y), 0.15f, 1.5f);
            material.SetVector(Center, new Vector4(center.x, center.y, 0f, 0f));
            material.SetFloat(Radius, battery.State.Current > 0f ? radius : radius * 0.3f);
            material.SetFloat(Aspect, (float)worldCamera.pixelWidth / Mathf.Max(1, worldCamera.pixelHeight));
            material.SetFloat(Opacity, Mathf.Min(0.8f, darknessOpacity + extraDarkness));
        }
        private void OnDisable()
        {
            if (overlay != null) { overlay.enabled = false; overlay.material = null; }
            if (material != null) Destroy(material);
        }
    }
}
