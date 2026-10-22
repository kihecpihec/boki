using Boki.Player;
using UnityEngine;

namespace Boki.Combat
{
    [DisallowMultipleComponent]
    public sealed class AutoLaser : MonoBehaviour
    {
        [SerializeField] private PlayerHealth playerHealth;
        [SerializeField] private Transform beamOrigin;
        [SerializeField] private LineRenderer beam;
        [Tooltip("Environment only. Exclude player and enemy geometry.")]
        [SerializeField] private LayerMask obstructionMask = 1;
        [SerializeField, Min(0.01f)] private float range = 10f;
        [SerializeField, Min(1)] private int damage = 20;
        [SerializeField, Min(0.01f)] private float fireInterval = 0.4f;
        [SerializeField, Min(0.01f)] private float beamDuration = 0.08f;
        private float cooldown;
        private float beamRemaining;
        private EnemyHealth beamTarget;

        private void OnEnable()
        {
            cooldown = beamRemaining = 0f;
            if (playerHealth == null || beamOrigin == null || beam == null)
            {
                Debug.LogError("Assign player health, beam origin, and beam renderer on AutoLaser.", this);
                enabled = false;
                return;
            }
            beam.useWorldSpace = true;
            beam.positionCount = 2;
            beam.enabled = false;
        }

        private void OnDisable()
        {
            if (beam != null) beam.enabled = false;
            beamTarget = null;
            cooldown = beamRemaining = 0f;
        }

        public EnemyHealth FindTarget()
        {
            if (beamOrigin == null) return null;
            EnemyHealth nearest = null;
            float nearestDistance = range * range;
            var enemies = EnemyHealth.Active;
            for (int i = 0; i < enemies.Count; i++)
            {
                var candidate = enemies[i];
                if (candidate == null || !candidate.isActiveAndEnabled || !candidate.IsAlive) continue;
                float distance = (candidate.AimPosition - beamOrigin.position).sqrMagnitude;
                if (distance > nearestDistance || !Visible(candidate)) continue;
                // Equal-distance targets keep their registration order.
                if (nearest != null && distance >= nearestDistance) continue;
                nearest = candidate;
                nearestDistance = distance;
            }
            return nearest;
        }

        private bool Visible(EnemyHealth target)
        {
            Vector3 delta = target.AimPosition - beamOrigin.position;
            return !Physics.Raycast(beamOrigin.position, delta.normalized, delta.magnitude,
                obstructionMask, QueryTriggerInteraction.Ignore);
        }

        private void Update()
        {
            if (playerHealth == null || !playerHealth.isActiveAndEnabled || !playerHealth.IsAlive)
            {
                beam.enabled = false;
                return;
            }
            float dt = Time.deltaTime;
            cooldown = Mathf.Max(0f, cooldown - dt);
            beamRemaining = Mathf.Max(0f, beamRemaining - dt);
            if (beamRemaining <= 0f || beamTarget == null || !beamTarget.isActiveAndEnabled
                || !beamTarget.IsAlive || (beamTarget.AimPosition - beamOrigin.position).sqrMagnitude > range * range
                || !Visible(beamTarget))
                beam.enabled = false;
            else
            {
                beam.SetPosition(0, beamOrigin.position);
                beam.SetPosition(1, beamTarget.AimPosition);
            }
            if (cooldown > 0f) return;
            EnemyHealth target = FindTarget();
            if (target == null) return;
            cooldown = Mathf.Max(0.01f, fireInterval);
            beamTarget = target;
            beamRemaining = beamDuration;
            beam.SetPosition(0, beamOrigin.position);
            beam.SetPosition(1, target.AimPosition);
            beam.enabled = true;
            target.TakeDamage(damage);
            // A lethal pulse is shown for this frame; the dead target cannot be hit again.
        }
    }
}
