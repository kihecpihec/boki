using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Boki.Combat
{
    [DisallowMultipleComponent]
    public sealed class EnemyDeathBurst : MonoBehaviour
    {
        private const int MaximumBursts = 6;
        private const float Duration = 0.35f;
        private static int activeCount;
        private ParticleSystem particles;
        private Material material;
        private float age;
        private bool counted;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCount() => activeCount = 0;

        internal static void Spawn(Vector3 position, Scene scene, Color color)
        {
            if (activeCount >= MaximumBursts) return;
            Shader shader = Resources.Load<Shader>("EnemyFeedbackParticles");
            if (shader == null) return;
            var root = new GameObject("Enemy Death Burst");
            SceneManager.MoveGameObjectToScene(root, scene);
            root.transform.position = position;
            var burst = root.AddComponent<EnemyDeathBurst>();
            burst.counted = true;
            activeCount++;
            burst.material = new Material(shader);
            burst.particles = root.AddComponent<ParticleSystem>();
            burst.particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = burst.particles.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = Duration;
            main.startLifetime = Duration;
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 3f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.13f);
            main.startColor = color;
            main.gravityModifier = 0.3f;
            main.maxParticles = 10;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            var emission = burst.particles.emission;
            emission.enabled = false;
            var shape = burst.particles.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.15f;
            var fade = burst.particles.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;
            var renderer = root.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = burst.material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            // Advance explicitly so the shared run pause also freezes detached particles.
            burst.particles.Play(false);
            burst.particles.Emit(10);
            burst.particles.Pause(false);
        }

        private void Update()
        {
            if (!EnemyFeedback.CanAnimate) return;
            float dt = Time.deltaTime;
            age += dt;
            particles.Simulate(dt, false, false, false);
            if (age >= Duration) Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (counted) activeCount = Mathf.Max(0, activeCount - 1);
            if (material != null) Destroy(material);
        }
    }
}
