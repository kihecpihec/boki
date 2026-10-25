using UnityEngine;

namespace Boki.Player
{
    [DisallowMultipleComponent]
    public sealed class BokiPlayerEffects : MonoBehaviour
    {
        [SerializeField] private TrailRenderer[] boostTrails = new TrailRenderer[0];
        [SerializeField] private Renderer[] damageRenderers = new Renderer[0];
        [SerializeField, ColorUsage(false, false)] private Color damageColor = new Color(1f, 0.15f, 0.25f);
        [SerializeField, Min(0.01f)] private float damageFlashDuration = 0.15f;

        [SerializeField] private ParticleSystem pickupGlow;
        [SerializeField] private ScooterController movement;
        [SerializeField] private PlayerHealth health;
        [SerializeField] private ParticleSystem ridingDirt;
        [SerializeField] private ParticleSystem landingDust;
        private Vector3 previousPosition;
        private bool wasGrounded;
        private float dirtRemainder;

        private void OnEnable()
        {
            previousPosition = transform.position;
            wasGrounded = movement != null && movement.IsGrounded;
            dirtRemainder = 0f;
            if (ridingDirt != null) ridingDirt.Pause(false);
            if (landingDust != null) landingDust.Pause(false);
        }

        private void LateUpdate()
        {
            Vector3 travel = transform.position - previousPosition;
            previousPosition = transform.position;
            if (movement == null || health == null || ridingDirt == null || landingDust == null) return;
            float dt = Time.deltaTime;
            if (dt <= 0f || (Boki.Core.RunSession.Instance != null
                && !Boki.Core.RunSession.Instance.IsGameplayActive)) return;
            bool grounded = movement.IsGrounded;
            if (movement.isActiveAndEnabled && health.IsAlive && travel.sqrMagnitude < 25f)
            {
                float speed = new Vector2(travel.x, travel.z).magnitude / dt;
                if (grounded && speed > 0.7f)
                {
                    dirtRemainder += dt * Mathf.Lerp(8f, 48f, Mathf.Clamp01(speed / movement.BoostSpeed));
                    int count = Mathf.FloorToInt(dirtRemainder);
                    dirtRemainder -= count;
                    if (count > 0)
                    {
                        ridingDirt.Emit(count);
                        ridingDirt.Pause(false);
                    }
                }
                else dirtRemainder = 0f;
                if (!wasGrounded && grounded)
                {
                    landingDust.Emit(18);
                    landingDust.Pause(false);
                }
            }
            wasGrounded = grounded;
            ridingDirt.Simulate(dt, false, false, false);
            landingDust.Simulate(dt, false, false, false);
        }

        public void PlayPickupGlow(bool batteryEnergy)
        {
            if (!isActiveAndEnabled || pickupGlow == null) return;
            var emission = new ParticleSystem.EmitParams
            {
                startColor = batteryEnergy ? new Color(0.2f, 1f, 0.75f) : new Color(1f, 0.75f, 0.15f)
            };
            pickupGlow.Emit(emission, 16);
            pickupGlow.Pause(false);
        }

        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorProperty = Shader.PropertyToID("_Color");
        private FlashTarget[] flashTargets;
        private float flashRemaining;
        private bool flashing;

        private sealed class FlashTarget
        {
            public Renderer Renderer;
            public int MaterialIndex;
            public int ColorPropertyId;
            public readonly MaterialPropertyBlock Original = new MaterialPropertyBlock();
            public readonly MaterialPropertyBlock Flash = new MaterialPropertyBlock();
        }

        private void Awake()
        {
            var targets = new System.Collections.Generic.List<FlashTarget>();
            var seenRenderers = new System.Collections.Generic.HashSet<Renderer>();
            foreach (Renderer target in damageRenderers)
            {
                if (target == null || !seenRenderers.Add(target))
                    continue;

                Material[] materials = target.sharedMaterials;
                for (int index = 0; index < materials.Length; index++)
                {
                    Material material = materials[index];
                    if (material == null)
                        continue;

                    int property = material.HasProperty(BaseColor) ? BaseColor : ColorProperty;
                    if (material.HasProperty(property))
                    {
                        targets.Add(new FlashTarget
                        {
                            Renderer = target,
                            MaterialIndex = index,
                            ColorPropertyId = property
                        });
                    }
                }
            }

            flashTargets = targets.ToArray();
            StopTrails();
        }

        /// <summary>Call with the controller's actual boost state, including early cancellation.</summary>
        public void SetBoostActive(bool active)
        {
            bool boostActive = active && isActiveAndEnabled;
            foreach (TrailRenderer trail in boostTrails)
            {
                if (trail != null)
                    trail.emitting = boostActive;
            }
        }

        /// <summary>Call only after health accepts damage. Repeated hits restart the flash.</summary>
        public void PlayDamageFlash()
        {
            if (!isActiveAndEnabled || flashTargets == null)
                return;

            foreach (FlashTarget target in flashTargets)
            {
                if (target.Renderer == null)
                    continue;

                if (!flashing)
                {
                    target.Renderer.GetPropertyBlock(target.Original, target.MaterialIndex);
                    target.Renderer.GetPropertyBlock(target.Flash, target.MaterialIndex);
                    // Preserve renderer-wide overrides when no material-specific block exists.
                    if (target.Flash.isEmpty)
                        target.Renderer.GetPropertyBlock(target.Flash);
                }

                target.Flash.SetColor(target.ColorPropertyId, damageColor);
                target.Renderer.SetPropertyBlock(target.Flash, target.MaterialIndex);
            }

            flashRemaining = damageFlashDuration;
            flashing = true;
        }

        private void Update()
        {
            if (Time.deltaTime <= 0f || (Boki.Core.RunSession.Instance != null
                && !Boki.Core.RunSession.Instance.IsGameplayActive)) return;
            if (pickupGlow != null) pickupGlow.Simulate(Time.deltaTime, false, false, false);
            if (flashRemaining <= 0f)
                return;

            flashRemaining -= Time.deltaTime;
            if (flashRemaining <= 0f)
                RestoreColors();
        }

        /// <summary>Call after a teleport/respawn to remove trails spanning the old position.</summary>
        public void ClearTrails()
        {
            foreach (TrailRenderer trail in boostTrails)
            {
                if (trail != null)
                    trail.Clear();
            }
        }

        private void OnDisable()
        {
            StopTrails();
            if (ridingDirt != null) ridingDirt.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (landingDust != null) landingDust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (pickupGlow != null) pickupGlow.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            RestoreColors();
        }

        private void StopTrails()
        {
            SetBoostActive(false);
            ClearTrails();
        }

        private void RestoreColors()
        {
            if (!flashing || flashTargets == null)
                return;

            foreach (FlashTarget target in flashTargets)
            {
                if (target.Renderer != null)
                    target.Renderer.SetPropertyBlock(target.Original, target.MaterialIndex);
            }

            flashRemaining = 0f;
            flashing = false;
        }
    }
}
