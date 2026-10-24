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
        [SerializeField] private ParticleSystem impactBurst;
        [Tooltip("Environment only. Exclude player and enemy geometry.")]
        [SerializeField] private LayerMask obstructionMask = 1;
        [SerializeField, Min(0.01f)] private float range = 10f;
        [SerializeField, Min(1)] private int damage = 20;
        [SerializeField, Min(0.01f)] private float fireInterval = 0.4f;
        [SerializeField, Min(0.01f)] private float beamDuration = 0.08f;
        private float cooldown;
        private float beamRemaining;
        private Vector3 impactPosition;

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
            if (impactBurst != null) impactBurst.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
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
            if (!EnemyFeedback.CanAnimate) return;
            float dt = Time.deltaTime;
            if (impactBurst != null) impactBurst.Simulate(dt, false, false, false);
            cooldown = Mathf.Max(0f, cooldown - dt);
            beamRemaining = Mathf.Max(0f, beamRemaining - dt);
            if (beamRemaining <= 0f) beam.enabled = false;
            else
            {
                beam.SetPosition(0, beamOrigin.position);
                beam.SetPosition(1, impactPosition);
            }
            if (cooldown > 0f) return;
            EnemyHealth target = FindTarget();
            if (target == null) return;
            cooldown = Mathf.Max(0.01f, fireInterval);
            impactPosition = target.AimPosition;
            beamRemaining = beamDuration;
            beam.SetPosition(0, beamOrigin.position);
            beam.SetPosition(1, impactPosition);
            beam.enabled = true;
            target.TakeDamage(damage);
            if (impactBurst != null)
            {
                impactBurst.transform.position = impactPosition;
                impactBurst.Emit(8);
                impactBurst.Pause(false);
            }
            // Keep the short pulse visible even when damage destroys its target.
        }
    }
}
