using Boki.Core;
using UnityEngine;

namespace Boki.Combat
{
    // Gameplay §5: short world-space feedback, without lights or screen interruptions.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyHealth))]
    public sealed class EnemyFeedback : MonoBehaviour
    {
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorProperty = Shader.PropertyToID("_Color");
        [SerializeField] private Color hitColor = new Color(1f, 0.85f, 0.5f, 1f);
        [SerializeField, Min(0.01f)] private float flashSeconds = 0.1f;
        private EnemyHealth health;
        private Surface[] surfaces;
        private float remaining;

        private struct Surface
        {
            public Renderer Renderer;
            public int MaterialIndex;
            public int ColorProperty;
            public Color RestColor;
            public MaterialPropertyBlock Original;
            public MaterialPropertyBlock Flash;
        }

        internal static bool CanAnimate => Time.deltaTime > 0f
            && (RunSession.Instance == null || RunSession.Instance.IsGameplayActive);

        private void Awake()
        {
            health = GetComponent<EnemyHealth>();
            var cached = new System.Collections.Generic.List<Surface>();
            foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
            {
                if (!(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer)) continue;
                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    Material material = materials[i];
                    if (material == null) continue;
                    int property = material.HasProperty(BaseColor) ? BaseColor : ColorProperty;
                    if (!material.HasProperty(property)) continue;
                    var original = new MaterialPropertyBlock();
                    var flash = new MaterialPropertyBlock();
                    renderer.GetPropertyBlock(original, i);
                    renderer.GetPropertyBlock(flash, i);
                    // A renderer-wide block is used when no per-material block exists.
                    if (flash.isEmpty) renderer.GetPropertyBlock(flash);
                    cached.Add(new Surface
                    {
                        Renderer = renderer, MaterialIndex = i, ColorProperty = property,
                        RestColor = flash.HasColor(property) ? flash.GetColor(property) : material.GetColor(property),
                        Original = original, Flash = flash
                    });
                }
            }
            surfaces = cached.ToArray();
        }

        private void OnEnable()
        {
            health.Damaged += OnDamaged;
            health.Died += OnDied;
        }

        private void OnDisable()
        {
            health.Damaged -= OnDamaged;
            health.Died -= OnDied;
            Restore();
        }

        private void OnDamaged()
        {
            if (!health.IsAlive) return;
            remaining = flashSeconds;
            Apply(1f);
        }

        private void OnDied()
        {
            Restore();
            EnemyDeathBurst.Spawn(health.AimPosition, gameObject.scene, hitColor);
        }

        private void Update()
        {
            if (remaining <= 0f || !CanAnimate) return;
            remaining = Mathf.Max(0f, remaining - Time.deltaTime);
            if (remaining <= 0f) Restore();
            else Apply(remaining / flashSeconds);
        }

        private void Apply(float strength)
        {
            for (int i = 0; i < surfaces.Length; i++)
            {
                Surface surface = surfaces[i];
                if (surface.Renderer == null) continue;
                Color color = Color.Lerp(surface.RestColor, hitColor, strength);
                color.a = surface.RestColor.a;
                surface.Flash.SetColor(surface.ColorProperty, color);
                surface.Renderer.SetPropertyBlock(surface.Flash, surface.MaterialIndex);
            }
        }

        private void Restore()
        {
            remaining = 0f;
            for (int i = 0; i < surfaces.Length; i++)
            {
                Surface surface = surfaces[i];
                if (surface.Renderer != null)
                    surface.Renderer.SetPropertyBlock(surface.Original.isEmpty ? null : surface.Original,
                        surface.MaterialIndex);
            }
        }
    }
}
