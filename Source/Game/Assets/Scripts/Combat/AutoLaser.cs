using Boki.Player;
using Boki.Core;
using UnityEngine;

namespace Boki.Combat
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)]
    public sealed class AutoLaser : MonoBehaviour
    {
        [SerializeField] private PlayerHealth playerHealth;
        [SerializeField] private Transform beamOrigin;
        [SerializeField] private LineRenderer beam;
        [SerializeField] private ParticleSystem impactBurst;
        [Tooltip("Environment only. Exclude player and enemy geometry.")]
        [SerializeField] private LayerMask obstructionMask = 1;
        [SerializeField, Min(0.01f)] private float range = 10f;
        [SerializeField, Min(1)] private int damage = 6;
        [SerializeField, Min(0.01f)] private float fireInterval = 0.24f;
        [SerializeField, Min(0.01f)] private float beamDuration = 0.08f;
        private float cooldown;
        private LaserBeamView view;
        public event System.Action Fired;
        public event System.Action Hit;
        public event System.Action Killed;
        public event System.Action Stopped;
        public PlayerHealth Health => playerHealth;

        private void OnEnable()
        {
            cooldown = 0f;
            if (playerHealth == null || beamOrigin == null || beam == null)
            {
                Debug.LogError("Assign player health, beam origin, and beam renderer on AutoLaser.", this);
                enabled = false;
                return;
            }
            view ??= new LaserBeamView(beam, beamOrigin, impactBurst, gameObject.scene);
            view.Hide();
            if (!TryGetComponent<ComboTracker>(out _)) gameObject.AddComponent<ComboTracker>();
        }

        private void OnDisable()
        {
            Stopped?.Invoke();
            view?.Hide();
            cooldown = 0f;
        }

        private void OnDestroy()
        {
            view?.Dispose();
            view = null;
        }

        public EnemyHealth FindTarget() => FindTargetExcept(null);

        private EnemyHealth FindTargetExcept(EnemyHealth excluded)
        {
            if (beamOrigin == null) return null;
            EnemyHealth nearest = null;
            float nearestDistance = range * range;
            var enemies = EnemyHealth.Active;
            for (int i = 0; i < enemies.Count; i++)
            {
                var candidate = enemies[i];
                if (candidate == excluded || candidate == null || !candidate.isActiveAndEnabled || !candidate.IsAlive) continue;
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
                view.Hide();
                return;
            }
            if (!EnemyFeedback.CanAnimate) return;
            float dt = Time.deltaTime;
            view.Tick(dt);
            cooldown = Mathf.Max(0f, cooldown - dt);

            if (cooldown > 0f) return;
            EnemyHealth target = FindTarget();
            if (target == null) return;
            cooldown = Mathf.Max(0.01f, fireInterval);
            view.Show(target.AimPosition, beamDuration);
            Fired?.Invoke();
            var upgrades = RunSession.Instance != null ? RunSession.Instance.State.Progression : null;
            int hitDamage = Mathf.RoundToInt(damage * (upgrades != null ? upgrades.DamageMultiplier : 1f));
            DamageTarget(target, hitDamage);
            Hit?.Invoke();
            if (upgrades != null && upgrades.HasDoubleTrouble)
            {
                EnemyHealth second = FindTargetExcept(target);
                if (second == null && target != null && target.isActiveAndEnabled && target.IsAlive
                    && (target.AimPosition - beamOrigin.position).sqrMagnitude <= range * range && Visible(target)) second = target;
                if (second != null)
                {
                    view.AddHit(second.AimPosition);
                    DamageTarget(second, hitDamage);
                    Hit?.Invoke();
                }
            }
            // The pulse stays visible even when damage destroys its target.
        }

        private void DamageTarget(EnemyHealth target, int hitDamage)
        {
            bool alive = target.IsAlive;
            target.TakeDamage(hitDamage);
            if (alive && !target.IsAlive) Killed?.Invoke();
        }

        private void LateUpdate() => view.Follow();
    }
}
