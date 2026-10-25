using Boki.Player;
using Boki.Core;
using Boki.Combat;
using UnityEngine;

namespace Boki.Enemies
{
    // Slow shared projectile (Gameplay §6): Watcher orbs and exam papers both use it with different visuals.
    [DisallowMultipleComponent]
    public sealed class EnemyProjectile : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float speed = 6f;
        [SerializeField, Min(0.1f)] private float lifetime = 6f;
        [SerializeField, Min(0.05f)] private float hitRadius = 0.35f;
        [SerializeField] private LayerMask obstructionMask = 1;
        [SerializeField, Min(0f)] private float spinDegreesPerSecond;
        [SerializeField] private Material trailMaterial;
        [SerializeField] private Color trailColor = new Color(1f, 0.3f, 0.08f, 1f);
        [SerializeField, Min(0.01f)] private float trailWidth = 0.12f;
        private PlayerHealth target;
        private CharacterController targetController;
        private Vector3 direction;
        private int damage;
        private float age;

        public void Launch(PlayerHealth newTarget, Vector3 newDirection, int newDamage)
        {
            target = newTarget;
            targetController = newTarget != null ? newTarget.GetComponent<CharacterController>() : null;
            direction = newDirection.normalized;
            damage = newDamage;
            age = 0f;
            if (direction != Vector3.zero) transform.rotation = Quaternion.LookRotation(direction);
            gameObject.SetActive(true);
            if (trailMaterial != null)
            {
                var trail = GetComponent<TrailRenderer>();
                if (trail == null) trail = gameObject.AddComponent<TrailRenderer>();
                trail.sharedMaterial = trailMaterial;
                trail.time = 0.22f;
                trail.minVertexDistance = 0.08f;
                trail.startWidth = trailWidth;
                trail.endWidth = 0f;
                trail.startColor = trailColor;
                trail.endColor = new Color(trailColor.r, trailColor.g, trailColor.b, 0f);
                trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                trail.receiveShadows = false;
                trail.Clear();
            }
        }

        private void Update()
        {
            if (target == null || direction == Vector3.zero) { Destroy(gameObject); return; }
            if (RunSession.Instance != null && !RunSession.Instance.IsGameplayActive) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            age += dt;
            if (age >= lifetime) { Destroy(gameObject); return; }

            Vector3 from = transform.position;
            Vector3 to = from + direction * speed * dt;
            if (Physics.Linecast(from, to, out RaycastHit hit, obstructionMask, QueryTriggerInteraction.Ignore))
            {
                Impact(hit.point);
                return;
            }
            transform.position = to;
            if (spinDegreesPerSecond > 0f) transform.Rotate(Vector3.forward, spinDegreesPerSecond * dt, Space.Self);

            Vector3 center = targetController != null
                ? target.transform.TransformPoint(targetController.center) : target.transform.position + Vector3.up * 0.9f;
            float reach = hitRadius + (targetController != null ? targetController.radius : 0.35f);
            Vector3 step = to - from;
            float t = step.sqrMagnitude > 0f ? Mathf.Clamp01(Vector3.Dot(center - from, step) / step.sqrMagnitude) : 0f;
            if (!target.IsAlive || (center - (from + step * t)).sqrMagnitude > reach * reach) return;
            target.TakeDamage(damage);
            Impact(from + step * t);
        }

        private void Impact(Vector3 position)
        {
            if (trailMaterial != null) EnemyDeathBurst.Spawn(position, gameObject.scene, trailColor);
            Destroy(gameObject);
        }
    }
}
