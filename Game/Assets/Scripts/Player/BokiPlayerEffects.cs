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
