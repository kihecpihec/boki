using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Boki.Combat
{
    // Presentation for AutoLaser shots: a glowing ribbon with a hot core that snaps in and fades,
    // a muzzle flare, and an impact flare with sparks thrown back toward the scooter. It is built
    // at runtime from Resources/LaserBeam so every scene upgrades without a scene edit; without
    // the shader the scene's original beam and burst are used unchanged.
    internal sealed class LaserBeamView : System.IDisposable
    {
        // Visual tail added to the authored beam duration; it never changes fire cadence or damage.
        private const float AfterglowSeconds = 0.07f;
        private const float BeamWidth = 0.28f;
        private const float MuzzleFlareSize = 0.32f;
        private const float MuzzleFlareSeconds = 0.09f;
        private const float ImpactFlareSize = 1.3f;
        private const float ImpactFlareSeconds = 0.13f;
        // The aim point sits inside the enemy; flares and sparks start nearer its shooter-facing side.
        private const float SurfaceOffset = 0.35f;
        private const int SparkCount = 10;
        private static readonly int Intensity = Shader.PropertyToID("_Intensity");
        private static readonly int Radial = Shader.PropertyToID("_Radial");
        private static readonly int DepthTest = Shader.PropertyToID("_ZTest");
        private readonly LineRenderer beam;
        private readonly Transform origin;
        private readonly ParticleSystem sparks;
        private readonly Material originalBeamMaterial;
        private readonly Material ribbonMaterial;
        private readonly Material flareMaterial;
        private readonly ParticleSystem muzzleFlare;
        private readonly ParticleSystem impactFlare;
        private Vector3 firstImpact;
        private Vector3 lastImpact;
        private float age;
        private float lifetime;

        public LaserBeamView(LineRenderer beam, Transform origin, ParticleSystem sparks, Scene scene)
        {
            this.beam = beam;
            this.origin = origin;
            this.sparks = sparks;
            originalBeamMaterial = beam.sharedMaterial;
            beam.useWorldSpace = true;
            beam.positionCount = 2;
            beam.enabled = false;
            Shader shader = Resources.Load<Shader>("LaserBeam");
            if (shader == null)
            {
                Debug.LogWarning("Resources/LaserBeam shader is missing; the laser keeps its original look.", beam);
                return;
            }
            ribbonMaterial = new Material(shader);
            flareMaterial = new Material(shader);
            flareMaterial.SetFloat(Radial, 1f);
            // Flares and sparks draw over geometry so the rider and the enemy body cannot bury them.
            flareMaterial.SetFloat(DepthTest, (float)CompareFunction.Always);
            beam.sharedMaterial = ribbonMaterial;
            beam.textureMode = LineTextureMode.Stretch;
            beam.alignment = LineAlignment.View;
            beam.numCapVertices = 0;
            beam.startColor = beam.endColor = Color.white;
            beam.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.6f));
            muzzleFlare = CreateFlare("Laser Muzzle Flare", scene, true, MuzzleFlareSeconds);
            impactFlare = CreateFlare("Laser Impact Flare", scene, false, ImpactFlareSeconds);
            if (sparks != null) StyleSparks();
        }

        public void Show(Vector3 impact, float holdSeconds)
        {
            lifetime = holdSeconds + AfterglowSeconds;
            age = 0f;
            firstImpact = lastImpact = impact;
            beam.positionCount = 2;
            beam.enabled = true;
            ApplyEnvelope(0f);
            Follow();
            EmitMuzzleFlare();
            EmitImpact(impact);
        }

        // Double Trouble: one renderer joins both instantaneous hits into a single pulse.
        public void AddHit(Vector3 impact)
        {
            lastImpact = impact;
            beam.positionCount = 4;
            Follow();
            EmitImpact(impact);
        }

        public void Tick(float deltaTime)
        {
            Advance(muzzleFlare, deltaTime);
            Advance(impactFlare, deltaTime);
            Advance(sparks, deltaTime);
            if (!beam.enabled) return;
            age += deltaTime;
            if (age >= lifetime) beam.enabled = false;
            else ApplyEnvelope(age / lifetime);
        }

        // Presentation can lean the scooter after combat updates its shot timers.
        public void Follow()
        {
            Vector3 start = origin.position;
            if (muzzleFlare != null) muzzleFlare.transform.SetPositionAndRotation(start, origin.rotation);
            if (!beam.enabled) return;
            beam.SetPosition(0, start);
            if (beam.positionCount == 4)
            {
                beam.SetPosition(1, firstImpact);
                beam.SetPosition(2, start);
            }
            beam.SetPosition(beam.positionCount - 1, lastImpact);
        }

        public void Hide()
        {
            // Scene teardown can destroy the renderer before this component's OnDisable runs.
            if (beam != null) beam.enabled = false;
            if (muzzleFlare != null) muzzleFlare.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (impactFlare != null) impactFlare.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (sparks != null) sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        public void Dispose()
        {
            if (beam != null) beam.sharedMaterial = originalBeamMaterial;
            if (muzzleFlare != null) Object.Destroy(muzzleFlare.gameObject);
            if (impactFlare != null) Object.Destroy(impactFlare.gameObject);
            if (ribbonMaterial != null) Object.Destroy(ribbonMaterial);
            if (flareMaterial != null) Object.Destroy(flareMaterial);
        }

        // Full width and brightness on the first frame, then a fast collapse.
        private void ApplyEnvelope(float progress)
        {
            float fade = 1f - progress;
            beam.widthMultiplier = BeamWidth * (0.35f + 0.65f * fade * fade);
            if (ribbonMaterial != null) ribbonMaterial.SetFloat(Intensity, 1f - progress * progress);
        }

        private void EmitMuzzleFlare()
        {
            if (muzzleFlare == null) return;
            Emit(muzzleFlare, Vector3.zero, MuzzleFlareSize, MuzzleFlareSeconds, new Color(0.8f, 1f, 1f));
        }

        private void EmitImpact(Vector3 point)
        {
            Vector3 towardShooter = origin.position - point;
            float distance = towardShooter.magnitude;
            Vector3 direction = distance > 0.001f ? towardShooter / distance : Vector3.up;
            Vector3 surface = point + direction * Mathf.Min(SurfaceOffset, distance * 0.5f);
            if (impactFlare != null) Emit(impactFlare, surface, ImpactFlareSize, ImpactFlareSeconds, Color.white);
            if (sparks == null) return;
            sparks.transform.SetPositionAndRotation(surface, Quaternion.LookRotation(direction));
            sparks.Emit(SparkCount);
            sparks.Pause(false);
        }

        private static void Emit(ParticleSystem flare, Vector3 position, float size, float seconds, Color color)
        {
            flare.Emit(new ParticleSystem.EmitParams
            {
                position = position,
                velocity = Vector3.zero,
                startSize = size,
                startLifetime = seconds,
                startColor = color
            }, 1);
            flare.Pause(false);
        }

        // Advance explicitly so the shared run pause also freezes these particles.
        private static void Advance(ParticleSystem particles, float deltaTime)
        {
            if (particles != null) particles.Simulate(deltaTime, false, false, false);
        }

        private ParticleSystem CreateFlare(string name, Scene scene, bool followsMuzzle, float seconds)
        {
            var root = new GameObject(name);
            SceneManager.MoveGameObjectToScene(root, scene);
            var particles = root.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = seconds;
            main.startLifetime = seconds;
            main.startSpeed = 0f;
            main.maxParticles = 4;
            // The muzzle flare rides its object, which Follow keeps on the emitter.
            main.simulationSpace = followsMuzzle ? ParticleSystemSimulationSpace.Local : ParticleSystemSimulationSpace.World;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            var emission = particles.emission;
            emission.enabled = false;
            var shape = particles.shape;
            shape.enabled = false;
            var size = particles.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.35f));
            FadeOut(particles);
            ConfigureRenderer(particles);
            return particles;
        }

        // The scene's authored cyan puff becomes spark streaks that spray back along the beam.
        private void StyleSparks()
        {
            var main = sparks.main;
            main.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.12f);
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.12f, 0.24f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.4f, 1f, 1f), Color.white);
            main.gravityModifier = 0.35f;
            main.maxParticles = 24;
            var shape = sparks.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 50f;
            shape.radius = 0.04f;
            FadeOut(sparks);
            var renderer = ConfigureRenderer(sparks);
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.06f;
            renderer.lengthScale = 2f;
        }

        private ParticleSystemRenderer ConfigureRenderer(ParticleSystem particles)
        {
            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = flareMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return renderer;
        }

        private static void FadeOut(ParticleSystem particles)
        {
            var fade = particles.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;
        }
    }
}
