using System;
using Boki.Battery;
using Boki.Combat;
using Boki.Player;
using UnityEngine;

namespace Boki.Enemies
{
    // Floating RangedShooter (Gameplay §6): the Watcher forest/city and FERI exam papers share this script.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyHealth))]
    public sealed class RangedEnemy : MonoBehaviour
    {
        [SerializeField] private PlayerHealth target;
        [SerializeField] private EnemyProjectile projectilePrefab;
        [SerializeField] private Transform muzzle;
        [SerializeField, Min(0f)] private float moveSpeed = 2.5f;
        [SerializeField, Min(0f)] private float preferredDistance = 9f;
        [SerializeField, Min(1f)] private float fireRange = 14f;
        [SerializeField, Min(0.1f)] private float fireInterval = 2.5f;
        [SerializeField, Min(0f)] private float firstShotDelay = 1f;
        [SerializeField, Min(1)] private int projectileDamage = 8;
        [SerializeField, Min(0f)] private float hoverAmplitude = 0.15f;
        [SerializeField, Min(0f)] private float hoverFrequency = 1.2f;
        [SerializeField] private LayerMask obstructionMask = 1;
        private float damageMultiplier = 1f;
        public void SetDamageMultiplier(float multiplier) => damageMultiplier = Mathf.Max(1f, multiplier);
        private EnemyHealth health;
        private CharacterController targetController;
        private LightBattery targetLight;
        private RangedShooterState shooter;
        private float baseHeight;
        private float hoverPhase;
        public event Action Fired;

        private void OnEnable()
        {
            health = GetComponent<EnemyHealth>();
            if (target == null || projectilePrefab == null)
            {
                Debug.LogError("Assign the player target and projectile prefab on RangedEnemy.", this);
                enabled = false;
                return;
            }
            if (muzzle == null) muzzle = transform;
            targetController = target.GetComponent<CharacterController>();
            targetLight = target.GetComponent<LightBattery>();
            shooter = new RangedShooterState(fireRange, fireInterval, firstShotDelay);
            baseHeight = transform.position.y;
            hoverPhase = UnityEngine.Random.value * Mathf.PI * 2f;
        }

        private void Update()
        {
            if (!health.IsAlive || !target.isActiveAndEnabled || !target.IsAlive) return;
            if (targetLight != null && !targetLight.IsGameplayActive) return;
            float dt = Time.deltaTime;
            Vector3 center = TargetCenter();
            Vector3 flat = Vector3.ProjectOnPlane(center - transform.position, Vector3.up);
            float distance = flat.magnitude;

            if (distance > preferredDistance && flat.sqrMagnitude > 0.001f) Advance(flat.normalized * (moveSpeed * (targetLight != null ? targetLight.EnemySpeedMultiplier : 1f) * dt));
            Vector3 position = transform.position;
            position.y = baseHeight + Mathf.Sin(Time.time * hoverFrequency + hoverPhase) * hoverAmplitude;
            transform.position = position;
            if (flat.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(flat.normalized);

            Vector3 origin = muzzle.position;
            bool visible = !Physics.Linecast(origin, center, obstructionMask, QueryTriggerInteraction.Ignore);
            if (!shooter.Tick(dt, Vector3.Distance(origin, center), visible)) return;
            EnemyProjectile shot = Instantiate(projectilePrefab, origin, Quaternion.identity);
            shot.Launch(target, center - origin, Mathf.RoundToInt(projectileDamage * damageMultiplier));
            Fired?.Invoke();
        }

        // Floating enemies have no CharacterController, so stop at walls rather than drifting through them.
        private void Advance(Vector3 step)
        {
            Vector3 from = transform.position;
            if (Physics.Linecast(from, from + step * 2f, obstructionMask, QueryTriggerInteraction.Ignore)) return;
            transform.position = from + step;
        }

        private Vector3 TargetCenter() => targetController != null
            ? target.transform.TransformPoint(targetController.center) : target.transform.position + Vector3.up * 0.9f;
    }
}
