using System;
using Boki.Combat;
using Boki.Player;
using UnityEngine;

namespace Boki.Enemies
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController), typeof(EnemyHealth))]
    public sealed class MeleeEnemy : MonoBehaviour
    {
        [SerializeField] private PlayerHealth target;
        [SerializeField, Min(0f)] private float speed = 3f;
        [SerializeField, Min(0f)] private float contactDistance = 0.9f;
        [SerializeField, Min(1)] private int damage = 10;
        [SerializeField, Min(0.01f)] private float attackInterval = 0.75f;
        private CharacterController controller;
        private EnemyHealth health;
        private CharacterController targetController;
        private float verticalSpeed;
        private float cooldown;
        public event Action Attacked;
        public bool IsMoving { get; private set; }

        private void OnEnable()
        {
            controller = GetComponent<CharacterController>();
            health = GetComponent<EnemyHealth>();
            if (target == null)
            {
                Debug.LogError("Assign the player target on MeleeEnemy.", this);
                enabled = false;
                return;
            }
            targetController = target.GetComponent<CharacterController>();
            cooldown = verticalSpeed = 0f;
        }

        private void Update()
        {
            if (!health.IsAlive || !health.isActiveAndEnabled || target == null
                || !target.isActiveAndEnabled || !target.IsAlive || !controller.enabled) return;
            float dt = Time.deltaTime;
            cooldown = Mathf.Max(0f, cooldown - dt);
            Vector3 delta = target.transform.position - transform.position;
            Vector3 horizontal = Vector3.ProjectOnPlane(delta, Vector3.up);
            Vector3 direction = horizontal.normalized;
            if (controller.isGrounded && verticalSpeed < 0f) verticalSpeed = -2f;
            verticalSpeed -= 25f * dt;
            CollisionFlags collision = controller.Move((direction * speed + Vector3.up * verticalSpeed) * dt);
            if ((collision & CollisionFlags.Below) != 0) verticalSpeed = -2f;
            if ((collision & CollisionFlags.Above) != 0 && verticalSpeed > 0f) verticalSpeed = 0f;
            IsMoving = horizontal.sqrMagnitude > 0.001f;
            if (IsMoving) transform.rotation = Quaternion.LookRotation(direction);

            // Compare capsule centers in 3D so jumping above contact range avoids damage.
            Vector3 ownCenter = transform.TransformPoint(controller.center);
            Vector3 playerCenter = targetController != null
                ? target.transform.TransformPoint(targetController.center) : target.transform.position + Vector3.up * 0.9f;
            if (cooldown > 0f || (playerCenter - ownCenter).sqrMagnitude > contactDistance * contactDistance) return;
            // A thin wall must not allow contact damage through it.
            if (Physics.Linecast(ownCenter, playerCenter, 1, QueryTriggerInteraction.Ignore)) return;
            cooldown = Mathf.Max(0.01f, attackInterval);
            target.TakeDamage(damage);
            Attacked?.Invoke();
        }
    }
}
