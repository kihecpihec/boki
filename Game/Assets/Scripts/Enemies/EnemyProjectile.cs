using Boki.Player;
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
        }

        private void Update()
        {
            if (target == null || direction == Vector3.zero) { Destroy(gameObject); return; }
            float dt = Time.deltaTime;
            age += dt;
            if (age >= lifetime) { Destroy(gameObject); return; }

            Vector3 from = transform.position;
            Vector3 to = from + direction * speed * dt;
            if (Physics.Linecast(from, to, obstructionMask, QueryTriggerInteraction.Ignore))
            {
                Destroy(gameObject);
                return;
            }
            transform.position = to;
            if (spinDegreesPerSecond > 0f) transform.Rotate(Vector3.forward, spinDegreesPerSecond * dt, Space.Self);

            Vector3 center = targetController != null
                ? target.transform.TransformPoint(targetController.center) : target.transform.position + Vector3.up * 0.9f;
            float reach = hitRadius + (targetController != null ? targetController.radius : 0.35f);
            if (!target.IsAlive || (center - to).sqrMagnitude > reach * reach) return;
            target.TakeDamage(damage);
            Destroy(gameObject);
        }
    }
}
