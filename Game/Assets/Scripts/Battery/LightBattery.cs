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
        [SerializeField, Min(0f)] private float intensity = 5f;
        [SerializeField] private Vector3 headlightPosition = new Vector3(0f, 0.65f, 0.85f);
        [SerializeField, Range(0f, 45f)] private float headlightDownwardAngle = 15f;
        [SerializeField, Min(1f)] private float headlightRange = 15f;
        [SerializeField, Range(1f, 179f)] private float headlightAngle = 85f;
        [SerializeField, Min(0f)] private float fillIntensity = 0.55f;
        [SerializeField, Min(0.1f)] private float fillRange = 4.5f;
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
            surroundingLight.transform.SetParent(transform, false);
            surroundingLight.transform.localPosition = headlightPosition;
            surroundingLight.transform.localRotation = Quaternion.Euler(headlightDownwardAngle, 0f, 0f);
            surroundingLight.type = LightType.Spot;
            surroundingLight.range = headlightRange;
            surroundingLight.spotAngle = headlightAngle;
            surroundingLight.innerSpotAngle = headlightAngle * 0.45f;
            surroundingLight.color = new Color(0.82f, 0.94f, 1f);
            surroundingLight.shadows = LightShadows.None;
            if (nearbyFill == null)
            {
                var fillObject = new GameObject("Scooter Nearby Fill");
                fillObject.transform.SetParent(transform, false);
                nearbyFill = fillObject.AddComponent<Light>();
            }
            nearbyFill.transform.localPosition = new Vector3(0f, 0.45f, 0.65f);
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
            surroundingLight.intensity = intensity;
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
