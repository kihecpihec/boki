using UnityEngine;
using UnityEngine.Rendering;

namespace Boki.Routes
{
    // One quad drawn by the Portal Vortex shader. Owned by PortalVortex and PortalTransit, which animate it.
    internal sealed class VortexDisc
    {
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        private static readonly int AlphaId = Shader.PropertyToID("_Alpha");
        private static readonly int SpinId = Shader.PropertyToID("_Spin");
        private static readonly int OpacityId = Shader.PropertyToID("_Opacity");
        private static readonly int SrcBlendId = Shader.PropertyToID("_SrcBlend");
        private static readonly int DstBlendId = Shader.PropertyToID("_DstBlend");
        private static readonly int ZTestId = Shader.PropertyToID("_ZTest");
        private static readonly Color PortalColor = new Color(0.2f, 1f, 0.85f);

        private readonly GameObject root;
        private readonly MeshRenderer meshRenderer;
        private readonly Material material;
        private readonly Mesh mesh;

        public Transform Transform => root.transform;

        // Additive glow in the world (depth tested), or an opaque swirl that ignores depth for the screen overlay.
        public static Material CreateMaterial(bool opaqueOverlay, bool depthTest)
        {
            Shader shader = Resources.Load<Shader>("PortalVortex");
            if (shader == null) return null;
            var material = new Material(shader);
            material.SetColor(ColorId, PortalColor);
            material.SetFloat(OpacityId, opaqueOverlay ? 0.96f : 0.3f);
            material.SetFloat(SrcBlendId, (float)BlendMode.SrcAlpha);
            material.SetFloat(DstBlendId, (float)(opaqueOverlay ? BlendMode.OneMinusSrcAlpha : BlendMode.One));
            material.SetFloat(ZTestId, (float)(depthTest ? CompareFunction.LessEqual : CompareFunction.Always));
            return material;
        }

        public static VortexDisc Create(string name, Transform parent)
        {
            Material material = CreateMaterial(false, true);
            return material == null ? null : new VortexDisc(name, parent, material);
        }

        private VortexDisc(string name, Transform parent, Material material)
        {
            this.material = material;
            mesh = new Mesh { name = name };
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
                new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f)
            };
            mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
            mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            mesh.RecalculateBounds();
            root = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            root.transform.SetParent(parent, false);
            root.GetComponent<MeshFilter>().sharedMesh = mesh;
            meshRenderer = root.GetComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = material;
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.enabled = false;
        }

        public void Show(float intensity, float spin)
        {
            bool visible = intensity > 0.001f;
            if (meshRenderer.enabled != visible) meshRenderer.enabled = visible;
            if (!visible) return;
            material.SetFloat(IntensityId, intensity);
            material.SetFloat(AlphaId, Mathf.Clamp01(intensity));
            material.SetFloat(SpinId, spin);
        }

        public void Destroy()
        {
            Object.Destroy(root);
            Object.Destroy(material);
            Object.Destroy(mesh);
        }
    }
}
